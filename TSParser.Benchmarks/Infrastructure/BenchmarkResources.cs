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

using TSParser.Tables;

namespace TSParser.Benchmarks.Infrastructure;

internal static class BenchmarkResources
{
    public static readonly string[] SupportedTableTypes =
    [
        "PAT", "CAT", "PMT", "NIT", "SDT", "BAT", "EIT", "TDT", "TOT", "AIT", "MIP", "SCTE35", "EWS", "EEWS",
    ];

    public static string Root
    {
        get
        {
            var corpus = Environment.GetEnvironmentVariable("TSPARSER_TEST_CORPUS");
            if (!string.IsNullOrWhiteSpace(corpus))
            {
                return Path.GetFullPath(corpus);
            }

            return Path.Combine(AppContext.BaseDirectory, "TestResources");
        }
    }

    public static string ResolvePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Root, normalized);
    }

    public static byte[] LoadBytes(string relativePath) => File.ReadAllBytes(ResolvePath(relativePath));

    public static bool UsesMipLoader(string tableType) =>
        tableType.Equals("MIP", StringComparison.OrdinalIgnoreCase);

    public static Table LoadTable(TableManifestEntry entry)
    {
        var bytes = LoadBytes(entry.RelativePath);
        return TsParser.GetOneTableFromBytes(bytes, UsesMipLoader(entry.Type));
    }

    public static byte[]? TryGetTableSampleBytes(string tableType, string sample = "L")
    {
        var entry = BenchmarkManifest.Tables.Tables.Values.FirstOrDefault(e =>
            e.Type.Equals(tableType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Sample, sample, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            return null;
        }

        var path = ResolvePath(entry.RelativePath);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public static byte? ParseCallerTableId(string? hex) =>
        string.IsNullOrWhiteSpace(hex) ? null : Convert.ToByte(hex, 16);

    public static string? TryGetPerfTsMedium()
    {
        var explicitPath = Environment.GetEnvironmentVariable("TSPARSER_PERF_TS_MEDIUM");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        return PickTsFile(preferLargest: false);
    }

    public static string? TryGetPerfTsLarge()
    {
        var explicitPath = Environment.GetEnvironmentVariable("TSPARSER_PERF_TS_LARGE");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        return PickTsFile(preferLargest: true);
    }

    private static string? PickTsFile(bool preferLargest)
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("TSPARSER_PERF_TS"),
            Environment.GetEnvironmentVariable("TSPARSER_TS_ROOT"),
            @"D:\Dvb\dvb_lib",
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            if (File.Exists(root) && root.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(root);
            }

            if (!Directory.Exists(root))
            {
                continue;
            }

            var files = Directory.EnumerateFiles(root, "*.ts", SearchOption.TopDirectoryOnly)
                .Select(p => new FileInfo(p))
                .Where(f => f.Length > 2048)
                .ToList();

            if (files.Count == 0)
            {
                continue;
            }

            var pick = preferLargest
                ? files.OrderByDescending(f => f.Length).First()
                : files.OrderBy(f => f.Length).Skip(files.Count / 2).FirstOrDefault() ?? files[0];

            return pick.FullName;
        }

        return null;
    }

    public static byte[] BuildSyntheticDescriptorLoop(int targetBytes = 4096)
    {
        var chunks = BenchmarkManifest.EnumerateDescriptorFixtures()
            .Take(32)
            .Select(e => LoadBytes(e.RelativePath))
            .Where(b => b.Length >= 2)
            .ToList();

        if (chunks.Count == 0)
        {
            return [0x48, 0x00];
        }

        using var ms = new MemoryStream(targetBytes + 256);
        while (ms.Length < targetBytes)
        {
            foreach (var chunk in chunks)
            {
                ms.Write(chunk);
                if (ms.Length >= targetBytes)
                {
                    break;
                }
            }
        }

        return ms.ToArray();
    }
}
