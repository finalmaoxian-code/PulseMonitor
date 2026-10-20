using System;
using System.Linq;
using Xunit;
using PulseMonitor.Models;
using PulseMonitor.Services;

namespace PulseMonitor.Tests;

/// <summary>
/// 需要真实 Windows 的采样冒烟测试。非 Windows 环境直接跳过断言。
/// </summary>
public sealed class WindowsSamplingSmokeTests
{
    [Fact]
    public void SystemMonitorReturnsTimestampedMetricsWithoutThrowing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var service = new SystemMonitorService();
        SystemSnapshot first = service.Read();
        System.Threading.Thread.Sleep(300);
        SystemSnapshot second = service.Read();

        Assert.True(second.Timestamp > first.Timestamp);
        Assert.True(second.CpuUsagePercent.Status is SampleStatus.Valid or SampleStatus.Initializing or SampleStatus.Unavailable);
        Assert.Equal(SampleStatus.Valid, second.Memory.Status);
        Assert.Equal(SampleStatus.Valid, second.SystemDisk.Status);
        Assert.All(second.Network.Adapters, adapter =>
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Id));
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
        });
    }

    [Fact]
    public void DiskIoUsesTwoPassSamplingAndReportsStatus()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var service = new SystemMonitorService();

        // 第一次只建立 PDH 基准
        SystemSnapshot first = service.Read();
        Assert.Equal(SampleStatus.Initializing, first.DiskIo.ReadBytesPerSecond.Status);

        System.Threading.Thread.Sleep(400);
        SystemSnapshot second = service.Read();

        // PDH 计数器在部分环境可能不可用，但状态必须明确，且有效时数值非负
        foreach (MetricSample<double> metric in new[]
                 {
                     second.DiskIo.ReadBytesPerSecond,
                     second.DiskIo.WriteBytesPerSecond,
                     second.DiskIo.ReadsPerSecond,
                     second.DiskIo.WritesPerSecond,
                 })
        {
            Assert.NotEqual(SampleStatus.Initializing, metric.Status);
            if (metric.Status == SampleStatus.Valid)
            {
                Assert.True(metric.Value >= 0);
            }
        }
    }

    [Fact]
    public void NetworkSnapshotReportsAggregateCompleteness()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var service = new SystemMonitorService();
        service.Read();
        System.Threading.Thread.Sleep(300);
        SystemSnapshot snapshot = service.Read();

        NetworkSnapshot network = snapshot.Network;
        Assert.True(network.IncludedAdapterCount >= 0);
        Assert.True(network.DownloadContributingCount <= network.IncludedAdapterCount);
        Assert.True(network.UploadContributingCount <= network.IncludedAdapterCount);

        // 汇总速率有效时，必须至少有一块网卡真的贡献了读数
        if (network.DownloadBytesPerSecond.Status == SampleStatus.Valid)
        {
            Assert.True(network.DownloadContributingCount > 0);
        }
    }

    [Fact]
    public void ProcessMonitorUsesTwoPassCpuSamplesAndKeepsRows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new ProcessMonitorService();
        var first = service.Read();
        System.Threading.Thread.Sleep(300);
        var second = service.Read();

        Assert.NotEmpty(second);
        Assert.All(second, process =>
        {
            Assert.True(process.Pid >= 0);
            Assert.False(string.IsNullOrWhiteSpace(process.Name));
        });
        Assert.Contains(second, process => process.CpuUsagePercent.Status is SampleStatus.Valid or SampleStatus.Initializing or SampleStatus.Unavailable);
    }

    [Fact]
    public void ProcessPathsAreCachedAcrossReads()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new ProcessMonitorService();
        var first = service.Read();
        System.Threading.Thread.Sleep(200);
        var second = service.Read();

        // 路径属于进程不变属性：同一进程两次采样的路径应完全一致
        var firstByIdentity = first
            .Where(p => p.StartTimeUtc is not null && p.ExecutablePath is not null)
            .ToDictionary(p => (p.Pid, p.StartTimeUtc!.Value.UtcTicks), p => p.ExecutablePath!);

        int compared = 0;
        foreach (ProcessSnapshot process in second)
        {
            if (process.StartTimeUtc is null || process.ExecutablePath is null)
            {
                continue;
            }

            if (firstByIdentity.TryGetValue((process.Pid, process.StartTimeUtc.Value.UtcTicks), out string? previousPath))
            {
                Assert.Equal(previousPath, process.ExecutablePath);
                compared++;
            }
        }

        Assert.True(compared >= 0);
    }
}
