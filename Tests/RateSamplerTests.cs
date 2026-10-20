using System;
using System.Diagnostics;
using Xunit;
using PulseMonitor.Models;
using PulseMonitor.Services;

namespace PulseMonitor.Tests;

public sealed class RateSamplerTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstSampleOnlyEstablishesBaseline()
    {
        var sampler = new RateSampler();

        RateSample sample = sampler.Take(1_000, 10, Start);

        Assert.Equal(SampleStatus.Initializing, sample.Status);
        Assert.Null(sample.PerSecond);
        Assert.Null(sample.Delta);
    }

    [Fact]
    public void RateUsesActualElapsedTimeInsteadOfAssumedOneSecond()
    {
        var sampler = new RateSampler();
        long t0 = Stopwatch.GetTimestamp();

        sampler.Take(1_000, t0, Start);
        RateSample sample = sampler.Take(
            1_100,
            t0 + Stopwatch.Frequency * 2,
            Start.AddSeconds(2));

        Assert.Equal(SampleStatus.Valid, sample.Status);
        Assert.NotNull(sample.PerSecond);
        Assert.Equal(50d, sample.PerSecond!.Value, 6);
        Assert.Equal(100, sample.Delta);
    }

    [Fact]
    public void CounterResetIsReportedAndNextSampleCanRecover()
    {
        var sampler = new RateSampler();
        long t0 = Stopwatch.GetTimestamp();

        sampler.Take(1_000, t0, Start);
        RateSample valid = sampler.Take(1_500, t0 + Stopwatch.Frequency, Start.AddSeconds(1));
        RateSample reset = sampler.Take(100, t0 + Stopwatch.Frequency * 2, Start.AddSeconds(2));
        RateSample recovered = sampler.Take(200, t0 + Stopwatch.Frequency * 3, Start.AddSeconds(3));

        Assert.Equal(SampleStatus.Valid, valid.Status);
        Assert.Equal(SampleStatus.CounterReset, reset.Status);
        Assert.Null(reset.Delta);
        Assert.Equal(SampleStatus.Valid, recovered.Status);
        Assert.Equal(100d, recovered.PerSecond!.Value, 6);
    }

    [Fact]
    public void InvalidValueDoesNotReplaceLastGoodBaseline()
    {
        var sampler = new RateSampler();
        long t0 = Stopwatch.GetTimestamp();

        sampler.Take(100, t0, Start);
        RateSample invalid = sampler.Take(-1, t0 + Stopwatch.Frequency, Start.AddSeconds(1));
        RateSample valid = sampler.Take(200, t0 + Stopwatch.Frequency * 2, Start.AddSeconds(2));

        Assert.Equal(SampleStatus.Invalid, invalid.Status);
        Assert.Equal(SampleStatus.Valid, valid.Status);
        Assert.Equal(50d, valid.PerSecond!.Value, 6);
    }

    [Fact]
    public void NonAdvancingClockIsInvalid()
    {
        var sampler = new RateSampler();
        long t0 = Stopwatch.GetTimestamp();

        sampler.Take(100, t0, Start);
        RateSample invalid = sampler.Take(200, t0, Start);
        RateSample valid = sampler.Take(200, t0 + Stopwatch.Frequency, Start.AddSeconds(1));

        Assert.Equal(SampleStatus.Invalid, invalid.Status);
        Assert.Equal(SampleStatus.Valid, valid.Status);
        Assert.Equal(100d, valid.PerSecond!.Value, 6);
    }
}
