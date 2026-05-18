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
using TSParser.Service;
using TSParser.Tests.Helpers;
using TSParser.TransportStream.T2mi;

namespace TSParser.Tests.T2mi;

[TestFixture]
public sealed class T2miAccessorsTests
{
    [Test]
    public void Accessors_parse_dvb_t2_timestamp_packet()
    {
        var packet = Convert.FromHexString("20AE300000580400000000001A5F59A00061FB5495");
        Assert.That(T2miAccessors.T2miType(packet), Is.EqualTo((byte)T2miPacketType.DvbT2Timestamp));
        Assert.That(T2miAccessors.T2miPacketSizeBytes(packet), Is.EqualTo(packet.Length));
        Assert.That(T2miAccessors.T2miSuperframeIndex(packet), Is.EqualTo(0x3));
        Assert.That(T2miAccessors.T2miStreamId(packet), Is.EqualTo(0));
    }

    [Test]
    public void GetCRC8_is_deterministic_for_sample_bytes()
    {
        var header = new byte[] { 0xC0, 0x01, 0x00, 0xD0, 0x00, 0x58, 0x47, 0x00, 0x00, 0x2A };
        var crc = Utils.GetCRC8(header.AsSpan(0, 9));
        Assert.That(Utils.GetCRC8(header.AsSpan(0, 9)), Is.EqualTo(crc));
    }

}
