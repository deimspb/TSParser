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
    /// <summary>Relative path to the bundled T2-MI cut (PID 0x1000 packets only).</summary>
    public const string T2miBundledRelativePath = "T2mi/t2mi_cut_pid1000.ts";

    /// <summary>Single assembled DVB-T2 timestamp T2-MI packet (hex regression fixture).</summary>
    public const string T2miTimestampPacketRelativePath = "T2mi/t2mi_dvb_t2_timestamp.packet";

    /// <summary>Minimal valid baseband T2-MI packet with PLP_ID 7 (hex regression fixture).</summary>
    public const string T2miBasebandPacketRelativePath = "T2mi/t2mi_baseband_plp07.packet";

    /// <summary>T2-MI PID in <see cref="T2miBundledRelativePath"/> and <c>t2mi_cut.ts</c> samples.</summary>
    public const ushort T2miSamplePid = 0x1000;

    /// <summary>Environment variable for the full <c>t2mi_cut.ts</c> capture (optional, local/extended tests).</summary>
    public const string T2miSampleEnvironmentVariable = "TSPARSER_T2MI_SAMPLE";

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

    /// <summary>
    /// Path to the T2-MI TS sample: <c>TSPARSER_T2MI_SAMPLE</c> when set, otherwise the bundled cut.
    /// </summary>
    public static string ResolveT2miSamplePath()
    {
        var sample = Environment.GetEnvironmentVariable(T2miSampleEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(sample))
        {
            return Path.GetFullPath(sample);
        }

        return ResolvePath(T2miBundledRelativePath);
    }

    public static byte[] LoadT2miSampleBytes()
    {
        var path = ResolveT2miSamplePath();
        Assert.That(File.Exists(path), Is.True, $"T2-MI sample not found: {path}");
        return File.ReadAllBytes(path);
    }

    public static bool TryGetT2miFullSamplePath(out string path)
    {
        var sample = Environment.GetEnvironmentVariable(T2miSampleEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(sample))
        {
            path = string.Empty;
            return false;
        }

        path = Path.GetFullPath(sample);
        return File.Exists(path);
    }

    public static Table LoadTable(TableManifestEntry entry)
    {
        var bytes = LoadBytes(entry.RelativePath);
        return TsParser.GetOneTableFromBytes(bytes, TableTestCatalog.UsesMipLoader(entry.Type));
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
