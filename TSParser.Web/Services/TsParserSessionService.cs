using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Components.Forms;
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
    private const long MaxUploadBytes = 8L * 1024 * 1024 * 1024;

    private readonly Channel<TsParserUiUpdate> _channel = Channel.CreateUnbounded<TsParserUiUpdate>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

    private readonly object _parserLock = new();
    private TsParser? _parser;
    private Task? _runTask;
    private bool _loggerSubscribed;
    private TsParserSessionInputMode _inputMode = TsParserSessionInputMode.None;
    private string? _currentFilePath;
    private string? _currentFileDisplayName;
    private string? _tempUploadPath;
    private string? _currentMulticastEndpoint;
    private string? _currentBindAddress;

    public TsParserSessionSettings Settings { get; } = new();

    public ChannelReader<TsParserUiUpdate> Updates => _channel.Reader;

    public TsParserSessionInputMode InputMode => _inputMode;

    public string? CurrentFilePath => _currentFilePath;

    /// <summary>User-visible file name from the opened path.</summary>
    public string? CurrentFileDisplayName => _currentFileDisplayName;

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

    public bool TryGetObservedPids(out IReadOnlyList<ushort> pids)
    {
        lock (_parserLock)
        {
            if (_parser is null)
            {
                pids = [];
                return false;
            }

            pids = _parser.PidList;
            return pids.Count > 0;
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

        await StartFileSessionAsync(path, Path.GetFileName(path), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saves a browser-selected file to a temp path and opens it with <see cref="ParserConfig.TsFileName"/>.</summary>
    public async Task OpenUploadedFileAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Size < MinimumFileBytes)
            throw new InvalidOperationException($"File must be at least {MinimumFileBytes} bytes.");

        DeleteTempUploadIfAny();

        var uploadDir = Path.Combine(Path.GetTempPath(), "TSParser.Web", "uploads");
        Directory.CreateDirectory(uploadDir);

        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrEmpty(extension))
            extension = ".ts";

        var tempPath = Path.Combine(uploadDir, $"{Guid.NewGuid():N}{extension}");

        await using (var readStream = file.OpenReadStream(MaxUploadBytes, cancellationToken))
        await using (var writeStream = File.Create(tempPath))
            await readStream.CopyToAsync(writeStream, cancellationToken).ConfigureAwait(false);

        _tempUploadPath = tempPath;
        await StartFileSessionAsync(tempPath, file.Name, cancellationToken).ConfigureAwait(false);
    }

    private async Task StartFileSessionAsync(string path, string displayName, CancellationToken cancellationToken)
    {
        if (_tempUploadPath is not null &&
            !string.Equals(path, _tempUploadPath, StringComparison.OrdinalIgnoreCase))
            DeleteTempUploadIfAny();

        await StopAndDisposeParserAsync().ConfigureAwait(false);

        Post(new TsParserUiUpdate.SessionReset(TsParserSessionResetReason.OpenFile));

        var config = BuildParserConfig(filePath: path);
        var parser = CreateParser(config);

        lock (_parserLock)
        {
            _parser = parser;
            _inputMode = TsParserSessionInputMode.File;
            _currentFilePath = path;
            _currentFileDisplayName = displayName;
            _currentMulticastEndpoint = null;
            _currentBindAddress = null;
        }

        var fileLength = new FileInfo(path).Length;
        Post(new TsParserUiUpdate.SessionStarted(
            TsParserSessionInputMode.File,
            displayName,
            null,
            null,
            fileLength));

        StartParserRunInBackground(parser, cancellationToken);
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
            _currentFileDisplayName = null;
            _currentMulticastEndpoint = $"{group}:{port}";
            _currentBindAddress = string.IsNullOrWhiteSpace(bindAddress) ? null : bindAddress.Trim();
        }

        Post(new TsParserUiUpdate.SessionStarted(
            TsParserSessionInputMode.Udp,
            null,
            _currentMulticastEndpoint,
            _currentBindAddress));

        StartParserRunInBackground(parser, cancellationToken);
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
        DeleteTempUploadIfAny();
        _channel.Writer.TryComplete();
    }

    private void DeleteTempUploadIfAny()
    {
        if (_tempUploadPath is not { } path)
            return;

        _tempUploadPath = null;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup of temp uploads.
        }
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

    private void StartParserRunInBackground(TsParser parser, CancellationToken cancellationToken) =>
        _ = RunParserAsync(parser, cancellationToken);

    private async Task RunParserAsync(TsParser parser, CancellationToken cancellationToken)
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
        }
        catch (Exception ex)
        {
            Post(new TsParserUiUpdate.LogMessage(ex.Message, true));
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

    private void OnLogMessage(LogMessage message)
    {
        if (message.LogStatus is not (LogStatus.EXCEPTION or LogStatus.FATAL))
            return;

        Post(new TsParserUiUpdate.LogMessage(message.ToString(), true));
    }

    private void OnPatReady(PAT pat) => PostTable(TsTableKind.Pat, pat);
    private void OnPmtReady(PMT pmt)
    {
        TryAutoConfigureOperatorEwsPids(pmt);
        PostTable(TsTableKind.Pmt, pmt);
    }

    /// <summary>
    /// Operator streams use stream_type 0x05 for EWS/EEWS without AIT descriptor 0x6F.
    /// When toolbar lists are empty, register those PIDs so TsParser delivers EWS/EEWS tables.
    /// </summary>
    private void TryAutoConfigureOperatorEwsPids(PMT pmt)
    {
        if (Settings.EwsPids.Count > 0 || Settings.EewsPids.Count > 0)
            return;

        if (pmt.EsInfoList is null || pmt.EsInfoList.Count == 0)
            return;

        lock (_parserLock)
        {
            if (_parser is null)
                return;

            foreach (var es in pmt.EsInfoList)
            {
                if (es.StreamType != 0x05)
                    continue;

                if (es.EsDescriptorList?.Any(d => d.DescriptorTag == 0x6F) == true)
                    continue;

                var pid = es.ElementaryPid;
                if (!_parser.EwsPidList.Contains(pid))
                    _parser.EwsPidList.Add(pid);

                if (!_parser.EewsPidList.Contains(pid))
                    _parser.EewsPidList.Add(pid);
            }
        }
    }
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
