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
using TSParser.Descriptors;
using TSParser.Tables;
using TSParser.TransportStream;

namespace TSParser.Tests.Helpers;

public static class FixtureLoader
{
    public static string TestResourcesRoot
    {
        get
        {
            var corpus = Environment.GetEnvironmentVariable("TSPARSER_TEST_CORPUS");
            if (!string.IsNullOrWhiteSpace(corpus))
            {
                return Path.GetFullPath(corpus);
            }

            return Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResources");
        }
    }

    public static string ResolvePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(TestResourcesRoot, normalized);
    }

    public static byte[] LoadBytes(string relativePath)
    {
        var path = ResolvePath(relativePath);
        Assert.That(File.Exists(path), Is.True, $"Fixture not found: {path}");
        return File.ReadAllBytes(path);
    }

    public static Table LoadTable(TableManifestEntry entry)
    {
        var bytes = LoadBytes(entry.RelativePath);
        var mip = entry.Type.Equals("MIP", StringComparison.OrdinalIgnoreCase);
        return TsParser.GetOneTableFromBytes(bytes, mip);
    }

    public static Descriptor LoadDescriptor(DescriptorManifestEntry entry)
    {
        var bytes = LoadBytes(entry.RelativePath);
        var callerTableId = HexParse.ParseNullableByte(entry.CallerTableId);
        return TsParser.GetOneDescriptorFromBytes(bytes, callerTableId);
    }

    public static TsPacket LoadTsPacket(string relativePath, int packetLength = 188)
    {
        var bytes = LoadBytes(relativePath);
        var parser = new TsParser();
        return parser.GetOneTsPacketFromBytes(bytes, packetLength);
    }

    public static uint Crc32FromSectionTail(ReadOnlySpan<byte> sectionBytes)
    {
        if (sectionBytes.Length < 4)
        {
            throw new ArgumentException("Section bytes too short for CRC32.", nameof(sectionBytes));
        }

        return BinaryPrimitives.ReadUInt32BigEndian(sectionBytes[^4..]);
    }
}
