using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseMonitor.ViewModels;

/// <summary>
/// 视图模型基类。
///
/// Avalonia 组件特性说明：
/// · 实现 INotifyPropertyChanged 后，XAML 里的 {Binding Xxx} 会自动刷新，
///   这是 Avalonia 数据绑定（编译期绑定 + 运行期绑定）的基础契约。
/// · 项目没有引入 CommunityToolkit.Mvvm，这里手写 SetField 泛型方法替代
///   [ObservableProperty] 源生成器，保持零新增依赖。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>触发属性变更通知。</summary>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>赋值并在值确实变化时发出通知，返回是否发生变化。</summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
