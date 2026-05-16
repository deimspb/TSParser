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
using NUnit.Framework;
using TSParser.Descriptors;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;
using TSParser.Tables.Scte35;

namespace TSParser.Tests.Helpers;

internal static class ExpectedAssert
{
    public static void AssertTable(Table table, TableManifestEntry entry)
    {
        Assert.That(table, Is.Not.Null);
        Assert.That(table.GetType().Name, Is.EqualTo(entry.ClrType),
            () => $"CLR type mismatch for {entry.RelativePath}");
        Assert.AreEqual(HexParse.ParseUInt32(entry.Crc32), table.CRC32, nameof(table.CRC32));
        Assert.AreEqual(entry.SectionLength, table.SectionLength, nameof(table.SectionLength));
        Assert.AreEqual(HexParse.ParseByte(entry.TableId), table.TableId, nameof(table.TableId));

        AssertCommonSection(table, entry.Expected);
        AssertTypeSpecific(table, entry.Expected, entry.RelativePath);
    }

    public static void AssertDescriptor(Descriptor descriptor, DescriptorManifestEntry entry)
    {
        Assert.That(descriptor, Is.Not.Null);
        Assert.That(descriptor.GetType().Name, Is.EqualTo(entry.ClrType),
            () => $"CLR type mismatch for {entry.RelativePath}");
        Assert.AreEqual(HexParse.ParseByte(entry.Tag), descriptor.DescriptorTag, nameof(descriptor.DescriptorTag));

        if (entry.Expected.TryGetValue("descriptorLength", out var length))
        {
            Assert.AreEqual(length.GetInt32(), descriptor.DescriptorLength, nameof(descriptor.DescriptorLength));
        }

        if (entry.Expected.TryGetValue("descriptorTotalLength", out var totalLength))
        {
            Assert.AreEqual(totalLength.GetInt32(), descriptor.DescriptorTotalLength, nameof(descriptor.DescriptorTotalLength));
        }

        if (entry.Expected.TryGetValue("descriptorName", out var name))
        {
            Assert.AreEqual(name.GetString(), descriptor.DescriptorName, nameof(descriptor.DescriptorName));
        }
    }

    private static void AssertCommonSection(Table table, IReadOnlyDictionary<string, JsonElement> expected)
    {
        if (expected.TryGetValue("sectionSyntaxIndicator", out var syntax))
        {
            Assert.AreEqual(syntax.GetBoolean(), table.SectionSyntaxIndicator);
        }

        if (expected.TryGetValue("versionNumber", out var version))
        {
            Assert.AreEqual(version.GetInt32(), table.VersionNumber);
        }

        if (expected.TryGetValue("currentNextIndicator", out var currentNext))
        {
            Assert.AreEqual(currentNext.GetBoolean(), table.CurrentNextIndicator);
        }

        if (expected.TryGetValue("sectionNumber", out var sectionNumber))
        {
            Assert.AreEqual(sectionNumber.GetInt32(), table.SectionNumber);
        }

        if (expected.TryGetValue("lastSectionNumber", out var lastSectionNumber))
        {
            Assert.AreEqual(lastSectionNumber.GetInt32(), table.LastSectionNumber);
        }
    }

    private static void AssertTypeSpecific(Table table, IReadOnlyDictionary<string, JsonElement> expected, string fixturePath)
    {
        switch (table)
        {
            case PAT pat:
                AssertInt(expected, "transportStreamId", pat.TransportStreamId);
                AssertInt(expected, "patRecordsCount", pat.PatRecords.Length);
                if (pat.PatRecords.Length > 0)
                {
                    AssertInt(expected, "firstProgramNumber", pat.PatRecords[0].ProgramNumber);
                    AssertInt(expected, "firstProgramPid", pat.PatRecords[0].Pid);
                }
                break;
            case PMT pmt:
                AssertInt(expected, "programNumber", pmt.ProgramNumber);
                AssertInt(expected, "pcrPid", pmt.PcrPid);
                AssertInt(expected, "programInfoLength", pmt.ProgramInfoLength);
                AssertInt(expected, "esInfoCount", pmt.EsInfoList.Count);
                if (pmt.EsInfoList.Count > 0)
                {
                    var es = pmt.EsInfoList[0];
                    AssertInt(expected, "firstEsPid", es.ElementaryPid);
                    AssertInt(expected, "firstStreamType", es.StreamType);
                    AssertInt(expected, "firstEsDescriptorCount", es.EsDescriptorList.Count);
                }
                break;
            case CAT cat:
                AssertInt(expected, "descriptorCount", cat.CatDescriptorList?.Count ?? 0);
                break;
            case NIT nit:
                AssertInt(expected, "networkId", nit.NetworkId);
                AssertInt(expected, "transportStreamLoopCount", nit.TransportStreamLoops?.Count ?? 0);
                AssertInt(expected, "nitDescriptorCount", nit.NitDescriptorList?.Count ?? 0);
                break;
            case SDT sdt:
                AssertInt(expected, "transportStreamId", sdt.TransportStreamId);
                AssertInt(expected, "originalNetworkId", sdt.OriginalNetworkId);
                AssertInt(expected, "serviceCount", sdt.SdtItemsList?.Count ?? 0);
                break;
            case BAT bat:
                AssertInt(expected, "bouquetId", bat.BouquetId);
                AssertInt(expected, "batTsLoopCount", bat.BatTsLoopList?.Count ?? 0);
                AssertInt(expected, "batDescriptorCount", bat.BatDescriptorList?.Count ?? 0);
                break;
            case EIT eit:
                AssertInt(expected, "serviceId", eit.ServiceId);
                AssertInt(expected, "transportStreamId", eit.TransportStreamId);
                AssertInt(expected, "originalNetworkId", eit.OriginalNetworkId);
                AssertInt(expected, "eventCount", eit.EventList?.Count ?? 0);
                break;
            case TDT tdt:
                if (expected.TryGetValue("utcDateTime", out var utc))
                {
                    Assert.AreEqual(DateTime.Parse(utc.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind), tdt.UtcDateTime);
                }
                break;
            case TOT tot:
                AssertInt(expected, "descriptorCount", tot.TotDescriptors?.Count ?? 0);
                break;
            case AIT ait:
                AssertInt(expected, "applicationType", ait.ApplicationType);
                AssertInt(expected, "applicationLoopCount", ait.ApplicationLoops?.Count ?? 0);
                AssertInt(expected, "commonDescriptorCount", ait.AitDescriptorsList?.Count ?? 0);
                break;
            case MIP mip:
                if (expected.TryGetValue("synchronizationId", out var syncId))
                {
                    Assert.AreEqual(HexParse.ParseByte(syncId.GetString()!), mip.SynchronizationId);
                }
                AssertInt(expected, "txIdFunctionCount", mip.TxIdFunctions?.Count ?? 0);
                break;
            case SCTE35 scte:
                AssertInt(expected, "protocolVersion", scte.ProtocolVersion);
                AssertInt(expected, "spliceCommandLength", scte.SpliceCommandLength);
                AssertInt(expected, "descriptorLoopLength", scte.DescriptorLoopLength);
                break;
            case EWS ews:
                AssertInt(expected, "ewsRegionId", ews.EwsRegionId);
                AssertInt(expected, "zoneLoopCount", ews.ZoneLoopList?.Count ?? 0);
                AssertInt(expected, "descriptorCount", ews.EwsDescriptorList?.Count ?? 0);
                break;
            case EEWS eews:
                AssertInt(expected, "eewsGroupId", eews.EewsGroupId);
                AssertInt(expected, "deviceLoopCount", eews.DeviceLoopList?.Count ?? 0);
                AssertInt(expected, "descriptorCount", eews.EewsDescriptorList?.Count ?? 0);
                break;
            default:
                Assert.Fail($"No type-specific assertions for {table.GetType().Name} ({fixturePath})");
                break;
        }
    }

    private static void AssertInt(IReadOnlyDictionary<string, JsonElement> expected, string name, int actual)
    {
        if (!expected.TryGetValue(name, out var value))
        {
            return;
        }

        Assert.AreEqual(value.GetInt32(), actual, name);
    }
}
