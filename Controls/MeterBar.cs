using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using PulseMonitor.ViewModels;

namespace PulseMonitor.Controls;

/// <summary>
/// 横向进度条（MeterBar）。
///
/// Avalonia 组件特性说明：
/// · 模板控件（TemplatedControl）+ 模板部件（PART_Scale）：
///   外观由 ControlTheme 提供，代码在 OnApplyTemplate 里按名字取出部件。
/// · 进度由两列按 Star 比例分配宽度，交给布局系统计算，
///   不需要在代码里量轨道宽度，读数变化即时生效。
/// · 颜色来自 IndicatorBrush（可在 XAML 里写 {DynamicResource ...}），
///   因此进度条本身不关心明暗主题，换肤零改动。
/// </summary>
[TemplatePart("PART_Track", typeof(Border))]
[TemplatePart("PART_Scale", typeof(Grid))]
public class MeterBar : TemplatedControl
{
    /// <summary>当前值。</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(Value));

    /// <summary>满值，默认 100。</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(Maximum), 100d);

    /// <summary>轨道底色。</summary>
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<MeterBar, IBrush?>(nameof(TrackBrush));

    /// <summary>进度颜色。</summary>
    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<MeterBar, IBrush?>(nameof(IndicatorBrush));

    /// <summary>条的高度。</summary>
    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(BarHeight), 8d);

    /// <summary>语义负载等级（可选）：设置后按等级自动换色。</summary>
    public static readonly StyledProperty<LoadKind?> LevelProperty =
        AvaloniaProperty.Register<MeterBar, LoadKind?>(nameof(Level));

    /// <summary>语义等级对应的样式类名，顺序与 <see cref="LoadKind"/> 取值一致。</summary>
    private static readonly string[] LevelClasses = { "calm", "normal", "high", "critical" };

    private Grid? _scale;
    private ColumnDefinition? _indicatorColumn;
    private ColumnDefinition? _remainderColumn;

    static MeterBar()
    {
        // 值或满值变化时重新计算进度宽度
        ValueProperty.Changed.AddClassHandler<MeterBar>((bar, _) => bar.UpdateIndicator());
        MaximumProperty.Changed.AddClassHandler<MeterBar>((bar, _) => bar.UpdateIndicator());

        // Level 变化 → 同步样式类，让 ControlTheme 里的类选择器接管配色
        LevelProperty.Changed.AddClassHandler<MeterBar>((bar, _) => bar.UpdateLevelClasses());
    }

    public MeterBar()
    {
        // 构造时按默认（未设置等级）状态挂一次样式类
        UpdateLevelClasses();
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
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

    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    public double BarHeight
    {
        get => GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>语义负载等级；为 null 时完全由外部的 IndicatorBrush 决定颜色。</summary>
    public LoadKind? Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <summary>模板套用完成后，按名字取出模板部件。</summary>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _scale = e.NameScope.Find<Grid>("PART_Scale");

        if (_scale is not null && _scale.ColumnDefinitions.Count >= 2)
        {
            _indicatorColumn = _scale.ColumnDefinitions[0];
            _remainderColumn = _scale.ColumnDefinitions[1];
        }

        UpdateIndicator();
    }

    /// <summary>
    /// 按比例拆分轨道：填充列与剩余列都写成 <c>*</c>，
    /// 宽度由布局系统分配，读数变化时立即生效。
    /// </summary>
    private void UpdateIndicator()
    {
        if (_indicatorColumn is null || _remainderColumn is null)
        {
            return;
        }

        var max = Maximum <= 0 ? 1d : Maximum;
        var ratio = Math.Clamp(Value / max, 0d, 1d);

        _indicatorColumn.Width = new GridLength(ratio, GridUnitType.Star);
        _remainderColumn.Width = new GridLength(1d - ratio, GridUnitType.Star);
    }

    /// <summary>
    /// 把 <see cref="Level"/> 同步成样式类。
    /// Level 为 null 时把所有语义类都摘掉，此时颜色完全由 IndicatorBrush 决定；
    /// 一旦设置了等级，ControlTheme 的类选择器就会覆盖颜色 —— 语义与皮肤的映射留在 XAML 侧。
    /// </summary>
    private void UpdateLevelClasses()
    {
        var level = Level;
        var current = level.HasValue ? (int)level.Value : -1;

        for (var i = 0; i < LevelClasses.Length; i++)
        {
            Classes.Set(LevelClasses[i], i == current);
        }
    }
}
