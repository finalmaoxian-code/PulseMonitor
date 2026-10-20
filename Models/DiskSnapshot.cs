using System;

namespace PulseMonitor.Models;

/// <summary>系统盘容量采样结果。磁盘容量与磁盘吞吐率是不同指标。</summary>
public sealed record DiskSnapshot
{
    public SampleStatus Status { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string DriveName { get; init; } = "";

    public long TotalBytes { get; init; }

    public long AvailableBytes { get; init; }

    public string? Error { get; init; }

    public double UsagePercent =>
        Status == SampleStatus.Valid && TotalBytes > 0
            ? (TotalBytes - AvailableBytes) * 100d / TotalBytes
            : 0d;
}

