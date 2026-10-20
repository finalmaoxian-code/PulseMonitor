using System;
using System.Runtime.InteropServices;
using PulseMonitor.Models;

namespace PulseMonitor.Services;

/// <summary>
/// 物理磁盘整体 I/O 采集（读/写字节速率与 IOPS），基于 Windows PDH。
///
/// · 使用 PdhAddEnglishCounterW 按英文计数器名建查询，
///   即使系统语言是中文也能正确匹配；
/// · 速率型计数器需要两次采集才有差值，首次返回 Initializing；
/// · 持有的是原生查询句柄，必须 Dispose 释放。
/// </summary>
public sealed class DiskIoMonitorService : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;

    // PDH 计数器状态：0 = 有效值，1 = 有新数据（新数据不一定立刻可计算，但仍是可用状态）
    private const uint PdhCStatusValid = 0x00000000;
    private const uint PdhCStatusNewData = 0x00000001;

    /// <summary>单个计数器在 PDH 中的英文路径，顺序与字段声明一致。</summary>
    private static readonly string[] CounterPaths =
    [
        @"\PhysicalDisk(_Total)\Disk Read Bytes/sec",
        @"\PhysicalDisk(_Total)\Disk Write Bytes/sec",
        @"\PhysicalDisk(_Total)\Disk Reads/sec",
        @"\PhysicalDisk(_Total)\Disk Writes/sec",
    ];

    private IntPtr _query;
    private IntPtr _readBytesCounter;
    private IntPtr _writeBytesCounter;
    private IntPtr _readsCounter;
    private IntPtr _writesCounter;

    private bool _initializationAttempted;
    private bool _hasBaseline;
    private bool _disposed;

    private string? _initializationError;

    public DiskIoSnapshot Read(DateTimeOffset timestamp)
    {
        if (_disposed)
        {
            return CreateState(timestamp, SampleStatus.Unavailable, "磁盘 I/O 采集服务已释放");
        }

        if (!_initializationAttempted)
        {
            Initialize();
        }

        if (_query == IntPtr.Zero)
        {
            return CreateState(
                timestamp,
                SampleStatus.Unavailable,
                _initializationError ?? "无法创建磁盘性能查询");
        }

        uint code = PdhCollectQueryData(_query);
        if (code != 0)
        {
            // 采集失败后需要重新建立基准，否则下一次会把跨越故障的差值当成速率
            _hasBaseline = false;
            return CreateState(timestamp, SampleStatus.Unavailable, $"PDH 采样失败：0x{code:X8}");
        }

        if (!_hasBaseline)
        {
            _hasBaseline = true;
            return CreateState(timestamp, SampleStatus.Initializing, null);
        }

        return new DiskIoSnapshot
        {
            Timestamp = timestamp,
            ReadBytesPerSecond = ReadCounter(_readBytesCounter, timestamp, "磁盘读取速度"),
            WriteBytesPerSecond = ReadCounter(_writeBytesCounter, timestamp, "磁盘写入速度"),
            ReadsPerSecond = ReadCounter(_readsCounter, timestamp, "读取 IOPS"),
            WritesPerSecond = ReadCounter(_writesCounter, timestamp, "写入 IOPS"),
        };
    }

    private void Initialize()
    {
        _initializationAttempted = true;

        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
        {
            _initializationError = "磁盘 I/O 采集仅支持 Windows x64";
            return;
        }

        try
        {
            uint code = PdhOpenQueryW(IntPtr.Zero, IntPtr.Zero, out _query);
            if (code != 0)
            {
                _initializationError = $"PdhOpenQuery 失败：0x{code:X8}";
                CloseQuery();
                return;
            }

            // 必须全部计数器都可用，否则读数会缺项且无法判断是哪一项；
            // 只要有一项失败就整体不可用，并在错误信息里指出具体路径。
            if (!TryAddCounter(CounterPaths[0], out _readBytesCounter) ||
                !TryAddCounter(CounterPaths[1], out _writeBytesCounter) ||
                !TryAddCounter(CounterPaths[2], out _readsCounter) ||
                !TryAddCounter(CounterPaths[3], out _writesCounter))
            {
                CloseQuery();
            }
        }
        catch (DllNotFoundException ex)
        {
            _initializationError = "pdh.dll 不可用：" + ex.Message;
            CloseQuery();
        }
        catch (EntryPointNotFoundException ex)
        {
            _initializationError = "pdh.dll 缺少所需入口点：" + ex.Message;
            CloseQuery();
        }
    }

    private bool TryAddCounter(string path, out IntPtr counter)
    {
        uint code = PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out counter);
        if (code == 0)
        {
            return true;
        }

        _initializationError = $"无法添加计数器 {path}：0x{code:X8}";
        return false;
    }

    private static MetricSample<double> ReadCounter(IntPtr counter, DateTimeOffset timestamp, string name)
    {
        uint code = PdhGetFormattedCounterValue(counter, PdhFmtDouble, out _, out PdhFormattedValue value);
        if (code != 0)
        {
            return MetricSample<double>.Unavailable(timestamp, $"{name}读取失败：0x{code:X8}");
        }

        if (value.CStatus != PdhCStatusValid && value.CStatus != PdhCStatusNewData)
        {
            return MetricSample<double>.Unavailable(timestamp, $"{name}状态异常：0x{value.CStatus:X8}");
        }

        if (!double.IsFinite(value.DoubleValue) || value.DoubleValue < 0)
        {
            return MetricSample<double>.Invalid(timestamp, $"{name}数值无效");
        }

        return new MetricSample<double>(value.DoubleValue, SampleStatus.Valid, timestamp);
    }

    private static DiskIoSnapshot CreateState(DateTimeOffset timestamp, SampleStatus status, string? error)
    {
        var metric = new MetricSample<double>(0, status, timestamp, error);

        return new DiskIoSnapshot
        {
            Timestamp = timestamp,
            ReadBytesPerSecond = metric,
            WriteBytesPerSecond = metric,
            ReadsPerSecond = metric,
            WritesPerSecond = metric,
        };
    }

    private void CloseQuery()
    {
        if (_query == IntPtr.Zero)
        {
            return;
        }

        PdhCloseQuery(_query);
        _query = IntPtr.Zero;
        _readBytesCounter = IntPtr.Zero;
        _writeBytesCounter = IntPtr.Zero;
        _readsCounter = IntPtr.Zero;
        _writesCounter = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseQuery();
    }

    // 对应 64 位 Windows 的 PDH_FMT_COUNTERVALUE
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PdhFormattedValue
    {
        [FieldOffset(0)]
        public uint CStatus;

        [FieldOffset(8)]
        public double DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhOpenQueryW(
        IntPtr dataSource,
        IntPtr userData,
        out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint PdhAddEnglishCounterW(
        IntPtr query,
        string counterPath,
        IntPtr userData,
        out IntPtr counter);

    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhGetFormattedCounterValue(
        IntPtr counter,
        uint format,
        out uint counterType,
        out PdhFormattedValue value);

    [DllImport("pdh.dll", ExactSpelling = true)]
    private static extern uint PdhCloseQuery(IntPtr query);
}
