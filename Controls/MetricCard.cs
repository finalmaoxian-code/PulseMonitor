using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PulseMonitor.Controls;

/// <summary>
/// 指标卡片（MetricCard）。
///
/// Avalonia 组件特性说明（体现"组件特性"的关键点）：
/// · 继承 ContentControl：既自带一套模板（Label / Value / Unit / Caption 四段式），
///   又能通过 Content 属性由外部塞入任意内容（例如右侧的迷你趋势图）。
/// · 外观全部写在 ControlTheme（Themes/Controls/MetricCardTheme.axaml）里，
///   代码只管"数据"，实现逻辑与皮肤彻底解耦 —— 换肤不必改一行 C#。
/// </summary>
public class MetricCard : ContentControl
{
    /// <summary>卡片左上角的小标签，例如 "CPU 使用率"。</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Label));

    /// <summary>主数值文本（外部已格式化，例如 "8.2"）。</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Value));

    /// <summary>数值单位，例如 "%"、"MB/s"。</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Unit));

    /// <summary>数值下方的补充说明。</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Caption));

    /// <summary>强调色，用于左侧色标与标签颜色。</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<MetricCard, IBrush?>(nameof(AccentBrush));

    /// <summary>面板代号（如 SYS-01），显示在标签右侧。</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<MetricCard, string?>(nameof(Code));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    /// <summary>面板代号（如 SYS-01），显示在标签右侧。</summary>
    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }
}
