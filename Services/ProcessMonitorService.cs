using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PulseMonitor.Models;

namespace PulseMonitor.Services;

/// <summary>
/// 进程采样服务。进程身份由 PID + 启动时间组成；CPU 使用两次累计处理器时间的差计算。
/// 无法读取的字段会返回 N/A，而不是 0。
/// </summary>
public sealed class ProcessMonitorService
{
    private Dictionary<ProcessKey, CpuBaseline> _previous = [];

    /// <summary>
    /// 可执行路径缓存。读取 MainModule 需要打开模块并做一次文件系统访问，
    /// 而同一进程（PID + 启动时间）在生命周期内路径不变，因此只需读一次。
    /// </summary>
    private readonly Dictionary<ProcessKey, string?> _pathCache = [];

    public IReadOnlyList<ProcessSnapshot> Read()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        long monotonicTimestamp = Stopwatch.GetTimestamp();
        int processorCount = Math.Max(1, Environment.ProcessorCount);
        var current = new Dictionary<ProcessKey, CpuBaseline>();
        var results = new List<ProcessSnapshot>();

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                int pid = process.Id;
                string name = $"PID {pid}";
                DateTimeOffset? startTimeUtc = null;
                long startTicks = 0;
                string? executablePath = null;

                try
                {
                    name = process.ProcessName;
                }
                catch (Exception ex) when (IsRecoverableProcessException(ex))
                {
                    // 保留 PID 作为定位信息。
                }

                try
                {
                    DateTime startTime = process.StartTime;
                    startTimeUtc = new DateTimeOffset(startTime).ToUniversalTime();
                    startTicks = startTimeUtc.Value.UtcTicks;
                }
                catch (Exception ex) when (IsRecoverableProcessException(ex))
                {
                    // 启动时间不可用时不建立 CPU 基准，避免 PID 复用导致错误归一化。
                }

                SampleStatus memoryStatus = SampleStatus.Valid;
                long workingSetBytes = 0;
                try
                {
                    workingSetBytes = process.WorkingSet64;
                    if (workingSetBytes < 0)
                    {
                        workingSetBytes = 0;
                        memoryStatus = SampleStatus.Invalid;
                    }
                }
                catch (Exception ex) when (IsRecoverableProcessException(ex))
                {
                    memoryStatus = SampleStatus.Unavailable;
                }

                SampleStatus privateMemoryStatus = SampleStatus.Valid;
                long privateMemoryBytes = 0;
                try
                {
                    privateMemoryBytes = process.PrivateMemorySize64;
                    if (privateMemoryBytes < 0)
                    {
                        privateMemoryBytes = 0;
                        privateMemoryStatus = SampleStatus.Invalid;
                    }
                }
                catch (Exception ex) when (IsRecoverableProcessException(ex))
                {
                    privateMemoryStatus = SampleStatus.Unavailable;
                }

                int threadCount = 0;
                try
                {
                    threadCount = process.Threads.Count;
                }
                catch (Exception ex) when (IsRecoverableProcessException(ex))
                {
                    // 权限不足时保持 0，并由 ThreadsText 显示 N/A。
                }

                int handleCount = 0;
                try
                {
                    handleCount = process.HandleCount;
                }
                catch (Exception ex) when (IsRecoverableProcessException(ex))
                {
                    // 权限不足时保持 0，并由 HandlesText 显示 N/A。
                }

                var key = new ProcessKey(pid, startTicks);
                executablePath = LookupPath(process, key, startTimeUtc);

                MetricSample<double> cpu = ReadCpu(
                    process,
                    pid,
                    startTimeUtc,
                    startTicks,
                    timestamp,
                    monotonicTimestamp,
                    processorCount,
                    current);

                results.Add(new ProcessSnapshot
                {
                    Pid = pid,
                    Name = name,
                    StartTimeUtc = startTimeUtc,
                    ExecutablePath = executablePath,
                    CpuUsagePercent = cpu,
                    MemoryStatus = memoryStatus,
                    WorkingSetBytes = workingSetBytes,
                    PrivateMemoryStatus = privateMemoryStatus,
                    PrivateMemoryBytes = privateMemoryBytes,
                    ThreadCount = threadCount,
                    HandleCount = handleCount,
                });
            }
        }

        _previous = current;

        // 进程已退出时清掉路径缓存，避免长期运行后缓存无限增长
        foreach (ProcessKey stale in _pathCache.Keys.Where(key => !current.ContainsKey(key)).ToArray())
        {
            _pathCache.Remove(stale);
        }

        return results
            .OrderByDescending(process => process.CpuUsagePercent.Status == SampleStatus.Valid)
            .ThenByDescending(process => process.CpuUsagePercent.Value)
            .ThenByDescending(process => process.WorkingSetBytes)
            .ToArray();
    }

    /// <summary>
    /// 按进程身份缓存可执行路径。启动时间不可用时不做缓存
    /// （无法安全校验 PID 是否被复用）。
    /// </summary>
    private string? LookupPath(Process process, ProcessKey key, DateTimeOffset? startTimeUtc)
    {
        if (startTimeUtc is null)
        {
            return TryReadPath(process);
        }

        if (_pathCache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        string? path = TryReadPath(process);
        _pathCache[key] = path;
        return path;
    }

    private static string? TryReadPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (IsRecoverableProcessException(ex))
        {
            // 路径不是核心指标，读取失败不影响进程行展示。
            return null;
        }
    }

    private MetricSample<double> ReadCpu(
        Process process,
        int pid,
        DateTimeOffset? startTimeUtc,
        long startTicks,
        DateTimeOffset timestamp,
        long monotonicTimestamp,
        int processorCount,
        Dictionary<ProcessKey, CpuBaseline> current)
    {
        if (startTimeUtc is null)
        {
            return MetricSample<double>.Unavailable(timestamp, "启动时间不可用，无法安全校验进程身份");
        }

        long cpuTicks;
        try
        {
            cpuTicks = process.TotalProcessorTime.Ticks;
        }
        catch (Exception ex) when (IsRecoverableProcessException(ex))
        {
            return MetricSample<double>.Unavailable(timestamp, ex.Message);
        }

        var key = new ProcessKey(pid, startTicks);
        current[key] = new CpuBaseline(cpuTicks, monotonicTimestamp);

        if (!_previous.TryGetValue(key, out CpuBaseline previous))
        {
            return MetricSample<double>.Initializing(timestamp);
        }

        long cpuDelta = cpuTicks - previous.CpuTicks;
        long elapsedTicks = monotonicTimestamp - previous.Timestamp;

        if (cpuDelta < 0)
        {
            return new MetricSample<double>(0, SampleStatus.CounterReset, timestamp, "进程 CPU 累计时间发生回退");
        }

        if (elapsedTicks <= 0)
        {
            return new MetricSample<double>(0, SampleStatus.Invalid, timestamp, "进程采样时间没有前进");
        }

        double elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
        double cpuSeconds = cpuDelta / (double)TimeSpan.TicksPerSecond;
        double percent = cpuSeconds / elapsedSeconds / processorCount * 100d;

        if (!double.IsFinite(percent))
        {
            return MetricSample<double>.Invalid(timestamp, "进程 CPU 计算结果不是有限数值");
        }

        return new MetricSample<double>(
            Math.Clamp(percent, 0d, 100d),
            SampleStatus.Valid,
            timestamp);
    }

    private static bool IsRecoverableProcessException(Exception ex) =>
        ex is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or NotSupportedException
            or UnauthorizedAccessException;

    private readonly record struct ProcessKey(int Pid, long StartTicks);

    private readonly record struct CpuBaseline(long CpuTicks, long Timestamp);
}

