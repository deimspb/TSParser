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
        var first = PsiTsPacketFactory.BuildTsPacket(
            FixtureLoader.T2miSamplePid, true, 0,
            PsiTsPacketFactory.BuildPusiPayload(0, t2mi.AsSpan(0, splitAt)));
        var second = PsiTsPacketFactory.BuildTsPacket(
            FixtureLoader.T2miSamplePid, false, 1, t2mi.AsSpan(splitAt));

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

    [Test]
    public void Assembler_emits_multiple_packets_from_one_payload_and_ignores_stuffing()
    {
        var first = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        var second = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        second[1]++;
        var crc = TSParser.Service.Utils.GetCRC32(second.AsSpan(0, second.Length - 4));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(second.AsSpan(second.Length - 4), crc);
        var payload = new byte[1 + first.Length + second.Length];
        first.CopyTo(payload, 1);
        second.CopyTo(payload, 1 + first.Length);
        var ts = PsiTsPacketFactory.BuildTsPacket(FixtureLoader.T2miSamplePid, true, 0, payload);

        var packets = new List<T2miPacket>();
        var assembler = new T2miPacketAssembler();
        assembler.PacketReady += packets.Add;
        assembler.PushPacket(ts);

        Assert.That(packets.Select(p => p.PacketCount), Is.EqualTo(new byte[] { first[1], second[1] }));
        Assert.That(packets.All(p => p.Crc32Valid), Is.True);
    }

    [Test]
    public void Assembler_pointer_prefix_finishes_pending_then_emits_next_packet()
    {
        var first = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        var second = T2miTestPacketFactory.BuildMinimalBasebandPacket();
        const int split = 9;
        var start = PsiTsPacketFactory.BuildTsPacket(
            FixtureLoader.T2miSamplePid, true, 0,
            PsiTsPacketFactory.BuildPusiPayload(0, first.AsSpan(0, split)));
        var remainder = first.AsSpan(split).ToArray();
        var next = PsiTsPacketFactory.BuildTsPacket(
            FixtureLoader.T2miSamplePid, true, 1,
            PsiTsPacketFactory.BuildPusiPayload((byte)remainder.Length, remainder, second));

        var packets = new List<T2miPacket>();
        var assembler = new T2miPacketAssembler();
        assembler.PacketReady += packets.Add;
        assembler.PushPacket(start);
        assembler.PushPacket(next);

        Assert.That(packets.Select(p => p.PacketType), Is.EqualTo(new[] { T2miPacketType.DvbT2Timestamp, T2miPacketType.BasebandFrame }));
    }

    [Test]
    public void Assembler_non_pusi_continuation_can_finish_one_and_start_another_packet()
    {
        var first = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        var second = T2miTestPacketFactory.BuildMinimalBasebandPacket();
        const int split = 8;
        var start = PsiTsPacketFactory.BuildTsPacket(FixtureLoader.T2miSamplePid, true, 0,
            PsiTsPacketFactory.BuildPusiPayload(0, first.AsSpan(0, split)));
        var continuationPayload = first.AsSpan(split).ToArray().Concat(second).ToArray();
        var continuation = PsiTsPacketFactory.BuildTsPacket(
            FixtureLoader.T2miSamplePid, false, 1, continuationPayload);
        var packets = new List<T2miPacket>();
        var assembler = new T2miPacketAssembler();
        assembler.PacketReady += packets.Add;

        assembler.PushPacket(start);
        assembler.PushPacket(continuation);

        Assert.That(packets.Select(p => p.PacketType), Is.EqualTo(new[] { T2miPacketType.DvbT2Timestamp, T2miPacketType.BasebandFrame }));
    }

    [Test]
    public void Assembler_identical_same_cc_duplicate_is_ignored()
    {
        var packet = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        var ts = T2miTestPacketFactory.WrapInSingleTsPacket(packet, FixtureLoader.T2miSamplePid, 4);
        var packets = new List<T2miPacket>();
        var assembler = new T2miPacketAssembler();
        assembler.PacketReady += packets.Add;

        assembler.PushPacket(ts);
        assembler.PushPacket(ts);

        Assert.That(packets, Has.Count.EqualTo(1));
    }

    [Test]
    public void Demuxer_invalid_baseband_crc_only_raises_packet_ready()
    {
        var packet = T2miTestPacketFactory.BuildMinimalBasebandPacket();
        packet[^1] ^= 0x01;
        var ts = T2miTestPacketFactory.WrapInSingleTsPacket(packet, FixtureLoader.T2miSamplePid);
        var demuxer = new T2miDemuxer(FixtureLoader.T2miSamplePid, deencapsulate: true);
        var packets = new List<T2miPacket>();
        var plps = new List<byte>();
        var inner = new List<byte[]>();
        demuxer.PacketReady += packets.Add;
        demuxer.PlpDiscovered += plps.Add;
        demuxer.PlpTsReady += (_, data) => inner.Add(data.ToArray());

        demuxer.PushPacket(ts);

        Assert.That(packets, Has.Count.EqualTo(1));
        Assert.That(packets[0].Crc32Valid, Is.False);
        Assert.That(plps, Is.Empty);
        Assert.That(inner, Is.Empty);
    }

    [Test]
    public void Assembler_invalid_non_baseband_crc_is_reported()
    {
        var packet = T2miTestPacketFactory.DvbT2TimestampPacket.ToArray();
        packet[^1] ^= 0x01;
        var ts = T2miTestPacketFactory.WrapInSingleTsPacket(packet, FixtureLoader.T2miSamplePid);
        T2miPacket? received = null;
        var assembler = new T2miPacketAssembler();
        assembler.PacketReady += value => received = value;

        assembler.PushPacket(ts);

        Assert.That(received, Is.Not.Null);
        Assert.That(received!.Crc32Valid, Is.False);
    }
}
