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

using System.Runtime.InteropServices;
using System.Text.Json;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Environments;
using Perfolizer.Horology;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace TSParser.Benchmarks.Infrastructure;

/// <summary>
/// Writes schemaVersion 1 baseline JSON under perf/baselines/ for compare-perf.ps1.
/// </summary>
internal sealed class PerfBaselineExporter : IExporter
{
    private static readonly object s_gate = new();
    private static readonly Dictionary<string, BenchmarkEntry> s_accumulated = new(StringComparer.Ordinal);
    private static string? s_lastRepoRoot;
    private static HostEnvironmentInfo? s_hostInfo;

    public string Name => "perf-baseline";

    public void ExportToLog(Summary summary, ILogger logger)
    {
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
        {
            consoleLogger.WriteLineError("PerfBaselineExporter: could not locate repo root (perf/baselines).");
            yield break;
        }

        lock (s_gate)
        {
            s_lastRepoRoot = repoRoot;
            s_hostInfo = summary.HostEnvironmentInfo;

            var timeUnit = summary.Style.TimeUnit ?? TimeUnit.Microsecond;
            foreach (var report in summary.Reports)
            {
                var key = BuildKey(report);
                var mean = report.ResultStatistics?.Mean ?? 0;
                var stdDev = report.ResultStatistics?.StandardDeviation ?? 0;

                long? allocated = report.Metrics.TryGetValue("Allocated Memory", out var metric)
                    ? (long?)Convert.ToInt64(metric.Value)
                    : null;

                s_accumulated[key] = new BenchmarkEntry(
                    ToNanoseconds(mean, timeUnit),
                    ToNanoseconds(stdDev, timeUnit),
                    allocated);
            }
        }

        var outPath = WriteAccumulated(consoleLogger);
        if (outPath != null)
        {
            yield return outPath;
        }
    }

    private static string? WriteAccumulated(ILogger consoleLogger)
    {
        lock (s_gate)
        {
            if (s_lastRepoRoot == null || s_accumulated.Count == 0)
            {
                return null;
            }

            var rid = RuntimeInformation.RuntimeIdentifier;
            if (string.IsNullOrWhiteSpace(rid))
            {
                rid = Environment.Is64BitProcess ? "win-x64" : "win-x86";
            }

            var outDir = Path.Combine(s_lastRepoRoot, "perf", "baselines");
            Directory.CreateDirectory(outDir);
            var outPath = Path.Combine(outDir, "current.json");
            var canonicalName = $"net10.0-{rid}.json";

            var benchmarks = s_accumulated.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)new
                {
                    meanNs = kvp.Value.MeanNs,
                    stdDevNs = kvp.Value.StdDevNs,
                    allocatedBytes = kvp.Value.AllocatedBytes,
                },
                StringComparer.Ordinal);

            var cpu = FormatCpu(s_hostInfo);
            var doc = new
            {
                schemaVersion = 1,
                generatedAt = DateTimeOffset.UtcNow.ToString("o"),
                runtime = RuntimeInformation.FrameworkDescription,
                rid,
                sdk = string.Empty,
                machine = Environment.MachineName,
                cpu,
                benchmarks,
            };

            var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outPath, json);
            consoleLogger.WriteLine($"Perf run written: {outPath} ({s_accumulated.Count} benchmarks; promote -> {canonicalName})");
            return outPath;
        }
    }

    private static string FormatCpu(HostEnvironmentInfo? host)
    {
        var cpu = host?.CpuInfo.Value;
        if (cpu != null && !string.IsNullOrWhiteSpace(cpu.ProcessorName))
        {
            return cpu.ProcessorName;
        }

        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? string.Empty;
    }

    private readonly record struct BenchmarkEntry(long MeanNs, long StdDevNs, long? AllocatedBytes);

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "perf", "baselines"))
                || File.Exists(Path.Combine(dir.FullName, "TSParser.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string BuildKey(BenchmarkReport report)
    {
        var descriptor = report.BenchmarkCase.Descriptor;
        var paramParts = report.BenchmarkCase.Parameters
            .Items
            .Select(p => $"{p.Name}={p.Value}")
            .ToArray();

        return paramParts.Length > 0
            ? $"{descriptor.Type.Name}.{descriptor.WorkloadMethod.Name}({string.Join(',', paramParts)})"
            : $"{descriptor.Type.Name}.{descriptor.WorkloadMethod.Name}";
    }

    private static long ToNanoseconds(double value, TimeUnit unit)
    {
        if (unit == TimeUnit.Nanosecond)
        {
            return (long)Math.Round(value);
        }

        if (unit == TimeUnit.Microsecond)
        {
            return (long)Math.Round(value * 1_000);
        }

        if (unit == TimeUnit.Millisecond)
        {
            return (long)Math.Round(value * 1_000_000);
        }

        if (unit == TimeUnit.Second)
        {
            return (long)Math.Round(value * 1_000_000_000);
        }

        return (long)Math.Round(value);
    }
}
