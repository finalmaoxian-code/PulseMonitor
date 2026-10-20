using System;
using System.Diagnostics;
using PulseMonitor.Models;

namespace PulseMonitor.Services;

/// <summary>一次累计计数器差分的结果，包含速率、原始增量和可信状态。</summary>
public readonly record struct RateSample(
    double? PerSecond,
    long? Delta,
    SampleStatus Status,
    DateTimeOffset Timestamp,
    string? Error = null)
{
    public bool HasRate => Status == SampleStatus.Valid && PerSecond.HasValue;
}

/// <summary>
/// 把只增不减的累计计数器转换为实际经过时间上的速率。
/// 每个计数器（例如一块网卡的接收或发送方向）必须使用独立实例。
/// </summary>
public sealed class RateSampler
{
    private long? _previousValue;
    private long _previousTimestamp;

    /// <summary>使用高精度单调时钟采样。</summary>
    public RateSample Take(long currentValue) => Take(currentValue, Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow);

    /// <summary>
    /// 显式传入单调时钟与 UTC 时间，便于测试和与其它指标共用同一时间基准。
    /// </summary>
    public RateSample Take(long currentValue, long monotonicTimestamp, DateTimeOffset utcTimestamp)
    {
        utcTimestamp = utcTimestamp.ToUniversalTime();

        if (currentValue < 0)
        {
            return new RateSample(null, null, SampleStatus.Invalid, utcTimestamp, "累计计数器不能为负数");
        }

        if (_previousValue is null)
        {
            _previousValue = currentValue;
            _previousTimestamp = monotonicTimestamp;
            return new RateSample(null, null, SampleStatus.Initializing, utcTimestamp);
        }

        if (monotonicTimestamp <= _previousTimestamp)
        {
            return new RateSample(null, null, SampleStatus.Invalid, utcTimestamp, "采样时间没有前进");
        }

        long delta = currentValue - _previousValue.Value;
        long elapsedTicks = monotonicTimestamp - _previousTimestamp;

        _previousValue = currentValue;
        _previousTimestamp = monotonicTimestamp;

        if (delta < 0)
        {
            return new RateSample(null, null, SampleStatus.CounterReset, utcTimestamp, "累计计数器发生回退，已重建基准");
        }

        double seconds = elapsedTicks / (double)Stopwatch.Frequency;
        if (!double.IsFinite(seconds) || seconds <= 0)
        {
            return new RateSample(null, null, SampleStatus.Invalid, utcTimestamp, "实际采样间隔无效");
        }

        double rate = delta / seconds;
        return new RateSample(rate, delta, SampleStatus.Valid, utcTimestamp);
    }
}
