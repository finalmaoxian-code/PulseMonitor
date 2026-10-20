using System;
using Xunit;
using PulseMonitor.ViewModels;

namespace PulseMonitor.Tests;

public sealed class MetricHistoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KeepsSamplesWithinCapacity()
    {
        var history = new MetricHistory(capacity: 5, TimeSpan.FromMinutes(10));

        for (int i = 0; i < 12; i++)
        {
            history.Push(Start.AddSeconds(i), i);
        }

        Assert.Equal(5, history.Count);
        Assert.Equal(5, history.Values.Count);
        Assert.Equal(7d, history.Values[0]);
        Assert.Equal(11d, history.Values[^1]);
    }

    [Fact]
    public void TrimsSamplesOlderThanWindow()
    {
        var history = new MetricHistory(capacity: 100, TimeSpan.FromSeconds(10));

        for (int i = 0; i < 5; i++)
        {
            history.Push(Start.AddSeconds(i), i);
        }

        // 跳跃 20 秒但未超过断档阈值时，旧样本应按时间窗被裁掉
        history.Push(Start.AddSeconds(4.5), 100);

        Assert.True(history.SpanSeconds <= 10.5);
    }

    [Fact]
    public void GapClearsHistoryInsteadOfDrawingAcrossIt()
    {
        var history = new MetricHistory(capacity: 60, TimeSpan.FromMinutes(1));

        history.Push(Start, 10);
        history.Push(Start.AddSeconds(1), 20);
        Assert.Equal(2, history.Count);

        // 超过 3 秒断档：清空后只保留断档后的新样本
        history.Push(Start.AddSeconds(30), 30);

        Assert.Equal(1, history.Count);
        Assert.Equal(30d, history.Values[0]);
    }

    [Fact]
    public void NonFiniteValuesAreIgnored()
    {
        var history = new MetricHistory(capacity: 10);

        history.Push(Start, double.NaN);
        history.Push(Start.AddSeconds(1), double.PositiveInfinity);
        history.Push(Start.AddSeconds(2), 5);

        Assert.Equal(1, history.Count);
        Assert.Equal(5d, history.Values[0]);
    }

    [Fact]
    public void SpanUsesActualSampleTimestamps()
    {
        var history = new MetricHistory(capacity: 60, TimeSpan.FromMinutes(1));

        history.Push(Start, 1);
        history.Push(Start.AddSeconds(2), 2);
        history.Push(Start.AddSeconds(4), 3);

        Assert.Equal(4d, history.SpanSeconds, 3);
    }
}
