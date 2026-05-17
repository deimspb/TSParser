using TSParser.Analysis;

namespace TSParser.Web.Services;

/// <summary>Accumulates stream and per-PID bitrate samples for file (offset X) and UDP (rolling time) charts.</summary>
public sealed class BitrateHistoryStore
{
    public const int MaxUdpPoints = 720;

    private static readonly TimeSpan UdpSumMergeWindow = TimeSpan.FromMilliseconds(150);

    private readonly object _lock = new();
    private readonly List<BitrateFilePoint> _filePoints = [];
    private readonly Queue<BitrateUdpPoint> _udpPoints = new();
    private readonly Dictionary<ushort, List<BitratePidFilePoint>> _pidFilePoints = new();
    private readonly Dictionary<ushort, Queue<BitratePidUdpPoint>> _pidUdpPoints = new();
    private readonly List<BitratePidFilePoint> _sumFilePoints = [];
    private readonly List<BitratePidUdpPoint> _sumUdpPoints = [];

    private HashSet<ushort> _chartPids = [];
    private bool _showStreamOnChart = true;
    private bool _showPidSumOnChart;
    private bool _showIndividualPidsOnChart = true;

    public TsParserSessionInputMode Mode { get; private set; } = TsParserSessionInputMode.None;

    public long? FileLengthBytes { get; private set; }

    public int Revision { get; private set; }

    public void ApplyChartSettings(
        IReadOnlyList<ushort> chartPids,
        bool showStreamOnChart,
        bool showPidSumOnChart,
        bool showIndividualPidsOnChart)
    {
        lock (_lock)
        {
            _chartPids = chartPids.Count > 0 ? chartPids.ToHashSet() : [];
            _showStreamOnChart = showStreamOnChart;
            _showPidSumOnChart = showPidSumOnChart && _chartPids.Count >= 2;
            _showIndividualPidsOnChart = showIndividualPidsOnChart && _chartPids.Count > 0;
            BumpRevision();
        }
    }

    public void ConfigureForFile(string? filePath, long? fileLengthBytes = null)
    {
        lock (_lock)
        {
            ClearSamplesLocked();
            Mode = TsParserSessionInputMode.File;
            FileLengthBytes = fileLengthBytes ?? (filePath is { Length: > 0 } && File.Exists(filePath)
                ? new FileInfo(filePath).Length
                : null);
            BumpRevision();
        }
    }

    public void ConfigureForUdp()
    {
        lock (_lock)
        {
            ClearSamplesLocked();
            Mode = TsParserSessionInputMode.Udp;
            FileLengthBytes = null;
            BumpRevision();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            ClearSamplesLocked();
            Mode = TsParserSessionInputMode.None;
            FileLengthBytes = null;
            BumpRevision();
        }
    }

    public bool TryAddSample(BitrateSample sample) =>
        sample.Pid.HasValue ? TryAddPidSample(sample) : TryAddStreamSample(sample);

    public bool TryAddStreamSample(BitrateSample sample)
    {
        if (sample.Pid.HasValue)
            return false;

        var totalBps = sample.HasDualStreamMeasurement
            ? sample.TotalBitsPerSecond!.Value
            : sample.BitsPerSecond;

        var usefulBps = sample.HasDualStreamMeasurement
            ? sample.UsefulBitsPerSecond!.Value
            : sample.BitsPerSecond;

        lock (_lock)
        {
            switch (Mode)
            {
                case TsParserSessionInputMode.File:
                    if (!sample.StreamByteOffset.HasValue)
                        return false;

                    _filePoints.Add(new BitrateFilePoint(
                        sample.StreamByteOffset.Value,
                        totalBps,
                        usefulBps));
                    break;

                case TsParserSessionInputMode.Udp:
                    _udpPoints.Enqueue(new BitrateUdpPoint(DateTime.UtcNow, totalBps, usefulBps));
                    TrimUdpQueue(_udpPoints);
                    break;

                default:
                    return false;
            }

            BumpRevision();
        }

        return true;
    }

    public bool TryAddPidSample(BitrateSample sample)
    {
        if (sample.Pid is not ushort pid)
            return false;

        lock (_lock)
        {
            if (_chartPids.Count == 0 || !_chartPids.Contains(pid))
                return false;

            switch (Mode)
            {
                case TsParserSessionInputMode.File:
                    if (!sample.StreamByteOffset.HasValue)
                        return false;

                    AddPidFilePoint(pid, sample.StreamByteOffset.Value, sample.BitsPerSecond);
                    break;

                case TsParserSessionInputMode.Udp:
                    AddPidUdpPoint(pid, DateTime.UtcNow, sample.BitsPerSecond);
                    break;

                default:
                    return false;
            }

            BumpRevision();
        }

        return true;
    }

    public BitrateChartSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var chartPids = _chartPids.OrderBy(p => p).ToArray();
            var pidFile = new Dictionary<ushort, IReadOnlyList<BitratePidFilePoint>>();
            var pidUdp = new Dictionary<ushort, IReadOnlyList<BitratePidUdpPoint>>();

            foreach (var pid in chartPids)
            {
                if (_pidFilePoints.TryGetValue(pid, out var fileList) && fileList.Count > 0)
                    pidFile[pid] = fileList.ToArray();

                if (_pidUdpPoints.TryGetValue(pid, out var udpQueue) && udpQueue.Count > 0)
                    pidUdp[pid] = udpQueue.ToArray();
            }

            var sumFile = BuildEffectiveSumFile(chartPids, pidFile);
            var sumUdp = BuildEffectiveSumUdp(chartPids, pidUdp);

            return Mode switch
            {
                TsParserSessionInputMode.File => BitrateChartSnapshot.ForFile(
                    _filePoints.ToArray(),
                    sumFile,
                    pidFile,
                    chartPids,
                    _showStreamOnChart,
                    _showPidSumOnChart,
                    _showIndividualPidsOnChart,
                    FileLengthBytes),

                TsParserSessionInputMode.Udp => BitrateChartSnapshot.ForUdp(
                    _udpPoints.ToArray(),
                    sumUdp,
                    pidUdp,
                    chartPids,
                    _showStreamOnChart,
                    _showPidSumOnChart,
                    _showIndividualPidsOnChart),

                _ => BitrateChartSnapshot.Empty
            };
        }
    }

    private IReadOnlyList<BitratePidFilePoint> BuildEffectiveSumFile(
        IReadOnlyList<ushort> chartPids,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidFilePoint>> pidFile)
    {
        if (!_showPidSumOnChart || chartPids.Count < 2)
            return [];

        var fromPidSeries = BitrateSumBuilder.FromFilePidSeries(chartPids, pidFile);
        if (fromPidSeries.Count > 0)
            return fromPidSeries;

        return _sumFilePoints.ToArray();
    }

    private IReadOnlyList<BitratePidUdpPoint> BuildEffectiveSumUdp(
        IReadOnlyList<ushort> chartPids,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidUdpPoint>> pidUdp)
    {
        if (!_showPidSumOnChart || chartPids.Count < 2)
            return [];

        var fromPidSeries = BitrateSumBuilder.FromUdpPidSeries(chartPids, pidUdp);
        if (fromPidSeries.Count > 0)
            return fromPidSeries;

        return _sumUdpPoints.ToArray();
    }

    private void AddPidFilePoint(ushort pid, long byteOffset, double bitsPerSecond)
    {
        if (!_pidFilePoints.TryGetValue(pid, out var list))
        {
            list = [];
            _pidFilePoints[pid] = list;
        }

        list.Add(new BitratePidFilePoint(byteOffset, bitsPerSecond));
        MergeSumFilePoint(byteOffset, bitsPerSecond);
    }

    private void AddPidUdpPoint(ushort pid, DateTime timestampUtc, double bitsPerSecond)
    {
        if (!_pidUdpPoints.TryGetValue(pid, out var queue))
        {
            queue = new Queue<BitratePidUdpPoint>();
            _pidUdpPoints[pid] = queue;
        }

        queue.Enqueue(new BitratePidUdpPoint(timestampUtc, bitsPerSecond));
        TrimUdpQueue(queue);
        MergeSumUdpPoint(timestampUtc, bitsPerSecond);
    }

    private void MergeSumFilePoint(long byteOffset, double bitsPerSecond)
    {
        if (_sumFilePoints.Count > 0 && _sumFilePoints[^1].ByteOffset == byteOffset)
        {
            var last = _sumFilePoints[^1];
            _sumFilePoints[^1] = new BitratePidFilePoint(byteOffset, last.BitsPerSecond + bitsPerSecond);
            return;
        }

        _sumFilePoints.Add(new BitratePidFilePoint(byteOffset, bitsPerSecond));
    }

    private void MergeSumUdpPoint(DateTime timestampUtc, double bitsPerSecond)
    {
        if (_sumUdpPoints.Count > 0)
        {
            var lastIndex = _sumUdpPoints.Count - 1;
            var last = _sumUdpPoints[lastIndex];
            if (timestampUtc - last.TimestampUtc <= UdpSumMergeWindow)
            {
                _sumUdpPoints[lastIndex] = new BitratePidUdpPoint(last.TimestampUtc, last.BitsPerSecond + bitsPerSecond);
                return;
            }
        }

        _sumUdpPoints.Add(new BitratePidUdpPoint(timestampUtc, bitsPerSecond));
        TrimUdpList(_sumUdpPoints);
    }

    private static void TrimUdpQueue<T>(Queue<T> queue)
    {
        while (queue.Count > MaxUdpPoints)
            queue.Dequeue();
    }

    private static void TrimUdpList<T>(List<T> list)
    {
        var remove = list.Count - MaxUdpPoints;
        if (remove > 0)
            list.RemoveRange(0, remove);
    }

    private void ClearSamplesLocked()
    {
        _filePoints.Clear();
        _udpPoints.Clear();
        _pidFilePoints.Clear();
        _pidUdpPoints.Clear();
        _sumFilePoints.Clear();
        _sumUdpPoints.Clear();
    }

    private void BumpRevision() => Revision++;
}

public readonly record struct BitrateFilePoint(long ByteOffset, double TotalBitsPerSecond, double UsefulBitsPerSecond);

public readonly record struct BitrateUdpPoint(DateTime TimestampUtc, double TotalBitsPerSecond, double UsefulBitsPerSecond);

public readonly record struct BitratePidFilePoint(long ByteOffset, double BitsPerSecond);

public readonly record struct BitratePidUdpPoint(DateTime TimestampUtc, double BitsPerSecond);

public enum BitrateChartMode
{
    None,
    File,
    Udp
}

public sealed class BitrateChartSnapshot
{
    public static readonly BitrateChartSnapshot Empty = new(
        BitrateChartMode.None,
        [],
        [],
        [],
        [],
        new Dictionary<ushort, IReadOnlyList<BitratePidFilePoint>>(),
        new Dictionary<ushort, IReadOnlyList<BitratePidUdpPoint>>(),
        [],
        false,
        false,
        false,
        null);

    public BitrateChartMode Mode { get; }

    public IReadOnlyList<BitrateFilePoint> FilePoints { get; }

    public IReadOnlyList<BitrateUdpPoint> UdpPoints { get; }

    public IReadOnlyList<BitratePidFilePoint> SumFilePoints { get; }

    public IReadOnlyList<BitratePidUdpPoint> SumUdpPoints { get; }

    public IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidFilePoint>> PidFilePoints { get; }

    public IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidUdpPoint>> PidUdpPoints { get; }

    public IReadOnlyList<ushort> ChartPids { get; }

    public bool ShowStreamOnChart { get; }

    public bool ShowPidSumOnChart { get; }

    public bool ShowIndividualPidsOnChart { get; }

    public long? FileLengthBytes { get; }

    private BitrateChartSnapshot(
        BitrateChartMode mode,
        IReadOnlyList<BitrateFilePoint> filePoints,
        IReadOnlyList<BitrateUdpPoint> udpPoints,
        IReadOnlyList<BitratePidFilePoint> sumFilePoints,
        IReadOnlyList<BitratePidUdpPoint> sumUdpPoints,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidFilePoint>> pidFilePoints,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidUdpPoint>> pidUdpPoints,
        IReadOnlyList<ushort> chartPids,
        bool showStreamOnChart,
        bool showPidSumOnChart,
        bool showIndividualPidsOnChart,
        long? fileLengthBytes)
    {
        Mode = mode;
        FilePoints = filePoints;
        UdpPoints = udpPoints;
        SumFilePoints = sumFilePoints;
        SumUdpPoints = sumUdpPoints;
        PidFilePoints = pidFilePoints;
        PidUdpPoints = pidUdpPoints;
        ChartPids = chartPids;
        ShowStreamOnChart = showStreamOnChart;
        ShowPidSumOnChart = showPidSumOnChart;
        ShowIndividualPidsOnChart = showIndividualPidsOnChart;
        FileLengthBytes = fileLengthBytes;
    }

    public static BitrateChartSnapshot ForFile(
        IReadOnlyList<BitrateFilePoint> points,
        IReadOnlyList<BitratePidFilePoint> sumPoints,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidFilePoint>> pidPoints,
        IReadOnlyList<ushort> chartPids,
        bool showStreamOnChart,
        bool showPidSumOnChart,
        bool showIndividualPidsOnChart,
        long? fileLengthBytes) =>
        new(
            BitrateChartMode.File,
            points,
            [],
            sumPoints,
            [],
            pidPoints,
            new Dictionary<ushort, IReadOnlyList<BitratePidUdpPoint>>(),
            chartPids,
            showStreamOnChart,
            showPidSumOnChart,
            showIndividualPidsOnChart,
            fileLengthBytes);

    public static BitrateChartSnapshot ForUdp(
        IReadOnlyList<BitrateUdpPoint> points,
        IReadOnlyList<BitratePidUdpPoint> sumPoints,
        IReadOnlyDictionary<ushort, IReadOnlyList<BitratePidUdpPoint>> pidPoints,
        IReadOnlyList<ushort> chartPids,
        bool showStreamOnChart,
        bool showPidSumOnChart,
        bool showIndividualPidsOnChart) =>
        new(
            BitrateChartMode.Udp,
            [],
            points,
            [],
            sumPoints,
            new Dictionary<ushort, IReadOnlyList<BitratePidFilePoint>>(),
            pidPoints,
            chartPids,
            showStreamOnChart,
            showPidSumOnChart,
            showIndividualPidsOnChart,
            null);

    public bool HasStoredSamples =>
        FilePoints.Count > 0
        || UdpPoints.Count > 0
        || PidFilePoints.Values.Any(v => v.Count > 0)
        || PidUdpPoints.Values.Any(v => v.Count > 0);

    public bool HasPidSamples =>
        PidFilePoints.Values.Any(v => v.Count > 0) || PidUdpPoints.Values.Any(v => v.Count > 0);

    public bool HasChartData =>
        Mode switch
        {
            BitrateChartMode.File => HasFileChartData,
            BitrateChartMode.Udp => HasUdpChartData,
            _ => false
        };

    private bool HasFileChartData =>
        (ShowStreamOnChart && FilePoints.Count > 0)
        || (ShowPidSumOnChart && SumFilePoints.Count > 0)
        || (ShowIndividualPidsOnChart && PidFilePoints.Values.Any(v => v.Count > 0));

    private bool HasUdpChartData =>
        (ShowStreamOnChart && UdpPoints.Count > 0)
        || (ShowPidSumOnChart && SumUdpPoints.Count > 0)
        || (ShowIndividualPidsOnChart && PidUdpPoints.Values.Any(v => v.Count > 0));

    public double? AverageTotalMegabitsPerSecond =>
        Mode == BitrateChartMode.File && FilePoints.Count > 0
            ? FilePoints.Average(p => p.TotalBitsPerSecond) / 1_000_000d
            : null;

    public double? AverageUsefulMegabitsPerSecond =>
        Mode == BitrateChartMode.File && FilePoints.Count > 0
            ? FilePoints.Average(p => p.UsefulBitsPerSecond) / 1_000_000d
            : null;
}
