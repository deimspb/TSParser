namespace TSParser.Web.Services;

/// <summary>Parses comma-separated PID lists from toolbar input (decimal or 0x hex).</summary>
public static class TsPidListParser
{
    public static bool TryParseList(string? input, out List<ushort> pids, out string? error)
    {
        pids = [];
        error = null;

        if (string.IsNullOrWhiteSpace(input))
            return true;

        foreach (var part in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParsePid(part, out var pid))
            {
                error = $"Invalid PID \"{part}\". Use decimal or hex (for example 0x0100).";
                pids = [];
                return false;
            }

            pids.Add(pid);
        }

        return true;
    }

    public static bool TryParsePid(string? input, out ushort pid)
    {
        pid = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hex = trimmed[2..];
            if (ushort.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out pid))
                return true;
        }

        return ushort.TryParse(trimmed, out pid);
    }
}
