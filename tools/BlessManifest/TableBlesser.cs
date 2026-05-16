using System.Buffers.Binary;
using System.Text.RegularExpressions;
using TSParser;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;

namespace BlessManifest;

internal static class TableBlesser
{
    private static readonly HashSet<string> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PAT", "CAT", "PMT", "SDT", "BAT", "NIT", "TDT", "TOT", "EIT", "AIT", "MIP", "SCTE35", "EWS", "EEWS",
    };

    private static readonly Regex SampleSuffix = new(@"_(S|M1|M2|L)\.tbl$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalFixture = new(
        @"^Tables[\\/](?<type>[A-Za-z0-9]+)[\\/]\k<type>_(?<sample>S|M1|M2|L)\.tbl$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static TablesManifest Bless(string fixturesRoot, TablesManifest? existing)
    {
        var tablesDir = Path.Combine(fixturesRoot, "Tables");
        var manifest = existing ?? new TablesManifest
        {
            Version = 1,
            GeneratedAt = DateTimeOffset.UtcNow,
            FixturesRoot = Path.GetFullPath(fixturesRoot),
        };

        manifest.FixturesRoot = Path.GetFullPath(fixturesRoot);
        manifest.BlessedAt = DateTimeOffset.UtcNow;
        manifest.Tables.Clear();
        manifest.Types.Clear();

        if (!Directory.Exists(tablesDir))
        {
            return manifest;
        }

        foreach (var file in Directory.EnumerateFiles(tablesDir, "*.tbl", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(fixturesRoot, file).Replace('\\', '/');
            if (!CanonicalFixture.IsMatch(relativePath))
            {
                Console.Error.WriteLine($"Skip non-canonical table fixture: {relativePath}");
                continue;
            }

            var existingEntry = existing?.Tables.GetValueOrDefault(relativePath);
            var bytes = File.ReadAllBytes(file);
            var type = InferTableType(file, bytes, existingEntry?.Type);
            if (type is null)
            {
                Console.Error.WriteLine($"Skip unrecognized table fixture: {relativePath}");
                continue;
            }

            var table = ParseTable(bytes, type);
            var entry = BuildEntry(relativePath, type, file, bytes, table, existingEntry);
            manifest.Tables[relativePath] = entry;
        }

        const int targetSamples = 4;
        foreach (var type in KnownTypes.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            var samples = manifest.Tables.Values.Count(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            var prior = existing?.Types.GetValueOrDefault(type);
            manifest.Types[type] = new TableTypeStats
            {
                Available = prior?.Available,
                Unique = prior?.Unique,
                Selected = prior?.Selected ?? samples,
                Samples = samples,
                Complete = samples >= targetSamples,
                Missing = samples == 0,
            };
        }

        return manifest;
    }

    private static Table ParseTable(byte[] bytes, string type)
    {
        try
        {
            return type.Equals("MIP", StringComparison.OrdinalIgnoreCase)
                ? TsParser.GetOneTableFromBytes(bytes, mip: true)
                : TsParser.GetOneTableFromBytes(bytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse {type} fixture ({bytes.Length} bytes, table_id=0x{(bytes.Length > 0 ? bytes[0] : 0):X2})", ex);
        }
    }

    private static TableManifestEntry BuildEntry(
        string relativePath,
        string type,
        string filePath,
        byte[] bytes,
        Table table,
        TableManifestEntry? existing)
    {
        var rawCrc = bytes.Length >= 4
            ? BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(bytes.Length - 4))
            : 0u;
        var effectiveCrc = table.CRC32 != 0 ? table.CRC32 : rawCrc;
        var sample = InferSample(Path.GetFileName(filePath), existing?.Sample);

        var entry = new TableManifestEntry
        {
            RelativePath = relativePath,
            Type = type,
            Sample = sample,
            Size = bytes.Length,
            Crc32 = HexFormat.UInt32(effectiveCrc),
            SectionLength = table.SectionLength,
            TableId = HexFormat.Byte(table.TableId),
            ClrType = table.GetType().Name,
            SourceTs = existing?.SourceTs,
            SourceStagingPath = existing?.SourceStagingPath,
            Expected = BuildExpected(table, bytes, effectiveCrc),
        };

        return entry;
    }

    private static Dictionary<string, object?> BuildExpected(Table table, byte[] bytes, uint effectiveCrc)
    {
        var expected = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["crc32"] = HexFormat.UInt32(effectiveCrc),
            ["sectionLength"] = table.SectionLength,
            ["sectionSyntaxIndicator"] = table.SectionSyntaxIndicator,
            ["versionNumber"] = table.VersionNumber,
            ["currentNextIndicator"] = table.CurrentNextIndicator,
            ["sectionNumber"] = table.SectionNumber,
            ["lastSectionNumber"] = table.LastSectionNumber,
        };

        switch (table)
        {
            case PAT pat:
                expected["transportStreamId"] = pat.TransportStreamId;
                expected["patRecordsCount"] = pat.PatRecords.Length;
                if (pat.PatRecords.Length > 0)
                {
                    expected["firstProgramNumber"] = pat.PatRecords[0].ProgramNumber;
                    expected["firstProgramPid"] = pat.PatRecords[0].Pid;
                }
                break;
            case PMT pmt:
                expected["programNumber"] = pmt.ProgramNumber;
                expected["pcrPid"] = pmt.PcrPid;
                expected["programInfoLength"] = pmt.ProgramInfoLength;
                expected["esInfoCount"] = pmt.EsInfoList.Count;
                if (pmt.EsInfoList.Count > 0)
                {
                    var es = pmt.EsInfoList[0];
                    expected["firstEsPid"] = es.ElementaryPid;
                    expected["firstStreamType"] = es.StreamType;
                    expected["firstEsDescriptorCount"] = es.EsDescriptorList.Count;
                }
                break;
            case CAT cat:
                expected["descriptorCount"] = cat.CatDescriptorList?.Count ?? 0;
                break;
            case NIT nit:
                expected["networkId"] = nit.NetworkId;
                expected["transportStreamLoopCount"] = nit.TransportStreamLoops?.Count ?? 0;
                expected["nitDescriptorCount"] = nit.NitDescriptorList?.Count ?? 0;
                break;
            case SDT sdt:
                expected["transportStreamId"] = sdt.TransportStreamId;
                expected["originalNetworkId"] = sdt.OriginalNetworkId;
                expected["serviceCount"] = sdt.SdtItemsList?.Count ?? 0;
                break;
            case BAT bat:
                expected["bouquetId"] = bat.BouquetId;
                expected["batTsLoopCount"] = bat.BatTsLoopList?.Count ?? 0;
                expected["batDescriptorCount"] = bat.BatDescriptorList?.Count ?? 0;
                break;
            case EIT eit:
                expected["serviceId"] = eit.ServiceId;
                expected["transportStreamId"] = eit.TransportStreamId;
                expected["originalNetworkId"] = eit.OriginalNetworkId;
                expected["eventCount"] = eit.EventList?.Count ?? 0;
                break;
            case TDT tdt:
                expected["utcDateTime"] = tdt.UtcDateTime.ToString("O");
                break;
            case TOT tot:
                expected["descriptorCount"] = tot.TotDescriptors?.Count ?? 0;
                break;
            case AIT ait:
                expected["applicationType"] = ait.ApplicationType;
                expected["applicationLoopCount"] = ait.ApplicationLoops?.Count ?? 0;
                expected["commonDescriptorCount"] = ait.AitDescriptorsList?.Count ?? 0;
                break;
            case MIP mip:
                expected["synchronizationId"] = HexFormat.Byte(mip.SynchronizationId);
                expected["txIdFunctionCount"] = mip.TxIdFunctions?.Count ?? 0;
                break;
            case SCTE35 scte:
                expected["protocolVersion"] = scte.ProtocolVersion;
                expected["spliceCommandLength"] = scte.SpliceCommandLength;
                expected["descriptorLoopLength"] = scte.DescriptorLoopLength;
                break;
            case EWS ews:
                expected["ewsRegionId"] = ews.EwsRegionId;
                expected["zoneLoopCount"] = ews.ZoneLoopList?.Count ?? 0;
                expected["descriptorCount"] = ews.EwsDescriptorList?.Count ?? 0;
                break;
            case EEWS eews:
                expected["eewsGroupId"] = eews.EewsGroupId;
                expected["deviceLoopCount"] = eews.DeviceLoopList?.Count ?? 0;
                expected["descriptorCount"] = eews.EewsDescriptorList?.Count ?? 0;
                break;
        }

        return expected;
    }

    private static string? InferTableType(string filePath, byte[] bytes, string? hint)
    {
        if (!string.IsNullOrEmpty(hint) && KnownTypes.Contains(hint))
        {
            return hint.ToUpperInvariant();
        }

        var dir = Path.GetFileName(Path.GetDirectoryName(filePath));
        if (!string.IsNullOrEmpty(dir) && KnownTypes.Contains(dir))
        {
            return dir.ToUpperInvariant();
        }

        var name = Path.GetFileName(filePath);
        var underscore = name.IndexOf('_', StringComparison.Ordinal);
        if (underscore > 0)
        {
            var prefix = name[..underscore];
            if (KnownTypes.Contains(prefix))
            {
                return prefix.ToUpperInvariant();
            }
        }

        if (bytes.Length == 0)
        {
            return null;
        }

        return bytes[0] switch
        {
            0x00 => "PAT",
            0x01 => "CAT",
            0x02 => "PMT",
            0x40 or 0x41 => "NIT",
            0x42 or 0x46 => "SDT",
            0x4A => "BAT",
            0x70 => "TDT",
            0x73 => "TOT",
            0x74 => "AIT",
            0x93 => "EWS",
            0x94 or 0x95 => "EEWS",
            0xFC => "SCTE35",
            byte n when n is >= 0x4E and <= 0x6F => "EIT",
            _ => null,
        };
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
