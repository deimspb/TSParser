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
using NUnit.Framework;
using TSParser.Enums;
using TSParser.Service;
using TSParser.Tables.DvbTables;
using TSParser.Tests.Helpers;
using TSParser.TransportStream;

namespace TSParser.Tests.Tables;

[TestFixture]
public sealed class SectionTableFactoryPolicyTests
{
    [Test]
    public void Single_section_factory_drops_duplicate_crc()
    {
        var parser = CreateTableParser();
        var patSections = new List<PAT>();
        parser.OnPatReady += patSections.Add;

        var section = FixtureLoader.LoadBytes("Tables/PAT/PAT_S.tbl");
        var stream = ConcatenatePsiStreams((ushort)ReservedPids.PAT, section, section);

        parser.PushBytes(stream, 188);

        Assert.That(patSections, Has.Count.EqualTo(1));
    }

    [Test]
    public void Single_section_factory_emits_version_change()
    {
        var parser = CreateTableParser();
        var patSections = new List<PAT>();
        parser.OnPatReady += patSections.Add;

        var section = FixtureLoader.LoadBytes("Tables/PAT/PAT_S.tbl");
        var changedVersion = WithVersion(section, NextVersion(section));
        var stream = ConcatenatePsiStreams((ushort)ReservedPids.PAT, section, changedVersion);

        parser.PushBytes(stream, 188);

        Assert.That(patSections, Has.Count.EqualTo(2));
        Assert.That(patSections[1].VersionNumber, Is.Not.EqualTo(patSections[0].VersionNumber));
    }

    [Test]
    public void Multi_section_factory_drops_same_key_same_version_with_new_crc()
    {
        var parser = CreateTableParser();
        var nitSections = new List<NIT>();
        parser.OnNitReady += nitSections.Add;

        var section = FixtureLoader.LoadBytes("Tables/NIT/NIT_S.tbl");
        var sameVersionDifferentCrc = WithCurrentNextToggled(section);
        var stream = ConcatenatePsiStreams((ushort)ReservedPids.NIT, section, sameVersionDifferentCrc);

        parser.PushBytes(stream, 188);

        Assert.That(nitSections, Has.Count.EqualTo(1));
    }

    private static TsParser CreateTableParser()
    {
        return new TsParser(new ParserOptions
        {
            CurrentDecodeMode = DecodeMode.Table,
        });
    }

    private static byte[] ConcatenatePsiStreams(ushort pid, params byte[][] sections)
    {
        var streams = sections.Select(section => PsiTsPacketFactory.BuildPsiTsStream(pid, section)).ToArray();
        var length = streams.Sum(stream => stream.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var stream in streams)
        {
            stream.CopyTo(result.AsSpan(offset));
            offset += stream.Length;
        }

        return result;
    }

    private static byte NextVersion(ReadOnlySpan<byte> section)
    {
        return (byte)((ReadVersion(section) + 1) & 0x1F);
    }

    private static byte ReadVersion(ReadOnlySpan<byte> section)
    {
        return (byte)((section[5] & 0x3E) >> 1);
    }

    private static byte[] WithVersion(ReadOnlySpan<byte> section, byte version)
    {
        var clone = section.ToArray();
        clone[5] = (byte)((clone[5] & 0xC1) | ((version & 0x1F) << 1));
        RewriteCrc(clone);
        return clone;
    }

    private static byte[] WithCurrentNextToggled(ReadOnlySpan<byte> section)
    {
        var clone = section.ToArray();
        clone[5] ^= 0x01;
        RewriteCrc(clone);
        return clone;
    }

    private static void RewriteCrc(byte[] section)
    {
        var crc = Utils.GetCRC32(section.AsSpan(0, section.Length - 4));
        BinaryPrimitives.WriteUInt32BigEndian(section.AsSpan(section.Length - 4), crc);
    }
}
