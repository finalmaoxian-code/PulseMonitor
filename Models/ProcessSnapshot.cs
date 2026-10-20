using System;

namespace PulseMonitor.Models;

/// <summary>
/// 进程快照。PID 与启动时间共同构成进程身份，避免 PID 被复用后把两个进程混为一谈。
/// 读取失败的字段会携带状态，而不是伪装成 0。
/// </summary>
public sealed record ProcessSnapshot
{
    public int Pid { get; init; }

    public string Name { get; init; } = "";

    public DateTimeOffset? StartTimeUtc { get; init; }

    public string? ExecutablePath { get; init; }

    public MetricSample<double> CpuUsagePercent { get; init; }

    public SampleStatus MemoryStatus { get; init; }

    public long WorkingSetBytes { get; init; }

    public SampleStatus PrivateMemoryStatus { get; init; }

    public long PrivateMemoryBytes { get; init; }

    public int ThreadCount { get; init; }

    public int HandleCount { get; init; }

    public string PidText => $"PID {Pid}";

    public string CpuText => CpuUsagePercent.Status switch
    {
        SampleStatus.Valid => $"{Math.Clamp(CpuUsagePercent.Value, 0d, 100d):F1}%",
        SampleStatus.Initializing => "采集中",
        SampleStatus.CounterReset => "重置",
        SampleStatus.Stale => "已过期",
        SampleStatus.Unavailable => "N/A",
        _ => "N/A",
    };

    public string MemoryText => MemoryStatus == SampleStatus.Valid
        ? $"{WorkingSetBytes / 1024d / 1024d:F1} MiB"
        : "N/A";

    public string IdentityText => StartTimeUtc is null
        ? "启动时间不可用"
        : $"{StartTimeUtc.Value.ToLocalTime():HH:mm:ss} 启动";

    public string CpuTooltip => CpuUsagePercent.Status == SampleStatus.Valid
        ? "CPU 已按全部逻辑处理器归一化"
        : $"{CpuUsagePercent.Status.ToShortText()}：{CpuUsagePercent.Error ?? "暂无可用读数"}"; 

    public string ThreadsText => ThreadCount > 0 ? ThreadCount.ToString() : "N/A";

    public string HandlesText => HandleCount > 0 ? HandleCount.ToString() : "N/A";
}
