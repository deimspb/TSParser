using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.Helpers;

internal static class PsiTsPacketFactory
{
    public static byte[] BuildSection(byte tableId, params byte[] body)
    {
        var sectionLength = body.Length;
        var section = new byte[sectionLength + 3];
        section[0] = tableId;
        section[1] = (byte)(0xB0 | ((sectionLength >> 8) & 0x0F));
        section[2] = (byte)(sectionLength & 0xFF);
        body.CopyTo(section, 3);
        return section;
    }

    public static byte[] BuildPusiPayload(byte pointerField, ReadOnlySpan<byte> prefixOrSectionData, params byte[][] extraSections)
    {
        var payload = new byte[1 + prefixOrSectionData.Length + extraSections.Sum(s => s.Length)];
        payload[0] = pointerField;
        prefixOrSectionData.CopyTo(payload.AsSpan(1));
        var offset = 1 + prefixOrSectionData.Length;
        foreach (var section in extraSections)
        {
            section.CopyTo(payload, offset);
            offset += section.Length;
        }

        return payload;
    }

    public static byte[] BuildTsPacket(
        ushort pid,
        bool payloadUnitStartIndicator,
        byte continuityCounter,
        ReadOnlySpan<byte> payload,
        bool discontinuityIndicator = false)
    {
        if (payload.Length > 184)
        {
            throw new ArgumentException("Payload must fit into one TS packet.", nameof(payload));
        }

        var packet = new byte[T2miAccessors.TsPacketSize];
        packet[0] = TsPacket.SYNC_BYTE;
        packet[1] = (byte)(((payloadUnitStartIndicator ? 0x40 : 0x00)) | ((pid >> 8) & 0x1F));
        packet[2] = (byte)(pid & 0xFF);
        var payloadOffset = 4;

        if (payload.Length < 184)
        {
            packet[3] = (byte)(0x30 | (continuityCounter & 0x0F)); // adaptation + payload
            var adaptationLength = 183 - payload.Length;
            packet[4] = (byte)adaptationLength;
            if (adaptationLength > 0)
            {
                packet[5] = discontinuityIndicator ? (byte)0x80 : (byte)0x00;
                for (var i = 6; i < 5 + adaptationLength; i++)
                {
                    packet[i] = 0xFF;
                }
            }

            payloadOffset = 5 + adaptationLength;
        }
        else
        {
            packet[3] = (byte)(0x10 | (continuityCounter & 0x0F)); // payload only
        }

        payload.CopyTo(packet.AsSpan(payloadOffset));
        for (var i = payloadOffset + payload.Length; i < packet.Length; i++)
        {
            packet[i] = 0xFF;
        }

        return packet;
    }

    public static byte[] BuildPsiTsPacket(ushort pid, ReadOnlySpan<byte> section, byte continuityCounter = 0)
    {
        if (section.Length > 183)
        {
            throw new ArgumentException("Section must fit in one TS payload with a pointer byte.", nameof(section));
        }

        var payload = BuildPusiPayload(pointerField: 0, section);
        return BuildTsPacket(pid, payloadUnitStartIndicator: true, continuityCounter, payload);
    }

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
            packets.Add(BuildTsPacket(
                pid,
                payloadUnitStartIndicator: false,
                continuityCounter++,
                section.Slice(offset, chunkLength)));
            offset += chunkLength;
        }

        var stream = new byte[packets.Count * T2miAccessors.TsPacketSize];
        for (var i = 0; i < packets.Count; i++)
        {
            packets[i].CopyTo(stream.AsSpan(i * T2miAccessors.TsPacketSize));
        }

        return stream;
    }
}
