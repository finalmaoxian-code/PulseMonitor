using System;

namespace PulseMonitor.Models;

/// <summary>物理内存、提交内存与系统缓存的一次采样结果。</summary>
public sealed record MemorySnapshot
{
    public SampleStatus Status { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public ulong TotalPhysicalBytes { get; init; }

    public ulong AvailablePhysicalBytes { get; init; }

    public ulong TotalCommitBytes { get; init; }

    public ulong AvailableCommitBytes { get; init; }

    public MetricSample<ulong> SystemCacheBytes { get; init; }

    public string? Error { get; init; }

    public ulong UsedPhysicalBytes =>
        TotalPhysicalBytes > AvailablePhysicalBytes
            ? TotalPhysicalBytes - AvailablePhysicalBytes
            : 0;

    public ulong UsedCommitBytes =>
        TotalCommitBytes > AvailableCommitBytes
            ? TotalCommitBytes - AvailableCommitBytes
            : 0;

    public double UsagePercent =>
        Status == SampleStatus.Valid && TotalPhysicalBytes > 0
            ? UsedPhysicalBytes * 100d / TotalPhysicalBytes
            : 0d;
}

