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

namespace TSParser.Tests.Transport;

[TestFixture]
public sealed class TransportStreamValidationTests
{
    [Test]
    public void GetOneTsPacketFromBytes_rejects_non_standard_buffer_sizes()
    {
        var parser = new TsParser();
        var bytes = new byte[100];

        var ex = Assert.Throws<Exception>(() => parser.GetOneTsPacketFromBytes(bytes, 188));
        Assert.That(ex!.Message, Does.Contain("188 or 204"));
    }

    [Test]
    public void GetOneTsPacketFromBytes_rejects_packet_length_mismatch()
    {
        var parser = new TsParser();
        var bytes = new byte[188];
        bytes[0] = 0x47;

        var ex = Assert.Throws<Exception>(() => parser.GetOneTsPacketFromBytes(bytes, 204));
        Assert.That(ex!.Message, Does.Contain("Not equal"));
    }
}
