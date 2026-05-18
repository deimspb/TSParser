using NUnit.Framework;
using TSParser.Tables;
using TSParser.Tests.Helpers;
using TSParser.TransportStream;

namespace TSParser.Tests.Tables;

[TestFixture]
public sealed class PsiSectionAssemblerTests
{
    private const ushort Pid = 0x0020;

    [Test]
    public void PushPacket_CompletesPendingOnPointerAndParsesNextSection()
    {
        var sectionA = PsiTsPacketFactory.BuildSection(0x00, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15);
        var sectionB = PsiTsPacketFactory.BuildSection(0x02, 0x20, 0x21, 0x22, 0x23);
        var assembler = new PsiSectionAssembler(Pid);

        const int firstChunkLength = 7;
        var firstPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionA.AsSpan(0, firstChunkLength))));
        var firstReady = PushAndCollect(assembler, firstPacket);
        Assert.That(firstReady, Is.Empty);

        var remainingA = sectionA[firstChunkLength..];
        var secondPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 1,
            payload: PsiTsPacketFactory.BuildPusiPayload((byte)remainingA.Length, remainingA.AsSpan(), sectionB)));
        var secondReady = PushAndCollect(assembler, secondPacket);

        Assert.That(secondReady, Has.Count.EqualTo(2));
        Assert.That(secondReady[0], Is.EqualTo(sectionA));
        Assert.That(secondReady[1], Is.EqualTo(sectionB));
    }

    [Test]
    public void PushPacket_HandlesSplitHeaderAcrossPackets()
    {
        var section = PsiTsPacketFactory.BuildSection(0x42, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35);
        var assembler = new PsiSectionAssembler(Pid);

        var firstPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, section.AsSpan(0, 2))));
        var firstReady = PushAndCollect(assembler, firstPacket);
        Assert.That(firstReady, Is.Empty);

        var secondPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: false,
            continuityCounter: 1,
            payload: section.AsSpan(2).ToArray()));
        var secondReady = PushAndCollect(assembler, secondPacket);

        Assert.That(secondReady, Has.Count.EqualTo(1));
        Assert.That(secondReady[0], Is.EqualTo(section));
    }

    [Test]
    public void PushPacket_IgnoresPointerPrefixWhenNoPendingSection()
    {
        var section = PsiTsPacketFactory.BuildSection(0x4A, 0x41, 0x42, 0x43, 0x44);
        var assembler = new PsiSectionAssembler(Pid);

        var packet = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 3, new byte[] { 0x00, 0xB0, 0x01 }, section)));
        var ready = PushAndCollect(assembler, packet);

        Assert.That(ready, Has.Count.EqualTo(1));
        Assert.That(ready[0], Is.EqualTo(section));
    }

    [Test]
    public void PushPacket_ParsesTwoSectionsFromSinglePayload()
    {
        var sectionA = PsiTsPacketFactory.BuildSection(0x42, 0x20, 0x21, 0x22, 0x23);
        var sectionB = PsiTsPacketFactory.BuildSection(0x4A, 0x30, 0x31, 0x32, 0x33, 0x34);
        var assembler = new PsiSectionAssembler(Pid);

        var packet = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionA, sectionB)));
        var ready = PushAndCollect(assembler, packet);

        Assert.That(ready, Has.Count.EqualTo(2));
        Assert.That(ready[0], Is.EqualTo(sectionA));
        Assert.That(ready[1], Is.EqualTo(sectionB));
    }

    [Test]
    public void PushPacket_DropsInvalidSectionLengthAndResyncsFromNextPusi()
    {
        var assembler = new PsiSectionAssembler(Pid);
        var validSection = PsiTsPacketFactory.BuildSection(0x00, 0x10, 0x11, 0x12, 0x13);

        var invalidHeader = new byte[] { 0x00, 0xBF, 0xFE }; // section_length=4094 (>4093)
        var invalidPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, invalidHeader)));
        var invalidReady = PushAndCollect(assembler, invalidPacket);
        Assert.That(invalidReady, Is.Empty);

        var resyncPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 1,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, validSection)));
        var validReady = PushAndCollect(assembler, resyncPacket);

        Assert.That(validReady, Has.Count.EqualTo(1));
        Assert.That(validReady[0], Is.EqualTo(validSection));
    }

    [Test]
    public void PushPacket_DropsPendingSectionOnContinuityLossAndResyncsFromNextPusi()
    {
        var sectionA = PsiTsPacketFactory.BuildSection(0x00, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15);
        var sectionB = PsiTsPacketFactory.BuildSection(0x02, 0x20, 0x21, 0x22, 0x23);
        var assembler = new PsiSectionAssembler(Pid);

        var firstPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionA.AsSpan(0, 5))));
        var firstReady = PushAndCollect(assembler, firstPacket);
        Assert.That(firstReady, Is.Empty);

        // CC jumps from 0 to 2 (packet with CC=1 lost): pending section must be dropped.
        var secondPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: false,
            continuityCounter: 2,
            payload: sectionA.AsSpan(5).ToArray()));
        var secondReady = PushAndCollect(assembler, secondPacket);
        Assert.That(secondReady, Is.Empty);

        var thirdPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 3,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionB)));
        var thirdReady = PushAndCollect(assembler, thirdPacket);

        Assert.That(thirdReady, Has.Count.EqualTo(1));
        Assert.That(thirdReady[0], Is.EqualTo(sectionB));
    }

    [Test]
    public void PushPacket_DropsPendingSectionOnDiscontinuityIndicatorAndResyncs()
    {
        var sectionA = PsiTsPacketFactory.BuildSection(0x42, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35);
        var sectionB = PsiTsPacketFactory.BuildSection(0x4A, 0x41, 0x42, 0x43, 0x44);
        var assembler = new PsiSectionAssembler(Pid);

        var firstPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionA.AsSpan(0, 4))));
        var firstReady = PushAndCollect(assembler, firstPacket);
        Assert.That(firstReady, Is.Empty);

        var discontinuityPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: false,
            continuityCounter: 7,
            payload: sectionA.AsSpan(4).ToArray(),
            discontinuityIndicator: true));
        var secondReady = PushAndCollect(assembler, discontinuityPacket);
        Assert.That(secondReady, Is.Empty);

        var thirdPacket = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 8,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionB)));
        var thirdReady = PushAndCollect(assembler, thirdPacket);

        Assert.That(thirdReady, Has.Count.EqualTo(1));
        Assert.That(thirdReady[0], Is.EqualTo(sectionB));
    }

    [Test]
    public void TableFactory_KeepsIndependentAssemblerStatePerPid()
    {
        const ushort otherPid = 0x0030;
        var sectionA = PsiTsPacketFactory.BuildSection(0x00, 0x10, 0x11, 0x12, 0x13, 0x14);
        var sectionB = PsiTsPacketFactory.BuildSection(0x02, 0x20, 0x21, 0x22, 0x23, 0x24);
        var factory = new CollectingTableFactory();

        var firstA = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionA.AsSpan(0, 4))));
        var firstB = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: otherPid,
            payloadUnitStartIndicator: true,
            continuityCounter: 0,
            payload: PsiTsPacketFactory.BuildPusiPayload(pointerField: 0, sectionB.AsSpan(0, 5))));
        var secondA = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: Pid,
            payloadUnitStartIndicator: false,
            continuityCounter: 1,
            payload: sectionA.AsSpan(4).ToArray()));
        var secondB = ParsePacket(PsiTsPacketFactory.BuildTsPacket(
            pid: otherPid,
            payloadUnitStartIndicator: false,
            continuityCounter: 1,
            payload: sectionB.AsSpan(5).ToArray()));

        factory.PushTable(firstA);
        factory.PushTable(firstB);
        factory.PushTable(secondA);
        factory.PushTable(secondB);

        Assert.That(factory.Sections, Has.Count.EqualTo(2));
        Assert.That(factory.Sections[0], Is.EqualTo(sectionA));
        Assert.That(factory.Sections[1], Is.EqualTo(sectionB));
    }

    private static List<byte[]> PushAndCollect(PsiSectionAssembler assembler, TsPacket packet)
    {
        return assembler.PushPacket(packet).Select(section => section.ToArray()).ToList();
    }

    private sealed class CollectingTableFactory : TableFactory
    {
        internal List<byte[]> Sections { get; } = new();

        internal override void PushTable(TsPacket tsPacket)
        {
            ProcessAssembledSections(tsPacket);
        }

        protected override void ProcessCurrentSection()
        {
            Sections.Add(TableData.ToArray());
        }
    }

    private static TsPacket ParsePacket(byte[] packetBytes)
    {
        var packetFactory = new TsPacketFactory();
        return packetFactory.GetTsPackets(packetBytes, 188)[0];
    }
}
