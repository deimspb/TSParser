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
public sealed class T2miFixtureTests
{
    private const int PacketSize = 188;

    [Test]
    public void Bundled_cut_contains_only_t2mi_pid_packets()
    {
        var bytes = FixtureLoader.LoadBytes(FixtureLoader.T2miBundledRelativePath);
        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(PacketSize));
        Assert.That(bytes.Length % PacketSize, Is.EqualTo(0));

        var packetCount = bytes.Length / PacketSize;
        Assert.That(packetCount, Is.GreaterThanOrEqualTo(100));

        for (var i = 0; i < packetCount; i++)
        {
            var offset = i * PacketSize;
            Assert.That(bytes[offset], Is.EqualTo(0x47), $"sync at packet {i}");

            var pid = (ushort)(((bytes[offset + 1] & 0x1F) << 8) | bytes[offset + 2]);
            Assert.That(pid, Is.EqualTo(FixtureLoader.T2miSamplePid), $"PID at packet {i}");
        }
    }

    [Test]
    public void ResolveT2miSamplePath_uses_bundled_fixture_when_env_unset()
    {
        var path = FixtureLoader.ResolveT2miSamplePath();
        Assert.That(path, Does.EndWith("t2mi_cut_pid1000.ts"));
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void Full_sample_from_env_has_t2mi_pid_when_configured()
    {
        if (!FixtureLoader.TryGetT2miFullSamplePath(out var path))
        {
            Assert.Ignore($"Set {FixtureLoader.T2miSampleEnvironmentVariable} to the full t2mi_cut.ts path to run this test.");
        }

        var bytes = File.ReadAllBytes(path);
        Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(2040));

        var found = 0;
        for (var i = 0; i <= bytes.Length - PacketSize; i += PacketSize)
        {
            if (bytes[i] != 0x47)
            {
                continue;
            }

            var pid = (ushort)(((bytes[i + 1] & 0x1F) << 8) | bytes[i + 2]);
            if (pid == FixtureLoader.T2miSamplePid)
            {
                found++;
                if (found >= 10)
                {
                    break;
                }
            }
        }

        Assert.That(found, Is.GreaterThanOrEqualTo(10), $"Expected PID 0x{FixtureLoader.T2miSamplePid:X} packets in {path}");
    }
}
