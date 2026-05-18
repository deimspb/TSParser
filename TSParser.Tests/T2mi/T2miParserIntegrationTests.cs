// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NUnit.Framework;
using TSParser.Enums;
using TSParser.Tests.Helpers;
using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.T2mi;

[TestFixture]
public sealed class T2miParserIntegrationTests
{
    [Test]
    public void RunParser_on_bundled_fixture_emits_t2mi_packets()
    {
        var path = FixtureLoader.ResolvePath(FixtureLoader.T2miBundledRelativePath);
        var packets = new List<T2miPacket>();
        var discoveredPlps = new List<byte>();

        var parser = new TsParser(new ParserConfig
        {
            TsFileName = path,
            T2miEnabled = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });

        parser.OnT2miPacketReady += packets.Add;
        parser.OnT2miPlpDiscovered += discoveredPlps.Add;
        parser.RunParser();

        Assert.That(packets, Is.Not.Empty);
        Assert.That(packets.All(p => p.SourcePid == FixtureLoader.T2miSamplePid), Is.True);
        Assert.That(
            packets.Any(p => p.PacketType is T2miPacketType.L1Current or T2miPacketType.DvbT2Timestamp),
            Is.True);

        var baseband = packets.Where(p => p.PacketType == T2miPacketType.BasebandFrame && p.Crc32Valid).ToList();
        if (baseband.Count > 0)
        {
            Assert.That(baseband.Any(p => p.PlpId.HasValue), Is.True);
            Assert.That(discoveredPlps, Is.Not.Empty);
        }
    }

    [Test]
    public void RunParser_table_mode_still_emits_t2mi_packets()
    {
        var path = FixtureLoader.ResolvePath(FixtureLoader.T2miBundledRelativePath);
        var packets = new List<T2miPacket>();

        var parser = new TsParser(new ParserConfig
        {
            TsFileName = path,
            T2miEnabled = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Table,
        });

        parser.OnT2miPacketReady += packets.Add;
        parser.RunParser();

        Assert.That(packets, Is.Not.Empty);
    }

    [Test]
    public void PushBytes_emits_t2mi_packets_when_t2mi_enabled()
    {
        var bytes = FixtureLoader.LoadT2miSampleBytes();
        var packets = new List<T2miPacket>();

        var parser = new TsParser(new ParserConfig
        {
            T2miEnabled = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });

        parser.OnT2miPacketReady += packets.Add;
        parser.PushBytes(bytes, packetLength: 188);

        Assert.That(packets, Is.Not.Empty);
    }

    [Test]
    public void CreateT2miDemuxer_standalone_matches_parser_wiring()
    {
        var bytes = FixtureLoader.LoadT2miSampleBytes();
        var demuxer = TsParser.CreateT2miDemuxer(FixtureLoader.T2miSamplePid);
        var packets = new List<T2miPacket>();
        demuxer.PacketReady += packets.Add;

        for (var i = 0; i + 188 <= bytes.Length; i += 188)
        {
            demuxer.PushPacket(bytes.AsSpan(i, 188), FixtureLoader.T2miSamplePid, (ulong)(i / 188));
        }

        Assert.That(packets, Is.Not.Empty);
    }

    [Test]
    public void ParserConfig_T2miDeencapsulate_exposes_OnPlpTsReady()
    {
        var parser = new TsParser(new ParserConfig
        {
            T2miEnabled = true,
            T2miDeencapsulate = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
        });

        var subscribed = false;
        parser.OnPlpTsReady += (_, _) => subscribed = true;
        Assert.That(subscribed, Is.False);
        Assert.That(parser, Is.Not.Null);
    }

    [Test]
    public void RunParser_on_full_sample_emits_baseband_when_configured()
    {
        if (!FixtureLoader.TryGetT2miFullSamplePath(out var path))
        {
            Assert.Ignore($"Set {FixtureLoader.T2miSampleEnvironmentVariable} to the full t2mi_cut.ts path to run this test.");
        }

        var packets = new List<T2miPacket>();
        var parser = new TsParser(new ParserConfig
        {
            TsFileName = path,
            T2miEnabled = true,
            T2miPids = [FixtureLoader.T2miSamplePid],
            CurrentDecodeMode = DecodeMode.Packet,
        });

        parser.OnT2miPacketReady += packets.Add;
        parser.RunParser();

        Assert.That(packets, Is.Not.Empty);
        Assert.That(
            packets.Any(p => p.PacketType == T2miPacketType.BasebandFrame && p.Crc32Valid && p.PlpId.HasValue),
            Is.True,
            "Full capture should contain at least one valid baseband frame with PLP_ID");
    }
}
