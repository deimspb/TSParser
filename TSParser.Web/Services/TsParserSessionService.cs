using System.Net;
using System.Threading.Channels;
using TSParser;
using TSParser.Analysis;
using TSParser.Enums;
using TSParser.Service;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;

namespace TSParser.Web.Services;

/// <summary>
/// Singleton host for <see cref="TsParser"/> lifecycle, table-mode SI events, and thread-safe UI updates.
/// Parser callbacks run on background tasks; consumers read <see cref="Updates"/>.
/// </summary>
public sealed class TsParserSessionService : IAsyncDisposable
{
    private const int MinimumFileBytes = 2040;

    private readonly Channel<TsParserUiUpdate> _channel = Channel.CreateUnbounded<TsParserUiUpdate>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

    private readonly object _parserLock = new();
    private TsParser? _parser;
    private Task? _runTask;
    private bool _loggerSubscribed;
    private TsParserSessionInputMode _inputMode = TsParserSessionInputMode.None;
    private string? _currentFilePath;
    private string? _currentMulticastEndpoint;
    private string? _currentBindAddress;

    public TsParserSessionSettings Settings { get; } = new();

    public ChannelReader<TsParserUiUpdate> Updates => _channel.Reader;

    public TsParserSessionInputMode InputMode => _inputMode;

    public string? CurrentFilePath => _currentFilePath;

    public string? CurrentMulticastEndpoint => _currentMulticastEndpoint;

    public string? CurrentBindAddress => _currentBindAddress;

    public bool IsRunning
    {
        get
        {
            lock (_parserLock)
                return _runTask is { IsCompleted: false };
        }
    }

    public void ResetUiState(TsParserSessionResetReason reason = TsParserSessionResetReason.Manual) =>
        Post(new TsParserUiUpdate.SessionReset(reason));

    public async Task OpenFileAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        var path = Path.GetFullPath(fullPath.Trim());
        if (!File.Exists(path))
            throw new FileNotFoundException("Transport stream file was not found.", path);

        if (new FileInfo(path).Length < MinimumFileBytes)
            throw new InvalidOperationException($"File must be at least {MinimumFileBytes} bytes.");

        await StopAndDisposeParserAsync().ConfigureAwait(false);

        Post(new TsParserUiUpdate.SessionReset(TsParserSessionResetReason.OpenFile));

        var config = BuildParserConfig(filePath: path);
        var parser = CreateParser(config);

        lock (_parserLock)
        {
            _parser = parser;
            _inputMode = TsParserSessionInputMode.File;
            _currentFilePath = path;
            _currentMulticastEndpoint = null;
            _currentBindAddress = null;
        }

        Post(new TsParserUiUpdate.SessionStarted(
            TsParserSessionInputMode.File,
            path,
            null,
            null));

        await StartRunAsync(parser, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartUdpAsync(
        string multicastEndpoint,
        string? bindAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(multicastEndpoint);

        if (!TryParseMulticastEndpoint(multicastEndpoint, out var group, out var port))
            throw new FormatException("Expected multicast address as host:port (for example 239.1.2.3:1234).");

        if (bindAddress is { Length: > 0 } && !IPAddress.TryParse(bindAddress, out _))
            throw new FormatException("Bind address must be a valid IPv4/IPv6 literal, or omitted for Any.");

        await StopAndDisposeParserAsync().ConfigureAwait(false);

        Post(new TsParserUiUpdate.SessionReset(TsParserSessionResetReason.StartUdp));

        var config = BuildParserConfig(
            multicastGroup: group,
            multicastPort: port,
            bindAddress: string.IsNullOrWhiteSpace(bindAddress) ? null : bindAddress.Trim());

        var parser = CreateParser(config);

        lock (_parserLock)
        {
            _parser = parser;
            _inputMode = TsParserSessionInputMode.Udp;
            _currentFilePath = null;
            _currentMulticastEndpoint = $"{group}:{port}";
            _currentBindAddress = string.IsNullOrWhiteSpace(bindAddress) ? null : bindAddress.Trim();
        }

        Post(new TsParserUiUpdate.SessionStarted(
            TsParserSessionInputMode.Udp,
            null,
            _currentMulticastEndpoint,
            _currentBindAddress));

        await StartRunAsync(parser, cancellationToken).ConfigureAwait(false);
    }

    public void Stop()
    {
        TsParser? parser;
        var mode = _inputMode;

        lock (_parserLock)
        {
            parser = _parser;
        }

        parser?.StopParser();
        Post(new TsParserUiUpdate.ParserStopped(mode));
    }

    public static bool TryParseMulticastEndpoint(string input, out string group, out int port)
    {
        group = "";
        port = 0;

        var trimmed = input.Trim();
        var colon = trimmed.LastIndexOf(':');
        if (colon <= 0 || colon >= trimmed.Length - 1)
            return false;

        group = trimmed[..colon].Trim();
        if (!IPAddress.TryParse(group, out var address))
            return false;

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        if (!int.TryParse(trimmed[(colon + 1)..].Trim(), out port) || port is < 1 or > 65535)
            return false;

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAndDisposeParserAsync().ConfigureAwait(false);
        _channel.Writer.TryComplete();
    }

    private ParserConfig BuildParserConfig(
        string? filePath = null,
        string? multicastGroup = null,
        int? multicastPort = null,
        string? bindAddress = null) => new()
    {
        CurrentTsMode = TsMode.DVB,
        CurrentDecodeMode = DecodeMode.Table,
        BitrateMeasurement = Settings.CreateBitrateOptions(),
        TsFileName = filePath,
        MulticastGroup = multicastGroup,
        MulticastPort = multicastPort,
        MulticastIncomingIp = bindAddress
    };

    private TsParser CreateParser(ParserConfig config)
    {
        var parser = new TsParser(config);
        ApplyPidLists(parser);
        SubscribeParser(parser);
        EnsureLoggerSubscribed();
        return parser;
    }

    private void ApplyPidLists(TsParser parser)
    {
        parser.EwsPidList = Settings.EwsPids.Count > 0
            ? Settings.EwsPids.ToList()
            : new List<ushort>();

        parser.EewsPidList = Settings.EewsPids.Count > 0
            ? Settings.EewsPids.ToList()
            : new List<ushort>();
    }

    private async Task StartRunAsync(TsParser parser, CancellationToken cancellationToken)
    {
        Task runTask;
        lock (_parserLock)
        {
            _runTask = runTask = parser.RunParserAsync();
        }

        try
        {
            await runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            parser.StopParser();
            throw;
        }
        catch (Exception ex)
        {
            Post(new TsParserUiUpdate.LogMessage(ex.Message, true));
            throw;
        }
    }

    private async Task StopAndDisposeParserAsync()
    {
        TsParser? parser;
        Task? runTask;

        lock (_parserLock)
        {
            parser = _parser;
            runTask = _runTask;
            _parser = null;
            _runTask = null;
        }

        if (parser is null)
            return;

        parser.StopParser();

        if (runTask is not null)
        {
            try
            {
                await runTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Post(new TsParserUiUpdate.LogMessage(ex.Message, true));
            }
        }

        UnsubscribeParser(parser);
        parser.Dispose();
    }

    private void SubscribeParser(TsParser parser)
    {
        parser.OnPatReady += OnPatReady;
        parser.OnPmtReady += OnPmtReady;
        parser.OnCatReady += OnCatReady;
        parser.OnNitReady += OnNitReady;
        parser.OnSdtReady += OnSdtReady;
        parser.OnBatReady += OnBatReady;
        parser.OnEitReady += OnEitReady;
        parser.OnTdtReady += OnTdtReady;
        parser.OnTotready += OnTotReady;
        parser.OnAitReady += OnAitReady;
        parser.OnMipReady += OnMipReady;
        parser.OnScte35Ready += OnScte35Ready;
        parser.OnEwsReady += OnEwsReady;
        parser.OnEewsReady += OnEewsReady;
        parser.OnBitrateMeasured += OnBitrateMeasured;
        parser.OnParserComplete += OnParserComplete;
    }

    private void UnsubscribeParser(TsParser parser)
    {
        parser.OnPatReady -= OnPatReady;
        parser.OnPmtReady -= OnPmtReady;
        parser.OnCatReady -= OnCatReady;
        parser.OnNitReady -= OnNitReady;
        parser.OnSdtReady -= OnSdtReady;
        parser.OnBatReady -= OnBatReady;
        parser.OnEitReady -= OnEitReady;
        parser.OnTdtReady -= OnTdtReady;
        parser.OnTotready -= OnTotReady;
        parser.OnAitReady -= OnAitReady;
        parser.OnMipReady -= OnMipReady;
        parser.OnScte35Ready -= OnScte35Ready;
        parser.OnEwsReady -= OnEwsReady;
        parser.OnEewsReady -= OnEewsReady;
        parser.OnBitrateMeasured -= OnBitrateMeasured;
        parser.OnParserComplete -= OnParserComplete;
    }

    private void EnsureLoggerSubscribed()
    {
        if (_loggerSubscribed)
            return;

        Logger.OnLogMessage += OnLogMessage;
        _loggerSubscribed = true;
    }

    private void OnLogMessage(LogMessage message) =>
        Post(new TsParserUiUpdate.LogMessage(
            message.ToString(),
            message.LogStatus is LogStatus.EXCEPTION or LogStatus.FATAL));

    private void OnPatReady(PAT pat) => PostTable(TsTableKind.Pat, pat);
    private void OnPmtReady(PMT pmt) => PostTable(TsTableKind.Pmt, pmt);
    private void OnCatReady(CAT cat) => PostTable(TsTableKind.Cat, cat);
    private void OnNitReady(NIT nit) => PostTable(TsTableKind.Nit, nit);
    private void OnSdtReady(SDT sdt) => PostTable(TsTableKind.Sdt, sdt);
    private void OnBatReady(BAT bat) => PostTable(TsTableKind.Bat, bat);
    private void OnEitReady(EIT eit) => PostTable(TsTableKind.Eit, eit);
    private void OnTdtReady(TDT tdt) => PostTable(TsTableKind.Tdt, tdt);
    private void OnTotReady(TOT tot) => PostTable(TsTableKind.Tot, tot);
    private void OnAitReady(AIT ait) => PostTable(TsTableKind.Ait, ait);
    private void OnMipReady(MIP mip) => PostTable(TsTableKind.Mip, mip);
    private void OnScte35Ready(SCTE35 scte35) => PostTable(TsTableKind.Scte35, scte35);
    private void OnEwsReady(EWS ews) => PostTable(TsTableKind.Ews, ews);
    private void OnEewsReady(EEWS eews) => PostTable(TsTableKind.Eews, eews);

    private void OnBitrateMeasured(BitrateSample sample) =>
        Post(new TsParserUiUpdate.BitrateMeasured(sample));

    private void OnParserComplete() =>
        Post(new TsParserUiUpdate.ParserCompleted());

    private void PostTable(TsTableKind kind, Table table) =>
        Post(new TsParserUiUpdate.TableParsed(kind, table));

    private void Post(TsParserUiUpdate update) =>
        _channel.Writer.TryWrite(update);
}
