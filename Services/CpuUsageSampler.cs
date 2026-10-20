using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PulseMonitor.Models;

namespace PulseMonitor.Services;

/// <summary>
/// 使用 GetSystemTimes 计算系统 CPU 使用率。
/// 在超过 64 个逻辑处理器的机器上，会逐处理器组暂换线程亲和性后求和；
/// 如果无法覆盖全部处理器组，则明确返回不可用，而不是把单组数据冒充为整机数据。
/// </summary>
public sealed class CpuUsageSampler
{
    private readonly Dictionary<int, CpuTimes> _previous = [];

    public MetricSample<double> Take(DateTimeOffset timestamp)
    {
        var readings = new List<GroupReading>();
        string? error = ReadAllGroups(readings);

        if (error is not null)
        {
            return MetricSample<double>.Unavailable(timestamp, error);
        }

        if (_previous.Count == 0)
        {
            Store(readings);
            return MetricSample<double>.Initializing(timestamp);
        }

        var idleDelta = 0UL;
        var totalDelta = 0UL;

        if (_previous.Count != readings.Count)
        {
            Store(readings);
            return new MetricSample<double>(0, SampleStatus.CounterReset, timestamp, "处理器组集合发生变化，已重建基准");
        }

        foreach (GroupReading reading in readings)
        {
            if (!_previous.TryGetValue(reading.Group, out CpuTimes previous))
            {
                Store(readings);
                return new MetricSample<double>(0, SampleStatus.CounterReset, timestamp, "处理器组集合发生变化，已重建基准");
            }

            CpuTimes current = reading.Times;
            if (current.Idle < previous.Idle ||
                current.Kernel < previous.Kernel ||
                current.User < previous.User)
            {
                Store(readings);
                return new MetricSample<double>(0, SampleStatus.CounterReset, timestamp, "CPU 累计计数器发生回退，已重建基准");
            }

            idleDelta += current.Idle - previous.Idle;
            // Windows 的 Kernel 时间已经包含 Idle，因此总时间只加 Kernel + User。
            totalDelta += (current.Kernel - previous.Kernel) + (current.User - previous.User);
        }

        Store(readings);

        if (totalDelta == 0)
        {
            return new MetricSample<double>(0, SampleStatus.Invalid, timestamp, "采样窗口内没有累计时间增量");
        }

        if (idleDelta > totalDelta)
        {
            return new MetricSample<double>(0, SampleStatus.Invalid, timestamp, "空闲时间增量大于总时间增量");
        }

        double percent = (totalDelta - idleDelta) * 100d / totalDelta;
        percent = Math.Clamp(percent, 0d, 100d);
        return new MetricSample<double>(percent, SampleStatus.Valid, timestamp);
    }

    private static string? ReadAllGroups(List<GroupReading> readings)
    {
        int groupCount;

        try
        {
            groupCount = Math.Max(1, (int)GetActiveProcessorGroupCount());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return "当前 Windows 版本不支持处理器组查询：" + ex.Message;
        }

        if (groupCount == 1)
        {
            return TryGetSystemTimes(0, readings, out string? error) ? null : error;
        }

        nint thread = GetCurrentThread();
        if (!GetThreadGroupAffinity(thread, out GroupAffinity original))
        {
            return Win32Error("无法读取线程处理器组亲和性");
        }




        string? failure = null;

        try
        {
            for (var group = 0; group < groupCount; group++)
            {
                uint processorCount = GetActiveProcessorCount((ushort)group);
                if (processorCount == 0)
                {
                    failure = $"处理器组 {group} 没有活动逻辑处理器";
                    break;
                }

                var requested = new GroupAffinity
                {
                    Group = (ushort)group,
                    Mask = CreateMask(processorCount),
                };

                if (!SetThreadGroupAffinity(thread, ref requested, out _))
                {
                    failure = Win32Error($"无法切换到处理器组 {group}，无法取得完整整机 CPU 数据");
                    break;
                }

                if (!TryGetSystemTimes(group, readings, out string? groupError))
                {
                    failure = groupError;
                    break;
                }
            }
        }
        finally
        {
            if (!SetThreadGroupAffinity(thread, ref original, out _) && failure is null)
            {
                failure = Win32Error("恢复线程处理器组亲和性失败");
            }

        }

        return failure;
    }

    private static bool TryGetSystemTimes(int group, List<GroupReading> readings, out string? error)
    {
        if (!GetSystemTimes(out NativeFileTime idle, out NativeFileTime kernel, out NativeFileTime user))
        {
            error = Win32Error($"GetSystemTimes 在处理器组 {group} 上失败");
            return false;
        }

        readings.Add(new GroupReading(
            group,
            new CpuTimes(idle.ToUInt64(), kernel.ToUInt64(), user.ToUInt64())));

        error = null;
        return true;
    }

    private void Store(List<GroupReading> readings)
    {
        _previous.Clear();
        foreach (GroupReading reading in readings)
        {
            _previous[reading.Group] = reading.Times;
        }
    }

    private static UIntPtr CreateMask(uint processorCount)
    {
        ulong mask = processorCount >= 64
            ? ulong.MaxValue
            : (1UL << checked((int)processorCount)) - 1UL;
        return UIntPtr.Size == 8
            ? new UIntPtr(mask)
            : new UIntPtr((uint)mask);
    }

    private static string Win32Error(string message)
    {
        int code = Marshal.GetLastWin32Error();
        return code == 0
            ? message
            : $"{message}（Win32 {code}: {new Win32Exception(code).Message}）";
    }

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

    private readonly record struct GroupReading(int Group, CpuTimes Times);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GroupAffinity
    {
        public ushort Group;
        public ushort Reserved0;
        public ushort Reserved1;
        public ushort Reserved2;
        public UIntPtr Mask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idleTime,
        out NativeFileTime kernelTime,
        out NativeFileTime userTime);

    [DllImport("kernel32.dll")]
    private static extern ushort GetActiveProcessorGroupCount();

    [DllImport("kernel32.dll")]
    private static extern uint GetActiveProcessorCount(ushort groupNumber);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadGroupAffinity(nint thread, out GroupAffinity affinity);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadGroupAffinity(
        nint thread,
        ref GroupAffinity affinity,
        out GroupAffinity previousAffinity);
}




