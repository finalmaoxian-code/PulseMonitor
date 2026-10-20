using PulseMonitor.Models;

namespace PulseMonitor.ViewModels;

/// <summary>网卡明细行。把原始采样状态转换成界面可直接显示的文本。</summary>
public sealed class NetworkAdapterViewModel
{
    public NetworkAdapterViewModel(NetworkAdapterSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public NetworkAdapterSnapshot Snapshot { get; }

    public string Name => Snapshot.Name;

    public string Description => Snapshot.Description;

    public string DetailText
    {
        get
        {
            string kind = Snapshot.IsLikelyVirtual ? "虚拟/VPN" : "物理网卡";
            string speed = Snapshot.LinkSpeedBitsPerSecond > 0
                ? " · " + Formatters.BitRate(Snapshot.LinkSpeedBitsPerSecond)
                : "";
            string description = string.IsNullOrWhiteSpace(Snapshot.Description)
                ? ""
                : " · " + Snapshot.Description;
            return $"{Snapshot.InterfaceType} · {kind}{speed}{description}";
        }
    }

    public string DownloadText => FormatRate(Snapshot.DownloadBytesPerSecond);

    public string UploadText => FormatRate(Snapshot.UploadBytesPerSecond);

    public string StatusText =>
        $"{Snapshot.DownloadBytesPerSecond.Status.ToShortText()} / {Snapshot.UploadBytesPerSecond.Status.ToShortText()}";

    public string AggregateText => Snapshot.IsIncludedInAggregate ? "计入汇总" : "已排除";

    public string Tooltip => Snapshot.IsIncludedInAggregate
        ? $"下载：{DownloadText}；上传：{UploadText}；状态：{StatusText}"
        : $"未计入汇总，避免虚拟网卡或 VPN 重复统计。状态：{StatusText}";

    private static string FormatRate(MetricSample<double> sample) => sample.Status switch
    {
        SampleStatus.Valid => Formatters.Rate(sample.Value),
        SampleStatus.Initializing => "采集中",
        SampleStatus.CounterReset => "重置",
        SampleStatus.Stale => "已过期",
        SampleStatus.Invalid => "采样无效",
        _ => "N/A",
    };
}
