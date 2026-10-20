using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using PulseMonitor.Models;
using PulseMonitor.Services;

namespace PulseMonitor.ViewModels;

/// <summary>
/// 仪表盘主视图模型。所有指标都只在采样状态为 Valid 时进入历史曲线和主数值；
/// 首次采样、权限不足、计数器重置、失效数据都有独立文案。
/// </summary>
public sealed class DashboardViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromSeconds(60);
    private const int HistoryCapacity = 60;

    /// <summary>进程页可见时的刷新间隔（Tick 数，1 Tick = 1 秒）。</summary>
    private const int ProcessRefreshTicksVisible = 2;

    /// <summary>进程页不可见时的刷新间隔：排行不展示，没必要每秒重建。</summary>
    private const int ProcessRefreshTicksHidden = 5;

    private readonly SystemMonitorService _systemMonitor = new();
    private readonly ProcessMonitorService _processMonitor = new();
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<ProcessKey, ProcessRowViewModel> _processRows = new();

    private bool _isRefreshing;
    private bool _isInitialized;
    private bool _disposed;
    private int _ticksSinceProcessRefresh;
    private bool _isProcessPageVisible;

    private long? _lastRefreshMonotonicTimestamp;
    private DateTimeOffset? _lastSuccessfulSystemSampleAt;
    private DateTimeOffset? _lastSuccessfulProcessSampleAt;

    private double _cpuPercent;
    private double _memoryPercent;
    private double _diskPercent;

    private string _cpuValueText = "--";
    private string _cpuPercentText = "--";
    private string _cpuCaptionText = "正在建立采样基准…";
    private string _cpuStatusText = "等待采样";
    private LoadKind _cpuLevel = LoadKind.Normal;
    private bool _cpuIsLive;

    private string _memoryValueText = "--";
    private string _memoryPercentText = "--";
    private string _memoryDetailText = "等待首次采样";
    private string _memoryCommitText = "等待首次采样";
    private string _memoryCacheText = "等待首次采样";
    private string _memoryStatusText = "等待采样";
    private LoadKind _memoryLevel = LoadKind.Normal;
    private bool _memoryIsLive;

    private string _diskValueText = "--";
    private string _diskDetailText = "等待首次采样";
    private string _diskStatusText = "等待采样";
    private bool _diskIsLive;

    private string _downloadRateText = "N/A";
    private string _uploadRateText = "N/A";
    private string _downloadRateValueText = "--";
    private string _downloadRateUnitText = "B/s";
    private string _uploadRateValueText = "--";
    private string _uploadRateUnitText = "B/s";
    private string _downloadStatusText = "等待采样";
    private string _uploadStatusText = "等待采样";
    private bool _downloadIsLive;
    private bool _uploadIsLive;

    private string _totalDownloadedText = "0 B";
    private string _totalUploadedText = "0 B";
    private string _networkSummaryText = "等待首次采样";

    private string _processCountText = "--";
    private string _processSummaryText = "正在读取进程…";

    private string _updatedAtText = "--:--:--";
    private string _samplingIntervalText = "--";
    private string _dataQualityText = "等待采样";
    private LoadKind _dataQualityLevel = LoadKind.Normal;

    private IReadOnlyList<NetworkAdapterViewModel> _networkAdapters = Array.Empty<NetworkAdapterViewModel>();

    public DashboardViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
    }

    public MetricHistory CpuHistory { get; } = new(HistoryCapacity, HistoryWindow);

    public MetricHistory MemoryHistory { get; } = new(HistoryCapacity, HistoryWindow);

    public MetricHistory DownloadHistory { get; } = new(HistoryCapacity, HistoryWindow);

    public MetricHistory UploadHistory { get; } = new(HistoryCapacity, HistoryWindow);

    public double CpuPercent
    {
        get => _cpuPercent;
        private set => SetField(ref _cpuPercent, value);
    }

    public double MemoryPercent
    {
        get => _memoryPercent;
        private set => SetField(ref _memoryPercent, value);
    }

    public double DiskPercent
    {
        get => _diskPercent;
        private set => SetField(ref _diskPercent, value);
    }

    public string CpuValueText
    {
        get => _cpuValueText;
        private set => SetField(ref _cpuValueText, value);
    }

    public string CpuPercentText
    {
        get => _cpuPercentText;
        private set => SetField(ref _cpuPercentText, value);
    }

    public string CpuCaptionText
    {
        get => _cpuCaptionText;
        private set => SetField(ref _cpuCaptionText, value);
    }

    public string CpuStatusText
    {
        get => _cpuStatusText;
        private set => SetField(ref _cpuStatusText, value);
    }

    public LoadKind CpuLevel
    {
        get => _cpuLevel;
        private set => SetField(ref _cpuLevel, value);
    }

    public bool CpuIsLive
    {
        get => _cpuIsLive;
        private set => SetField(ref _cpuIsLive, value);
    }

    public string MemoryValueText
    {
        get => _memoryValueText;
        private set => SetField(ref _memoryValueText, value);
    }

    public string MemoryPercentText
    {
        get => _memoryPercentText;
        private set => SetField(ref _memoryPercentText, value);
    }

    public string MemoryDetailText
    {
        get => _memoryDetailText;
        private set => SetField(ref _memoryDetailText, value);
    }

    public string MemoryCommitText
    {
        get => _memoryCommitText;
        private set => SetField(ref _memoryCommitText, value);
    }

    public string MemoryCacheText
    {
        get => _memoryCacheText;
        private set => SetField(ref _memoryCacheText, value);
    }

    public string MemoryStatusText
    {
        get => _memoryStatusText;
        private set => SetField(ref _memoryStatusText, value);
    }

    public LoadKind MemoryLevel
    {
        get => _memoryLevel;
        private set => SetField(ref _memoryLevel, value);
    }

    public bool MemoryIsLive
    {
        get => _memoryIsLive;
        private set => SetField(ref _memoryIsLive, value);
    }

    public string DiskValueText
    {
        get => _diskValueText;
        private set => SetField(ref _diskValueText, value);
    }

    public string DiskDetailText
    {
        get => _diskDetailText;
        private set => SetField(ref _diskDetailText, value);
    }

    public string DiskStatusText
    {
        get => _diskStatusText;
        private set => SetField(ref _diskStatusText, value);
    }

    public bool DiskIsLive
    {
        get => _diskIsLive;
        private set => SetField(ref _diskIsLive, value);
    }

    public string DownloadRateText
    {
        get => _downloadRateText;
        private set => SetField(ref _downloadRateText, value);
    }

    public string UploadRateText
    {
        get => _uploadRateText;
        private set => SetField(ref _uploadRateText, value);
    }

    public string DownloadRateValueText
    {
        get => _downloadRateValueText;
        private set => SetField(ref _downloadRateValueText, value);
    }

    public string DownloadRateUnitText
    {
        get => _downloadRateUnitText;
        private set => SetField(ref _downloadRateUnitText, value);
    }

    public string UploadRateValueText
    {
        get => _uploadRateValueText;
        private set => SetField(ref _uploadRateValueText, value);
    }

    public string UploadRateUnitText
    {
        get => _uploadRateUnitText;
        private set => SetField(ref _uploadRateUnitText, value);
    }

    public string DownloadStatusText
    {
        get => _downloadStatusText;
        private set => SetField(ref _downloadStatusText, value);
    }

    public string UploadStatusText
    {
        get => _uploadStatusText;
        private set => SetField(ref _uploadStatusText, value);
    }

    public bool DownloadIsLive
    {
        get => _downloadIsLive;
        private set => SetField(ref _downloadIsLive, value);
    }

    public bool UploadIsLive
    {
        get => _uploadIsLive;
        private set => SetField(ref _uploadIsLive, value);
    }

    public string TotalDownloadedText
    {
        get => _totalDownloadedText;
        private set => SetField(ref _totalDownloadedText, value);
    }

    public string TotalUploadedText
    {
        get => _totalUploadedText;
        private set => SetField(ref _totalUploadedText, value);
    }

    public string NetworkSummaryText
    {
        get => _networkSummaryText;
        private set => SetField(ref _networkSummaryText, value);
    }

    public IReadOnlyList<NetworkAdapterViewModel> NetworkAdapters
    {
        get => _networkAdapters;
        private set => SetField(ref _networkAdapters, value);
    }

    public string ProcessCountText
    {
        get => _processCountText;
        private set => SetField(ref _processCountText, value);
    }

    public string ProcessSummaryText
    {
        get => _processSummaryText;
        private set => SetField(ref _processSummaryText, value);
    }

    /// <summary>进程排行行。集合实例长期复用，只在进程增删时做增量操作。</summary>
    public ObservableCollection<ProcessRowViewModel> Processes { get; } = new();

    public string UpdatedAtText
    {
        get => _updatedAtText;
        private set => SetField(ref _updatedAtText, value);
    }

    public string SamplingIntervalText
    {
        get => _samplingIntervalText;
        private set => SetField(ref _samplingIntervalText, value);
    }

    public string DataQualityText
    {
        get => _dataQualityText;
        private set => SetField(ref _dataQualityText, value);
    }

    public LoadKind DataQualityLevel
    {
        get => _dataQualityLevel;
        private set => SetField(ref _dataQualityLevel, value);
    }

    public async Task StartAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        // 第一次只建立 CPU/网络累计值基准；建立后立刻采集一次进程，确保列表及时出现。
        await RefreshCoreAsync(forceProcessRefresh: false, deferProcessRefresh: true);
        await RefreshCoreAsync(forceProcessRefresh: true);
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    /// <summary>进程页是否在屏幕上。不可见时降低进程采样频率，UI 线程几乎零开销。</summary>
    public bool IsProcessPageVisible
    {
        get => _isProcessPageVisible;
        set
        {
            if (!SetField(ref _isProcessPageVisible, value))
            {
                return;
            }

            // 刚切到进程页时，让下一次 Tick 立刻补采一次，避免看到 5 秒前的旧排行
            if (value)
            {
                _ticksSinceProcessRefresh = ProcessRefreshTicksVisible;
            }
        }
    }

    public Task RefreshAsync() => RefreshCoreAsync(forceProcessRefresh: false);

    private async Task RefreshCoreAsync(bool forceProcessRefresh, bool deferProcessRefresh = false)
    {
        if (_isRefreshing || _disposed)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            _ticksSinceProcessRefresh++;
            int processRefreshInterval = IsProcessPageVisible
                ? ProcessRefreshTicksVisible
                : ProcessRefreshTicksHidden;

            bool refreshProcess = !deferProcessRefresh && (
                forceProcessRefresh
                || Processes.Count == 0
                || _ticksSinceProcessRefresh >= processRefreshInterval);

            Task<SystemSnapshot> systemTask = Task.Run(() => _systemMonitor.Read());
            Task<IReadOnlyList<ProcessSnapshot>>? processTask = refreshProcess
                ? Task.Run(() => _processMonitor.Read())
                : null;

            Exception? systemError = null;

            try
            {
                SystemSnapshot snapshot = await systemTask;
                ApplySnapshot(snapshot);
                _lastSuccessfulSystemSampleAt = snapshot.Timestamp;
                UpdateSamplingInterval();
            }
            catch (Exception ex)
            {
                systemError = ex;
                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (_lastSuccessfulSystemSampleAt is null ||
                    now - _lastSuccessfulSystemSampleAt.Value >= StaleAfter)
                {
                    ApplyStaleState(ex.Message);
                }
                else
                {
                    MarkRefreshDelay(ex.Message);
                }
            }

            if (processTask is not null)
            {
                _ticksSinceProcessRefresh = 0;

                try
                {
                    IReadOnlyList<ProcessSnapshot> processes = await processTask;
                    ApplyProcesses(processes);
                    _lastSuccessfulProcessSampleAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    if (_lastSuccessfulProcessSampleAt is null ||
                        now - _lastSuccessfulProcessSampleAt.Value >= StaleAfter)
                    {
                        ClearProcesses();
                        ProcessCountText = "N/A";
                    }

                    ProcessSummaryText = "进程采样失败：" + ex.Message;
                }
            }

            if (systemError is not null && _lastSuccessfulSystemSampleAt is not null)
            {
                ProcessSummaryText = "刷新延迟：" + systemError.Message;
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ApplySnapshot(SystemSnapshot snapshot)
    {
        ApplyCpu(snapshot.CpuUsagePercent);
        ApplyMemory(snapshot.Memory);
        ApplyDisk(snapshot.SystemDisk);
        ApplyNetwork(snapshot.Network);
        UpdateDataQuality(snapshot);

        UpdatedAtText = snapshot.Timestamp.ToLocalTime().ToString("HH:mm:ss");
    }

    private void ApplyCpu(MetricSample<double> sample)
    {
        if (sample.Status == SampleStatus.Valid)
        {
            double percent = Math.Clamp(sample.Value, 0d, 100d);
            CpuPercent = percent;
            CpuValueText = Formatters.PercentValue(percent);
            CpuPercentText = CpuValueText + " %";
            CpuLevel = LoadScale.ToKind(percent);
            CpuStatusText = LoadScale.Describe(CpuLevel);
            CpuIsLive = true;
            CpuHistory.Push(sample.Timestamp, percent);
            CpuCaptionText = BuildCpuHistoryCaption();
            return;
        }

        CpuPercent = 0d;
        CpuValueText = "--";
        CpuPercentText = "--";
        CpuLevel = LoadKind.Normal;
        CpuStatusText = sample.Status.ToShortText();
        CpuCaptionText = DescribeUnavailableSample(sample, "正在建立 CPU 采样基准");
        CpuIsLive = false;
    }

    private void ApplyMemory(MemorySnapshot sample)
    {
        if (sample.Status == SampleStatus.Valid)
        {
            double percent = Math.Clamp(sample.UsagePercent, 0d, 100d);
            MemoryPercent = percent;
            MemoryValueText = Formatters.PercentValue(percent);
            MemoryPercentText = MemoryValueText + " %";
            MemoryDetailText = $"{Formatters.Bytes(sample.UsedPhysicalBytes)} 已用 / {Formatters.Bytes(sample.TotalPhysicalBytes)}";
            MemoryCommitText = $"{Formatters.Bytes(sample.UsedCommitBytes)} / {Formatters.Bytes(sample.TotalCommitBytes)}";
            MemoryCacheText = sample.SystemCacheBytes.Status == SampleStatus.Valid
                ? Formatters.Bytes(sample.SystemCacheBytes.Value)
                : sample.SystemCacheBytes.Status.ToShortText();
            MemoryLevel = LoadScale.ToKind(percent);
            MemoryStatusText = LoadScale.Describe(MemoryLevel);
            MemoryIsLive = true;
            MemoryHistory.Push(sample.Timestamp, percent);
            return;
        }

        MemoryPercent = 0d;
        MemoryValueText = "--";
        MemoryPercentText = "--";
        MemoryDetailText = DescribeUnavailableStatus(sample.Status, "内存读数不可用");
        MemoryCommitText = "N/A";
        MemoryCacheText = "N/A";
        MemoryLevel = LoadKind.Normal;
        MemoryStatusText = sample.Status.ToShortText();
        MemoryIsLive = false;
    }

    private void ApplyDisk(DiskSnapshot sample)
    {
        if (sample.Status == SampleStatus.Valid)
        {
            double percent = Math.Clamp(sample.UsagePercent, 0d, 100d);
            DiskPercent = percent;
            DiskValueText = Formatters.PercentValue(percent);
            DiskDetailText = $"{sample.DriveName} {Formatters.Bytes(sample.AvailableBytes)} 可用 / {Formatters.Bytes(sample.TotalBytes)}";
            DiskStatusText = "有效";
            DiskIsLive = true;
            return;
        }

        DiskPercent = 0d;
        DiskValueText = "--";
        DiskDetailText = DescribeUnavailableStatus(sample.Status, sample.Error ?? "磁盘容量不可用");
        DiskStatusText = sample.Status.ToShortText();
        DiskIsLive = false;
    }

    private void ApplyNetwork(NetworkSnapshot sample)
    {
        ApplyRateSample(
            sample.DownloadBytesPerSecond,
            "下载",
            DownloadHistory,
            value => DownloadRateValueText = value,
            value => DownloadRateUnitText = value,
            value => DownloadRateText = value,
            value => DownloadStatusText = value,
            value => DownloadIsLive = value);

        ApplyRateSample(
            sample.UploadBytesPerSecond,
            "上传",
            UploadHistory,
            value => UploadRateValueText = value,
            value => UploadRateUnitText = value,
            value => UploadRateText = value,
            value => UploadStatusText = value,
            value => UploadIsLive = value);

        TotalDownloadedText = Formatters.Bytes(sample.SessionDownloadedBytes);
        TotalUploadedText = Formatters.Bytes(sample.SessionUploadedBytes);
        NetworkAdapters = sample.Adapters.Select(adapter => new NetworkAdapterViewModel(adapter)).ToArray();

        int included = sample.Adapters.Count(adapter => adapter.IsIncludedInAggregate);
        int excluded = sample.Adapters.Count - included;
        NetworkSummaryText = sample.Adapters.Count == 0
            ? "没有活动网络适配器"
            : $"计入 {included} 块网卡 · 排除 {excluded} 块虚拟/VPN 网卡";
    }

    private void ApplyRateSample(
        MetricSample<double> sample,
        string label,
        MetricHistory history,
        Action<string> setValue,
        Action<string> setUnit,
        Action<string> setText,
        Action<string> setStatus,
        Action<bool> setLive)
    {
        if (sample.Status == SampleStatus.Valid)
        {
            (string value, string unit) = Formatters.RateParts(sample.Value);
            setValue(value);
            setUnit(unit);
            setText(Formatters.Rate(sample.Value));
            setStatus("有效");
            setLive(true);
            history.Push(sample.Timestamp, sample.Value / 1024d / 1024d);
            return;
        }

        setValue("--");
        setUnit("B/s");
        setText(sample.Status.ToShortText());
        setStatus(sample.Status.ToShortText());
        setLive(false);
    }

    private void UpdateDataQuality(SystemSnapshot snapshot)
    {
        int valid = 0;
        const int total = 4;

        if (snapshot.CpuUsagePercent.Status == SampleStatus.Valid)
        {
            valid++;
        }

        if (snapshot.Memory.Status == SampleStatus.Valid)
        {
            valid++;
        }

        if (snapshot.SystemDisk.Status == SampleStatus.Valid)
        {
            valid++;
        }

        if (snapshot.Network.DownloadBytesPerSecond.Status == SampleStatus.Valid &&
            snapshot.Network.UploadBytesPerSecond.Status == SampleStatus.Valid)
        {
            valid++;
        }

        DataQualityText = valid == total
            ? "4 / 4 项核心指标有效"
            : $"{valid} / {total} 项核心指标有效";
        DataQualityLevel = valid == total
            ? LoadKind.Calm
            : valid == 0 ? LoadKind.Critical : LoadKind.High;
    }

    private void ApplyProcesses(IReadOnlyList<ProcessSnapshot> processes)
    {
        var desired = new List<ProcessRowViewModel>(processes.Count);
        var live = new HashSet<ProcessKey>(processes.Count);

        foreach (ProcessSnapshot snapshot in processes)
        {
            var key = new ProcessKey(snapshot.Pid, snapshot.StartTimeUtc?.UtcTicks ?? 0);
            live.Add(key);

            if (_processRows.TryGetValue(key, out ProcessRowViewModel? row))
            {
                row.Update(snapshot);
            }
            else
            {
                row = new ProcessRowViewModel(snapshot);
                _processRows[key] = row;
            }

            desired.Add(row);
        }

        foreach (ProcessKey key in _processRows.Keys.Where(key => !live.Contains(key)).ToArray())
        {
            _processRows.Remove(key);
        }

        SyncProcessOrder(desired);

        ProcessCountText = processes.Count.ToString();

        int cpuValid = processes.Count(process => process.CpuUsagePercent.Status == SampleStatus.Valid);
        int cpuInitializing = processes.Count(process => process.CpuUsagePercent.Status == SampleStatus.Initializing);
        int memoryUnavailable = processes.Count(process => process.MemoryStatus != SampleStatus.Valid);

        ProcessSummaryText = processes.Count == 0
            ? "没有可读取的进程"
            : $"共 {processes.Count} 个 · CPU 有效 {cpuValid} · 采集中 {cpuInitializing} · 内存受限 {memoryUnavailable}";
    }

    /// <summary>
    /// 让集合顺序与最新排行一致。只对位置不同的行做 Move，
    /// 已存在的行（及其控件容器）不会被销毁重建。
    /// </summary>
    private void SyncProcessOrder(List<ProcessRowViewModel> desired)
    {
        var keep = new HashSet<ProcessRowViewModel>(desired);

        for (int i = Processes.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Processes[i]))
            {
                Processes.RemoveAt(i);
            }
        }

        for (int target = 0; target < desired.Count; target++)
        {
            ProcessRowViewModel row = desired[target];
            if (target < Processes.Count && ReferenceEquals(Processes[target], row))
            {
                continue;
            }

            int current = Processes.IndexOf(row);
            if (current < 0)
            {
                Processes.Insert(target, row);
            }
            else
            {
                Processes.Move(current, target);
            }
        }
    }

    private void ClearProcesses()
    {
        _processRows.Clear();
        Processes.Clear();
    }

    private void MarkRefreshDelay(string reason)
    {
        CpuStatusText = "刷新延迟";
        CpuIsLive = false;
        MemoryStatusText = "刷新延迟";
        MemoryIsLive = false;
        DiskStatusText = "刷新延迟";
        DiskIsLive = false;
        DownloadStatusText = "刷新延迟";
        DownloadIsLive = false;
        UploadStatusText = "刷新延迟";
        UploadIsLive = false;
        DataQualityText = "刷新延迟 · 显示上次有效采样";
        DataQualityLevel = LoadKind.High;
        ProcessSummaryText = "刷新延迟：" + reason;
    }

    private void ApplyStaleState(string reason)
    {
        CpuPercent = 0d;
        CpuValueText = "--";
        CpuPercentText = "--";
        CpuStatusText = "数据已过期";
        CpuCaptionText = "超过 3 秒没有取得新样本";
        CpuIsLive = false;

        MemoryPercent = 0d;
        MemoryValueText = "--";
        MemoryPercentText = "--";
        MemoryDetailText = "数据已过期";
        MemoryCommitText = "N/A";
        MemoryCacheText = "N/A";
        MemoryStatusText = "数据已过期";
        MemoryIsLive = false;

        DiskPercent = 0d;
        DiskValueText = "--";
        DiskDetailText = "数据已过期";
        DiskStatusText = "数据已过期";
        DiskIsLive = false;

        DownloadRateValueText = "--";
        DownloadRateUnitText = "B/s";
        DownloadRateText = "数据已过期";
        DownloadStatusText = "数据已过期";
        DownloadIsLive = false;

        UploadRateValueText = "--";
        UploadRateUnitText = "B/s";
        UploadRateText = "数据已过期";
        UploadStatusText = "数据已过期";
        UploadIsLive = false;

        NetworkSummaryText = "网络采样已停止：" + reason;
        DataQualityText = "数据已过期";
        DataQualityLevel = LoadKind.Critical;

        if (_lastSuccessfulProcessSampleAt is null ||
            DateTimeOffset.UtcNow - _lastSuccessfulProcessSampleAt.Value >= StaleAfter)
        {
            ClearProcesses();
            ProcessCountText = "N/A";
        }
    }

    private void UpdateSamplingInterval()
    {
        long now = Stopwatch.GetTimestamp();

        if (_lastRefreshMonotonicTimestamp is long previous)
        {
            double seconds = (now - previous) / (double)Stopwatch.Frequency;
            SamplingIntervalText = seconds > 0 && double.IsFinite(seconds)
                ? $"{seconds:0.0} 秒"
                : "无效";
        }

        _lastRefreshMonotonicTimestamp = now;
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        await RefreshAsync();
    }

    private string BuildCpuHistoryCaption()
    {
        if (CpuHistory.Count == 0)
        {
            return "等待有效采样…";
        }

        double max = CpuHistory.Values.Max();
        double average = CpuHistory.Values.Average();
        return $"峰值 {max:F1}% · 均 {average:F1}% · {CpuHistory.SpanSeconds:0.0} 秒";
    }

    private static string DescribeUnavailableSample(MetricSample<double> sample, string initializingText) =>
        sample.Status switch
        {
            SampleStatus.Initializing => initializingText,
            SampleStatus.CounterReset => sample.Error ?? "累计计数器已重置",
            SampleStatus.Stale => "数据已过期",
            SampleStatus.Unavailable => sample.Error ?? "接口不可用或权限不足",
            _ => sample.Error ?? "采样结果无效",
        };

    private static string DescribeUnavailableStatus(SampleStatus status, string fallback) => status switch
    {
        SampleStatus.Initializing => "正在采集…",
        SampleStatus.Stale => "数据已过期",
        SampleStatus.Unavailable => fallback,
        _ => fallback,
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    private readonly record struct ProcessKey(int Pid, long StartTicks);
}



