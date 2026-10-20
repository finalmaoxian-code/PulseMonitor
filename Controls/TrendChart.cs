using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PulseMonitor.Controls;

/// <summary>
/// 趋势折线图（TrendChart）—— 纯自绘控件。
///
/// Avalonia 组件特性说明：
/// · 继承 Control 并重写 Render(DrawingContext)，用 StreamGeometry 现画折线与渐变填充；
///   相比"用几十个 Line/Polyline 拼"更轻，也更贴合 Avalonia 的渲染管线。
/// · 数据源通过 <see cref="Values"/> 这个 StyledProperty 暴露，
///   并监听 INotifyCollectionChanged —— 这样 ViewModel 只要往 ObservableCollection
///   里 Add，图表就自动重绘，属于 Avalonia 里"属性驱动的命令式绘制"。
/// · 所有颜色都是 StyledProperty，可写成 {DynamicResource ...}，自动跟随明暗主题切换。
/// · 同时也保留 AddSample() 命令式接口，非 MVVM 场景也能直接用。
/// </summary>
public class TrendChart : Control
{
    /// <summary>纵轴最大值（AutoScale 为 true 时忽略）。</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<TrendChart, double>(nameof(Maximum), 100d);

    /// <summary>是否按数据自适应纵向范围。</summary>
    public static readonly StyledProperty<bool> AutoScaleProperty =
        AvaloniaProperty.Register<TrendChart, bool>(nameof(AutoScale));

    /// <summary>折线与填充的主色。</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<TrendChart, IBrush?>(nameof(LineBrush));

    /// <summary>网格线画刷。</summary>
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TrendChart, IBrush?>(nameof(GridBrush));

    /// <summary>折线粗细。</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<TrendChart, double>(nameof(StrokeThickness), 2d);

    /// <summary>画布上保留的样本数量（决定横向疏密）。</summary>
    public static readonly StyledProperty<int> MaxSamplesProperty =
        AvaloniaProperty.Register<TrendChart, int>(nameof(MaxSamples), 60);

    /// <summary>数据源。绑定 ObservableCollection 时增删会自动重绘。</summary>
    public static readonly StyledProperty<IEnumerable<double>?> ValuesProperty =
        AvaloniaProperty.Register<TrendChart, IEnumerable<double>?>(nameof(Values));

    static TrendChart()
    {
        AffectsRender<TrendChart>(
            MaximumProperty,
            AutoScaleProperty,
            LineBrushProperty,
            GridBrushProperty,
            StrokeThicknessProperty,
            MaxSamplesProperty);

        // 数据源换了 → 重新挂钩集合变更事件
        ValuesProperty.Changed.AddClassHandler<TrendChart>((chart, _) => chart.OnValuesChanged());
    }

    /// <summary>命令式追加的样本（未绑定 Values 时使用）。</summary>
    private readonly List<double> _samples = new();

    private INotifyCollectionChanged? _observed;

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public bool AutoScale
    {
        get => GetValue(AutoScaleProperty);
        set => SetValue(AutoScaleProperty, value);
    }

    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public int MaxSamples
    {
        get => GetValue(MaxSamplesProperty);
        set => SetValue(MaxSamplesProperty, value);
    }

    public IEnumerable<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>追加一个样本点（未绑定 Values 时使用）。</summary>
    public void AddSample(double value)
    {
        var capacity = Math.Max(2, MaxSamples);
        _samples.Add(double.IsFinite(value) ? value : 0d);

        // 环形缓冲：超过容量就把最老的样本挤掉
        while (_samples.Count > capacity)
        {
            _samples.RemoveAt(0);
        }

        InvalidateVisual();
    }

    /// <summary>清空命令式样本。</summary>
    public void Clear()
    {
        _samples.Clear();
        InvalidateVisual();
    }

    /// <summary>重新挂钩数据源的集合变更事件。</summary>
    private void OnValuesChanged()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnCollectionChanged;
        }

        _observed = Values as INotifyCollectionChanged;

        if (_observed is not null)
        {
            _observed.CollectionChanged += OnCollectionChanged;
        }

        InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 2 || height <= 2)
        {
            return;
        }

        var gridPen = new Pen(GridBrush ?? new SolidColorBrush(Colors.SlateGray, 0.18), 1);

        // 1) 横向网格线（4 等分）
        const int rows = 4;
        for (var i = 1; i < rows; i++)
        {
            var y = height * i / rows;
            context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
        }

        var samples = Values is null ? _samples : new List<double>(Values);
        if (samples.Count == 0)
        {
            return;
        }

        // 2) 纵轴范围
        var max = AutoScale ? Math.Max(1d, GetMaxSample(samples) * 1.15) : Math.Max(1d, Maximum);

        // 3) 折线几何（右侧对齐：最新样本贴右边缘）
        var capacity = Math.Max(2, MaxSamples);
        var stepX = width / (capacity - 1);
        var startIndex = capacity - samples.Count;
        var lineBrush = LineBrush ?? Brushes.DeepSkyBlue;

        var line = new StreamGeometry();
        var fill = new StreamGeometry();

        using (var ctx = line.Open())
        using (var fillCtx = fill.Open())
        {
            var first = new Point(startIndex * stepX, ToY(samples[0], max, height));

            ctx.BeginFigure(first, false);
            fillCtx.BeginFigure(new Point(first.X, height), true);
            fillCtx.LineTo(first);

            for (var i = 1; i < samples.Count; i++)
            {
                var point = new Point((startIndex + i) * stepX, ToY(samples[i], max, height));
                ctx.LineTo(point);
                fillCtx.LineTo(point);
            }

            var lastX = (startIndex + samples.Count - 1) * stepX;
            ctx.EndFigure(false);
            fillCtx.LineTo(new Point(lastX, height));
            fillCtx.EndFigure(true);
        }

        // 4) 折线下方的渐变填充：工业风只保留极淡的一层，避免"发光"感
        var baseColor = (lineBrush as ISolidColorBrush)?.Color ?? Colors.DeepSkyBlue;
        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(38, baseColor.R, baseColor.G, baseColor.B), 0),
                new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1),
            },
        };

        context.DrawGeometry(fillBrush, null, fill);

        // 5) 折线本体：细线直角，不用圆角端点
        context.DrawGeometry(null,
            new Pen(lineBrush, Math.Max(1d, StrokeThickness), lineJoin: PenLineJoin.Miter),
            line);

        // 6) 最新样本：一个小方块标记当前位置
        var lastPoint = new Point(
            (startIndex + samples.Count - 1) * stepX,
            ToY(samples[samples.Count - 1], max, height));

        context.DrawRectangle(lineBrush, null, new Rect(lastPoint.X - 2, lastPoint.Y - 2, 4, 4));
    }

    /// <summary>把数值映射成画布纵坐标（顶部为 0，底部为 height）。</summary>
    private static double ToY(double value, double max, double height)
    {
        var ratio = Math.Clamp(value / max, 0d, 1d);
        // 上下留 3px 余量，避免线贴边被裁掉
        return height - 3d - (height - 6d) * ratio;
    }

    private static double GetMaxSample(IReadOnlyList<double> samples)
    {
        var max = 0d;
        for (var i = 0; i < samples.Count; i++)
        {
            if (samples[i] > max)
            {
                max = samples[i];
            }
        }

        return max;
    }
}
