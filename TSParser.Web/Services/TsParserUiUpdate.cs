using TSParser.Analysis;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;

namespace TSParser.Web.Services;

/// <summary>Discriminator for SI tables delivered on the UI channel.</summary>
public enum TsTableKind
{
    Pat,
    Pmt,
    Cat,
    Nit,
    Sdt,
    Bat,
    Eit,
    Tdt,
    Tot,
    Ait,
    Mip,
    Scte35,
    Ews,
    Eews
}

/// <summary>Reason the session signaled a UI reset (tree / bitrate history).</summary>
public enum TsParserSessionResetReason
{
    OpenFile,
    StartUdp,
    Manual
}

/// <summary>Background parser input mode.</summary>
public enum TsParserSessionInputMode
{
    None,
    File,
    Udp
}

/// <summary>Messages from <see cref="TsParserSessionService"/> to Blazor UI consumers.</summary>
public abstract record TsParserUiUpdate
{
    public sealed record TableParsed(TsTableKind Kind, Table Table) : TsParserUiUpdate;

    public sealed record BitrateMeasured(BitrateSample Sample) : TsParserUiUpdate;

    public sealed record ParserCompleted : TsParserUiUpdate;

    public sealed record SessionStarted(
        TsParserSessionInputMode InputMode,
        string? FilePath,
        string? MulticastEndpoint,
        string? BindAddress) : TsParserUiUpdate;

    public sealed record SessionReset(TsParserSessionResetReason Reason) : TsParserUiUpdate;

    public sealed record ParserStopped(TsParserSessionInputMode PreviousMode) : TsParserUiUpdate;

    public sealed record LogMessage(string Text, bool IsError) : TsParserUiUpdate;
}
