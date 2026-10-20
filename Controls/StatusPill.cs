using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using PulseMonitor.ViewModels;

namespace PulseMonitor.Controls;

/// <summary>
/// 状态胶囊（StatusPill）：左边一个呼吸色点 + 右边一段文字。
///
/// Avalonia 组件特性说明：
/// · 纯 TemplatedControl，没有一行绘制代码，全部靠 ControlTheme 里的模板描述。
/// · <see cref="Level"/> 暴露的是"语义"（轻载 / 正常 / 偏高 / 过载）。
///   控件在 Level 变化时把语义写成 CSS 风格样式类（calm / normal / high / critical），
///   ControlTheme 里再用样式选择器 <c>^.high</c> 挑颜色 ——
///   于是"语义 → 颜色"的映射完全留在皮肤文件里，C# 没有一处硬编码颜色，
///   明暗主题也能各自取到自己的画刷。
/// · IsLive 为 true 时，模板里的光晕会播放无限循环的透明度动画（纯 XAML）。
/// </summary>
public class StatusPill : TemplatedControl
{
    /// <summary>显示的文字。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusPill, string?>(nameof(Text));

    /// <summary>色点的兜底画刷（保持这个属性，方便脱离 Level 独立使用）。</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<StatusPill, IBrush?>(nameof(AccentBrush));

    /// <summary>
    /// 是否播放呼吸动画。默认关闭：动画会持续请求重绘，
    /// 只应由真正表达「实时在线」的那一个胶囊显式开启。
    /// </summary>
    public static readonly StyledProperty<bool> PulseProperty =
        AvaloniaProperty.Register<StatusPill, bool>(nameof(Pulse));

    /// <summary>
    /// 语义负载等级：设置后色点颜色由 ControlTheme 的语义类决定；
    /// 保持 null 时则完全由 <see cref="AccentBrush"/> 决定，两种用法互不干扰。
    /// </summary>
    public static readonly StyledProperty<LoadKind?> LevelProperty =
        AvaloniaProperty.Register<StatusPill, LoadKind?>(nameof(Level));

    /// <summary>语义等级对应的样式类名，顺序必须与 <see cref="LoadKind"/> 的取值一致。</summary>
    private static readonly string[] LevelClasses = { "calm", "normal", "high", "critical" };

    static StatusPill()
    {
        // Level 变化 → 同步样式类，ControlTheme 的选择器随之生效
        LevelProperty.Changed.AddClassHandler<StatusPill>((pill, _) => pill.UpdateLevelClasses());
    }

    public StatusPill()
    {
        // 构造时先按默认值挂一次样式类，保证首次渲染就有正确颜色
        UpdateLevelClasses();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public bool Pulse
    {
        get => GetValue(PulseProperty);
        set => SetValue(PulseProperty, value);
    }

    public LoadKind? Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <summary>
    /// 把当前 Level 同步成样式类（只保留一个，避免选择器互相干扰）。
    /// Level 为 null 时把所有语义类摘掉，此时颜色回落到 AccentBrush。
    /// </summary>
    private void UpdateLevelClasses()
    {
        var level = Level;
        var current = level.HasValue ? (int)level.Value : -1;

        for (var i = 0; i < LevelClasses.Length; i++)
        {
            // Classes.Set 第二个参数为 true 表示"添加"，false 表示"移除"
            Classes.Set(LevelClasses[i], i == current);
        }
    }
}
