using System;
using PulseMonitor.Models;

namespace PulseMonitor.ViewModels;

/// <summary>
/// 进程列表中的一行。行对象按「PID + 启动时间」长期复用，
/// 刷新时只把新快照写进来并通知变化的属性，避免整个列表重建。
/// </summary>
public sealed class ProcessRowViewModel : ViewModelBase
{
    private ProcessSnapshot _snapshot;

    public ProcessRowViewModel(ProcessSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public int Pid => _snapshot.Pid;

    public DateTimeOffset? StartTimeUtc => _snapshot.StartTimeUtc;

    public string Name => _snapshot.Name;

    public string? ExecutablePath => _snapshot.ExecutablePath;

    public string PidText => _snapshot.PidText;

    public string IdentityText => _snapshot.IdentityText;

    public string CpuText => _snapshot.CpuText;

    public string CpuTooltip => _snapshot.CpuTooltip;

    public string MemoryText => _snapshot.MemoryText;

    public string ThreadsText => _snapshot.ThreadsText;

    public string HandlesText => _snapshot.HandlesText;

    public void Update(ProcessSnapshot snapshot)
    {
        if (Equals(_snapshot, snapshot))
        {
            return;
        }

        _snapshot = snapshot;

        RaisePropertyChanged(nameof(Name));
        RaisePropertyChanged(nameof(ExecutablePath));
        RaisePropertyChanged(nameof(CpuText));
        RaisePropertyChanged(nameof(CpuTooltip));
        RaisePropertyChanged(nameof(MemoryText));
        RaisePropertyChanged(nameof(ThreadsText));
        RaisePropertyChanged(nameof(HandlesText));
    }
}
