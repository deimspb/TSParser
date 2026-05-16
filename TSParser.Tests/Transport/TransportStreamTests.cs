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

namespace TSParser.Tests.Transport;

[TestFixture]
public sealed class TransportStreamTests
{
    private const string AdaptationFieldPacket = "TsPackets/hb_ts6000.pkt";
    private const string PesHeaderPacket = "TsPackets/pes_hdr.pkt";

    [Test]
    public void Ts_packet_adaptation_field()
    {
        if (!FixtureExists(AdaptationFieldPacket))
        {
            Assert.Ignore($"Missing fixture {AdaptationFieldPacket}");
        }

        var packet = FixtureLoader.LoadTsPacket(AdaptationFieldPacket);

        Assert.AreEqual(false, packet.TransportErrorIndicator);
        Assert.AreEqual(false, packet.PayloadUnitStartIndicator);
        Assert.AreEqual(false, packet.TransportPriority);
        Assert.AreEqual(1670, packet.Pid);
        Assert.AreEqual(2, packet.TransportScramblingControl);
        Assert.AreEqual(3, packet.AdaptationFieldControl);
        Assert.AreEqual(8, packet.ContinuityCounter);
        Assert.AreEqual(true, packet.HasAdaptationField);
        Assert.AreEqual(true, packet.HasPayload);

        var af = packet.Adaptation_field!;
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
    public void Ts_packet_pes_header()
    {
        if (!FixtureExists(PesHeaderPacket))
        {
            Assert.Ignore($"Missing fixture {PesHeaderPacket}");
        }

        var packet = FixtureLoader.LoadTsPacket(PesHeaderPacket);

        Assert.AreEqual(false, packet.TransportErrorIndicator);
        Assert.AreEqual(true, packet.PayloadUnitStartIndicator);
        Assert.AreEqual(false, packet.TransportPriority);
        Assert.AreEqual(251, packet.Pid);
        Assert.AreEqual(0, packet.TransportScramblingControl);
        Assert.AreEqual(1, packet.AdaptationFieldControl);
        Assert.AreEqual(10, packet.ContinuityCounter);
        Assert.AreEqual(true, packet.HasPesHeader);

        var ph = packet.Pes_header!;
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

    private static bool FixtureExists(string relativePath) =>
        File.Exists(FixtureLoader.ResolvePath(relativePath));
}
