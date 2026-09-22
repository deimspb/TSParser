using Avalonia.Threading;

using TSParser.Desktop.Models;

using TSParser.Desktop.Services;



namespace TSParser.Desktop.ViewModels;



/// <summary>

/// Shell view model: parser session pump (parity with <c>Home.razor</c>), tree/bitrate stores, selection.

/// </summary>

public sealed class MainWindowViewModel : ViewModelBase, IAsyncDisposable

{

    private static readonly TimeSpan ChartUiThrottle = TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan TableUiThrottle = TimeSpan.FromMilliseconds(200);



    private readonly CancellationTokenSource _cts = new();

    private readonly object _uiPendingLock = new();

    private readonly List<Action> _pendingTreeMutations = [];

    private int _disposed;

    private int _uiRefreshScheduled;

    private Task? _pumpTask;

    private DateTime _lastChartUiUtc = DateTime.MinValue;

    private DateTime _lastTableUiUtc = DateTime.MinValue;

    private int _syncedObservedPidCount;

    private string? _pendingStatusText;

    private Guid? _pendingSelectedNodeId;

    private int _pendingBitrateRevision;

    private int _pendingChartFitRevision;

    private bool _hasPendingUi;

    private string _uiStatusText = "Idle";

    private Guid? _uiSelectedNodeId;



    private string _statusText = "Idle";

    private string _windowTitle = WindowTitleFormatter.AppName;

    private string? _pendingWindowTitle;

    private Guid? _selectedNodeId;

    private TableTreeNode? _selectedNode;

    private int _bitrateRevision;

    private int _chartConfigRevision;

    private int _chartFitRevision;

    private long _treeRevision;



    public TsParserSessionService Session { get; }



    public TableVersionStore TreeStore { get; }



    public BitrateHistoryStore BitrateStore { get; }



    public NetworkInterfaceService NetworkInterfaces { get; }



    public string StatusText

    {

        get => _statusText;

        private set => SetProperty(ref _statusText, value);

    }



    public string WindowTitle

    {

        get => _windowTitle;

        private set => SetProperty(ref _windowTitle, value);

    }



    public Guid? SelectedNodeId

    {

        get => _selectedNodeId;

        private set => SetProperty(ref _selectedNodeId, value);

    }



    public TableTreeNode? SelectedNode

    {

        get => _selectedNode;

        private set => SetProperty(ref _selectedNode, value);

    }



    public int BitrateRevision

    {

        get => _bitrateRevision;

        private set => SetProperty(ref _bitrateRevision, value);

    }



    public int ChartConfigRevision

    {

        get => _chartConfigRevision;

        private set => SetProperty(ref _chartConfigRevision, value);

    }



    public int ChartFitRevision

    {

        get => _chartFitRevision;

        private set => SetProperty(ref _chartFitRevision, value);

    }



    public long TreeRevision

    {

        get => _treeRevision;

        private set => SetProperty(ref _treeRevision, value);

    }



    public MainWindowViewModel()

    {

        Session = new TsParserSessionService();

        TreeStore = new TableVersionStore();

        BitrateStore = new BitrateHistoryStore();

        NetworkInterfaces = new NetworkInterfaceService();



        Session.Settings.ApplyChartTo(BitrateStore);

        BitrateRevision = BitrateStore.Revision;

        TreeRevision = TreeStore.Revision;

        _pumpTask = PumpUpdatesAsync(_cts.Token);

    }



    public void SelectTreeNode(TableTreeNode node)

    {

        _uiSelectedNodeId = node.Id;

        SelectedNodeId = node.Id;

        SelectedNode = node;

    }



    public void OnChartSettingsChanged()

    {

        BitrateRevision = BitrateStore.Revision;

        ChartConfigRevision++;

        ChartFitRevision++;

    }



    private async Task PumpUpdatesAsync(CancellationToken cancellationToken)

    {

        try

        {

            await foreach (var update in Session.Updates.ReadAllAsync(cancellationToken).ConfigureAwait(false))

            {

                try

                {

                var needsRefresh = false;

                var statusText = _uiStatusText;

                var selectedNodeId = _uiSelectedNodeId;

                var bitrateRevision = BitrateRevision;

                var chartFitRevision = ChartFitRevision;



                switch (update)

                {

                    case TsParserUiUpdate.TableParsed(var kind, var table, var pcr):

                        EnqueueTreeMutation(() => TreeStore.ApplyTable(kind, table, pcr));

                        statusText = $"Parsed {kind}";

                        if (ShouldRefreshTableUi())

                            needsRefresh = true;

                        break;



                    case TsParserUiUpdate.BitrateMeasured(var sample):

                        if (BitrateStore.TryAddSample(sample) && ShouldRefreshChart())

                        {

                            bitrateRevision = BitrateStore.Revision;

                            needsRefresh = true;

                        }

                        break;



                    case TsParserUiUpdate.SessionReset:

                        EnqueueTreeMutation(() => TreeStore.Clear());

                        BitrateStore.Clear();

                        selectedNodeId = null;

                        bitrateRevision = BitrateStore.Revision;

                        chartFitRevision++;

                        _lastTableUiUtc = DateTime.MinValue;

                        _lastChartUiUtc = DateTime.MinValue;

                        _syncedObservedPidCount = 0;

                        statusText = "Session reset";

                        needsRefresh = true;

                        break;



                    case TsParserUiUpdate.SessionStarted(var mode, var filePath, var endpoint, _, var fileLengthBytes):

                        if (mode == TsParserSessionInputMode.File)

                            BitrateStore.ConfigureForFile(filePath, fileLengthBytes);

                        else if (mode == TsParserSessionInputMode.Udp)

                            BitrateStore.ConfigureForUdp();



                        Session.Settings.ApplyChartTo(BitrateStore);

                        bitrateRevision = BitrateStore.Revision;

                        chartFitRevision++;

                        statusText = mode switch

                        {

                            TsParserSessionInputMode.File => $"File: {filePath}",

                            TsParserSessionInputMode.Udp => $"UDP: {endpoint}",

                            _ => "Running"

                        };

                        RequestUiRefresh(

                            statusText,

                            selectedNodeId,

                            bitrateRevision,

                            chartFitRevision,

                            WindowTitleFormatter.Format(mode, filePath, endpoint));

                        needsRefresh = false;

                        break;



                    case TsParserUiUpdate.ParserStopped:

                        statusText = "Stopped";

                        chartFitRevision++;

                        needsRefresh = true;

                        break;



                    case TsParserUiUpdate.ParserCompleted:

                        statusText = "Parse complete";

                        if (BitrateStore.Revision != bitrateRevision)

                            bitrateRevision = BitrateStore.Revision;



                        chartFitRevision++;

                        _lastTableUiUtc = DateTime.MinValue;

                        _lastChartUiUtc = DateTime.MinValue;

                        TrySyncPidTree(force: true);

                        needsRefresh = true;

                        break;



                    case TsParserUiUpdate.PlpDiscovered(var plpId):
                        statusText = $"PLP {plpId} discovered";
                        needsRefresh = true;
                        break;

                    case TsParserUiUpdate.PlpServicesUpdated(var t2miPid, var plpId, var services):
                        EnqueueTreeMutation(() => TreeStore.ApplyPlpServices(t2miPid, plpId, services));
                        statusText = $"PLP {plpId} services";
                        if (ShouldRefreshTableUi())
                            needsRefresh = true;
                        break;

                    case TsParserUiUpdate.LogMessage(var text, var isError):

                        statusText = text;

                        needsRefresh = true;

                        break;

                    case TsParserUiUpdate.PidCatalogPoll:

                        break;

                }



                if (TrySyncPidTree())

                    needsRefresh = true;



                if (needsRefresh)

                    RequestUiRefresh(statusText, selectedNodeId, bitrateRevision, chartFitRevision);

                }

                catch (Exception ex) when (ex is not OperationCanceledException)

                {

                    RequestUiRefresh(ex.Message, _uiSelectedNodeId, BitrateRevision, ChartFitRevision);

                }

            }

        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)

        {

        }

    }



    private bool TrySyncPidTree(bool force = false)

    {

        if (!Session.TryGetObservedPids(out var pids) || pids.Count == 0)

            return false;



        if (!force)

        {

            if (pids.Count == _syncedObservedPidCount)

                return false;

        }



        _syncedObservedPidCount = pids.Count;

        var snapshot = pids.ToArray();

        EnqueueTreeMutation(() => TreeStore.SyncObservedPids(snapshot));

        return true;

    }



    private void EnqueueTreeMutation(Action mutation)

    {

        lock (_uiPendingLock)

            _pendingTreeMutations.Add(mutation);



        EnsureUiFlushScheduled();

    }



    private void EnsureUiFlushScheduled()

    {

        if (Interlocked.CompareExchange(ref _uiRefreshScheduled, 1, 0) != 0)

            return;



        _ = Dispatcher.UIThread.InvokeAsync(FlushPendingUiRefreshAsync, DispatcherPriority.Background);

    }



    private bool ShouldRefreshTableUi()

    {

        var now = DateTime.UtcNow;

        if (now - _lastTableUiUtc < TableUiThrottle)

            return false;



        _lastTableUiUtc = now;

        return true;

    }



    private bool ShouldRefreshChart()

    {

        var now = DateTime.UtcNow;

        if (now - _lastChartUiUtc < ChartUiThrottle)

            return false;



        _lastChartUiUtc = now;

        return true;

    }



    private void RequestUiRefresh(

        string statusText,

        Guid? selectedNodeId,

        int bitrateRevision,

        int chartFitRevision,

        string? windowTitle = null)

    {

        lock (_uiPendingLock)

        {

            _pendingStatusText = statusText;

            _pendingSelectedNodeId = selectedNodeId;

            _pendingBitrateRevision = bitrateRevision;

            _pendingChartFitRevision = chartFitRevision;

            if (windowTitle is not null)

                _pendingWindowTitle = windowTitle;

            _hasPendingUi = true;

        }



        EnsureUiFlushScheduled();

    }



    private void FlushPendingUiRefreshAsync()

    {

        string statusText;

        Guid? selectedNodeId;

        int bitrateRevision;

        int chartFitRevision;

        string? windowTitle;

        List<Action> treeMutations;

        var applyUi = false;



        lock (_uiPendingLock)

        {

            treeMutations = _pendingTreeMutations.Count > 0

                ? [.. _pendingTreeMutations]

                : [];

            _pendingTreeMutations.Clear();



            if (!_hasPendingUi && treeMutations.Count == 0)

            {

                Interlocked.Exchange(ref _uiRefreshScheduled, 0);

                return;

            }



            applyUi = _hasPendingUi;

            statusText = _pendingStatusText ?? _uiStatusText;

            selectedNodeId = _pendingSelectedNodeId;

            bitrateRevision = _pendingBitrateRevision;

            chartFitRevision = _pendingChartFitRevision;

            windowTitle = _pendingWindowTitle;

            _pendingWindowTitle = null;

            _hasPendingUi = false;

        }



        foreach (var mutation in treeMutations)
        {
            try
            {
                mutation();
            }
            catch (Exception ex)
            {
                statusText = ex.Message;
                applyUi = true;
            }
        }



        try

        {

            if (applyUi)

                ApplyUiRefresh(statusText, selectedNodeId, bitrateRevision, chartFitRevision, windowTitle);

            else if (treeMutations.Count > 0)

                TreeRevision = TreeStore.Revision;

        }

        finally

        {

            var reschedule = false;

            lock (_uiPendingLock)

                reschedule = _hasPendingUi || _pendingTreeMutations.Count > 0;



            Interlocked.Exchange(ref _uiRefreshScheduled, 0);



            if (reschedule && Interlocked.CompareExchange(ref _uiRefreshScheduled, 1, 0) == 0)

                _ = Dispatcher.UIThread.InvokeAsync(FlushPendingUiRefreshAsync, DispatcherPriority.Background);

        }

    }



    private void ApplyUiRefresh(

        string statusText,

        Guid? selectedNodeId,

        int bitrateRevision,

        int chartFitRevision,

        string? windowTitle = null)

    {

        if (!Dispatcher.UIThread.CheckAccess())

        {

            RequestUiRefresh(statusText, selectedNodeId, bitrateRevision, chartFitRevision, windowTitle);

            return;

        }



        _uiStatusText = statusText;

        _uiSelectedNodeId = selectedNodeId;



        StatusText = statusText;

        if (windowTitle is not null)

            WindowTitle = windowTitle;

        SelectedNodeId = selectedNodeId;

        SelectedNode = selectedNodeId is Guid id ? TreeStore.FindNode(id) : null;

        BitrateRevision = bitrateRevision;

        ChartFitRevision = chartFitRevision;

        var treeRev = TreeStore.Revision;

        if (treeRev != _treeRevision)

            TreeRevision = treeRev;

    }



    public async ValueTask DisposeAsync()

    {

        if (Interlocked.Exchange(ref _disposed, 1) != 0)

            return;



        await _cts.CancelAsync().ConfigureAwait(false);

        if (_pumpTask is not null)

        {

            try

            {

                await _pumpTask.ConfigureAwait(false);

            }

            catch (OperationCanceledException)

            {

            }

        }



        _cts.Dispose();

        await Session.DisposeAsync().ConfigureAwait(false);

    }

}


