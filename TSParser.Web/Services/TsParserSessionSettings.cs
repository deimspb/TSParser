using TSParser.Analysis;

namespace TSParser.Web.Services;

/// <summary>User-configurable parser and bitrate options applied on the next session start.</summary>
public sealed class TsParserSessionSettings
{
    public BitrateClockSource ClockSource { get; set; } = BitrateClockSource.Pcr;

    public TimeSpan MeasurementWindow { get; set; } = TimeSpan.FromSeconds(1);

    public ushort? ReferencePid { get; set; }

    public double AssumedBitsPerSecond { get; set; } = 10_000_000;

    public bool MeasureStreamBitrate { get; set; } = true;

    public bool MeasureUsefulAndTotalBitrate { get; set; } = true;

    public bool MeasurePerPidBitrate { get; set; }

    public IReadOnlyList<ushort> EwsPids { get; private set; } = [];

    public IReadOnlyList<ushort> EewsPids { get; private set; } = [];

    public void SetEwsPids(IEnumerable<ushort> pids) =>
        EwsPids = pids?.ToList() ?? [];

    public void SetEewsPids(IEnumerable<ushort> pids) =>
        EewsPids = pids?.ToList() ?? [];

    internal BitrateMeasurementOptions CreateBitrateOptions() => new()
    {
        Enabled = true,
        ClockSource = ClockSource,
        MeasurementWindow = MeasurementWindow,
        ReferencePid = ReferencePid,
        AssumedBitsPerSecond = AssumedBitsPerSecond,
        MeasureStreamBitrate = MeasureStreamBitrate,
        MeasureUsefulAndTotalBitrate = MeasureUsefulAndTotalBitrate,
        MeasurePerPidBitrate = MeasurePerPidBitrate,
        IncludeNullPackets = false
    };
}
