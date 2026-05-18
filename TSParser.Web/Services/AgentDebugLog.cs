using System.Text.Json;

namespace TSParser.Web.Services;

// #region agent log
/// <summary>Debug-session NDJSON logger (remove after verification).</summary>
internal static class AgentDebugLog
{
    private static readonly object Gate = new();
    private static string? _logPath;

    private static string LogPath => _logPath ??= ResolveLogPath();

    public static void Write(string hypothesisId, string location, string message, object? data = null, string runId = "pre-fix")
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = "9f8061",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runId
            });

            lock (Gate)
                File.AppendAllText(LogPath, payload + Environment.NewLine);
        }
        catch
        {
            // ignore debug logging failures
        }
    }

    private static string ResolveLogPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TSParser.sln")))
                return Path.Combine(dir.FullName, "debug-9f8061.log");

            dir = dir.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "debug-9f8061.log");
    }
}
// #endregion
