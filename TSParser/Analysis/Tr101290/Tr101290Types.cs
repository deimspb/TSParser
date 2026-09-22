// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace TSParser.Analysis;

/// <summary>TR 101 290 priority. Priority 1 is required for decodability.</summary>
public enum Tr101290Priority
{
    First = 1,
    Second = 2,
    Third = 3,
}

/// <summary>
/// Indicators from ETSI TR 101 290 V1.4.1 implemented by <see cref="Tr101290Monitor"/>.
/// PCR accuracy, PTS, and the T-STD buffer model are not included.
/// </summary>
public enum Tr101290Indicator
{
    TsSyncLoss,
    SyncByteError,
    PatError2,
    ContinuityCountError,
    PmtError2,
    PidError,
    TransportError,
    CrcError,
    PcrRepetitionError,
    PcrDiscontinuityIndicatorError,
    CatError,
    NitActualError,
    NitOtherError,
    SiRepetitionError,
    UnreferencedPid,
    SdtActualError,
    SdtOtherError,
    EitActualError,
    EitOtherError,
    EitPfError,
    RstError,
    TdtError,
}

/// <summary>Current condition of one indicator instance.</summary>
public enum Tr101290IndicatorState
{
    NotMeasured,
    Ok,
    Error,
}

/// <summary>Whether an indicator entered or left the error state.</summary>
public enum Tr101290EventKind
{
    Raised,
    Cleared,
}

/// <summary>One TR 101 290 state change. <see cref="OccurrenceCount"/> is the total raises for this indicator and PID.</summary>
public readonly record struct Tr101290Event(
    Tr101290Priority Priority,
    Tr101290Indicator Indicator,
    Tr101290EventKind Kind,
    ushort? Pid,
    ulong PacketNumber,
    ulong OccurrenceCount,
    string Detail);

/// <summary>Enables TR 101 290 monitoring. Disabled by default so existing parses stay unchanged.</summary>
public sealed record Tr101290Options
{
    public static Tr101290Options Disabled { get; } = new();

    /// <summary>Monitoring with the standard thresholds and a 5 s <see cref="PidErrorTimeout"/>.</summary>
    public static Tr101290Options Enable { get; } = new() { Enabled = true };

    public bool Enabled { get; init; }

    /// <summary>How long a PMT-referenced PID may be absent before <see cref="Tr101290Indicator.PidError"/>. Default 5 s.</summary>
    public TimeSpan PidErrorTimeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>Names and priority lookup for TR 101 290 indicators.</summary>
public static class Tr101290Names
{
    public static Tr101290Priority PriorityOf(Tr101290Indicator indicator) => indicator switch
    {
        Tr101290Indicator.TsSyncLoss or
        Tr101290Indicator.SyncByteError or
        Tr101290Indicator.PatError2 or
        Tr101290Indicator.ContinuityCountError or
        Tr101290Indicator.PmtError2 or
        Tr101290Indicator.PidError => Tr101290Priority.First,
        Tr101290Indicator.TransportError or
        Tr101290Indicator.CrcError or
        Tr101290Indicator.PcrRepetitionError or
        Tr101290Indicator.PcrDiscontinuityIndicatorError or
        Tr101290Indicator.CatError => Tr101290Priority.Second,
        _ => Tr101290Priority.Third,
    };

    public static string Indicator(Tr101290Indicator indicator) => indicator switch
    {
        Tr101290Indicator.TsSyncLoss => "TS_sync_loss",
        Tr101290Indicator.SyncByteError => "Sync_byte_error",
        Tr101290Indicator.PatError2 => "PAT_error_2",
        Tr101290Indicator.ContinuityCountError => "Continuity_count_error",
        Tr101290Indicator.PmtError2 => "PMT_error_2",
        Tr101290Indicator.PidError => "PID_error",
        Tr101290Indicator.TransportError => "Transport_error",
        Tr101290Indicator.CrcError => "CRC_error",
        Tr101290Indicator.PcrRepetitionError => "PCR_repetition_error",
        Tr101290Indicator.PcrDiscontinuityIndicatorError => "PCR_discontinuity_indicator_error",
        Tr101290Indicator.CatError => "CAT_error",
        Tr101290Indicator.NitActualError => "NIT_actual_error",
        Tr101290Indicator.NitOtherError => "NIT_other_error",
        Tr101290Indicator.SiRepetitionError => "SI_repetition_error",
        Tr101290Indicator.UnreferencedPid => "Unreferenced_PID",
        Tr101290Indicator.SdtActualError => "SDT_actual_error",
        Tr101290Indicator.SdtOtherError => "SDT_other_error",
        Tr101290Indicator.EitActualError => "EIT_actual_error",
        Tr101290Indicator.EitOtherError => "EIT_other_error",
        Tr101290Indicator.EitPfError => "EIT_PF_error",
        Tr101290Indicator.RstError => "RST_error",
        Tr101290Indicator.TdtError => "TDT_error",
        _ => indicator.ToString(),
    };
}

internal static class Tr101290Limits
{
    public const ulong PcrModulus = (1UL << 33) * 300UL;
    public const ulong TickHz = 27_000_000UL;
    public const ulong Ms25 = 675_000UL;
    public const ulong Ms40 = 1_080_000UL;
    public const ulong Ms100 = 2_700_000UL;
    public const ulong Ms500 = 13_500_000UL;
    public const ulong Sec2 = 54_000_000UL;
    public const ulong Sec5 = 135_000_000UL;
    public const ulong Sec10 = 270_000_000UL;
    public const ulong Sec30 = 810_000_000UL;

    public static ulong ForwardDelta(ulong newer, ulong older)
    {
        if (newer >= older)
            return newer - older;

        return (PcrModulus - older) + newer;
    }

    public static ulong Ticks(TimeSpan time)
    {
        if (time <= TimeSpan.Zero)
            return Sec5;

        var ticks = time.TotalSeconds * TickHz;
        if (ticks >= ulong.MaxValue)
            return ulong.MaxValue;

        return (ulong)ticks;
    }
}
