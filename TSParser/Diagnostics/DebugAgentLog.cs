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

using System.Text.Json;

namespace TSParser.Diagnostics;

/// <summary>NDJSON debug logging for agent debug sessions (session 2a6ba5).</summary>
public static class DebugAgentLog
{
    private const string SessionId = "2a6ba5";
    private static string? _logPath;

    private static string ResolveLogPath()
    {
        if (_logPath is not null)
            return _logPath;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TSParser.sln")))
            {
                _logPath = Path.Combine(dir.FullName, "debug-2a6ba5.log");
                return _logPath;
            }
        }

        _logPath = Path.Combine(Directory.GetCurrentDirectory(), "debug-2a6ba5.log");
        return _logPath;
    }

    public static void Write(string location, string message, object? data, string hypothesisId, string? runId = null)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["sessionId"] = SessionId,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["location"] = location,
                ["message"] = message,
                ["data"] = data,
                ["hypothesisId"] = hypothesisId,
            };
            if (runId is not null)
                payload["runId"] = runId;

            var line = JsonSerializer.Serialize(payload);
            File.AppendAllText(ResolveLogPath(), line + Environment.NewLine);
        }
        catch
        {
            // ignore logging failures
        }
    }
}
