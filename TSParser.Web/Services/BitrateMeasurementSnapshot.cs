using TSParser.Analysis;

namespace TSParser.Web.Services;

/// <summary>Parser-facing bitrate options (excludes chart display flags).</summary>
public readonly record struct BitrateMeasurementSnapshot(
    BitrateClockSource ClockSource,
    TimeSpan MeasurementWindow,
    ushort? ReferencePid,
    double AssumedBitsPerSecond,
    bool MeasureStreamBitrate,
    bool MeasureUsefulAndTotalBitrate,
    IReadOnlyList<ushort> ChartPids)
{
    public static BitrateMeasurementSnapshot From(TsParserSessionSettings settings) =>
        new(
            settings.ClockSource,
            settings.MeasurementWindow,
            settings.ReferencePid,
            settings.AssumedBitsPerSecond,
            settings.MeasureStreamBitrate,
            settings.MeasureUsefulAndTotalBitrate,
            settings.ChartPids.ToArray());

    public bool RequiresParserRestart(in BitrateMeasurementSnapshot previous) =>
        ClockSource != previous.ClockSource
        || MeasurementWindow != previous.MeasurementWindow
        || ReferencePid != previous.ReferencePid
        || Math.Abs(AssumedBitsPerSecond - previous.AssumedBitsPerSecond) > 1
        || MeasureStreamBitrate != previous.MeasureStreamBitrate
        || MeasureUsefulAndTotalBitrate != previous.MeasureUsefulAndTotalBitrate
        || !ChartPids.SequenceEqual(previous.ChartPids);
}
