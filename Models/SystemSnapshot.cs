using System;

namespace PulseMonitor.Models;

/// <summary>一次完整系统采样。所有子指标都携带自己的状态和时间戳。</summary>
public sealed record SystemSnapshot
{
    public DateTimeOffset Timestamp { get; init; }

    public MetricSample<double> CpuUsagePercent { get; init; }

    public MemorySnapshot Memory { get; init; } = new();

    public DiskSnapshot SystemDisk { get; init; } = new();

    public NetworkSnapshot Network { get; init; } = new();

    public DiskIoSnapshot DiskIo { get; init; } = new();
}

