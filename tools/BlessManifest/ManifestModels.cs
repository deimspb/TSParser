using System.Text.Json.Serialization;

namespace BlessManifest;

internal sealed class TablesManifest
{
    public int Version { get; set; } = 1;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? BlessedAt { get; set; }
    public string FixturesRoot { get; set; } = string.Empty;
    public string? StagingRoot { get; set; }
    public Dictionary<string, TableTypeStats> Types { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TableManifestEntry> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class TableTypeStats
{
    public int Samples { get; set; }
}

internal sealed class TableManifestEntry
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
    public Dictionary<string, object?> Expected { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class DescriptorsManifest
{
    public int Version { get; set; } = 1;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? BlessedAt { get; set; }
    public string FixturesRoot { get; set; } = string.Empty;
    public string? StagingRoot { get; set; }
    public Dictionary<string, DescriptorTypeStats> Types { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DescriptorManifestEntry> Descriptors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class DescriptorTypeStats
{
    public int Samples { get; set; }
}

internal sealed class DescriptorManifestEntry
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
    public Dictionary<string, object?> Expected { get; set; } = new(StringComparer.Ordinal);
}

internal static class ManifestJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };
}
