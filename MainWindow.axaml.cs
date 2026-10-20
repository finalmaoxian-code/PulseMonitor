using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using PulseMonitor.ViewModels;

namespace PulseMonitor;

/// <summary>
/// 主窗口后台代码。
///
/// 这里刻意保持"很薄"，只做三件视图层才该做的事：
///   1. 创建并挂上 ShellViewModel（DataContext）；
///   2. 窗口打开 / 关闭时驱动监控的启动与释放；
///   3. 处理主题切换这类纯粹属于"窗口外观"的交互。
/// 所有数据采集、格式化、状态派生都在 ViewModel 里，界面结构在 AXAML 里。
/// </summary>
public partial class MainWindow : Window
{
    private readonly ShellViewModel _shell = new();

    private bool _isClosed;

    public MainWindow()
    {
        InitializeComponent();

        // 整个窗口共用一份外壳视图模型；
        // DataContext 会沿着可视树向下继承，三个页面再由各自绑定取 Dashboard。
        DataContext = _shell;

        Opened += OnOpened;
        Closed += OnClosed;
    }

    /// <summary>窗口打开后启动采样。</summary>
    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            await _shell.Dashboard.StartAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            Title = "PulseMonitor · 监控初始化失败：" + ex.Message;
        }
    }

    /// <summary>窗口关闭后停止定时器，避免后台线程继续跑。</summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _shell.Dashboard.Dispose();
    }

    /// <summary>
    /// 主题切换：直接改 Application 的 RequestedThemeVariant，
    /// 色板里的 ThemeDictionaries 会自动换成另一套画刷，
    /// 界面上所有 {DynamicResource ...} 引用会即时更新，无需重建界面。
    /// </summary>
    private void OnThemeSwitchChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is null || sender is not ToggleSwitch toggle)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = toggle.IsChecked == true ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}
