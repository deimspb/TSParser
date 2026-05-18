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

namespace TSParser.Tests.T2mi;

[TestFixture]
[Explicit("Run with WRITE_T2MI_FIXTURES=1 to regenerate hex packet fixtures.")]
public sealed class T2miFixtureGeneratorTests
{
    [Test]
    public void Write_packet_hex_fixtures()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WRITE_T2MI_FIXTURES"), "1", StringComparison.Ordinal))
        {
            Assert.Ignore("Set WRITE_T2MI_FIXTURES=1 to regenerate fixtures.");
        }

        var root = Path.Combine(
            Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "TestResources")),
            "T2mi");
        Directory.CreateDirectory(root);

        File.WriteAllBytes(
            Path.Combine(root, Path.GetFileName(FixtureLoader.T2miTimestampPacketRelativePath)),
            T2miTestPacketFactory.DvbT2TimestampPacket.ToArray());

        File.WriteAllBytes(
            Path.Combine(root, Path.GetFileName(FixtureLoader.T2miBasebandPacketRelativePath)),
            T2miTestPacketFactory.BuildMinimalBasebandPacket());
    }
}
