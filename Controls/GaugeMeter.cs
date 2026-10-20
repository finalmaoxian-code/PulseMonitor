using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PulseMonitor.Controls;

/// <summary>
/// 环形仪表盘（GaugeMeter）—— 纯自绘控件。
///
/// Avalonia 组件特性说明：
/// · 继承 Control 并重写 Render(DrawingContext)，属于 Avalonia 的"即时绘制"路线，
///   用于表达原生控件难以组合出来的图形（弧线 + 渐变 + 端点光点）。
/// · 使用 StyledProperty + AffectsRender，属性一改自动触发重绘，无需手动订阅。
/// · 遵守主题：所有画刷都可以从外部注入（{DynamicResource ...}），跟随主题切换。
/// </summary>
public class GaugeMeter : Control
{
    /// <summary>当前值。</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<GaugeMeter, double>(nameof(Value));

    /// <summary>最小值。</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<GaugeMeter, double>(nameof(Minimum));

    /// <summary>最大值。</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<GaugeMeter, double>(nameof(Maximum), 100d);

    /// <summary>轨道画刷（底部那一圈）。</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<GaugeMeter, IBrush?>(nameof(TrackBrush));

    /// <summary>进度画刷（通常是渐变色）。</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<GaugeMeter, IBrush?>(nameof(AccentBrush));

    /// <summary>弧线粗细。</summary>
    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<GaugeMeter, double>(nameof(TrackThickness), 12d);

    /// <summary>刻度线是否显示。</summary>
    public static readonly StyledProperty<bool> ShowTicksProperty =
        AvaloniaProperty.Register<GaugeMeter, bool>(nameof(ShowTicks), true);

    /// <summary>圆心显示的文字（通常是当前读数）。</summary>
    public static readonly StyledProperty<string?> CenterTextProperty =
        AvaloniaProperty.Register<GaugeMeter, string?>(nameof(CenterText));

    /// <summary>圆心文字下方的小标签。</summary>
    public static readonly StyledProperty<string?> CenterLabelProperty =
        AvaloniaProperty.Register<GaugeMeter, string?>(nameof(CenterLabel));

    /// <summary>圆心文字画刷。</summary>
    public static readonly StyledProperty<IBrush?> CenterBrushProperty =
        AvaloniaProperty.Register<GaugeMeter, IBrush?>(nameof(CenterBrush));

    static GaugeMeter()
    {
        // 任意视觉相关属性变化 → 自动重绘
        AffectsRender<GaugeMeter>(
            ValueProperty,
            MinimumProperty,
            MaximumProperty,
            TrackBrushProperty,
            AccentBrushProperty,
            TrackThicknessProperty,
            ShowTicksProperty,
            CenterTextProperty,
            CenterLabelProperty,
            CenterBrushProperty);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public double TrackThickness
    {
        get => GetValue(TrackThicknessProperty);
        set => SetValue(TrackThicknessProperty, value);
    }

    public bool ShowTicks
    {
        get => GetValue(ShowTicksProperty);
        set => SetValue(ShowTicksProperty, value);
    }

    /// <summary>圆心显示的文字（通常是当前读数）。</summary>
    public string? CenterText
    {
        get => GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    /// <summary>圆心文字下方的小标签。</summary>
    public string? CenterLabel
    {
        get => GetValue(CenterLabelProperty);
        set => SetValue(CenterLabelProperty, value);
    }

    /// <summary>圆心文字画刷。</summary>
    public IBrush? CenterBrush
    {
        get => GetValue(CenterBrushProperty);
        set => SetValue(CenterBrushProperty, value);
    }

    /// <summary>弧线的起点角度（数学坐标系，逆时针为正）。</summary>
    private const double StartAngle = 135d;

    /// <summary>弧线扫过的角度。</summary>
    private const double SweepAngle = 270d;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var thickness = Math.Max(2d, TrackThickness);
        // 半径取可用区域中较小的一半，并预留描边宽度
        var radius = Math.Min(width, height) / 2d - thickness / 2d - 4d;
        if (radius <= 4d)
        {
            return;
        }

        var center = new Point(width / 2d, height / 2d);

        // 归一化进度（0 ~ 1）
        var min = Minimum;
        var max = Maximum <= min ? min + 1d : Maximum;
        var progress = Math.Clamp((Value - min) / (max - min), 0d, 1d);

        var trackPen = new Pen(TrackBrush ?? Brushes.DimGray, thickness, lineCap: PenLineCap.Flat);

        // 1) 先画完整轨道（270°）
        if (SweepAngle > 0)
        {
            context.DrawGeometry(null, trackPen,
                BuildArc(center, radius, StartAngle, StartAngle - SweepAngle));
        }

        // 2) 再画进度弧
        if (progress > 0.0001)
        {
            var endAngle = StartAngle - SweepAngle * progress;
            var accentPen = new Pen(AccentBrush ?? Brushes.DeepSkyBlue, thickness, lineCap: PenLineCap.Flat);
            context.DrawGeometry(null, accentPen,
                BuildArc(center, radius, StartAngle, endAngle));
        }

        // 3) 刻度线：细、直角，向内收
        if (ShowTicks)
        {
            var tickBrush = (TrackBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
            var tickPen = new Pen(new SolidColorBrush(tickBrush, 0.55), 1);

            const int tickCount = 27;
            var inner = radius - thickness / 2d - 5d;

            for (var i = 0; i <= tickCount; i++)
            {
                var angle = StartAngle - SweepAngle * i / tickCount;
                // 每第 9 根为长刻度
                var length = i % 9 == 0 ? 9d : 5d;

                context.DrawLine(tickPen,
                    PolarToPoint(center, inner, angle),
                    PolarToPoint(center, inner - length, angle));
            }
        }

        // 4) 圆心读数：工业仪表的核心信息
        if (!string.IsNullOrEmpty(CenterText))
        {
            var centerBrush = CenterBrush ?? Brushes.White;
            var number = new FormattedText(
                CenterText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Bahnschrift"),
                Math.Max(16, radius * 0.46),
                centerBrush);

            // 圆弧开口朝下，读数略高于圆心才落在视觉中心
            context.DrawText(number, new Point(
                center.X - number.Width / 2d,
                center.Y - number.Height * 0.75));

            if (!string.IsNullOrEmpty(CenterLabel))
            {
                var label = new FormattedText(
                    CenterLabel,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono"),
                    Math.Max(8, radius * 0.11),
                    centerBrush);

                context.DrawText(label, new Point(
                    center.X - label.Width / 2d,
                    center.Y + number.Height * 0.35));
            }
        }
    }

    /// <summary>
    /// 构造一段圆弧几何。
    /// 角度用"数学坐标系"表达（逆时针为正、0° 在右侧），但屏幕 y 轴向下，
    /// 因此从 StartAngle 逐步**减小**角度画弧，在屏幕上看起来是顺时针 ——
    /// 所以这里必须是 SweepDirection.Clockwise，否则弧会跑到圆的另一侧。
    /// </summary>
    private static Geometry BuildArc(Point center, double radius, double fromAngle, double toAngle)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            var start = PolarToPoint(center, radius, fromAngle);
            var end = PolarToPoint(center, radius, toAngle);

            ctx.BeginFigure(start, false);

            // 单段弧最大 180°，超过则拆成两段，避免渲染器误判
            var delta = Math.Abs(fromAngle - toAngle);
            if (delta > 180d)
            {
                var midAngle = fromAngle - Math.Sign(fromAngle - toAngle) * delta / 2d;
                var mid = PolarToPoint(center, radius, midAngle);
                ctx.ArcTo(mid, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            }
            else
            {
                ctx.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            }

            ctx.EndFigure(false);
        }

        return geometry;
    }

    /// <summary>极坐标 → 屏幕坐标（注意屏幕 y 轴向下，所以取负号）。</summary>
    private static Point PolarToPoint(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180d;
        return new Point(
            center.X + radius * Math.Cos(radians),
            center.Y - radius * Math.Sin(radians));
    }
}
