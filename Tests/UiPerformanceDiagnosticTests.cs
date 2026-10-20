using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseMonitor;
using PulseMonitor.ViewModels;
using Xunit;

namespace PulseMonitor.Tests;

/// <summary>
/// 进程页与页面版式的性能回归测试。
///
/// 注意：整个测试类共用一个 Avalonia 应用实例（见 AvaloniaTestHost），
/// 因为 Avalonia 的平台初始化在进程内只能执行一次；重复 SetupWithoutStarting
/// 会让控件样式与字体系统处于不一致状态，产生难以复现的假失败。
/// </summary>
public sealed class UiPerformanceDiagnosticTests
{
    [Fact]
    public void ProcessPageStaysVirtualizedAndRefreshIsCheap()
    {
        using (var host = AvaloniaTestHost.CreateWindow())
        {
            ShellViewModel shell = host.Shell;
            Window window = host.Window;

            Pump(shell.Dashboard.StartAsync(), TimeSpan.FromSeconds(60));
            Assert.NotEmpty(shell.Dashboard.Processes);

            long beforeNav = CountControls(window);

            var sw = Stopwatch.StartNew();
            shell.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            sw.Stop();

            long afterNav = CountControls(window);
            int rowContainers = window.GetVisualDescendants().OfType<ListBoxItem>().Count();

            // 数百进程 × 每行约 12 个控件，不虚拟化时控件数会到 4000+
            Assert.True(afterNav - beforeNav < 1500, $"进程页新增控件过多：{afterNav - beforeNav}");
            Assert.True(rowContainers < 40, $"进程行容器未被虚拟化：{rowContainers}");
            Assert.True(sw.ElapsedMilliseconds < 800, $"进入进程页耗时 {sw.ElapsedMilliseconds} ms");

            for (int i = 0; i < 3; i++)
            {
                sw.Restart();
                Pump(shell.Dashboard.RefreshAsync(), TimeSpan.FromSeconds(60));
                sw.Stop();

                Assert.True(sw.ElapsedMilliseconds < 800, $"进程页第 {i} 次刷新耗时 {sw.ElapsedMilliseconds} ms");
            }
        }
    }

    [Fact]
    public void OverviewAndNetworkPagesFitWithoutScrolling()
    {
        using (var host = AvaloniaTestHost.CreateWindow())
        {
            ShellViewModel shell = host.Shell;
            Window window = host.Window;

            Pump(shell.Dashboard.StartAsync(), TimeSpan.FromSeconds(60));

            for (int index = 0; index < 2; index++)
            {
                shell.SelectedIndex = index;
                Dispatcher.UIThread.RunJobs();
                Settle();

                UserControl page = window.GetVisualDescendants().OfType<UserControl>()
                    .First(c => c.IsEffectivelyVisible &&
                                c is PulseMonitor.Views.OverviewPage or PulseMonitor.Views.NetworkPage);
                ScrollViewer scroll = page.GetVisualDescendants().OfType<ScrollViewer>().First();

                Assert.True(
                    scroll.Extent.Height <= scroll.Viewport.Height + 1,
                    $"页面 {page.GetType().Name} 出现纵向滚动：extent={scroll.Extent.Height:F0} viewport={scroll.Viewport.Height:F0}");
            }
        }
    }

    internal static void Pump(Task task, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (!task.IsCompleted && sw.Elapsed < timeout)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompleted, "采样任务超时未完成");
        task.GetAwaiter().GetResult();
    }

    internal static void Settle()
    {
        for (int i = 0; i < 30; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    internal static long CountControls(Window window)
        => window.GetVisualDescendants().OfType<Control>().Count();
}

/// <summary>整个测试进程共用一次 Avalonia headless 初始化。</summary>
internal static class AvaloniaTestHost
{
    private static readonly object Gate = new();
    private static bool _initialized;

    public static TestWindow CreateWindow()
    {
        lock (Gate)
        {
            if (!_initialized)
            {
                AppBuilder.Configure<App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
                    .SetupWithoutStarting();
                _initialized = true;
            }
        }

        var shell = new ShellViewModel();
        var window = new MainWindow { DataContext = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new TestWindow(shell, window);
    }
}

internal sealed class TestWindow(ShellViewModel shell, Window window) : IDisposable
{
    public ShellViewModel Shell { get; } = shell;

    public Window Window { get; } = window;

    public void Dispose()
    {
        Shell.Dashboard.Dispose();
        Window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
