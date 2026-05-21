using Avalonia.Threading;
using TSParser.Desktop.Models;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.ViewModels;

/// <summary>
/// Shell view model: parser session pump (parity with <c>Home.razor</c>), tree/bitrate stores, selection.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private static readonly TimeSpan UdpChartThrottle = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan PidTreeThrottle = TimeSpan.FromMilliseconds(400);

    private readonly CancellationTokenSource _cts = new();
    private int _disposed;
    private Task? _pumpTask;
    private DateTime _lastChartUiUtc = DateTime.MinValue;
    private DateTime _lastPidTreeSyncUtc = DateTime.MinValue;

    private string _statusText = "Idle";
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
        SelectedNodeId = node.Id;
        SelectedNode = node;
    }

    public void NotifyTreeStructureChanged()
    {
        TreeRevision = TreeStore.Revision;
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
                var needsRefresh = false;
                var statusText = StatusText;
                var selectedNodeId = SelectedNodeId;
                var selectedNode = SelectedNode;
                var bitrateRevision = BitrateRevision;
                var chartFitRevision = ChartFitRevision;

                switch (update)
                {
                    case TsParserUiUpdate.TableParsed(var kind, var table):
                        TreeStore.ApplyTable(kind, table);
                        statusText = $"Parsed {kind}";
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
                        TreeStore.Clear();
                        BitrateStore.Clear();
                        selectedNodeId = null;
                        selectedNode = null;
                        bitrateRevision = BitrateStore.Revision;
                        chartFitRevision++;
                        _lastPidTreeSyncUtc = DateTime.MinValue;
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
                        needsRefresh = true;
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
                        if (TrySyncPidTree(force: true))
                            needsRefresh = true;
                        break;

                    case TsParserUiUpdate.LogMessage(var text, var isError):
                        if (isError)
                        {
                            statusText = text;
                            needsRefresh = true;
                        }
                        break;
                }

                if (TrySyncPidTree())
                    needsRefresh = true;

                if (needsRefresh)
                {
                    await PostUiRefreshAsync(
                        statusText,
                        selectedNodeId,
                        selectedNode,
                        bitrateRevision,
                        chartFitRevision).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private bool TrySyncPidTree(bool force = false)
    {
        if (!force)
        {
            var now = DateTime.UtcNow;
            if (now - _lastPidTreeSyncUtc < PidTreeThrottle)
                return false;

            _lastPidTreeSyncUtc = now;
        }

        if (!Session.TryGetObservedPids(out var pids) || pids.Count == 0)
            return false;

        TreeStore.SyncObservedPids(pids);
        return true;
    }

    private bool ShouldRefreshChart()
    {
        if (Session.InputMode != TsParserSessionInputMode.Udp)
            return true;

        var now = DateTime.UtcNow;
        if (now - _lastChartUiUtc < UdpChartThrottle)
            return false;

        _lastChartUiUtc = now;
        return true;
    }

    private async Task PostUiRefreshAsync(
        string statusText,
        Guid? selectedNodeId,
        TableTreeNode? selectedNode,
        int bitrateRevision,
        int chartFitRevision)
    {
        if (Dispatcher.UIThread.CheckAccess())
            ApplyUiRefresh(statusText, selectedNodeId, selectedNode, bitrateRevision, chartFitRevision);
        else
            await Dispatcher.UIThread.InvokeAsync(() =>
                ApplyUiRefresh(statusText, selectedNodeId, selectedNode, bitrateRevision, chartFitRevision));
    }

    private void ApplyUiRefresh(
        string statusText,
        Guid? selectedNodeId,
        TableTreeNode? selectedNode,
        int bitrateRevision,
        int chartFitRevision)
    {
        StatusText = statusText;
        SelectedNodeId = selectedNodeId;
        SelectedNode = selectedNode;
        BitrateRevision = bitrateRevision;
        ChartFitRevision = chartFitRevision;
        TreeRevision = TreeStore.Revision;
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
