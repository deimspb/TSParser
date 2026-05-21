using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TSParser.Analysis;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.ViewModels;

public sealed partial class BitrateSettingsViewModel : ViewModelBase
{
    private readonly TsParserSessionService _session;
    private readonly BitrateHistoryStore _bitrateStore;
    private readonly HashSet<ushort> _chartSelectedPids = [];
    private BitrateMeasurementSnapshot _measurementBaseline;
    private List<ushort> _availablePids = [];

    public BitrateSettingsViewModel(TsParserSessionService session, BitrateHistoryStore bitrateStore)
    {
        _session = session;
        _bitrateStore = bitrateStore;
        LoadFromSession();
        _measurementBaseline = BitrateMeasurementSnapshot.From(_session.Settings);
    }

    public bool NeedsParserRestart { get; private set; }

    public IReadOnlyList<ClockSourceOption> ClockSourceOptions { get; } =
    [
        new(BitrateClockSource.Pcr, "PCR"),
        new(BitrateClockSource.Pts, "PTS"),
        new(BitrateClockSource.Dts, "DTS"),
        new(BitrateClockSource.AssumedTransportRate, "Assumed transport rate")
    ];

    [ObservableProperty]
    private ClockSourceOption? _selectedClockSource;

    [ObservableProperty]
    private double _measurementWindowSeconds = 1;

    [ObservableProperty]
    private string _referencePidText = "";

    [ObservableProperty]
    private double _assumedMbps = 10;

    [ObservableProperty]
    private bool _measureStreamBitrate = true;

    [ObservableProperty]
    private bool _measureUsefulAndTotal = true;

    [ObservableProperty]
    private bool _showStreamOnChart = true;

    [ObservableProperty]
    private bool _showPidSumOnChart = true;

    [ObservableProperty]
    private bool _showIndividualPidsOnChart = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _chartSelectedCount;

    public bool ShowAssumedRate => SelectedClockSource?.Value == BitrateClockSource.AssumedTransportRate;

    public bool CanEnablePidSum => ChartSelectedCount >= 2;

    public bool CanEnableIndividualPids => ChartSelectedCount > 0;

    public bool HasAvailablePids => _availablePids.Count > 0;

    public ObservableCollection<PidChartItem> PidItems { get; } = [];

    partial void OnSelectedClockSourceChanged(ClockSourceOption? value) =>
        OnPropertyChanged(nameof(ShowAssumedRate));

    public void LoadFromSession()
    {
        var s = _session.Settings;
        SelectedClockSource = ClockSourceOptions.First(o => o.Value == s.ClockSource);
        MeasurementWindowSeconds = s.MeasurementWindow.TotalSeconds;
        ReferencePidText = s.ReferencePid is { } refPid ? $"0x{refPid:X4}" : "";
        AssumedMbps = s.AssumedBitsPerSecond / 1_000_000d;
        MeasureStreamBitrate = s.MeasureStreamBitrate;
        MeasureUsefulAndTotal = s.MeasureUsefulAndTotalBitrate;
        ShowStreamOnChart = s.ShowStreamOnChart;
        ShowPidSumOnChart = s.ShowPidSumOnChart;
        ShowIndividualPidsOnChart = s.ShowIndividualPidsOnChart;
        _chartSelectedPids.Clear();
        foreach (var pid in s.ChartPids)
            _chartSelectedPids.Add(pid);

        RefreshObservedPids();
        ErrorMessage = null;
    }

    public void RefreshObservedPids()
    {
        if (_session.TryGetObservedPids(out var pids))
            _availablePids = pids.OrderBy(p => p).ToList();
        else
            _availablePids = [];

        var available = _availablePids.ToHashSet();
        _chartSelectedPids.RemoveWhere(p => !available.Contains(p));
        RebuildPidItems();
        OnPropertyChanged(nameof(HasAvailablePids));
    }

    public void SelectAllChartPids()
    {
        foreach (var pid in _availablePids)
            _chartSelectedPids.Add(pid);

        SyncPidItemChecks();
    }

    public void ClearChartPids()
    {
        _chartSelectedPids.Clear();
        SyncPidItemChecks();
    }

    public void OnPidSelectionChanged(ushort pid, bool selected)
    {
        if (selected)
            _chartSelectedPids.Add(pid);
        else
            _chartSelectedPids.Remove(pid);

        ChartSelectedCount = _chartSelectedPids.Count;
        OnPropertyChanged(nameof(CanEnablePidSum));
        OnPropertyChanged(nameof(CanEnableIndividualPids));
    }

    public bool TrySave()
    {
        ErrorMessage = null;

        if (MeasurementWindowSeconds <= 0)
        {
            ErrorMessage = "Measurement window must be greater than zero.";
            return false;
        }

        ushort? referencePid = null;
        if (!string.IsNullOrWhiteSpace(ReferencePidText))
        {
            if (!TsPidListParser.TryParsePid(ReferencePidText, out var pid))
            {
                ErrorMessage = "Reference PID is invalid.";
                return false;
            }

            referencePid = pid;
        }

        if (AssumedMbps <= 0)
        {
            ErrorMessage = "Assumed rate must be greater than zero.";
            return false;
        }

        if (!ShowStreamOnChart && _chartSelectedPids.Count == 0)
        {
            ErrorMessage = "Select stream and/or at least one PID for the chart.";
            return false;
        }

        if (!ShowStreamOnChart && !ShowIndividualPidsOnChart && (!ShowPidSumOnChart || _chartSelectedPids.Count < 2))
        {
            ErrorMessage = "Enable Stream, Per-PID lines, or Sum of selected PIDs (needs 2+ PIDs).";
            return false;
        }

        if (_chartSelectedPids.Count > 0 && _availablePids.Count > 0)
        {
            var unknown = _chartSelectedPids.Where(p => !_availablePids.Contains(p)).ToList();
            if (unknown.Count > 0)
            {
                ErrorMessage = $"PID(s) not in stream: {string.Join(", ", unknown.Select(p => $"0x{p:X4}"))}.";
                return false;
            }
        }

        var chartPids = _chartSelectedPids.OrderBy(p => p).ToList();
        var settings = _session.Settings;
        settings.ClockSource = SelectedClockSource?.Value ?? BitrateClockSource.Pcr;
        settings.MeasurementWindow = TimeSpan.FromSeconds(MeasurementWindowSeconds);
        settings.ReferencePid = referencePid;
        settings.AssumedBitsPerSecond = AssumedMbps * 1_000_000d;
        settings.MeasureStreamBitrate = MeasureStreamBitrate;
        settings.MeasureUsefulAndTotalBitrate = MeasureUsefulAndTotal;
        settings.ShowStreamOnChart = ShowStreamOnChart;
        settings.ShowPidSumOnChart = ShowPidSumOnChart && chartPids.Count >= 2;
        settings.ShowIndividualPidsOnChart = ShowIndividualPidsOnChart && chartPids.Count > 0;
        settings.SetChartPids(chartPids);
        settings.ApplyChartTo(_bitrateStore);

        NeedsParserRestart = _session.HasActiveSource
            && BitrateMeasurementSnapshot.From(settings).RequiresParserRestart(_measurementBaseline);

        return true;
    }

    private void RebuildPidItems()
    {
        PidItems.Clear();
        foreach (var pid in _availablePids)
        {
            var item = new PidChartItem(pid, _chartSelectedPids.Contains(pid));
            item.SelectionChanged += (_, selected) => OnPidSelectionChanged(pid, selected);
            PidItems.Add(item);
        }

        ChartSelectedCount = _chartSelectedPids.Count;
        OnPropertyChanged(nameof(CanEnablePidSum));
        OnPropertyChanged(nameof(CanEnableIndividualPids));
    }

    private void SyncPidItemChecks()
    {
        foreach (var item in PidItems)
            item.SetSelected(_chartSelectedPids.Contains(item.Pid));

        ChartSelectedCount = _chartSelectedPids.Count;
        OnPropertyChanged(nameof(CanEnablePidSum));
        OnPropertyChanged(nameof(CanEnableIndividualPids));
    }

    public sealed record ClockSourceOption(BitrateClockSource Value, string Label);

    public sealed class PidChartItem : ObservableObject
    {
        private bool _isSelected;

        public PidChartItem(ushort pid, bool isSelected)
        {
            Pid = pid;
            _isSelected = isSelected;
            Display = $"0x{pid:X4}";
        }

        public ushort Pid { get; }

        public string Display { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                    SelectionChanged?.Invoke(this, value);
            }
        }

        public event Action<PidChartItem, bool>? SelectionChanged;

        public void SetSelected(bool selected)
        {
            if (_isSelected == selected)
                return;

            _isSelected = selected;
            OnPropertyChanged(nameof(IsSelected));
        }
    }
}
