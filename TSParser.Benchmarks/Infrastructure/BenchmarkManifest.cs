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
using System.Text.Json.Serialization;

namespace TSParser.Benchmarks.Infrastructure;

internal static class BenchmarkManifest
{
    private static readonly Lazy<TablesManifest> s_tables = new(LoadTables);
    private static readonly Lazy<DescriptorsManifest> s_descriptors = new(LoadDescriptors);

    public static TablesManifest Tables => s_tables.Value;
    public static DescriptorsManifest Descriptors => s_descriptors.Value;

    public static IEnumerable<TableManifestEntry> EnumerateTableSamples(string sampleLabel = "L") =>
        Tables.Tables.Values
            .Where(e => string.Equals(e.Sample, sampleLabel, StringComparison.OrdinalIgnoreCase))
            .Where(e => File.Exists(BenchmarkResources.ResolvePath(e.RelativePath)))
            .OrderBy(e => e.Type, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<DescriptorManifestEntry> EnumerateDescriptorFixtures() =>
        Descriptors.Descriptors.Values
            .Where(e => File.Exists(BenchmarkResources.ResolvePath(e.RelativePath)))
            .OrderBy(e => e.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<DescriptorManifestEntry> TopDescriptorGroups(int count)
    {
        var ranked = Descriptors.Descriptors.Values
            .Where(e => File.Exists(BenchmarkResources.ResolvePath(e.RelativePath)))
            .GroupBy(e => e.Group, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Group = g.Key, Count = g.Count(), Entries = g.ToList() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Group, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .ToList();

        foreach (var bucket in ranked)
        {
            var pick = bucket.Entries
                .OrderByDescending(e => e.Size)
                .First();
            yield return pick;
        }
    }

    private static TablesManifest LoadTables() =>
        Load<TablesManifest>("manifest.tables.json") ?? new TablesManifest();

    private static DescriptorsManifest LoadDescriptors() =>
        Load<DescriptorsManifest>("manifest.descriptors.json") ?? new DescriptorsManifest();

    private static T? Load<T>(string fileName) where T : class
    {
        var path = Path.Combine(BenchmarkResources.Root, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

internal sealed class TablesManifest
{
    public Dictionary<string, TableTypeStats> Types { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TableManifestEntry> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class TableTypeStats
{
    public int Samples { get; set; }
    public bool Missing { get; set; }
}

internal sealed class TableManifestEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Sample { get; set; }
    public string ClrType { get; set; } = string.Empty;
}

internal sealed class DescriptorsManifest
{
    public Dictionary<string, DescriptorManifestEntry> Descriptors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class DescriptorManifestEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? CallerTableId { get; set; }
    public int Size { get; set; }
}
