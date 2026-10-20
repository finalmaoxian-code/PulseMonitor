using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PulseMonitor;

/// <summary>
/// 应用入口的后台代码。
///
/// 这里只做两件事：
///   1. 加载 App.axaml（全局资源 + 样式都在里面装配）；
///   2. 指定启动主题与主窗口。
/// 主题变体（Dark / Light）由 Themes/PulsePalette.axaml 的 ThemeDictionaries 提供，
/// 窗口里的切换按钮会改 Application.RequestedThemeVariant 来实时换肤。
/// </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 默认使用深色主题（与色板里 Dark 那一套对应）
            RequestedThemeVariant = ThemeVariant.Dark;
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
