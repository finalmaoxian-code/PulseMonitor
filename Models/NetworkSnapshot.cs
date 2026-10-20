using System;

using System.Collections.Generic;

namespace PulseMonitor.Models;

/// <summary>单块网卡的速率采样。每块网卡拥有独立的时间基准与计数器状态。</summary>
public sealed record NetworkAdapterSnapshot
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string Description { get; init; } = "";

    public string InterfaceType { get; init; } = "";

    public bool IsLikelyVirtual { get; init; }

    public bool IsIncludedInAggregate { get; init; }

    public long LinkSpeedBitsPerSecond { get; init; }

    public MetricSample<double> DownloadBytesPerSecond { get; init; }

    public MetricSample<double> UploadBytesPerSecond { get; init; }
}

/// <summary>网络汇总采样。汇总只包含被选中的物理网卡，避免 VPN/虚拟网卡重复计数。</summary>
public sealed record NetworkSnapshot
{
    public DateTimeOffset Timestamp { get; init; }

    public MetricSample<double> DownloadBytesPerSecond { get; init; }

    public MetricSample<double> UploadBytesPerSecond { get; init; }

    public long SessionDownloadedBytes { get; init; }

    public long SessionUploadedBytes { get; init; }

    public IReadOnlyList<NetworkAdapterSnapshot> Adapters { get; init; } = [];
}


