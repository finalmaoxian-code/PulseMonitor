using System;

namespace PulseMonitor.ViewModels;

/// <summary>展示层数值格式化工具。</summary>
public static class Formatters
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        double value = bytes;
        var unit = 0;

        while (value >= 1024d && unit < ByteUnits.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {ByteUnits[unit]}"
            : $"{value:0.0} {ByteUnits[unit]}";
    }

    public static string Bytes(ulong bytes) => Bytes((long)Math.Min(bytes, long.MaxValue));

    public static string Rate(double bytesPerSecond)
    {
        (string value, string unit) = RateParts(bytesPerSecond);
        return $"{value} {unit}";
    }

    /// <summary>拆分速率数字和单位，便于大字号卡片排版。</summary>
    public static (string Value, string Unit) RateParts(double bytesPerSecond)
    {
        if (!double.IsFinite(bytesPerSecond) || bytesPerSecond <= 0.5)
        {
            return ("0", "B/s");
        }

        (double scaled, string unit) = Scale(bytesPerSecond);
        return (scaled.ToString("0.0"), unit + "/s");
    }

    public static string BitRate(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
        {
            return "未知";
        }

        if (bitsPerSecond >= 1_000_000_000)
        {
            return $"{bitsPerSecond / 1_000_000_000d:0.0} Gbps";
        }

        if (bitsPerSecond >= 1_000_000)
        {
            return $"{bitsPerSecond / 1_000_000d:0.0} Mbps";
        }

        if (bitsPerSecond >= 1_000)
        {
            return $"{bitsPerSecond / 1_000d:0.0} Kbps";
        }

        return $"{bitsPerSecond} bps";
    }

    public static string PercentValue(double percent)
        => Math.Clamp(percent, 0d, 100d).ToString("0.0");

    public static string Percent(double percent)
        => PercentValue(percent) + "%";

    private static (double Value, string Unit) Scale(double bytes)
    {
        var value = bytes;
        var index = 0;

        while (value >= 1024d && index < ByteUnits.Length - 1)
        {
            value /= 1024d;
            index++;
        }

        return (value, ByteUnits[index]);
    }
}
