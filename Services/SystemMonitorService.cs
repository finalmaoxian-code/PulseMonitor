using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using PulseMonitor.Models;

namespace PulseMonitor.Services;

/// <summary>
/// Windows 系统指标采集服务。
/// 所有速率都基于实际单调时钟间隔；单项采集失败只影响该项，并返回明确状态。
/// </summary>
public sealed class SystemMonitorService : IDisposable
{
    private static readonly string[] VirtualAdapterKeywords =
    [
        "virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "vbox",
        "vpn", "tap", "tun", "wintun", "wireguard", "tailscale", "zerotier",
        "loopback", "bluetooth", "npcap", "pseudo", "tunnel"
    ];

    private readonly object _sync = new();
    private readonly CpuUsageSampler _cpuSampler = new();
    private readonly DiskIoMonitorService _diskIoMonitor = new();
    private readonly Dictionary<string, AdapterRateState> _networkStates = new(StringComparer.Ordinal);
    private readonly DriveInfo _systemDrive;

    private long _sessionDownloadedBytes;
    private long _sessionUploadedBytes;

    public SystemMonitorService()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("此监控服务仅支持 Windows");
        }

        string? root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("无法定位 Windows 系统盘");
        }

        _systemDrive = new DriveInfo(root);
    }

    /// <summary>执行一次系统采样。只要进程还活着，单个指标失败不会让整个快照抛出异常。</summary>
    public SystemSnapshot Read()
    {
        lock (_sync)
        {
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;

            MetricSample<double> cpu = _cpuSampler.Take(timestamp);
            MemorySnapshot memory = ReadMemory(timestamp);
            DiskSnapshot disk = ReadDisk(timestamp);
            NetworkSnapshot network = ReadNetwork(timestamp);

            return new SystemSnapshot
            {
                Timestamp = timestamp,
                CpuUsagePercent = cpu,
                Memory = memory,
                SystemDisk = disk,
                DiskIo = _diskIoMonitor.Read(timestamp),
                Network = network,
            };
        }
    }

    private static MemorySnapshot ReadMemory(DateTimeOffset timestamp)
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>(),
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            int code = Marshal.GetLastWin32Error();
            return new MemorySnapshot
            {
                Status = SampleStatus.Unavailable,
                Timestamp = timestamp,
                Error = $"GlobalMemoryStatusEx 失败（Win32 {code}: {new Win32Exception(code).Message}）",
                SystemCacheBytes = MetricSample<ulong>.Unavailable(timestamp),
            };
        }

        MetricSample<ulong> cache = ReadSystemCache(timestamp);

        return new MemorySnapshot
        {
            Status = SampleStatus.Valid,
            Timestamp = timestamp,
            TotalPhysicalBytes = status.TotalPhysical,
            AvailablePhysicalBytes = status.AvailablePhysical,
            TotalCommitBytes = status.TotalPageFile,
            AvailableCommitBytes = status.AvailablePageFile,
            SystemCacheBytes = cache,
        };
    }

    private static MetricSample<ulong> ReadSystemCache(DateTimeOffset timestamp)
    {
        try
        {
            uint size = (uint)Marshal.SizeOf<PerformanceInformation>();
            var info = new PerformanceInformation { Size = size };

            if (!GetPerformanceInfo(out info, size))
            {
                int code = Marshal.GetLastWin32Error();
                return MetricSample<ulong>.Unavailable(
                    timestamp,
                    $"GetPerformanceInfo 失败（Win32 {code}: {new Win32Exception(code).Message}）");
            }

            ulong pageSize = (ulong)info.PageSize;
            ulong pages = (ulong)info.SystemCache;

            if (pageSize == 0 || pages > ulong.MaxValue / pageSize)
            {
                return MetricSample<ulong>.Invalid(timestamp, "系统缓存计数溢出");
            }

            return new MetricSample<ulong>(pages * pageSize, SampleStatus.Valid, timestamp);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return MetricSample<ulong>.Unavailable(timestamp, "当前 Windows 版本不支持系统缓存查询");
        }
    }

    private DiskSnapshot ReadDisk(DateTimeOffset timestamp)
    {
        try
        {
            if (!_systemDrive.IsReady)
            {
                return new DiskSnapshot
                {
                    Status = SampleStatus.Unavailable,
                    Timestamp = timestamp,
                    DriveName = _systemDrive.Name,
                    Error = "系统盘尚未就绪",
                };
            }

            long total = _systemDrive.TotalSize;
            long available = _systemDrive.AvailableFreeSpace;

            if (total <= 0 || available < 0 || available > total)
            {
                return new DiskSnapshot
                {
                    Status = SampleStatus.Invalid,
                    Timestamp = timestamp,
                    DriveName = _systemDrive.Name,
                    Error = "系统盘容量读数不合理",
                };
            }

            return new DiskSnapshot
            {
                Status = SampleStatus.Valid,
                Timestamp = timestamp,
                DriveName = _systemDrive.Name,
                TotalBytes = total,
                AvailableBytes = available,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new DiskSnapshot
            {
                Status = SampleStatus.Unavailable,
                Timestamp = timestamp,
                DriveName = _systemDrive.Name,
                Error = ex.Message,
            };
        }
    }

    private NetworkSnapshot ReadNetwork(DateTimeOffset timestamp)
    {
        long monotonicTimestamp = Stopwatch.GetTimestamp();
        var activeIds = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<AdapterCandidate>();

        foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up ||
                network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            string id = network.Id;
            activeIds.Add(id);

            bool likelyVirtual = IsLikelyVirtualAdapter(network);
            RateSample download;
            RateSample upload;

            try
            {
                IPInterfaceStatistics stats = network.GetIPStatistics();
                AdapterRateState state = GetOrCreateAdapterState(id);

                download = state.Download.Take(stats.BytesReceived, monotonicTimestamp, timestamp);
                upload = state.Upload.Take(stats.BytesSent, monotonicTimestamp, timestamp);
            }
            catch (NetworkInformationException ex)
            {
                download = new RateSample(null, null, SampleStatus.Unavailable, timestamp, ex.Message);
                upload = new RateSample(null, null, SampleStatus.Unavailable, timestamp, ex.Message);
            }
            catch (PlatformNotSupportedException ex)
            {
                download = new RateSample(null, null, SampleStatus.Unavailable, timestamp, ex.Message);
                upload = new RateSample(null, null, SampleStatus.Unavailable, timestamp, ex.Message);
            }

            candidates.Add(new AdapterCandidate(
                id,
                SafeInterfaceName(network),
                SafeDescription(network),
                network.NetworkInterfaceType.ToString(),
                likelyVirtual,
                SafeSpeed(network),
                download,
                upload));
        }

        foreach (string id in _networkStates.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            _networkStates.Remove(id);
        }

        bool hasPhysicalCandidate = candidates.Any(candidate => !candidate.IsLikelyVirtual);
        foreach (AdapterCandidate candidate in candidates)
        {
            candidate.IncludedInAggregate = hasPhysicalCandidate
                ? !candidate.IsLikelyVirtual
                : true;
        }

        var selected = candidates.Where(candidate => candidate.IncludedInAggregate).ToArray();
        RateSample aggregateDownload = Aggregate(
            selected,
            static candidate => candidate.Download,
            timestamp,
            "下载",
            out int downloadContributors);
        RateSample aggregateUpload = Aggregate(
            selected,
            static candidate => candidate.Upload,
            timestamp,
            "上传",
            out int uploadContributors);

        if (aggregateDownload.Status == SampleStatus.Valid)
        {
            _sessionDownloadedBytes += aggregateDownload.Delta ?? 0;
        }

        if (aggregateUpload.Status == SampleStatus.Valid)
        {
            _sessionUploadedBytes += aggregateUpload.Delta ?? 0;
        }

        var adapters = candidates
            .OrderByDescending(candidate => candidate.IncludedInAggregate)
            .ThenBy(candidate => candidate.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(candidate => new NetworkAdapterSnapshot
            {
                Id = candidate.Id,
                Name = candidate.Name,
                Description = candidate.Description,
                InterfaceType = candidate.InterfaceType,
                IsLikelyVirtual = candidate.IsLikelyVirtual,
                IsIncludedInAggregate = candidate.IncludedInAggregate,
                LinkSpeedBitsPerSecond = candidate.LinkSpeed,
                DownloadBytesPerSecond = ToMetric(candidate.Download),
                UploadBytesPerSecond = ToMetric(candidate.Upload),
            })
            .ToArray();

        return new NetworkSnapshot
        {
            Timestamp = timestamp,
            DownloadBytesPerSecond = ToMetric(aggregateDownload, selected.Length == 0 ? "没有活动网络适配器" : null),
            UploadBytesPerSecond = ToMetric(aggregateUpload, selected.Length == 0 ? "没有活动网络适配器" : null),
            SessionDownloadedBytes = _sessionDownloadedBytes,
            SessionUploadedBytes = _sessionUploadedBytes,
            Adapters = adapters,
            IncludedAdapterCount = selected.Length,
            DownloadContributingCount = downloadContributors,
            UploadContributingCount = uploadContributors,
        };
    }

    private AdapterRateState GetOrCreateAdapterState(string id)
    {
        if (!_networkStates.TryGetValue(id, out AdapterRateState? state))
        {
            state = new AdapterRateState();
            _networkStates.Add(id, state);
        }

        return state;
    }

    /// <summary>
    /// 汇总已选网卡。单块网卡不可用时不再让整体速率失效：
    /// 只累加采样有效的网卡，并通过 contributingCount 告知调用方统计是否完整。
    /// </summary>
    private static RateSample Aggregate(
        IReadOnlyList<AdapterCandidate> selected,
        Func<AdapterCandidate, RateSample> selector,
        DateTimeOffset timestamp,
        string direction,
        out int contributingCount)
    {
        contributingCount = 0;

        if (selected.Count == 0)
        {
            return new RateSample(null, null, SampleStatus.Unavailable, timestamp, "没有活动网络适配器");
        }

        var samples = selected.Select(selector).ToArray();
        var usable = samples.Where(sample => sample.Status == SampleStatus.Valid).ToArray();
        contributingCount = usable.Length;

        if (usable.Length == 0)
        {
            SampleStatus status = CombineStatus(samples.Select(sample => sample.Status));
            string detail = string.Join(
                "；",
                samples.Where(sample => sample.Status != SampleStatus.Valid)
                    .Select(sample => sample.Error)
                    .Where(error => !string.IsNullOrWhiteSpace(error))
                    .Distinct());
            return new RateSample(
                null,
                null,
                status,
                timestamp,
                string.IsNullOrWhiteSpace(detail) ? $"{direction}速率尚未就绪" : detail);
        }

        double rate = usable.Sum(sample => sample.PerSecond ?? 0d);
        long delta = usable.Sum(sample => sample.Delta ?? 0L);
        return new RateSample(rate, delta, SampleStatus.Valid, timestamp);
    }

    private static SampleStatus CombineStatus(IEnumerable<SampleStatus> statuses)
    {
        var values = statuses.ToArray();
        if (values.Length == 0)
        {
            return SampleStatus.Unavailable;
        }

        if (values.Any(status => status == SampleStatus.Invalid))
        {
            return SampleStatus.Invalid;
        }

        if (values.Any(status => status == SampleStatus.Unavailable))
        {
            return SampleStatus.Unavailable;
        }

        if (values.Any(status => status == SampleStatus.CounterReset))
        {
            return SampleStatus.CounterReset;
        }

        if (values.Any(status => status == SampleStatus.Stale))
        {
            return SampleStatus.Stale;
        }

        if (values.Any(status => status == SampleStatus.Initializing))
        {
            return SampleStatus.Initializing;
        }

        return SampleStatus.Valid;
    }

    private static MetricSample<double> ToMetric(RateSample sample, string? fallbackError = null)
        => new(
            sample.PerSecond ?? 0d,
            sample.Status,
            sample.Timestamp,
            sample.Error ?? (sample.Status == SampleStatus.Valid ? null : fallbackError));

    private static bool IsLikelyVirtualAdapter(NetworkInterface network)
    {
        if (network.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Loopback)
        {
            return true;
        }

        string text = $"{network.Name} {network.Description}";
        return VirtualAdapterKeywords.Any(keyword =>
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string SafeInterfaceName(NetworkInterface network)
    {
        try
        {
            return string.IsNullOrWhiteSpace(network.Name) ? "未命名网卡" : network.Name;
        }
        catch
        {
            return "未命名网卡";
        }
    }

    private static string SafeDescription(NetworkInterface network)
    {
        try
        {
            return network.Description ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static long SafeSpeed(NetworkInterface network)
    {
        try
        {
            return network.Speed;
        }
        catch
        {
            return 0;
        }
    }

    private sealed class AdapterRateState
    {
        public RateSampler Download { get; } = new();

        public RateSampler Upload { get; } = new();
    }

    private sealed class AdapterCandidate(
        string id,
        string name,
        string description,
        string interfaceType,
        bool isLikelyVirtual,
        long linkSpeed,
        RateSample download,
        RateSample upload)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string Description { get; } = description;
        public string InterfaceType { get; } = interfaceType;
        public bool IsLikelyVirtual { get; } = isLikelyVirtual;
        public long LinkSpeed { get; } = linkSpeed;
        public RateSample Download { get; } = download;
        public RateSample Upload { get; } = upload;
        public bool IncludedInAggregate { get; set; }
    }

    /// <summary>
    /// 释放 PDH 查询句柄。Read() 已用 _sync 串行保护采集，
    /// 这里加同样的锁避免句柄在采样进行中被关闭。
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            _diskIoMonitor.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation performanceInformation, uint size);
}

