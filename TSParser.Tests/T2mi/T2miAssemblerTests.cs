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
using TSParser.Tests.Helpers;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.T2mi;

[TestFixture]
public sealed class T2miAssemblerTests
{
    [Test]
    public void Assembler_single_ts_hex_emits_dvb_t2_timestamp_packet()
    {
        var t2mi = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        var ts = T2miTestPacketFactory.WrapInSingleTsPacket(t2mi, FixtureLoader.T2miSamplePid);

        var assembler = new T2miPacketAssembler();
        T2miPacket? completed = null;
        assembler.PacketReady += p => completed = p;

        assembler.PushPacket(ts, FixtureLoader.T2miSamplePid, 42);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.PacketType, Is.EqualTo(T2miPacketType.DvbT2Timestamp));
        Assert.That(completed.Crc32Valid, Is.True);
        Assert.That(completed.SuperframeIndex, Is.EqualTo(0x3));
        Assert.That(completed.StreamId, Is.EqualTo(0));
        Assert.That(completed.SourcePid, Is.EqualTo(FixtureLoader.T2miSamplePid));
        Assert.That(completed.PacketNumber, Is.EqualTo(42));
        Assert.That(completed.Payload.Length, Is.EqualTo(11));
    }

    [Test]
    public void Assembler_hex_fixture_file_matches_timestamp_packet()
    {
        var fromFile = FixtureLoader.LoadBytes(FixtureLoader.T2miTimestampPacketRelativePath);
        var fromHex = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();

        Assert.That(fromFile, Is.EqualTo(fromHex));
        Assert.That(T2miAccessors.T2miPacketSizeBytes(fromFile), Is.EqualTo(fromFile.Length));
    }

    [Test]
    public void Assembler_single_ts_hex_emits_baseband_frame_with_plp()
    {
        var t2mi = FixtureLoader.LoadBytes(FixtureLoader.T2miBasebandPacketRelativePath);
        var ts = T2miTestPacketFactory.WrapInSingleTsPacket(t2mi, FixtureLoader.T2miSamplePid);

        var assembler = new T2miPacketAssembler();
        T2miPacket? completed = null;
        assembler.PacketReady += p => completed = p;

        assembler.PushPacket(ts, FixtureLoader.T2miSamplePid);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.PacketType, Is.EqualTo(T2miPacketType.BasebandFrame));
        Assert.That(completed.Crc32Valid, Is.True);
        Assert.That(completed.PlpId, Is.EqualTo(T2miTestPacketFactory.SampleBasebandPlpId));
        Assert.That(completed.FrameIndex, Is.EqualTo(0));
        Assert.That(completed.Payload.Length, Is.GreaterThanOrEqualTo(10));
    }

    [Test]
    public void Assembler_multipart_ts_hex_reassembles_timestamp_packet()
    {
        var t2mi = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        var splitAt = 10;
        var first = T2miTestPacketFactory.WrapInSingleTsPacket(t2mi.AsSpan(0, splitAt), FixtureLoader.T2miSamplePid, continuityCounter: 0);
        var second = T2miTestPacketFactory.WrapInContinuationTsPacket(
            t2mi.AsSpan(splitAt),
            FixtureLoader.T2miSamplePid,
            continuityCounter: 1);

        var assembler = new T2miPacketAssembler();
        var packets = new List<T2miPacket>();
        assembler.PacketReady += packets.Add;

        assembler.PushPacket(first, FixtureLoader.T2miSamplePid, 0);
        assembler.PushPacket(second, FixtureLoader.T2miSamplePid, 1);

        Assert.That(packets, Has.Count.EqualTo(1));
        Assert.That(packets[0].PacketType, Is.EqualTo(T2miPacketType.DvbT2Timestamp));
        Assert.That(packets[0].Crc32Valid, Is.True);
    }

    [Test]
    public void Assembler_on_bundled_fixture_emits_t2mi_packets()
    {
        var bytes = FixtureLoader.LoadT2miSampleBytes();
        var assembler = new T2miPacketAssembler();
        var packets = new List<T2miPacket>();
        assembler.PacketReady += packets.Add;

        for (var i = 0; i + T2miAccessors.TsPacketSize <= bytes.Length; i += T2miAccessors.TsPacketSize)
        {
            assembler.PushPacket(bytes.AsSpan(i, T2miAccessors.TsPacketSize), FixtureLoader.T2miSamplePid, (ulong)(i / T2miAccessors.TsPacketSize));
        }

        Assert.That(packets, Is.Not.Empty);
        Assert.That(packets.All(p => p.SourcePid == FixtureLoader.T2miSamplePid), Is.True);
        Assert.That(
            packets.Any(p => p.PacketType is T2miPacketType.L1Current or T2miPacketType.DvbT2Timestamp or T2miPacketType.FefPartNull),
            Is.True);
    }
}
