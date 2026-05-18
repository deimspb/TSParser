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

namespace TSParser.TransportStream.T2mi;

/// <summary>Bit-level accessors for 188-byte MPEG-TS and assembled T2-MI packets (ported from sl-demux <c>t2mi.h</c>).</summary>
internal static class T2miAccessors
{
    public const int TsPacketSize = 188;
    public const int TsHeaderSize = 4;
    public const int T2miPacketHeaderSize = 6;

    public static byte TsSyncByte(ReadOnlySpan<byte> packet) => packet[0];

    public static bool TsTransportErrorIndicator(ReadOnlySpan<byte> packet) => (packet[1] & 0x80) != 0;

    public static bool TsPayloadUnitStartIndicator(ReadOnlySpan<byte> packet) => (packet[1] & 0x40) != 0;

    public static bool TsTransportPriority(ReadOnlySpan<byte> packet) => (packet[1] & 0x20) != 0;

    public static ushort TsPid(ReadOnlySpan<byte> packet) =>
        (ushort)(BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(1, 2)) & 0x1FFF);

    public static byte TsContinuityCounter(ReadOnlySpan<byte> packet) => (byte)(packet[3] & 0x0F);

    public static byte TsTransportScramblingControl(ReadOnlySpan<byte> packet) => (byte)(packet[3] >> 6);

    public static byte TsAdaptationFieldControl(ReadOnlySpan<byte> packet) => (byte)((packet[3] >> 4) & 0x03);

    public static bool TsHasPayload(ReadOnlySpan<byte> packet) => (packet[3] & 0x10) != 0;

    public static bool TsHasAdaptationField(ReadOnlySpan<byte> packet) => (packet[3] & 0x20) != 0;

    public static int TsAdaptationFieldLength(ReadOnlySpan<byte> packet) =>
        TsHasAdaptationField(packet) ? packet[4] : 0;

    public static bool TsDiscontinuityIndicator(ReadOnlySpan<byte> packet) =>
        TsAdaptationFieldLength(packet) > 0 && (packet[5] & 0x80) != 0;

    public static ReadOnlySpan<byte> TsPayload(ReadOnlySpan<byte> packet)
    {
        return TsAdaptationFieldControl(packet) switch
        {
            1 => packet.Slice(TsHeaderSize),
            3 => packet.Slice(TsHeaderSize + packet[4] + 1),
            _ => ReadOnlySpan<byte>.Empty,
        };
    }

    public static byte T2miType(ReadOnlySpan<byte> packet) => packet[0];

    public static byte T2miCount(ReadOnlySpan<byte> packet) => packet[1];

    public static byte T2miSuperframeIndex(ReadOnlySpan<byte> packet) => (byte)(packet[2] >> 4);

    public static ushort T2miRfu(ReadOnlySpan<byte> packet) =>
        (ushort)(((packet[2] << 5) + (packet[3] >> 3)) & 0x01FF);

    public static byte T2miStreamId(ReadOnlySpan<byte> packet) => (byte)(packet[3] & 0x07);

    public static ushort T2miPayloadLengthBits(ReadOnlySpan<byte> packet) =>
        BinaryPrimitives.ReadUInt16BigEndian(packet.Slice(4, 2));

    public static ReadOnlySpan<byte> T2miPayload(ReadOnlySpan<byte> packet) => packet.Slice(T2miPacketHeaderSize);

    public static int T2miPacketSizeBytes(ReadOnlySpan<byte> packet) =>
        T2miPacketHeaderSize + (T2miPayloadLengthBits(packet) + 7) / 8 + 4;

    public static uint T2miCrc32(ReadOnlySpan<byte> packet)
    {
        var payloadBytes = (T2miPayloadLengthBits(packet) + 7) / 8;
        return BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(T2miPacketHeaderSize + payloadBytes));
    }

    public static byte T2miType00PlpId(ReadOnlySpan<byte> packet) => packet[7];

    public static byte T2miType00FrameIndex(ReadOnlySpan<byte> packet) => packet[6];

    public static bool T2miType00IntlFrameStart(ReadOnlySpan<byte> packet) => (packet[8] & 0x80) != 0;

    public static byte T2miType00Rfu(ReadOnlySpan<byte> packet) => (byte)(packet[8] & 0x7F);

    public static ushort T2miType00PayloadLengthBits(ReadOnlySpan<byte> packet) =>
        (ushort)(T2miPayloadLengthBits(packet) - 24);

    public static ReadOnlySpan<byte> T2miType00Payload(ReadOnlySpan<byte> packet) =>
        T2miPayload(packet).Slice(3);
}
