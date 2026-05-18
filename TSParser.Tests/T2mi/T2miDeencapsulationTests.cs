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

using NUnit.Framework;
using TSParser.Enums;
using TSParser.Tables.DvbTables;
using TSParser.Tests.Helpers;
using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.T2mi;

[TestFixture]
public sealed class T2miDeencapsulationTests
{
    private const string PatSmallFixture = "Tables/PAT/PAT_S.tbl";

    [Test]
    public void Deencapsulated_inner_ts_packets_have_sync_byte_every_188_bytes()
    {
        var patSection = FixtureLoader.LoadBytes(PatSmallFixture);
        var innerPat = T2miTestPacketFactory.BuildPsiTsPacket(0, patSection);
        var outer = T2miTestPacketFactory.BuildT2miCarrierStream(innerPat);

        var plpData = CollectPlpTsViaPushBytes(outer);

        Assert.That(plpData, Has.Length.GreaterThanOrEqualTo(T2miAccessors.TsPacketSize));
        Assert.That(plpData[0], Is.EqualTo(TsPacket.SYNC_BYTE));
        AssertSyncBytesEveryPacket(plpData);
    }

    [Test]
    public void Nested_ts_parser_on_plp_ts_ready_parses_pat()
    {
        var patSection = FixtureLoader.LoadBytes(PatSmallFixture);
        var innerPat = T2miTestPacketFactory.BuildPsiTsPacket(0, patSection);
        var outer = T2miTestPacketFactory.BuildT2miCarrierStream(innerPat);

        var plpData = CollectPlpTsViaPushBytes(outer);
        Assert.That(plpData, Has.Length.GreaterThanOrEqualTo(T2miAccessors.TsPacketSize));

        PAT? pat = null;
        var inner = new TsParser(new ParserConfig
        {
            CurrentDecodeMode = DecodeMode.Table,
        });
        inner.OnPatReady += table => pat = table;
        inner.PushBytes(plpData, T2miAccessors.TsPacketSize);

        Assert.That(pat, Is.Not.Null);
        Assert.That(pat!.TableId, Is.EqualTo(0x00));
        Assert.That(pat.TransportStreamId, Is.EqualTo(2));
        Assert.That(pat.VersionNumber, Is.EqualTo(3));
    }

    [Test]
    public void RunParser_with_deencapsulate_on_t2mi_sample_emits_valid_inner_ts_when_baseband_present()
    {
        var path = FixtureLoader.ResolveT2miSamplePath();
        var plpData = CollectPlpTsViaRunParser(path);

        if (plpData.Length == 0)
        {
            Assert.Ignore("Sample has no decapsulatable baseband BB frames with DFL > 0.");
        }

        Assert.That(plpData.Length % T2miAccessors.TsPacketSize, Is.EqualTo(0));
        Assert.That(plpData[0], Is.EqualTo(TsPacket.SYNC_BYTE));
        AssertSyncBytesEveryPacket(plpData);
    }

    [Test]
    public void RunParser_on_full_sample_deencapsulates_valid_inner_ts_when_configured()
    {
        if (!FixtureLoader.TryGetT2miFullSamplePath(out var path))
        {
            Assert.Ignore($"Set {FixtureLoader.T2miSampleEnvironmentVariable} to the full t2mi_cut.ts path to run this test.");
        }

        var plpData = CollectPlpTsViaRunParser(path);
        if (plpData.Length == 0)
        {
            Assert.Ignore("Full capture has no decapsulatable baseband BB frames with DFL > 0.");
        }

        Assert.That(plpData.Length, Is.GreaterThanOrEqualTo(T2miAccessors.TsPacketSize));
        AssertSyncBytesEveryPacket(plpData);
    }

    private static byte[] CollectPlpTsViaPushBytes(byte[] outerStream)
    {
        var plpData = new List<byte>();
        var parser = new TsParser(new ParserConfig
        {
            T2miEnabled = true,
            T2miDeencapsulate = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });

        parser.OnPlpTsReady += (t2miPid, _, data) =>
        {
            Assert.That(t2miPid, Is.EqualTo(FixtureLoader.T2miSamplePid));
            plpData.AddRange(data.ToArray());
        };
        parser.PushBytes(outerStream, T2miAccessors.TsPacketSize);
        return plpData.ToArray();
    }

    private static byte[] CollectPlpTsViaRunParser(string tsPath)
    {
        var plpData = new List<byte>();
        var parser = new TsParser(new ParserConfig
        {
            TsFileName = tsPath,
            T2miEnabled = true,
            T2miDeencapsulate = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });

        parser.OnPlpTsReady += (t2miPid, _, data) =>
        {
            Assert.That(t2miPid, Is.EqualTo(FixtureLoader.T2miSamplePid));
            plpData.AddRange(data.ToArray());
        };
        parser.RunParser();
        return plpData.ToArray();
    }

    private static void AssertSyncBytesEveryPacket(ReadOnlySpan<byte> tsData)
    {
        Assert.That(tsData.Length % T2miAccessors.TsPacketSize, Is.EqualTo(0),
            "PLP TS buffer length must be a multiple of 188 bytes.");

        for (var offset = 0; offset < tsData.Length; offset += T2miAccessors.TsPacketSize)
        {
            Assert.That(tsData[offset], Is.EqualTo(TsPacket.SYNC_BYTE),
                $"Expected 0x47 at packet offset {offset}.");
        }
    }
}
