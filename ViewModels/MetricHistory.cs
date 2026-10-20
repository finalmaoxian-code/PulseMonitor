using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PulseMonitor.ViewModels;

/// <summary>
/// 趋势图数据缓冲：同时按「点数」和「时间窗」两个上限裁剪历史。
/// 采样出现超过 3 秒的断档时清空，避免把断档两端连成一条误导性的直线。
/// </summary>
public sealed class MetricHistory
{
    private static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(60);

    private readonly Queue<(DateTimeOffset Timestamp, double Value)> _samples = new();
    private readonly int _capacity;
    private readonly TimeSpan _window;
    private DateTimeOffset? _lastTimestamp;

    public MetricHistory(int capacity = 60, TimeSpan? window = null)
    {
        _capacity = Math.Max(2, capacity);
        _window = window ?? (capacity == 60 ? DefaultWindow : TimeSpan.FromSeconds(_capacity));
    }

    /// <summary>供图表绑定的数值序列，与内部样本一一对应。</summary>
    public ObservableCollection<double> Values { get; } = new();

    public int Count => _samples.Count;

    /// <summary>当前窗口内首尾样本的实际时间跨度（秒）。</summary>
    public double SpanSeconds => _samples.Count <= 1 || _lastTimestamp is null
        ? 0d
        : (_lastTimestamp.Value - _samples.Peek().Timestamp).TotalSeconds;

    public void Push(DateTimeOffset timestamp, double value)
    {
        if (!double.IsFinite(value))
        {
            return;
        }

        if (_lastTimestamp is { } last && timestamp - last > MaxGap)
        {
            Clear();
        }

        _lastTimestamp = timestamp;
        _samples.Enqueue((timestamp, value));
        Values.Add(value);
        Trim(timestamp);
    }

    public void Clear()
    {
        _samples.Clear();
        Values.Clear();
    }

    private void Trim(DateTimeOffset newest)
    {
        while (_samples.Count > _capacity)
        {
            _samples.Dequeue();
            Values.RemoveAt(0);
        }

        while (_samples.Count > 1 && newest - _samples.Peek().Timestamp > _window)
        {
            _samples.Dequeue();
            Values.RemoveAt(0);
        }
    }
}
