namespace TSParser.Web.Models;

/// <summary>One row in the transport-stream PID list (observed and/or SI-defined).</summary>
public sealed class PidTreeEntry
{
    public ushort Pid { get; init; }

    /// <summary>Human-readable role from PAT/PMT/CAT/SI tables; null when unknown.</summary>
    public string? TypeDescription { get; init; }

    /// <summary>At least one TS packet was seen on this PID.</summary>
    public bool IsObservedInStream { get; init; }

    /// <summary>Declared in SI (PAT/PMT/CAT/…) but no packets observed.</summary>
    public bool IsMissingFromStream { get; init; }

    public string Label => FormatLabel();

    private string FormatLabel()
    {
        var hex = $"pid: 0x{Pid:X} ({Pid})";
        if (string.IsNullOrWhiteSpace(TypeDescription))
            return hex;

        var suffix = IsMissingFromStream ? " ?" : "";
        return $"{hex} => {TypeDescription}{suffix}";
    }
}
