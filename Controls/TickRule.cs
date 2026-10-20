using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PulseMonitor.Controls;

/// <summary>
/// 刻度尺（TickRule）—— 本项目的标志性元素。
///
/// 一条发丝线上按固定间隔画出刻度，每 <see cref="MajorEvery"/> 个刻度加长一次，
/// 像仪表盘边缘的量程尺。它同时承担「分隔线」和「量程参照」两个职责：
/// 刻度间隔固定，因此同一列里的两条刻度尺在视觉上可以互相对齐。
/// </summary>
public class TickRule : Control
{
    /// <summary>刻度总数（含两端）。</summary>
    public static readonly StyledProperty<int> TickCountProperty =
        AvaloniaProperty.Register<TickRule, int>(nameof(TickCount), 48);

    /// <summary>每隔多少个刻度出现一根长刻度。</summary>
    public static readonly StyledProperty<int> MajorEveryProperty =
        AvaloniaProperty.Register<TickRule, int>(nameof(MajorEvery), 4);

    /// <summary>刻度线颜色。</summary>
    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<TickRule, IBrush?>(nameof(TickBrush));

    /// <summary>短刻度长度。</summary>
    public static readonly StyledProperty<double> MinorLengthProperty =
        AvaloniaProperty.Register<TickRule, double>(nameof(MinorLength), 4d);

    /// <summary>长刻度长度。</summary>
    public static readonly StyledProperty<double> MajorLengthProperty =
        AvaloniaProperty.Register<TickRule, double>(nameof(MajorLength), 7d);

    static TickRule()
    {
        AffectsRender<TickRule>(
            TickCountProperty,
            MajorEveryProperty,
            TickBrushProperty,
            MinorLengthProperty,
            MajorLengthProperty);
    }

    public int TickCount
    {
        get => GetValue(TickCountProperty);
        set => SetValue(TickCountProperty, value);
    }

    public int MajorEvery
    {
        get => GetValue(MajorEveryProperty);
        set => SetValue(MajorEveryProperty, value);
    }

    public IBrush? TickBrush
    {
        get => GetValue(TickBrushProperty);
        set => SetValue(TickBrushProperty, value);
    }

    public double MinorLength
    {
        get => GetValue(MinorLengthProperty);
        set => SetValue(MinorLengthProperty, value);
    }

    public double MajorLength
    {
        get => GetValue(MajorLengthProperty);
        set => SetValue(MajorLengthProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 2 || height <= 1)
        {
            return;
        }

        IBrush brush = TickBrush ?? Brushes.DimGray;
        var hairline = new Pen(brush, 1);
        var tick = new Pen(brush, 1);

        // 基线贴底，刻度向上生长
        double baseline = height - 0.5;
        context.DrawLine(hairline, new Point(0, baseline), new Point(width, baseline));

        int count = Math.Max(2, TickCount);
        int major = Math.Max(1, MajorEvery);
        double step = width / (count - 1);

        for (int i = 0; i < count; i++)
        {
            bool isMajor = i % major == 0;
            double length = isMajor ? MajorLength : MinorLength;
            double x = Math.Min(width - 0.5, i * step);
            context.DrawLine(tick, new Point(x, baseline), new Point(x, baseline - length));
        }
    }
}
