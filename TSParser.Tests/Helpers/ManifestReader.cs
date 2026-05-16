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

namespace TSParser.Tests.Helpers;

public sealed class ManifestReader
{
    private static readonly Lazy<ManifestReader> s_default = new(() => new ManifestReader());

    public static ManifestReader Default => s_default.Value;

    public TablesManifest Tables { get; }
    public DescriptorsManifest Descriptors { get; }

    private ManifestReader()
    {
        Tables = LoadManifest<TablesManifest>("manifest.tables.json") ?? new TablesManifest();
        Descriptors = LoadManifest<DescriptorsManifest>("manifest.descriptors.json") ?? new DescriptorsManifest();
    }

    public IEnumerable<TableManifestEntry> EnumerateTableFixtures()
    {
        foreach (var entry in Tables.Tables.Values.OrderBy(e => e.Type, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(FixtureLoader.ResolvePath(entry.RelativePath)))
            {
                yield return entry;
            }
        }
    }

    public IEnumerable<DescriptorManifestEntry> EnumerateDescriptorFixtures()
    {
        foreach (var entry in Descriptors.Descriptors.Values.OrderBy(e => e.Group, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(FixtureLoader.ResolvePath(entry.RelativePath)))
            {
                yield return entry;
            }
        }
    }

    private static T? LoadManifest<T>(string fileName) where T : class
    {
        var path = Path.Combine(FixtureLoader.TestResourcesRoot, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, ManifestJson.Options);
    }
}
