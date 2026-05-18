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
using TSParser.Service;
using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.T2mi;

internal static class T2miTestPacketFactory
{
    /// <summary>Known DVB-T2 timestamp T2-MI packet (22 bytes, from sl-demux sample).</summary>
    public static ReadOnlySpan<byte> DvbT2TimestampPacket => DvbT2TimestampPacketBytes;

    private static readonly byte[] DvbT2TimestampPacketBytes =
        Convert.FromHexString("20AE300000580400000000001A5F59A00061FB5495");

    public const byte SampleBasebandPlpId = 7;

    public static byte[] BuildMinimalBasebandPacket(byte plpId = SampleBasebandPlpId)
    {
        const int bbBytes = 10;
        const int payloadBytes = 3 + bbBytes;
        const ushort payloadBits = (ushort)(payloadBytes * 8);

        var packet = new byte[6 + payloadBytes + 4];
        packet[0] = (byte)T2miPacketType.BasebandFrame;
        packet[1] = 0x01;
        packet[2] = 0x20;
        packet[3] = 0x00;
        packet[4] = (byte)(payloadBits >> 8);
        packet[5] = (byte)payloadBits;
        packet[6] = 0x00;
        packet[7] = plpId;
        packet[8] = 0x00;
        for (var i = 9; i < 6 + payloadBytes; i++)
        {
            packet[i] = 0;
        }

        var crc = Utils.GetCRC32(packet.AsSpan(0, 6 + payloadBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(6 + payloadBytes), crc);
        return packet;
    }

    public static byte[] WrapInSingleTsPacket(ReadOnlySpan<byte> t2miPacket, ushort pid, byte continuityCounter = 0)
    {
        if (t2miPacket.Length > 183)
        {
            throw new ArgumentException("T2-MI packet must fit in one PUSI TS payload.", nameof(t2miPacket));
        }

        var ts = new byte[T2miAccessors.TsPacketSize];
        ts[0] = 0x47;
        ts[1] = (byte)(0x40 | ((pid >> 8) & 0x1F));
        ts[2] = (byte)(pid & 0xFF);
        ts[3] = (byte)(0x10 | (continuityCounter & 0x0F));
        ts[4] = 0x00;
        t2miPacket.CopyTo(ts.AsSpan(5));
        for (var i = 5 + t2miPacket.Length; i < ts.Length; i++)
        {
            ts[i] = 0xFF;
        }

        return ts;
    }

    /// <summary>Normal-mode BB frame carrying one 188-byte MPEG-TS packet (SYNCD=0, SYNC=0x47).</summary>
    public static byte[] BuildNormalModeBbFrame(ReadOnlySpan<byte> tsPacket)
    {
        if (tsPacket.Length != T2miAccessors.TsPacketSize || tsPacket[0] != TsPacket.SYNC_BYTE)
        {
            throw new ArgumentException("Expected a 188-byte packet starting with 0x47.", nameof(tsPacket));
        }

        const ushort uplBits = T2miAccessors.TsPacketSize * 8;
        const ushort dflBits = T2miAccessors.TsPacketSize * 8;
        var header = new byte[BbHeader.Size];
        header[0] = 0xC0;
        header[1] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), uplBits);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), dflBits);
        header[6] = TsPacket.SYNC_BYTE;
        header[7] = 0;
        header[8] = 0;
        header[9] = Utils.GetCRC8(header.AsSpan(0, 9));

        var frame = new byte[BbHeader.Size + T2miAccessors.TsPacketSize];
        header.CopyTo(frame, 0);
        frame[BbHeader.Size] = 0;
        tsPacket.Slice(1).CopyTo(frame.AsSpan(BbHeader.Size + 1));
        return frame;
    }

    public static byte[] BuildBasebandT2miPacket(byte plpId, ReadOnlySpan<byte> bbFramePayload)
    {
        var payloadBytes = 3 + bbFramePayload.Length;
        var payloadBits = (ushort)(payloadBytes * 8);
        var packet = new byte[6 + payloadBytes + 4];
        packet[0] = (byte)T2miPacketType.BasebandFrame;
        packet[1] = 0x01;
        packet[2] = 0x20;
        packet[3] = 0x00;
        packet[4] = (byte)(payloadBits >> 8);
        packet[5] = (byte)payloadBits;
        packet[6] = 0x00;
        packet[7] = plpId;
        packet[8] = 0x00;
        bbFramePayload.CopyTo(packet.AsSpan(9));

        var crc = Utils.GetCRC32(packet.AsSpan(0, 6 + payloadBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(6 + payloadBytes), crc);
        return packet;
    }

    public static byte[] WrapInContinuationTsPacket(ReadOnlySpan<byte> t2miChunk, ushort pid, byte continuityCounter)
    {
        if (t2miChunk.Length > 184)
        {
            throw new ArgumentException("Chunk must fit in TS payload.", nameof(t2miChunk));
        }

        var ts = new byte[T2miAccessors.TsPacketSize];
        ts[0] = 0x47;
        ts[1] = (byte)((pid >> 8) & 0x1F);
        ts[2] = (byte)(pid & 0xFF);
        ts[3] = (byte)(0x10 | (continuityCounter & 0x0F));
        t2miChunk.CopyTo(ts.AsSpan(4));
        for (var i = 4 + t2miChunk.Length; i < ts.Length; i++)
        {
            ts[i] = 0xFF;
        }

        return ts;
    }

    /// <summary>Builds one PUSI TS packet carrying a PSI section (pointer field 0).</summary>
    public static byte[] BuildPsiTsPacket(ushort pid, ReadOnlySpan<byte> section)
    {
        if (section.Length > 183)
        {
            throw new ArgumentException("Section must fit in one TS payload with a pointer byte.", nameof(section));
        }

        var ts = new byte[T2miAccessors.TsPacketSize];
        ts[0] = TsPacket.SYNC_BYTE;
        ts[1] = (byte)(0x40 | ((pid >> 8) & 0x1F));
        ts[2] = (byte)(pid & 0xFF);
        ts[3] = 0x10;
        ts[4] = 0x00;
        section.CopyTo(ts.AsSpan(5));
        for (var i = 5 + section.Length; i < ts.Length; i++)
        {
            ts[i] = 0xFF;
        }

        return ts;
    }

    /// <summary>Builds a PSI section split across one PUSI packet and optional continuation packets.</summary>
    public static byte[] BuildPsiTsStream(ushort pid, ReadOnlySpan<byte> section)
    {
        if (section.Length <= 183)
        {
            return BuildPsiTsPacket(pid, section);
        }

        var packets = new List<byte[]>
        {
            BuildPsiTsPacket(pid, section.Slice(0, 183)),
        };

        var offset = 183;
        byte continuityCounter = 1;
        while (offset < section.Length)
        {
            var chunkLength = Math.Min(184, section.Length - offset);
            packets.Add(BuildPsiContinuationTsPacket(pid, section.Slice(offset, chunkLength), continuityCounter++));
            offset += chunkLength;
        }

        var stream = new byte[packets.Count * T2miAccessors.TsPacketSize];
        for (var i = 0; i < packets.Count; i++)
        {
            packets[i].CopyTo(stream.AsSpan(i * T2miAccessors.TsPacketSize));
        }

        return stream;
    }

    private static byte[] BuildPsiContinuationTsPacket(ushort pid, ReadOnlySpan<byte> payload, byte continuityCounter)
    {
        if (payload.Length > 184)
        {
            throw new ArgumentException("Continuation payload must fit in one TS packet.", nameof(payload));
        }

        var ts = new byte[T2miAccessors.TsPacketSize];
        ts[0] = TsPacket.SYNC_BYTE;
        ts[1] = (byte)((pid >> 8) & 0x1F);
        ts[2] = (byte)(pid & 0xFF);
        ts[3] = (byte)(0x10 | (continuityCounter & 0x0F));
        payload.CopyTo(ts.AsSpan(4));
        for (var i = 4 + payload.Length; i < ts.Length; i++)
        {
            ts[i] = 0xFF;
        }

        return ts;
    }

    /// <summary>Outer MPEG-TS (T2-MI PID) carrying one decapsulatable baseband frame with the given inner TS packet.</summary>
    public static byte[] BuildT2miCarrierStream(
        ReadOnlySpan<byte> innerTsPacket,
        byte plpId = SampleBasebandPlpId,
        ushort t2miPid = 0x1000)
    {
        var bbFrame = BuildNormalModeBbFrame(innerTsPacket);
        var t2mi = BuildBasebandT2miPacket(plpId, bbFrame);
        return WrapT2miPacketInTsStream(t2mi, t2miPid);
    }

    /// <summary>
    /// Concatenates one outer TS packet per inner packet, each carrying a separate baseband frame on the same PLP.
    /// </summary>
    public static byte[] BuildT2miCarrierStream(
        IReadOnlyList<ReadOnlyMemory<byte>> innerTsPackets,
        byte plpId = SampleBasebandPlpId,
        ushort t2miPid = 0x1000)
    {
        if (innerTsPackets.Count == 0)
        {
            throw new ArgumentException("At least one inner TS packet is required.", nameof(innerTsPackets));
        }

        using var stream = new MemoryStream();
        foreach (var inner in innerTsPackets)
        {
            var carrier = BuildT2miCarrierStream(inner.Span, plpId, t2miPid);
            stream.Write(carrier);
        }

        return stream.ToArray();
    }

    /// <summary>Serializes a T2-MI packet into one or more MPEG-TS packets on <paramref name="pid"/>.</summary>
    public static byte[] WrapT2miPacketInTsStream(ReadOnlySpan<byte> t2miPacket, ushort pid)
    {
        const int firstPayloadCapacity = 183;
        if (t2miPacket.Length <= firstPayloadCapacity)
        {
            return WrapInSingleTsPacket(t2miPacket, pid);
        }

        var chunks = new List<byte[]>
        {
            WrapInSingleTsPacket(t2miPacket.Slice(0, firstPayloadCapacity), pid, continuityCounter: 0),
        };

        var offset = firstPayloadCapacity;
        byte continuityCounter = 1;
        while (offset < t2miPacket.Length)
        {
            var chunkLength = Math.Min(184, t2miPacket.Length - offset);
            chunks.Add(WrapInContinuationTsPacket(
                t2miPacket.Slice(offset, chunkLength),
                pid,
                continuityCounter++));
            offset += chunkLength;
        }

        var stream = new byte[chunks.Count * T2miAccessors.TsPacketSize];
        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].CopyTo(stream.AsSpan(i * T2miAccessors.TsPacketSize));
        }

        return stream;
    }
}
