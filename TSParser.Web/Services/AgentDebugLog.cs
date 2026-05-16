using System.Text.Json;

namespace TSParser.Web.Services;

// #region agent log
internal static class AgentDebugLog
{
    private static readonly string LogPath = ResolveLogPath();

    public static void Write(string hypothesisId, string location, string message, object? data = null, string runId = "pre-fix")
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = "098f9b",
                ["hypothesisId"] = hypothesisId,
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["runId"] = runId
            };
            File.AppendAllText(LogPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // ignore debug logging failures
        }
    }

    private static string ResolveLogPath()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "TSParser.sln")))
                return Path.Combine(dir, "debug-098f9b.log");

            dir = Directory.GetParent(dir)?.FullName;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "debug-098f9b.log");
    }
}
// #endregion
