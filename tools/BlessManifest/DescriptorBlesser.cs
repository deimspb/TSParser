using System.Text.RegularExpressions;
using TSParser;
using TSParser.Descriptors;

namespace BlessManifest;

internal static class DescriptorBlesser
{
    private static readonly Regex SampleSuffix = new(@"_(S|M1|M2|L)\.desc$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static DescriptorsManifest Bless(string fixturesRoot, DescriptorsManifest? existing)
    {
        var descriptorsDir = Path.Combine(fixturesRoot, "Descriptors");
        var manifest = existing ?? new DescriptorsManifest
        {
            Version = 1,
            GeneratedAt = DateTimeOffset.UtcNow,
            FixturesRoot = Path.GetFullPath(fixturesRoot),
        };

        manifest.FixturesRoot = Path.GetFullPath(fixturesRoot);
        manifest.BlessedAt = DateTimeOffset.UtcNow;
        manifest.Descriptors.Clear();
        manifest.Types.Clear();

        if (!Directory.Exists(descriptorsDir))
        {
            return manifest;
        }

        foreach (var file in Directory.EnumerateFiles(descriptorsDir, "*.desc", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(fixturesRoot, file).Replace('\\', '/');
            var existingEntry = existing?.Descriptors.GetValueOrDefault(relativePath);
            var bytes = File.ReadAllBytes(file);
            var group = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
            var callerTableId = ParseCallerTableId(existingEntry?.CallerTableId, group);
            Descriptor descriptor;
            try
            {
                descriptor = TsParser.GetOneDescriptorFromBytes(bytes, callerTableId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse descriptor fixture {relativePath} (tag=0x{(bytes.Length > 0 ? bytes[0] : 0):X2})", ex);
            }

            manifest.Descriptors[relativePath] = BuildEntry(relativePath, group, file, bytes, descriptor, existingEntry, callerTableId);
        }

        foreach (var group in manifest.Descriptors.Values.GroupBy(d => d.Group, StringComparer.OrdinalIgnoreCase))
        {
            manifest.Types[group.Key] = new DescriptorTypeStats { Samples = group.Count() };
        }

        return manifest;
    }

    private static DescriptorManifestEntry BuildEntry(
        string relativePath,
        string group,
        string filePath,
        byte[] bytes,
        Descriptor descriptor,
        DescriptorManifestEntry? existing,
        byte? callerTableId)
    {
        var crc = Crc32Helper.Compute(bytes.AsSpan());
        var family = group.Split('_')[0];
        var tag = bytes[0];
        byte? extensionTag = group.Contains("_ext", StringComparison.OrdinalIgnoreCase) && bytes.Length > 2
            ? bytes[2]
            : null;

        return new DescriptorManifestEntry
        {
            RelativePath = relativePath,
            Group = group,
            Sample = InferSample(Path.GetFileName(filePath), existing?.Sample),
            Family = family,
            Tag = HexFormat.Byte(tag),
            ExtensionTag = extensionTag is byte ext ? HexFormat.Byte(ext) : existing?.ExtensionTag,
            CallerTableId = callerTableId is byte c ? HexFormat.Byte(c) : existing?.CallerTableId,
            Size = bytes.Length,
            Crc32 = HexFormat.UInt32(crc),
            ClrType = descriptor.GetType().Name,
            SourceStagingPath = existing?.SourceStagingPath,
            Expected = BuildExpected(descriptor),
        };
    }

    private static Dictionary<string, object?> BuildExpected(Descriptor descriptor)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["clrType"] = descriptor.GetType().Name,
            ["tag"] = HexFormat.Byte(descriptor.DescriptorTag),
            ["descriptorLength"] = descriptor.DescriptorLength,
            ["descriptorTotalLength"] = descriptor.DescriptorTotalLength,
            ["descriptorName"] = descriptor.DescriptorName,
        };
    }

    private static byte? ParseCallerTableId(string? manifestValue, string groupName)
    {
        if (!string.IsNullOrEmpty(manifestValue))
        {
            var hex = manifestValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? manifestValue[2..]
                : manifestValue;
            if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
            {
                return parsed;
            }
        }

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

    private static string? InferSample(string fileName, string? hint)
    {
        if (!string.IsNullOrEmpty(hint))
        {
            return hint;
        }

        var match = SampleSuffix.Match(fileName);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }
}
