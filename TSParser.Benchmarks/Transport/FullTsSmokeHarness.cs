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

using TSParser.Benchmarks.Infrastructure;
using TSParser.Enums;
using TSParser.Service;

namespace TSParser.Benchmarks.Transport;

/// <summary>
/// One-shot full-file SI smoke: table event counters and ETSI/EXCEPTION log capture.
/// Run via <c>dotnet run --project TSParser.Benchmarks -- --smoke</c> (Release recommended).
/// </summary>
internal static class FullTsSmokeHarness
{
    private const int MaxLoggedIssues = 64;

    public static int Run(string[] args)
    {
        var files = ResolveFiles(args);
        if (files.Count == 0)
        {
            PrintUsage();
            return 1;
        }

#if DEBUG
        Console.WriteLine(
            "Note: Logger.OnLogMessage is not raised in DEBUG builds; use -c Release to capture ETSI/EXCEPTION logs.");
#endif

        var anyFailed = false;
        foreach (var (label, path) in files)
        {
            anyFailed |= !RunOne(label, path);
        }

        Console.WriteLine(anyFailed ? "SMOKE: FAIL" : "SMOKE: PASS");
        return anyFailed ? 1 : 0;
    }

    private static bool RunOne(string label, string path)
    {
        var fileInfo = new FileInfo(path);
        Console.WriteLine();
        Console.WriteLine($"=== {label}: {path} ({fileInfo.Length:N0} bytes) ===");

        if (!fileInfo.Exists || fileInfo.Length < 2040)
        {
            Console.Error.WriteLine("  ERROR: file missing or smaller than 2040 bytes.");
            return false;
        }

        var counters = new SmokeCounters();
        using var parser = CreateParser(path);
        WireTableHandlers(parser, counters);

        Logger.LogHandler logHandler = msg =>
        {
            if (msg.LogStatus is not LogStatus.EXCEPTION and not LogStatus.ETSI)
            {
                return;
            }

            counters.LogIssueCount++;
            if (counters.CapturedIssues.Count < MaxLoggedIssues)
            {
                counters.CapturedIssues.Add(msg.ToString().TrimEnd());
            }
        };

        Logger.OnLogMessage += logHandler;
        try
        {
            parser.RunParser();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ERROR: RunParser failed: {ex.GetType().Name}: {ex.Message}");
            counters.UnhandledExceptions++;
        }
        finally
        {
            Logger.OnLogMessage -= logHandler;
        }

        PrintSummary(counters);
        return counters.IsSuccess;
    }

    private static TsParser CreateParser(string path) =>
        new(new ParserOptions
        {
            TsFileName = path,
            CurrentDecodeMode = DecodeMode.Table,
            AllowAnalyzer = true,
        });

    private static void WireTableHandlers(TsParser parser, SmokeCounters counters)
    {
        parser.OnPatReady += _ => counters.Pat++;
        parser.OnPmtReady += pmt =>
        {
            counters.Pmt++;
            counters.PmtPids.Add(pmt.TablePid);
        };
        parser.OnSdtReady += _ => counters.Sdt++;
        parser.OnNitReady += _ => counters.Nit++;
        parser.OnEitReady += _ => counters.Eit++;
        parser.OnTdtReady += _ => counters.Tdt++;
        parser.OnTotReady += _ => counters.Tot++;
        parser.OnScte35Ready += _ => counters.Scte35++;
        parser.OnAitReady += _ => counters.Ait++;
    }

    private static void PrintSummary(SmokeCounters counters)
    {
        Console.WriteLine("  Tables:");
        Console.WriteLine($"    PAT={counters.Pat}  PMT={counters.Pmt}  SDT={counters.Sdt}  NIT={counters.Nit}  EIT={counters.Eit}");
        Console.WriteLine($"    TDT={counters.Tdt}  TOT={counters.Tot}  SCTE-35={counters.Scte35}  AIT={counters.Ait}");
        Console.WriteLine($"    Unique PMT PIDs ({counters.PmtPids.Count}): {FormatPidList(counters.PmtPids)}");

        Console.WriteLine($"  Log (ETSI + EXCEPTION): {counters.LogIssueCount} total, showing {counters.CapturedIssues.Count}");
        foreach (var line in counters.CapturedIssues)
        {
            Console.WriteLine($"    {line}");
        }

        if (counters.UnhandledExceptions > 0)
        {
            Console.WriteLine($"  Unhandled exceptions: {counters.UnhandledExceptions}");
        }

        if (!counters.IsSuccess)
        {
            Console.WriteLine("  Result: FAIL (PAT < 1 or PMT < 1 or unhandled exception)");
        }
        else
        {
            Console.WriteLine("  Result: OK");
        }
    }

    private static string FormatPidList(HashSet<ushort> pids)
    {
        if (pids.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", pids.OrderBy(p => p).Select(p => $"0x{p:X4}"));
    }

    private static List<(string Label, string Path)> ResolveFiles(string[] args)
    {
        var explicitPaths = args
            .Where(a => !a.StartsWith('-'))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .ToList();

        if (explicitPaths.Count > 0)
        {
            return explicitPaths
                .Select(p => (Path.GetFileName(p), p))
                .ToList();
        }

        var list = new List<(string, string)>();
        var medium = BenchmarkResources.TryGetPerfTsMedium();
        if (medium != null)
        {
            list.Add(("medium", medium));
        }

        var large = BenchmarkResources.TryGetPerfTsLarge();
        if (large != null && !string.Equals(large, medium, StringComparison.OrdinalIgnoreCase))
        {
            list.Add(("large", large));
        }

        return list;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            """
            Full TS SI smoke harness (table decode mode).

            Usage:
              dotnet run -c Release --project TSParser.Benchmarks -p:Platform=x64 -- --smoke
              dotnet run -c Release --project TSParser.Benchmarks -p:Platform=x64 -- --smoke path1.ts path2.ts

            Without explicit paths, runs medium + large from:
              TSPARSER_PERF_TS_MEDIUM, TSPARSER_PERF_TS_LARGE, or corpus under TSPARSER_TS_ROOT / D:\Dvb\dvb_lib

            Example (plan corpus):
              $env:TSPARSER_PERF_TS_MEDIUM = 'D:\Dvb\dvb_lib\9.ts'
              $env:TSPARSER_PERF_TS_LARGE  = 'D:\Dvb\dvb_lib\27_5min.ts'
              dotnet run -c Release --project TSParser.Benchmarks -p:Platform=x64 -- --smoke
            """);
    }

    private sealed class SmokeCounters
    {
        public int Pat;
        public int Pmt;
        public int Sdt;
        public int Nit;
        public int Eit;
        public int Tdt;
        public int Tot;
        public int Scte35;
        public int Ait;
        public int UnhandledExceptions;
        public int LogIssueCount;
        public HashSet<ushort> PmtPids { get; } = [];
        public List<string> CapturedIssues { get; } = [];

        public bool IsSuccess => UnhandledExceptions == 0 && Pat >= 1 && Pmt >= 1;
    }
}
