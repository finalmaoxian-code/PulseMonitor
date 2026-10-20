using Avalonia.Controls;

namespace PulseMonitor.Views;

/// <summary>
/// 侧边栏组件的后台代码。
/// 选中状态完全由 ListBox + 绑定驱动，这里不需要任何逻辑。
/// </summary>
public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }
}
