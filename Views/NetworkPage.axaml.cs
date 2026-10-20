using Avalonia.Controls;

namespace PulseMonitor.Views;

/// <summary>
/// 网络页的后台代码。逻辑全部在 DashboardViewModel 中，这里只负责加载 XAML。
/// </summary>
public partial class NetworkPage : UserControl
{
    public NetworkPage()
    {
        InitializeComponent();
    }
}
