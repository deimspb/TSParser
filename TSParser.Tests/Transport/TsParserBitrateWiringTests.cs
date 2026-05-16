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
using TSParser;
using TSParser.Analysis;
using TSParser.Enums;

namespace TSParser.Tests.Transport;

[TestFixture]
public sealed class TsParserBitrateWiringTests
{
    private const ushort PcrPid = 0x682;
    private const ulong PcrStep = 27_000;

    [Test]
    public void BitrateMeasurement_enabled_activates_analyzer_without_AllowAnalyzer()
    {
        var samples = new List<BitrateSample>();
        var parser = CreateParser(
            allowAnalyzer: false,
            options => options.MeasurePerPidBitrate = false,
            samples);

        PushPcrStream(parser, packetCount: 1200);

        Assert.That(samples, Is.Not.Empty);
        Assert.That(samples[0].Pid, Is.Null);
    }

    [Test]
    public void OnBitrateMeasured_forwards_per_pid_samples()
    {
        var samples = new List<BitrateSample>();
        var parser = CreateParser(
            allowAnalyzer: false,
            options =>
            {
                options.MeasureStreamBitrate = false;
                options.MeasurePerPidBitrate = true;
            },
            samples);

        PushPcrStream(parser, packetCount: 1200);

        Assert.That(samples, Is.Not.Empty);
        Assert.That(samples[0].Pid, Is.EqualTo(PcrPid));
    }

    [Test]
    public void OnRate_legacy_fires_with_bitrate_measurement_per_pid_pcr()
    {
        var rates = new List<(ushort Pid, ulong Packets, ulong Ticks)>();
        var parser = CreateParser(
            allowAnalyzer: false,
            _ => { },
            samples: null);
        parser.OnRate += (pid, deltaPackets, deltaTime) => rates.Add((pid, deltaPackets, deltaTime));

        PushPcrStream(parser, packetCount: 1200);

        Assert.That(rates, Is.Not.Empty);
        Assert.That(rates[0].Pid, Is.EqualTo(PcrPid));
        Assert.That(rates[0].Packets, Is.GreaterThan(0));
        Assert.That(rates[0].Ticks, Is.GreaterThan(0));
    }

    [Test]
    public void PushBytes_uses_configured_packet_length_for_byte_count()
    {
        var samples188 = new List<BitrateSample>();
        var samples204 = new List<BitrateSample>();

        var parser188 = CreateParser(false, o => o.MeasurePerPidBitrate = false, samples188);
        var parser204 = CreateParser(false, o => o.MeasurePerPidBitrate = false, samples204);

        PushPcrStream(parser188, packetCount: 1200, packetLength: 188);
        PushPcrStream(parser204, packetCount: 1200, packetLength: 204);

        Assert.That(samples188, Is.Not.Empty);
        Assert.That(samples204, Is.Not.Empty);
        Assert.That(
            (double)samples204[0].BytesInWindow / samples188[0].BytesInWindow,
            Is.EqualTo(204.0 / 188.0).Within(0.001));
    }

    private static TsParser CreateParser(
        bool allowAnalyzer,
        Action<BitrateMeasurementOptions> configure,
        List<BitrateSample>? samples)
    {
        var options = new BitrateMeasurementOptions
        {
            Enabled = true,
            MeasurementWindow = TimeSpan.FromSeconds(1),
            ClockSource = BitrateClockSource.Pcr,
            MeasureStreamBitrate = true,
            MeasurePerPidBitrate = true,
        };
        configure(options);

        var parser = new TsParser(new ParserConfig
        {
            CurrentDecodeMode = DecodeMode.Packet,
            AllowAnalyzer = allowAnalyzer,
            BitrateMeasurement = options,
        });

        if (samples != null)
            parser.OnBitrateMeasured += samples.Add;

        return parser;
    }

    private static void PushPcrStream(TsParser parser, int packetCount, int packetLength = 188)
    {
        var bytes = new byte[packetCount * packetLength];
        for (var i = 0; i < packetCount; i++)
        {
            var packet = BuildPcrPacket(PcrPid, (ulong)i * PcrStep);
            if (packetLength == 188)
            {
                packet.CopyTo(bytes.AsSpan(i * 188, 188));
            }
            else
            {
                packet.CopyTo(bytes.AsSpan(i * packetLength, 188));
            }
        }

        parser.PushBytes(bytes, packetLength);
    }

    private static byte[] BuildPcrPacket(ushort pid, ulong pcr27MHz)
    {
        var packet = new byte[188];
        packet[0] = 0x47;
        packet[1] = (byte)((pid >> 8) & 0x1F);
        packet[2] = (byte)(pid & 0xFF);
        packet[3] = 0x20;
        packet[4] = 7;
        packet[5] = 0x10;
        WritePcr(packet.AsSpan(6, 8), pcr27MHz);
        return packet;
    }

    private static void WritePcr(Span<byte> pcrBytes, ulong pcr27MHz)
    {
        var pcrBase = pcr27MHz / 300;
        var pcrExt = (ushort)(pcr27MHz % 300);

        pcrBytes[0] = (byte)(pcrBase >> 25);
        pcrBytes[1] = (byte)(pcrBase >> 17);
        pcrBytes[2] = (byte)(pcrBase >> 9);
        pcrBytes[3] = (byte)(pcrBase >> 1);
        pcrBytes[4] = (byte)(((pcrBase & 1) << 7) | 0x7E);
        pcrBytes[5] = (byte)(pcrExt >> 8);
        pcrBytes[6] = (byte)pcrExt;
    }
}
