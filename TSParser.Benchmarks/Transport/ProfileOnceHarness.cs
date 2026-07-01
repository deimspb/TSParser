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

namespace TSParser.Benchmarks.Transport;

/// <summary>
/// Single <see cref="ParseFullTsBenchmarks.ParseFullTs_AllTables"/> invocation for CPU profiling (no BenchmarkDotNet).
/// </summary>
internal static class ProfileOnceHarness
{
    public static int Run(string[] args)
    {
        var path = ResolvePath(args);
        if (path == null)
        {
            PrintUsage();
            return 1;
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists || fileInfo.Length < 2040)
        {
            Console.Error.WriteLine($"ERROR: file missing or smaller than 2040 bytes: {path}");
            return 1;
        }

        Console.WriteLine($"PROFILE: ParseFullTs_AllTables once on {path} ({fileInfo.Length:N0} bytes)");

        var patCount = 0;
        using var parser = new TsParser(new ParserConfig
        {
            TsFileName = path,
            CurrentDecodeMode = DecodeMode.Table,
            AllowAnalyzer = true,
        });

        parser.OnPatReady += _ => patCount++;
        parser.RunParser();
        Console.WriteLine($"PROFILE: complete (PAT={patCount})");
        return 0;
    }

    private static string? ResolvePath(string[] args)
    {
        var explicitPath = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (!string.IsNullOrEmpty(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        return BenchmarkResources.TryGetPerfTsMedium();
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            """
            Single full-file parse matching ParseFullTsBenchmarks.ParseFullTs_AllTables (table mode, analyzer on).

            Usage:
              dotnet run -c Release --project TSParser.Benchmarks -p:Platform=x64 -- --profile-once
              dotnet run -c Release --project TSParser.Benchmarks -p:Platform=x64 -- --profile-once D:\path\9.ts

            Without a path, uses TSPARSER_PERF_TS_MEDIUM or corpus under TSPARSER_TS_ROOT.
            """);
    }
}
