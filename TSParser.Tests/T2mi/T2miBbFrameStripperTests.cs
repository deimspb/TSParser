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

using System.Buffers.Binary;
using NUnit.Framework;
using TSParser.Service;
using TSParser.Tests.Helpers;
using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.T2mi;

[TestFixture]
public sealed class T2miBbFrameStripperTests
{
    [Test]
    public void BbHeader_parses_sample_header_bytes()
    {
        var header = new byte[] { 0xC0, 0x01, 0x00, 0xD0, 0x00, 0x58, 0x47, 0x00, 0x00, 0x2A };
        var bb = new BbHeader(header);

        Assert.That(bb.TsGs, Is.EqualTo(3));
        Assert.That(bb.Upl, Is.EqualTo(0x00D0));
        Assert.That(bb.Dfl, Is.EqualTo(0x0058));
        Assert.That(bb.Sync, Is.EqualTo(0x47));
    }

    [Test]
    public void Stripper_normal_mode_emits_one_ts_packet()
    {
        var nullPacket = new byte[T2miAccessors.TsPacketSize];
        nullPacket[0] = TsPacket.SYNC_BYTE;
        nullPacket[1] = 0x1F;
        nullPacket[2] = 0xFF;
        nullPacket[3] = 0x10;

        var bbFrame = T2miTestPacketFactory.BuildNormalModeBbFrame(nullPacket);
        var stripper = new BbFrameStripper(T2miTestPacketFactory.SampleBasebandPlpId);
        byte[]? emitted = null;
        stripper.TsPacketsReady += data => emitted = data.ToArray();

        stripper.Receive(bbFrame);

        Assert.That(emitted, Is.Not.Null);
        Assert.That(emitted!.Length, Is.EqualTo(T2miAccessors.TsPacketSize));
        Assert.That(emitted[0], Is.EqualTo(TsPacket.SYNC_BYTE));
        Assert.That(emitted.AsSpan(1).ToArray(), Is.EqualTo(nullPacket.AsSpan(1).ToArray()));
    }

    [Test]
    public void Stripper_normal_mode_non_zero_syncd_completes_cached_packet()
    {
        var expected = new byte[T2miAccessors.TsPacketSize];
        expected[0] = TsPacket.SYNC_BYTE;
        expected[1] = TsPacket.SYNC_BYTE;
        for (var i = 2; i < expected.Length; i++)
        {
            expected[i] = (byte)i;
        }

        var firstFrame = BuildNormalModeBbFrameData(
            new[] { expected[0] },
            syncdBits: 0,
            sync: TsPacket.SYNC_BYTE);

        var secondData = new byte[187];
        expected.AsSpan(2, 186).CopyTo(secondData);
        secondData[^1] = 0x00;
        var secondFrame = BuildNormalModeBbFrameData(
            secondData,
            syncdBits: 186 * 8,
            sync: 0);

        var stripper = new BbFrameStripper(T2miTestPacketFactory.SampleBasebandPlpId);
        byte[]? emitted = null;
        stripper.TsPacketsReady += data => emitted = data.ToArray();

        stripper.Receive(firstFrame);

        Assert.That(emitted, Is.Null);

        stripper.Receive(secondFrame);

        Assert.That(emitted, Is.Not.Null);
        Assert.That(emitted!.Length, Is.EqualTo(T2miAccessors.TsPacketSize));
        Assert.That(emitted, Is.EqualTo(expected));
    }

    [Test]
    public void Demuxer_with_deencapsulate_wires_bb_stripper_per_plp()
    {
        var demuxer = TsParser.CreateT2miDemuxer(FixtureLoader.T2miSamplePid, deencapsulate: true);
        Assert.That(demuxer.Deencapsulate, Is.True);

        var nullPacket = new byte[T2miAccessors.TsPacketSize];
        nullPacket[0] = TsPacket.SYNC_BYTE;
        var bbFrame = T2miTestPacketFactory.BuildNormalModeBbFrame(nullPacket);
        var stripper = new BbFrameStripper(T2miTestPacketFactory.SampleBasebandPlpId);
        byte[]? plpTs = null;
        stripper.TsPacketsReady += data => plpTs = data.ToArray();

        stripper.Receive(bbFrame);

        Assert.That(plpTs, Is.Not.Null);
        Assert.That(plpTs![0], Is.EqualTo(TsPacket.SYNC_BYTE));
    }

    private static byte[] BuildNormalModeBbFrameData(
        ReadOnlySpan<byte> dataField,
        ushort syncdBits,
        byte sync)
    {
        const ushort uplBits = T2miAccessors.TsPacketSize * 8;
        var header = new byte[BbHeader.Size];
        header[0] = 0xC0;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), uplBits);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)(dataField.Length * 8));
        header[6] = sync;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(7), syncdBits);
        header[9] = Utils.GetCRC8(header.AsSpan(0, 9));

        var frame = new byte[BbHeader.Size + dataField.Length];
        header.CopyTo(frame, 0);
        dataField.CopyTo(frame.AsSpan(BbHeader.Size));

        return frame;
    }
}
