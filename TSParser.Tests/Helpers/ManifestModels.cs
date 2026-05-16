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

namespace TSParser.Tests.Helpers;

public sealed class TablesManifest
{
    public int Version { get; set; } = 1;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? BlessedAt { get; set; }
    public string FixturesRoot { get; set; } = string.Empty;
    public string? StagingRoot { get; set; }
    public Dictionary<string, TableTypeStats> Types { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TableManifestEntry> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TableTypeStats
{
    public int? Available { get; set; }
    public int? Unique { get; set; }
    public int? Selected { get; set; }
    public int Samples { get; set; }
    public bool Complete { get; set; }
    public bool Missing { get; set; }
}

public sealed class TableManifestEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Sample { get; set; }
    public int Size { get; set; }
    public string Crc32 { get; set; } = string.Empty;
    public int SectionLength { get; set; }
    public string TableId { get; set; } = string.Empty;
    public string ClrType { get; set; } = string.Empty;
    public string? SourceTs { get; set; }
    public string? SourceStagingPath { get; set; }
    public Dictionary<string, JsonElement> Expected { get; set; } = new(StringComparer.Ordinal);
}

public sealed class DescriptorsManifest
{
    public int Version { get; set; } = 1;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? BlessedAt { get; set; }
    public string FixturesRoot { get; set; } = string.Empty;
    public string? StagingRoot { get; set; }
    public Dictionary<string, DescriptorTypeStats> Types { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DescriptorManifestEntry> Descriptors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DescriptorTypeStats
{
    public int Samples { get; set; }
}

public sealed class DescriptorManifestEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? Sample { get; set; }
    public string Family { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? ExtensionTag { get; set; }
    public string? CallerTableId { get; set; }
    public int Size { get; set; }
    public string Crc32 { get; set; } = string.Empty;
    public string ClrType { get; set; } = string.Empty;
    public string? SourceStagingPath { get; set; }
    public Dictionary<string, JsonElement> Expected { get; set; } = new(StringComparer.Ordinal);
}

internal static class ManifestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
