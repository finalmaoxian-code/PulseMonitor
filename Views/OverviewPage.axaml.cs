using Avalonia.Controls;

namespace PulseMonitor.Views;

/// <summary>
/// 总览页的后台代码。
///
/// 这里故意保持为空：所有状态都在 DashboardViewModel 里，
/// 页面只由 XAML 描述外观 —— 这是 Avalonia 推荐的 MVVM 分工。
/// </summary>
public partial class OverviewPage : UserControl
{
    public OverviewPage()
    {
        InitializeComponent();
    }
}
