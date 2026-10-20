using System;

namespace PulseMonitor.Models;

/// <summary>
/// 单个指标在一次采样中的可信状态。
/// 只有 <see cref="Valid"/> 的数值可以当成真实测量结果展示。
/// </summary>
public enum SampleStatus
{
    /// <summary>首次采样，已建立基准但还没有可用于计算的差值。</summary>
    Initializing,

    /// <summary>采样值有效。</summary>
    Valid,

    /// <summary>累计计数器回退，通常意味着设备重置或进程身份变化。</summary>
    CounterReset,

    /// <summary>接口不可用、权限不足或平台不支持。</summary>
    Unavailable,

    /// <summary>超过允许的新鲜度窗口，当前展示值已经过期。</summary>
    Stale,

    /// <summary>读数存在，但计算结果不可信。</summary>
    Invalid,
}

/// <summary>
/// 带状态、时间戳和失败原因的单值指标。
/// 泛型值始终存在，但只有 <see cref="Status"/> 为 Valid 时才应被读取。
/// </summary>
public readonly record struct MetricSample<T>(
    T Value,
    SampleStatus Status,
    DateTimeOffset Timestamp,
    string? Error = null)
    where T : struct
{
    public bool HasValue => Status == SampleStatus.Valid;

    public static MetricSample<T> Initializing(DateTimeOffset timestamp)
        => new(default, SampleStatus.Initializing, timestamp);

    public static MetricSample<T> Unavailable(DateTimeOffset timestamp, string? error = null)
        => new(default, SampleStatus.Unavailable, timestamp, error);

    public static MetricSample<T> Invalid(DateTimeOffset timestamp, string? error = null)
        => new(default, SampleStatus.Invalid, timestamp, error);
}

/// <summary>状态到界面短文案的统一映射。</summary>
public static class SampleStatusText
{
    public static string ToShortText(this SampleStatus status) => status switch
    {
        SampleStatus.Initializing => "采集中",
        SampleStatus.Valid => "有效",
        SampleStatus.CounterReset => "计数器重置",
        SampleStatus.Unavailable => "N/A",
        SampleStatus.Stale => "数据已过期",
        SampleStatus.Invalid => "采样无效",
        _ => "未知",
    };
}

