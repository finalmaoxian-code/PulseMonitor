using System;

namespace PulseMonitor.Models;

/// <summary>物理磁盘整体 I/O 采样。四项指标各自携带状态，缺项不会互相污染。</summary>
public sealed record DiskIoSnapshot
{
    public DateTimeOffset Timestamp { get; init; }

    public MetricSample<double> ReadBytesPerSecond { get; init; }

    public MetricSample<double> WriteBytesPerSecond { get; init; }

    public MetricSample<double> ReadsPerSecond { get; init; }

    public MetricSample<double> WritesPerSecond { get; init; }
}
