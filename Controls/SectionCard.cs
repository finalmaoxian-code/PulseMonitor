using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace PulseMonitor.Controls;

/// <summary>
/// 内容分区卡片（SectionCard）。
///
/// Avalonia 组件特性说明：
/// · 继承自 <see cref="HeaderedContentControl"/>：直接获得 Header / HeaderTemplate 两个
///   现成属性，不用自己再声明一遍 —— 这是 Avalonia 基础控件库的分层设计（控件的
///   通用能力都在 Avalonia.Controls.Primitives 里复用）。
/// · 再加 <see cref="Hint"/>（右上角灰字说明）和 <see cref="AccentBrush"/>（强调色），
///   把"这个卡片想强调什么"也变成可绑定属性，而不是写死在模板里。
/// · 外观在 Themes/Controls/SectionCardTheme.axaml，且模板里的 ContentPresenter
///   会自动承接外部塞进来的任意内容（图表、列表、按钮……）。
/// </summary>
public class SectionCard : HeaderedContentControl
{
    /// <summary>标题右侧的补充说明文字。</summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<SectionCard, string?>(nameof(Hint));

    /// <summary>标题色点的强调色。</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<SectionCard, IBrush?>(nameof(AccentBrush));

    /// <summary>面板代号（如 SYS-01），显示在标题与提示之间。</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<SectionCard, string?>(nameof(Code));

    /// <summary>标题右侧的补充说明文字。</summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>标题色点的强调色。</summary>
    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    /// <summary>面板代号（如 SYS-01），显示在标题与提示之间。</summary>
    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }
}
