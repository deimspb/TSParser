using System.Text.Json;
using System.Text.Json.Serialization;

namespace CorpusHarvester;

internal sealed class DescriptorSampleSelector
{
    private static readonly string[] SampleLabels = ["S", "M1", "M2", "L"];

    private readonly string _stagingRoot;
    private readonly string _fixturesRoot;
    private readonly int _targetSamples;

    public DescriptorSampleSelector(string stagingRoot, string fixturesRoot, int targetSamples = 4)
    {
        _stagingRoot = stagingRoot;
        _fixturesRoot = fixturesRoot;
        _targetSamples = targetSamples;
    }

    public int SelectAll(bool dryRun = false)
    {
        if (!Directory.Exists(_stagingRoot))
        {
            throw new DirectoryNotFoundException($"Staging directory not found: {_stagingRoot}");
        }

        var descriptorsOut = Path.Combine(_fixturesRoot, "Descriptors");
        var manifestPath = Path.Combine(_fixturesRoot, "manifest.descriptors.json");
        var groups = Directory.GetDirectories(_stagingRoot);
        var manifest = new DescriptorManifest
        {
            Version = 1,
            GeneratedAt = DateTimeOffset.UtcNow,
            StagingRoot = Path.GetFullPath(_stagingRoot),
            FixturesRoot = Path.GetFullPath(_fixturesRoot),
        };

        var copied = 0;
        foreach (var groupDir in groups.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var groupName = Path.GetFileName(groupDir)!;
            var descFiles = Directory.GetFiles(groupDir, "*.desc", SearchOption.TopDirectoryOnly);
            if (descFiles.Length == 0)
            {
                continue;
            }

            var unique = new Dictionary<string, StagedDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in descFiles)
            {
                var bytes = File.ReadAllBytes(file);
                if (bytes.Length < 2)
                {
                    continue;
                }

                var crc = Crc32Helper.Compute(bytes);
                var dedup = $"{crc:X8}";
                if (unique.ContainsKey(dedup))
                {
                    continue;
                }

                var caller = InferCallerTableId(groupName);
                unique[dedup] = new StagedDescriptor
                {
                    Bytes = bytes,
                    Crc32 = crc,
                    Size = bytes.Length,
                    SourcePath = file,
                    CallerTableId = caller,
                    Tag = bytes[0],
                    ExtensionTag = groupName.Contains("_ext", StringComparison.OrdinalIgnoreCase) && bytes.Length > 2
                        ? bytes[2]
                        : null,
                };
            }

            var sorted = unique.Values.OrderBy(v => v.Size).ThenBy(v => v.SourcePath, StringComparer.OrdinalIgnoreCase).ToList();
            var indices = GetSampleIndices(sorted.Count, _targetSamples);
            var labels = GetSampleLabels(indices.Length, _targetSamples);

            manifest.Types[groupName] = new DescriptorTypeStats
            {
                Available = descFiles.Length,
                Unique = sorted.Count,
                Selected = indices.Length,
                Samples = indices.Length,
            };

            if (sorted.Count == 0)
            {
                continue;
            }

            var outDir = Path.Combine(descriptorsOut, groupName);
            if (!dryRun)
            {
                Directory.CreateDirectory(outDir);
            }

            Console.WriteLine($"{groupName}: {sorted.Count} unique / {descFiles.Length} total -> {indices.Length} sample(s)");

            for (var i = 0; i < indices.Length; i++)
            {
                var entry = sorted[indices[i]];
                var label = labels[i];
                var destName = $"{groupName}_{label}.desc";
                var relativePath = $"Descriptors/{groupName}/{destName}".Replace('\\', '/');
                var destPath = Path.Combine(outDir, destName);

                if (dryRun)
                {
                    Console.WriteLine($"  [dry-run] {entry.SourcePath} -> {destPath} ({label}, {entry.Size} bytes)");
                }
                else
                {
                    File.WriteAllBytes(destPath, entry.Bytes);
                    copied++;
                }

                manifest.Descriptors[relativePath] = new DescriptorManifestEntry
                {
                    RelativePath = relativePath,
                    Group = groupName,
                    Sample = label,
                    Family = groupName.Split('_')[0],
                    Tag = $"0x{entry.Tag:X2}",
                    ExtensionTag = entry.ExtensionTag is byte ext ? $"0x{ext:X2}" : null,
                    CallerTableId = entry.CallerTableId is byte c ? $"0x{c:X2}" : null,
                    Size = entry.Size,
                    Crc32 = $"0x{entry.Crc32:X8}",
                    SourceStagingPath = entry.SourcePath,
                };
            }
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(_fixturesRoot);
            var json = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
            File.WriteAllText(manifestPath, json);
            Console.WriteLine($"Manifest: {manifestPath}");
        }

        return copied;
    }

    private static byte? InferCallerTableId(string groupName)
    {
        if (groupName.StartsWith("AIT_", StringComparison.OrdinalIgnoreCase))
        {
            return 0x74;
        }

        if (groupName.StartsWith("SCTE_", StringComparison.OrdinalIgnoreCase))
        {
            return 0xFC;
        }

        return null;
    }

    private static int[] GetSampleIndices(int count, int target)
    {
        if (count <= 0)
        {
            return [];
        }

        if (count >= target)
        {
            return new[] { 0, count / 3, (2 * count) / 3, count - 1 }.Distinct().ToArray();
        }

        return Enumerable.Range(0, count).ToArray();
    }

    private static string[] GetSampleLabels(int selectedCount, int target)
    {
        if (selectedCount <= 0)
        {
            return [];
        }

        if (selectedCount >= target)
        {
            return SampleLabels;
        }

        return SampleLabels[..selectedCount];
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class StagedDescriptor
    {
        public required byte[] Bytes { get; init; }
        public required uint Crc32 { get; init; }
        public required int Size { get; init; }
        public required string SourcePath { get; init; }
        public required byte Tag { get; init; }
        public byte? ExtensionTag { get; init; }
        public byte? CallerTableId { get; init; }
    }
}

internal sealed class DescriptorManifest
{
    public int Version { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string StagingRoot { get; set; } = string.Empty;
    public string FixturesRoot { get; set; } = string.Empty;
    public Dictionary<string, DescriptorTypeStats> Types { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DescriptorManifestEntry> Descriptors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class DescriptorTypeStats
{
    public int Available { get; set; }
    public int Unique { get; set; }
    public int Selected { get; set; }
    public int Samples { get; set; }
}

internal sealed class DescriptorManifestEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Sample { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? ExtensionTag { get; set; }
    public string? CallerTableId { get; set; }
    public int Size { get; set; }
    public string Crc32 { get; set; } = string.Empty;
    public string SourceStagingPath { get; set; } = string.Empty;
}
