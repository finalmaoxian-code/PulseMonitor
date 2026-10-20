using System;
using Xunit;
using PulseMonitor.Models;

namespace PulseMonitor.Tests;

public sealed class MetricSampleTests
{
    [Fact]
    public void OnlyValidSampleExposesHasValue()
    {
        var timestamp = DateTimeOffset.UtcNow;

        Assert.True(new MetricSample<double>(42, SampleStatus.Valid, timestamp).HasValue);
        Assert.False(new MetricSample<double>(42, SampleStatus.Initializing, timestamp).HasValue);
        Assert.False(new MetricSample<double>(0, SampleStatus.Unavailable, timestamp).HasValue);
        Assert.False(new MetricSample<double>(0, SampleStatus.Stale, timestamp).HasValue);
    }

    [Fact]
    public void ProcessSnapshotDoesNotRenderUnavailableAsZero()
    {
        var process = new ProcessSnapshot
        {
            Pid = 42,
            Name = "example",
            CpuUsagePercent = MetricSample<double>.Unavailable(DateTimeOffset.UtcNow),
            MemoryStatus = SampleStatus.Unavailable,
        };

        Assert.Equal("N/A", process.CpuText);
        Assert.Equal("N/A", process.MemoryText);
        Assert.Equal("启动时间不可用", process.IdentityText);
    }
}
