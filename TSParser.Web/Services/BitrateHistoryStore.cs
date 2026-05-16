using TSParser.Analysis;

namespace TSParser.Web.Services;

/// <summary>Accumulates stream bitrate samples for file (offset X) and UDP (rolling time) charts.</summary>
public sealed class BitrateHistoryStore
{
    public const int MaxUdpPoints = 720;

    private readonly object _lock = new();
    private readonly List<BitrateFilePoint> _filePoints = [];
    private readonly Queue<BitrateUdpPoint> _udpPoints = new();

    public TsParserSessionInputMode Mode { get; private set; } = TsParserSessionInputMode.None;

    public long? FileLengthBytes { get; private set; }

    public int Revision { get; private set; }

    public void ConfigureForFile(string? filePath)
    {
        lock (_lock)
        {
            _filePoints.Clear();
            _udpPoints.Clear();
            Mode = TsParserSessionInputMode.File;
            FileLengthBytes = filePath is { Length: > 0 } && File.Exists(filePath)
                ? new FileInfo(filePath).Length
                : null;
            BumpRevision();
        }
    }

    public void ConfigureForUdp()
    {
        lock (_lock)
        {
            _filePoints.Clear();
            _udpPoints.Clear();
            Mode = TsParserSessionInputMode.Udp;
            FileLengthBytes = null;
            BumpRevision();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _filePoints.Clear();
            _udpPoints.Clear();
            Mode = TsParserSessionInputMode.None;
            FileLengthBytes = null;
            BumpRevision();
        }
    }

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
                    while (_udpPoints.Count > MaxUdpPoints)
                        _udpPoints.Dequeue();
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
            return Mode switch
            {
                TsParserSessionInputMode.File => BitrateChartSnapshot.ForFile(
                    _filePoints.ToArray(),
                    FileLengthBytes),

                TsParserSessionInputMode.Udp => BitrateChartSnapshot.ForUdp(_udpPoints.ToArray()),

                _ => BitrateChartSnapshot.Empty
            };
        }
    }

    private void BumpRevision() => Revision++;
}

public readonly record struct BitrateFilePoint(long ByteOffset, double TotalBitsPerSecond, double UsefulBitsPerSecond);

public readonly record struct BitrateUdpPoint(DateTime TimestampUtc, double TotalBitsPerSecond, double UsefulBitsPerSecond);

public enum BitrateChartMode
{
    None,
    File,
    Udp
}

public sealed class BitrateChartSnapshot
{
    public static readonly BitrateChartSnapshot Empty = new(BitrateChartMode.None, [], [], null);

    public BitrateChartMode Mode { get; }

    public IReadOnlyList<BitrateFilePoint> FilePoints { get; }

    public IReadOnlyList<BitrateUdpPoint> UdpPoints { get; }

    public long? FileLengthBytes { get; }

    private BitrateChartSnapshot(
        BitrateChartMode mode,
        IReadOnlyList<BitrateFilePoint> filePoints,
        IReadOnlyList<BitrateUdpPoint> udpPoints,
        long? fileLengthBytes)
    {
        Mode = mode;
        FilePoints = filePoints;
        UdpPoints = udpPoints;
        FileLengthBytes = fileLengthBytes;
    }

    public static BitrateChartSnapshot ForFile(IReadOnlyList<BitrateFilePoint> points, long? fileLengthBytes) =>
        new(BitrateChartMode.File, points, [], fileLengthBytes);

    public static BitrateChartSnapshot ForUdp(IReadOnlyList<BitrateUdpPoint> points) =>
        new(BitrateChartMode.Udp, [], points, null);

    public double? AverageTotalMegabitsPerSecond =>
        Mode == BitrateChartMode.File && FilePoints.Count > 0
            ? FilePoints.Average(p => p.TotalBitsPerSecond) / 1_000_000d
            : null;

    public double? AverageUsefulMegabitsPerSecond =>
        Mode == BitrateChartMode.File && FilePoints.Count > 0
            ? FilePoints.Average(p => p.UsefulBitsPerSecond) / 1_000_000d
            : null;
}
