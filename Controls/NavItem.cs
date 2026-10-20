using Avalonia;
using Avalonia.Controls;

namespace PulseMonitor.Controls;

/// <summary>
/// 侧边栏导航项（NavItem）。
///
/// Avalonia 组件特性说明：
/// · 继承 ListBoxItem：直接获得 ListBox 的容器语义（IsSelected / :selected 伪类），
///   同时保留 ContentControl 的 Content 能力，是 Avalonia 里做导航的惯用做法。
///   侧边栏因此不需要任何自定义选中逻辑 —— ListBox 维护 SelectedIndex，
///   伪类驱动视觉，绑定把索引送到内容区。
/// · 外观（图标 + 主标题 + 副标题 + 选中态）全部由 ControlTheme 描述，
///   见 Themes/Controls/NavItemTheme.axaml，控件本身只暴露数据属性。
/// · Glyph 使用 Windows 自带的 Segoe MDL2 Assets 图标字体，无需内嵌图片资源。
/// </summary>
public class NavItem : ListBoxItem
{
    /// <summary>图标字形（Segoe MDL2 Assets 码位）。</summary>
    public static readonly StyledProperty<string?> GlyphProperty =
        AvaloniaProperty.Register<NavItem, string?>(nameof(Glyph));

    /// <summary>导航主标题。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<NavItem, string?>(nameof(Text));

    /// <summary>标题下方的小字说明。</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<NavItem, string?>(nameof(Subtitle));

    /// <summary>通道编号（如 01），显示在图标左侧。</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<NavItem, string?>(nameof(Code));

    /// <summary>图标字形（Segoe MDL2 Assets 码位）。</summary>
    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>导航主标题。</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>标题下方的小字说明。</summary>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>通道编号（如 01），显示在图标左侧。</summary>
    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }
}
