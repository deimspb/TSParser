using System.Threading.Channels;

namespace TSParser.Input;

internal sealed class UdpDatagramRecorder : IDisposable
{
    internal const int DefaultQueueCapacity = 256;

    private readonly object _gate = new();
    private readonly int _queueCapacity;
    private readonly Func<string, Stream> _streamFactory;
    private Stream? _stream;
    private Channel<byte[]>? _channel;
    private Task? _writerTask;
    private UdpRecordingOptions? _options;
    private DateTimeOffset _startedAt;
    private long _bytesAccepted;
    private long _bytesWritten;
    private Timer? _durationTimer;
    private UdpRecordingStopReason? _stopReason;
    private Exception? _stopError;
    private bool _starting;
    private bool _disposed;

    public UdpDatagramRecorder()
        : this(DefaultQueueCapacity, path => new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
    {
    }

    internal UdpDatagramRecorder(int queueCapacity, Func<string, Stream> streamFactory)
    {
        if (queueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));

        _queueCapacity = queueCapacity;
        _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
    }

    public event Action<UdpRecordingResult>? Completed;

    public bool IsRecording
    {
        get { lock (_gate) return _stream is not null; }
    }

    public UdpRecordingStatus? Status
    {
        get
        {
            lock (_gate)
                return _stream is null || _options is null
                    ? null
                    : new UdpRecordingStatus(_options.FilePath, _bytesWritten, DateTimeOffset.UtcNow - _startedAt);
        }
    }

    public void Start(UdpRecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var fullPath = Path.GetFullPath(options.FilePath);
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(_queueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stream is not null || _starting)
                throw new InvalidOperationException("UDP recording is already active.");
            _starting = true;
        }

        Stream stream;
        try
        {
            stream = _streamFactory(fullPath);
        }
        catch
        {
            lock (_gate)
                _starting = false;
            throw;
        }

        lock (_gate)
        {
            _starting = false;
            if (_disposed)
            {
                stream.Dispose();
                throw new ObjectDisposedException(nameof(UdpDatagramRecorder));
            }
            _options = options with { FilePath = fullPath };
            _startedAt = DateTimeOffset.UtcNow;
            _bytesAccepted = 0;
            _bytesWritten = 0;
            _stopReason = null;
            _stopError = null;
            _stream = stream;
            _channel = channel;
            _writerTask = Task.Run(() => WriteQueuedDatagramsAsync(channel.Reader, stream));
            if (options.MaxDuration is { } duration)
                _durationTimer = new Timer(_ => Stop(UdpRecordingStopReason.TimeLimitReached), null, duration, Timeout.InfiniteTimeSpan);
        }
    }

    public void Record(ReadOnlySpan<byte> datagram)
    {
        lock (_gate)
        {
            if (_stream is null || _options is null || _channel is null || _stopReason is not null)
                return;

            if (_options.MaxBytes is { } maxBytes && datagram.Length > maxBytes - _bytesAccepted)
            {
                BeginStopLocked(UdpRecordingStopReason.ByteLimitReached, null);
                return;
            }

            var copy = datagram.ToArray();
            if (!_channel.Writer.TryWrite(copy))
            {
                BeginStopLocked(
                    UdpRecordingStopReason.Error,
                    new IOException($"UDP recording queue capacity ({_queueCapacity} datagrams) was exceeded."));
                return;
            }

            _bytesAccepted += copy.Length;
            if (_options.MaxBytes is { } exactLimit && _bytesAccepted == exactLimit)
                BeginStopLocked(UdpRecordingStopReason.ByteLimitReached, null);
        }
    }

    public void Stop(UdpRecordingStopReason reason = UdpRecordingStopReason.StoppedByUser)
    {
        lock (_gate)
            BeginStopLocked(reason, null);
    }

    public void Dispose()
    {
        Task? writerTask;
        lock (_gate)
        {
            _disposed = true;
            BeginStopLocked(UdpRecordingStopReason.SourceStopped, null);
            writerTask = _stream is null ? null : _writerTask;
        }

        if (writerTask is not null && Task.CurrentId != writerTask.Id)
            writerTask.GetAwaiter().GetResult();
    }

    private void BeginStopLocked(UdpRecordingStopReason reason, Exception? error)
    {
        if (_stream is null || _channel is null || _stopReason is not null)
            return;

        _stopReason = reason;
        _stopError = error;
        _durationTimer?.Dispose();
        _durationTimer = null;
        _channel.Writer.TryComplete();
    }

    private async Task WriteQueuedDatagramsAsync(ChannelReader<byte[]> reader, Stream stream)
    {
        Exception? writeError = null;
        try
        {
            await foreach (var datagram in reader.ReadAllAsync().ConfigureAwait(false))
            {
                await stream.WriteAsync(datagram).ConfigureAwait(false);
                lock (_gate)
                    _bytesWritten += datagram.Length;
            }
        }
        catch (Exception ex)
        {
            writeError = ex;
        }

        Exception? disposeError = null;
        try
        {
            stream.Dispose();
        }
        catch (Exception ex)
        {
            disposeError = ex;
        }

        UdpRecordingResult result;
        lock (_gate)
        {
            var options = _options!;
            var reason = writeError is null && disposeError is null
                ? _stopReason ?? UdpRecordingStopReason.Error
                : UdpRecordingStopReason.Error;
            var error = writeError ?? disposeError ?? _stopError;

            result = new UdpRecordingResult(
                options.FilePath,
                _bytesWritten,
                DateTimeOffset.UtcNow - _startedAt,
                reason,
                error);

            _stream = null;
            _channel = null;
            _options = null;
            _durationTimer?.Dispose();
            _durationTimer = null;
        }

        Completed?.Invoke(result);
    }
}
