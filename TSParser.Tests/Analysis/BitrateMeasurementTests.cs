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
using TSParser.Tests.Helpers;
using TSParser.TransportStream;

namespace TSParser.Tests.Analysis;

[TestFixture]
public sealed class BitrateMeasurementTests
{
    private const string PesHeaderFixture = "TsPackets/pes_hdr.pkt";
    private const ushort PcrPid = 0x682;
    private const ushort VideoPid = 0x100;
    private const ushort AudioPid = 0x200;
    private const int PacketLength = 188;
    private const ulong PcrStepTicks = 27_000;
    private const ulong PtsStepTicks = 90;

    [Test]
    public void Pcr_stream_sample_reports_expected_bytes_and_bitrate()
    {
        const int packetsPerSecond = 1000;
        const int packetCount = packetsPerSecond + 100;
        var samples = new List<BitrateSample>();

        var parser = CreateParser(
            options =>
            {
                options.MeasurementWindow = TimeSpan.FromSeconds(1);
                options.ClockSource = BitrateClockSource.Pcr;
                options.MeasureStreamBitrate = true;
                options.MeasurePerPidBitrate = false;
            },
            samples);

        PushUniformPcrStream(parser, PcrPid, packetCount, PcrStepTicks);

        Assert.That(samples, Is.Not.Empty);
        var stream = samples.First(s => s.Pid is null);
        Assert.That(stream.ClockSource, Is.EqualTo(BitrateClockSource.Pcr));
        Assert.That(stream.BytesInWindow, Is.EqualTo((ulong)(packetsPerSecond * PacketLength)).Within(PacketLength));
        Assert.That(stream.WindowDuration.TotalSeconds, Is.EqualTo(1.0).Within(0.01));
        Assert.That(
            stream.BitsPerSecond,
            Is.EqualTo(stream.BytesInWindow * 8.0).Within(stream.BytesInWindow * 0.02));
    }

    [Test]
    public void Measurement_window_shorter_produces_more_emits_over_same_pcr_span()
    {
        const int packetCount = 4000;
        var samplesHalfSecond = new List<BitrateSample>();
        var samplesOneSecond = new List<BitrateSample>();

        var parser500 = CreateParser(
            o => o.MeasurementWindow = TimeSpan.FromMilliseconds(500),
            samplesHalfSecond);
        var parser1000 = CreateParser(
            o => o.MeasurementWindow = TimeSpan.FromSeconds(1),
            samplesOneSecond);

        PushUniformPcrStream(parser500, PcrPid, packetCount, PcrStepTicks);
        PushUniformPcrStream(parser1000, PcrPid, packetCount, PcrStepTicks);

        var halfSecondStream = samplesHalfSecond.Where(s => s.Pid is null).ToList();
        var oneSecondStream = samplesOneSecond.Where(s => s.Pid is null).ToList();

        Assert.That(halfSecondStream, Is.Not.Empty);
        Assert.That(oneSecondStream, Is.Not.Empty);
        Assert.That(halfSecondStream.Count, Is.GreaterThan(oneSecondStream.Count));
        Assert.That(
            (double)halfSecondStream.Count / oneSecondStream.Count,
            Is.EqualTo(2.0).Within(0.35));
    }

    [Test]
    public void Per_pid_bitrate_reflects_different_packet_density()
    {
        const int videoPackets = 1200;
        const int audioPackets = 120;
        var samples = new List<BitrateSample>();

        var parser = CreateParser(
            options =>
            {
                options.MeasureStreamBitrate = false;
                options.MeasurePerPidBitrate = true;
            },
            samples);

        PushMixedDensityPcrStream(parser, videoPackets, audioPackets);

        var video = samples.Where(s => s.Pid == VideoPid).ToList();
        var audio = samples.Where(s => s.Pid == AudioPid).ToList();

        Assert.That(video, Is.Not.Empty);
        Assert.That(audio, Is.Not.Empty);

        var videoBps = video.Average(s => s.BitsPerSecond);
        var audioBps = audio.Average(s => s.BitsPerSecond);

        Assert.That(videoBps, Is.GreaterThan(audioBps * 5));
        Assert.That(
            video.First().BytesInWindow / (double)audio.First().BytesInWindow,
            Is.EqualTo(videoPackets / (double)audioPackets).Within(0.15));
    }

    [Test]
    public void Pts_clock_emits_on_fixture_or_synthetic_pes_stream()
    {
        if (File.Exists(FixtureLoader.ResolvePath(PesHeaderFixture)))
        {
            AssertPtsFromFixture();
            return;
        }

        AssertPtsFromSyntheticStream();
    }

    [Test]
    public void Legacy_OnRate_fires_when_per_pid_pcr_measurement_enabled()
    {
        var rates = new List<(ushort Pid, ulong Packets, ulong Ticks)>();
        var parser = CreateParser(
            _ => { },
            samples: null);
        parser.OnRate += (pid, deltaPackets, deltaTime) => rates.Add((pid, deltaPackets, deltaTime));

        PushUniformPcrStream(parser, PcrPid, packetCount: 1500, PcrStepTicks);

        Assert.That(rates, Is.Not.Empty);
        Assert.That(rates[0].Pid, Is.EqualTo(PcrPid));
        Assert.That(rates[0].Packets, Is.GreaterThan(0));
        Assert.That(rates[0].Ticks, Is.GreaterThan(0));
    }

    private static void AssertPtsFromFixture()
    {
        var reference = FixtureLoader.LoadTsPacket(PesHeaderFixture);
        var samples = new List<BitrateSample>();

        var parser = CreateParser(
            options =>
            {
                options.ClockSource = BitrateClockSource.Pts;
                options.ReferencePid = reference.Pid;
                options.MeasurementWindow = TimeSpan.FromMilliseconds(100);
                options.MeasureStreamBitrate = true;
                options.MeasurePerPidBitrate = false;
            },
            samples);

        var bytes = BuildPtsStreamFromTemplate(reference, packetCount: 1200, ptsStep: PtsStepTicks);
        parser.PushBytes(bytes, PacketLength);

        Assert.That(samples, Is.Not.Empty);
        Assert.That(samples[0].ClockSource, Is.EqualTo(BitrateClockSource.Pts));
        Assert.That(samples[0].Pid, Is.Null);
        Assert.That(samples[0].BytesInWindow, Is.GreaterThan(0));
        Assert.That(samples[0].BitsPerSecond, Is.GreaterThan(0));
    }

    private static void AssertPtsFromSyntheticStream()
    {
        const ushort pid = 0x0FB;
        const ulong initialPts = 1_000_000;
        var samples = new List<BitrateSample>();

        var parser = CreateParser(
            options =>
            {
                options.ClockSource = BitrateClockSource.Pts;
                options.ReferencePid = pid;
                options.MeasurementWindow = TimeSpan.FromSeconds(1);
                options.MeasureStreamBitrate = true;
                options.MeasurePerPidBitrate = false;
            },
            samples);

        const int packetsPerSecond = 1000;
        const int packetCount = packetsPerSecond + 50;
        var bytes = new byte[packetCount * PacketLength];
        for (var i = 0; i < packetCount; i++)
        {
            BuildPtsPacket(pid, initialPts + (ulong)i * PtsStepTicks, includeDts: true)
                .CopyTo(bytes.AsSpan(i * PacketLength, PacketLength));
        }

        parser.PushBytes(bytes, PacketLength);

        Assert.That(samples, Is.Not.Empty);
        var sample = samples.First(s => s.Pid is null);
        Assert.That(sample.ClockSource, Is.EqualTo(BitrateClockSource.Pts));
        Assert.That(sample.BytesInWindow, Is.EqualTo((ulong)(packetsPerSecond * PacketLength)).Within(PacketLength * 2));
        Assert.That(sample.BitsPerSecond, Is.EqualTo(sample.BytesInWindow * 8.0).Within(sample.BytesInWindow * 0.05));
    }

    private static TsParser CreateParser(
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
            AllowAnalyzer = false,
            BitrateMeasurement = options,
        });

        if (samples != null)
            parser.OnBitrateMeasured += samples.Add;

        return parser;
    }

    private static void PushUniformPcrStream(TsParser parser, ushort pid, int packetCount, ulong pcrStep)
    {
        var bytes = new byte[packetCount * PacketLength];
        for (var i = 0; i < packetCount; i++)
        {
            BuildPcrPacket(pid, (ulong)i * pcrStep)
                .CopyTo(bytes.AsSpan(i * PacketLength, PacketLength));
        }

        parser.PushBytes(bytes, PacketLength);
    }

    private static void PushMixedDensityPcrStream(TsParser parser, int videoPackets, int audioPackets)
    {
        var ratio = videoPackets / audioPackets;
        var totalPackets = videoPackets + audioPackets;
        var bytes = new byte[totalPackets * PacketLength];
        var offset = 0;
        ulong pcrTick = 0;
        var audioSent = 0;

        for (var i = 0; i < videoPackets; i++)
        {
            BuildPcrPacket(VideoPid, pcrTick).CopyTo(bytes.AsSpan(offset, PacketLength));
            offset += PacketLength;
            pcrTick += PcrStepTicks;

            if (audioSent >= audioPackets || (i + 1) % ratio != 0)
                continue;

            BuildPcrPacket(AudioPid, pcrTick).CopyTo(bytes.AsSpan(offset, PacketLength));
            offset += PacketLength;
            audioSent++;
        }

        if (offset == bytes.Length)
        {
            parser.PushBytes(bytes, PacketLength);
            return;
        }

        var exact = new byte[offset];
        Buffer.BlockCopy(bytes, 0, exact, 0, offset);
        parser.PushBytes(exact, PacketLength);
    }

    private static byte[] BuildPtsStreamFromTemplate(TsPacket template, int packetCount, ulong ptsStep)
    {
        var templateBytes = FixtureLoader.LoadBytes(PesHeaderFixture);
        var bytes = new byte[packetCount * PacketLength];
        var basePts = template.Pes_header.PTSHex;

        for (var i = 0; i < packetCount; i++)
        {
            templateBytes.CopyTo(bytes.AsSpan(i * PacketLength, PacketLength));
            BuildPtsPacket(template.Pid, basePts + (ulong)i * ptsStep, includeDts: true)
                .CopyTo(bytes.AsSpan(i * PacketLength, PacketLength));
        }

        return bytes;
    }

    private static byte[] BuildPcrPacket(ushort pid, ulong pcr27MHz)
    {
        var packet = new byte[PacketLength];
        packet[0] = 0x47;
        packet[1] = (byte)((pid >> 8) & 0x1F);
        packet[2] = (byte)(pid & 0xFF);
        packet[3] = 0x20;
        packet[4] = 7;
        packet[5] = 0x10;
        WritePcr(packet.AsSpan(6, 6), pcr27MHz);
        return packet;
    }

    private static byte[] BuildPtsPacket(ushort pid, ulong pts90kHz, bool includeDts)
    {
        var packet = new byte[PacketLength];
        packet[0] = 0x47;
        packet[1] = (byte)(0x40 | ((pid >> 8) & 0x1F));
        packet[2] = (byte)(pid & 0xFF);
        packet[3] = 0x10;

        var payload = packet.AsSpan(4);
        payload[0] = 0x00;
        payload[1] = 0x00;
        payload[2] = 0x01;
        payload[3] = 0xE0;
        payload[4] = 0x00;
        payload[5] = 0x00;
        payload[6] = 0x80;
        payload[7] = includeDts ? (byte)0xC0 : (byte)0x80;
        payload[8] = includeDts ? (byte)10 : (byte)5;
        WritePts(payload.Slice(9, 5), pts90kHz, prefix: 0x30);
        if (includeDts)
            WritePts(payload.Slice(14, 5), pts90kHz - 300, prefix: 0x10);

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
        if (pcrBytes.Length > 6)
            pcrBytes[6] = (byte)pcrExt;
    }

    private static void WritePts(Span<byte> dest, ulong pts, byte prefix)
    {
        dest[0] = (byte)(prefix | ((pts >> 30) & 0x07) << 1 | 0x01);
        dest[1] = (byte)((pts >> 22) & 0xFF);
        dest[2] = (byte)(((pts >> 14) & 0x7F) << 1 | 0x01);
        dest[3] = (byte)((pts >> 7) & 0xFF);
        dest[4] = (byte)(((pts >> 0) & 0x7F) << 1 | 0x01);
    }
}
