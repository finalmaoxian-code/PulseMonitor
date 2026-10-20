namespace PulseMonitor.ViewModels;

/// <summary>
/// 负载等级。
///
/// Avalonia 组件特性说明：
/// · 把它做成枚举而不是"直接算颜色"，是为了让 ViewModel 只描述**语义**
///   （轻载 / 正常 / 偏高 / 过载），具体用哪个画刷交给 ControlTheme 决定。
/// · 自定义控件（如 StatusPill）暴露 <c>Level</c> 属性后，
///   主题里可以用属性选择器 <c>^[Level=High]</c> 直接换色，
///   于是同一套控件在明暗主题下自动取到对应的画刷，代码零改动。
/// </summary>
public enum LoadKind
{
    /// <summary>轻载（&lt; 40%）。</summary>
    Calm,

    /// <summary>正常（40% ~ 70%）。</summary>
    Normal,

    /// <summary>偏高（70% ~ 90%）。</summary>
    High,

    /// <summary>过载（≥ 90%）。</summary>
    Critical,
}

/// <summary>
/// 负载等级的换算工具：百分比 ↔ 语义等级 ↔ 中文描述。
/// 阈值集中在这里，界面各处判断标准始终一致。
/// </summary>
public static class LoadScale
{
    /// <summary>依据负载百分比给出等级。</summary>
    public static LoadKind ToKind(double percent) => percent switch
    {
        < 40d => LoadKind.Calm,
        < 70d => LoadKind.Normal,
        < 90d => LoadKind.High,
        _ => LoadKind.Critical,
    };

    /// <summary>等级对应的中文描述，直接显示在界面上。</summary>
    public static string Describe(LoadKind kind) => kind switch
    {
        LoadKind.Calm => "轻载",
        LoadKind.High => "偏高",
        LoadKind.Critical => "过载",
        _ => "正常",
    };

    /// <summary>百分比 → 中文描述（一步到位）。</summary>
    public static string Describe(double percent) => Describe(ToKind(percent));
}
