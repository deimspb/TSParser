using NUnit.Framework;
using TSParser.Tables;
using TSParser.TransportStream;

namespace TSParser.Tests.Tables;

[TestFixture]
public sealed class PsiSectionAssemblerTests
{
    private const ushort Pid = 0x0020;

    [Test]
    public void PushPacket_CompletesPendingOnPointerAndParsesNextSection()
    {
        var sectionA = BuildSection(0x00, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15);
        var sectionB = BuildSection(0x02, 0x20, 0x21, 0x22, 0x23);
        var assembler = new PsiSectionAssembler(Pid);

        var firstChunkLength = 7;
        var firstPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 0,
            payload: CreatePusiPayload(pointerField: 0, sectionA.AsSpan(0, firstChunkLength))));
        var firstReady = assembler.PushPacket(firstPacket);
        Assert.That(firstReady, Is.Empty);

        var remainingA = sectionA[firstChunkLength..];
        var secondPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 1,
            payload: CreatePusiPayload(pointerField: remainingA.Length, remainingA.AsSpan(), sectionB)));
        var secondReady = assembler.PushPacket(secondPacket);

        Assert.That(secondReady, Has.Count.EqualTo(2));
        Assert.That(secondReady[0].ToArray(), Is.EqualTo(sectionA));
        Assert.That(secondReady[1].ToArray(), Is.EqualTo(sectionB));
    }

    [Test]
    public void PushPacket_HandlesSplitHeaderAcrossPackets()
    {
        var section = BuildSection(0x42, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35);
        var assembler = new PsiSectionAssembler(Pid);

        var firstPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 0,
            payload: CreatePusiPayload(pointerField: 0, section.AsSpan(0, 2))));
        var firstReady = assembler.PushPacket(firstPacket);
        Assert.That(firstReady, Is.Empty);

        var secondPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: false,
            continuityCounter: 1,
            payload: section.AsSpan(2).ToArray()));
        var secondReady = assembler.PushPacket(secondPacket);

        Assert.That(secondReady, Has.Count.EqualTo(1));
        Assert.That(secondReady[0].ToArray(), Is.EqualTo(section));
    }

    [Test]
    public void PushPacket_IgnoresPointerPrefixWhenNoPendingSection()
    {
        var section = BuildSection(0x4A, 0x41, 0x42, 0x43, 0x44);
        var assembler = new PsiSectionAssembler(Pid);

        var packet = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 0,
            payload: CreatePusiPayload(pointerField: 3, new byte[] { 0x00, 0xB0, 0x01 }, section)));
        var ready = assembler.PushPacket(packet);

        Assert.That(ready, Has.Count.EqualTo(1));
        Assert.That(ready[0].ToArray(), Is.EqualTo(section));
    }

    [Test]
    public void PushPacket_DropsPendingSectionOnContinuityLossAndResyncsFromNextPusi()
    {
        var sectionA = BuildSection(0x00, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15);
        var sectionB = BuildSection(0x02, 0x20, 0x21, 0x22, 0x23);
        var assembler = new PsiSectionAssembler(Pid);

        var firstPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 0,
            payload: CreatePusiPayload(pointerField: 0, sectionA.AsSpan(0, 5))));
        var firstReady = assembler.PushPacket(firstPacket);
        Assert.That(firstReady, Is.Empty);

        // CC jumps from 0 to 2 (packet with CC=1 lost): pending section must be dropped.
        var secondPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: false,
            continuityCounter: 2,
            payload: sectionA.AsSpan(5).ToArray()));
        var secondReady = assembler.PushPacket(secondPacket);
        Assert.That(secondReady, Is.Empty);

        var thirdPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 3,
            payload: CreatePusiPayload(pointerField: 0, sectionB)));
        var thirdReady = assembler.PushPacket(thirdPacket);

        Assert.That(thirdReady, Has.Count.EqualTo(1));
        Assert.That(thirdReady[0].ToArray(), Is.EqualTo(sectionB));
    }

    [Test]
    public void PushPacket_DropsPendingSectionOnDiscontinuityIndicatorAndResyncs()
    {
        var sectionA = BuildSection(0x42, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35);
        var sectionB = BuildSection(0x4A, 0x41, 0x42, 0x43, 0x44);
        var assembler = new PsiSectionAssembler(Pid);

        var firstPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 0,
            payload: CreatePusiPayload(pointerField: 0, sectionA.AsSpan(0, 4))));
        var firstReady = assembler.PushPacket(firstPacket);
        Assert.That(firstReady, Is.Empty);

        var discontinuityPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: false,
            continuityCounter: 7,
            payload: sectionA.AsSpan(4).ToArray(),
            discontinuityIndicator: true));
        var secondReady = assembler.PushPacket(discontinuityPacket);
        Assert.That(secondReady, Is.Empty);

        var thirdPacket = ParsePacket(BuildTsPacket(
            pid: Pid,
            pusi: true,
            continuityCounter: 8,
            payload: CreatePusiPayload(pointerField: 0, sectionB)));
        var thirdReady = assembler.PushPacket(thirdPacket);

        Assert.That(thirdReady, Has.Count.EqualTo(1));
        Assert.That(thirdReady[0].ToArray(), Is.EqualTo(sectionB));
    }

    private static byte[] BuildSection(byte tableId, params byte[] body)
    {
        var sectionLength = body.Length;
        var section = new byte[sectionLength + 3];
        section[0] = tableId;
        section[1] = (byte)(0xB0 | ((sectionLength >> 8) & 0x0F));
        section[2] = (byte)(sectionLength & 0xFF);
        body.CopyTo(section, 3);
        return section;
    }

    private static byte[] CreatePusiPayload(int pointerField, ReadOnlySpan<byte> prefixOrSectionData, params byte[][] extraSections)
    {
        var payload = new byte[1 + prefixOrSectionData.Length + extraSections.Sum(s => s.Length)];
        payload[0] = (byte)pointerField;
        prefixOrSectionData.CopyTo(payload.AsSpan(1));
        var offset = 1 + prefixOrSectionData.Length;
        foreach (var section in extraSections)
        {
            section.CopyTo(payload, offset);
            offset += section.Length;
        }

        return payload;
    }

    private static byte[] BuildTsPacket(
        ushort pid,
        bool pusi,
        byte continuityCounter,
        byte[] payload,
        bool discontinuityIndicator = false)
    {
        Assert.That(payload.Length, Is.LessThanOrEqualTo(184), "Payload must fit into one TS packet");

        var packet = new byte[188];
        packet[0] = TsPacket.SYNC_BYTE;
        packet[1] = (byte)(((pusi ? 0x40 : 0x00)) | ((pid >> 8) & 0x1F));
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

    private static TsPacket ParsePacket(byte[] packetBytes)
    {
        var packetFactory = new TsPacketFactory();
        return packetFactory.GetTsPackets(packetBytes, 188)[0];
    }
}
