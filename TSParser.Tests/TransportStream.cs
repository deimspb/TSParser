using System.IO;
using NUnit.Framework;
using TSParser.TransportStream;

namespace TSParser.Tests
{
    [TestFixture]
    public class Tests
    {
        private TsPacket GetTsPacket(string fileName)
        {
            TsParser parser = new ();
            var filePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"..\..\..\",fileName);
            byte[] bytes= File.ReadAllBytes(filePath);
            return parser.GetOneTsPacketFromBytes(bytes, 188);
        }
        [SetUp]
        public void Setup()
        {
            
        }

        [Test]
        public void Test_TSpacket_adaptationField()
        {
            var packet = GetTsPacket(@"TestResources\TsPackets\hb_ts6000.pkt");
            //ts packet header
            Assert.AreEqual(false, packet.TransportErrorIndicator);
            Assert.AreEqual(false, packet.PayloadUnitStartIndicator);
            Assert.AreEqual(false, packet.TransportPriority);
            Assert.AreEqual(1670, packet.Pid);
            Assert.AreEqual(2, packet.TransportScramblingControl);
            Assert.AreEqual(3, packet.AdaptationFieldControl);
            Assert.AreEqual(8, packet.ContinuityCounter);
            Assert.AreEqual(true, packet.HasAdaptationField);
            Assert.AreEqual(true, packet.HasPayload);
            //Adaptation field
            var af = packet.Adaptation_field;
            Assert.AreEqual(7, af.AdaptationFieldLength);
            Assert.AreEqual(false, af.DiscontinuityIndicator);
            Assert.AreEqual(false, af.RandomAccessIndicator);
            Assert.AreEqual(false, af.ElementaryStreamPriorityIndicator);
            Assert.AreEqual(true, af.PCRFlag);
            Assert.AreEqual(false, af.OPCRFlag);
            Assert.AreEqual(false, af.SplicingPointFlag);
            Assert.AreEqual(false, af.TransportPrivateDataFlag);
            Assert.AreEqual(false, af.AdaptationFieldExtensionFlag);
            Assert.AreEqual((ulong)0x1CB2976C2, af.ProgramClockReferenceBase);
            Assert.AreEqual(0xB6, af.ProgramClockReferenceExtension);
        }
        [Test]
        public void Test_TsPacket_PesHeader()
        {
            var packet = GetTsPacket(@"TestResources\TsPackets\pes_hdr.pkt");

            //tspacket
            Assert.AreEqual(false, packet.TransportErrorIndicator);
            Assert.AreEqual(true, packet.PayloadUnitStartIndicator);
            Assert.AreEqual(false, packet.TransportPriority);
            Assert.AreEqual(251, packet.Pid);
            Assert.AreEqual(0, packet.TransportScramblingControl);
            Assert.AreEqual(1, packet.AdaptationFieldControl);
            Assert.AreEqual(10, packet.ContinuityCounter);
            Assert.AreEqual(true, packet.HasPesHeader);

            //pes header
            var ph = packet.Pes_header;

            Assert.AreEqual(224, ph.StreamId);
            Assert.AreEqual(0, ph.PESPacketLength);
            Assert.AreEqual(0, ph.PESScramblingControl);
            Assert.AreEqual(false, ph.PESPriority);
            Assert.AreEqual(false, ph.DataAlignmentIndicator);
            Assert.AreEqual(false, ph.Copyright);
            Assert.AreEqual(false, ph.OriginalOrCopy);
            Assert.AreEqual(3, ph.PTSDTSFlags);
            Assert.AreEqual(false, ph.ESCRFlag);
            Assert.AreEqual(false, ph.ESRateFlag);
            Assert.AreEqual(false, ph.DSMTrickModeFlag);
            Assert.AreEqual(false, ph.AdditionalCopyInfoFlag);
            Assert.AreEqual(false, ph.PESCRCFlag);
            Assert.AreEqual(false, ph.PESExtensionFlag);
            Assert.AreEqual(10, ph.PESHeaderDataLength);
            Assert.AreEqual((ulong)0x18BEB50BB, ph.PTSHex);
            Assert.AreEqual((ulong)0x18BEB268B, ph.DTSHex);
        }
    }
}