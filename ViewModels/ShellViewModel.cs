namespace PulseMonitor.ViewModels;

/// <summary>
/// 外壳视图模型：负责"当前显示第几页"。
///
/// Avalonia 组件特性说明：
/// · 侧边栏是 ListBox（容器是自定义的 NavItem），内容区是 Carousel；
///   两者的 SelectedIndex 都双向绑定到这里的 <see cref="SelectedIndex"/>，
///   于是"点侧边栏 → 内容区翻页"整个过程不需要写一行事件代码。
/// · 三个页面共享同一个 <see cref="DashboardViewModel"/>，数据只采集一次，
///   页面之间切换不会重新开始采样。
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private int _selectedIndex;

    public ShellViewModel()
    {
        Dashboard = new DashboardViewModel();
    }

    /// <summary>仪表盘数据，三个页面共用。</summary>
    public DashboardViewModel Dashboard { get; }

    /// <summary>当前页索引（0 总览 / 1 网络 / 2 进程）。</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            // 容器还没生成时绑定会塞 -1 进来，这里做一次保护
            if (value < 0)
            {
                return;
            }

            if (SetField(ref _selectedIndex, value))
            {
                // 顶栏标题是派生值，索引一变就要通知界面刷新
                RaisePropertyChanged(nameof(PageTitle));
                RaisePropertyChanged(nameof(PageCode));
                // 进程排行只在可见时才需要高频刷新
                Dashboard.IsProcessPageVisible = value == 2;
            }
        }
    }

    /// <summary>当前页标题（派生属性，随 <see cref="SelectedIndex"/> 变化）。</summary>
    public string PageTitle => _selectedIndex switch
    {
        1 => "网络流量",
        2 => "进程明细",
        _ => "运行总览",
    };

    /// <summary>当前页通道代号，显示在顶栏标题右侧。</summary>
    public string PageCode => _selectedIndex switch
    {
        1 => "NET / 02",
        2 => "PROC / 03",
        _ => "SYS / 01",
    };
}
