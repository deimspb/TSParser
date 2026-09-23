using NUnit.Framework;
using System.Buffers.Binary;
using TSParser;
using TSParser.Analysis;
using TSParser.Enums;
using TSParser.Service;
using TSParser.Tests.Helpers;

namespace TSParser.Tests.Analysis;

[TestFixture]
public class Tr101290ParserTests
{
    [TestCase(DecodeMode.Table, 1)]
    [TestCase(DecodeMode.Packet, 0)]
    public void Repeated_same_crc_section_is_monitored_in_both_modes_without_changing_public_dedup(
        DecodeMode mode,
        int expectedPublicEvents)
    {
        using var parser = new TsParser(new ParserOptions
        {
            CurrentDecodeMode = mode,
            Tr101290 = Tr101290Options.Enable,
        });
        var events = new List<Tr101290Event>();
        var nitCount = 0;
        parser.OnTr101290Event += events.Add;
        parser.OnNitReady += _ => nitCount++;

        var nit = NitSection();
        parser.PushBytes(Join(
            PcrPacket(0x0100, 0, 0),
            PsiTsPacketFactory.BuildPsiTsPacket(0x0010, nit, 0),
            PcrPacket(0x0100, Tr101290Limits.Ms25 / 2, 1),
            PsiTsPacketFactory.BuildPsiTsPacket(0x0010, nit, 1)), 188);

        Assert.That(nitCount, Is.EqualTo(expectedPublicEvents));
        Assert.That(events, Has.Some.Matches<Tr101290Event>(evt =>
            evt.Indicator == Tr101290Indicator.SiRepetitionError &&
            evt.Kind == Tr101290EventKind.Raised &&
            evt.Pid == 0x0010));
    }

    [Test]
    public void Invalid_repeat_with_cached_crc_is_still_reported()
    {
        using var parser = new TsParser(new ParserOptions
        {
            CurrentDecodeMode = DecodeMode.Table,
            Tr101290 = Tr101290Options.Enable,
        });
        var events = new List<Tr101290Event>();
        var nitCount = 0;
        parser.OnTr101290Event += events.Add;
        parser.OnNitReady += _ => nitCount++;

        var valid = NitSection();
        var invalid = valid.ToArray();
        invalid[4] ^= 0x01; // keep the cached trailing CRC while changing covered bytes
        parser.PushBytes(Join(
            PsiTsPacketFactory.BuildPsiTsPacket(0x0010, valid, 0),
            PsiTsPacketFactory.BuildPsiTsPacket(0x0010, invalid, 1)), 188);

        Assert.That(nitCount, Is.EqualTo(1));
        Assert.That(events, Has.Some.Matches<Tr101290Event>(evt =>
            evt.Indicator == Tr101290Indicator.CrcError &&
            evt.Kind == Tr101290EventKind.Raised &&
            evt.Pid == 0x0010));
    }

    [Test]
    public void PushBytes_reports_continuity_error()
    {
        using var parser = new TsParser(new ParserOptions
        {
            CurrentDecodeMode = DecodeMode.Table,
            Tr101290 = Tr101290Options.Enable,
        });
        var events = new List<Tr101290Event>();
        parser.OnTr101290Event += events.Add;

        var first = PsiTsPacketFactory.BuildTsPacket(0x0100, payloadUnitStartIndicator: false, continuityCounter: 0, payload: [0xFF]);
        var second = PsiTsPacketFactory.BuildTsPacket(0x0100, payloadUnitStartIndicator: false, continuityCounter: 2, payload: [0xFF]);
        var buffer = new byte[first.Length + second.Length];
        first.CopyTo(buffer, 0);
        second.CopyTo(buffer, first.Length);

        parser.PushBytes(buffer, 188);

        Assert.That(events, Has.Some.Matches<Tr101290Event>(evt =>
            evt.Indicator == Tr101290Indicator.ContinuityCountError &&
            evt.Kind == Tr101290EventKind.Raised &&
            evt.Pid == 0x0100));
    }

    [Test]
    public void PushBytes_reports_sync_loss_for_two_bad_sync_bytes()
    {
        using var parser = new TsParser(new ParserOptions
        {
            CurrentDecodeMode = DecodeMode.Packet,
            Tr101290 = Tr101290Options.Enable,
        });
        var events = new List<Tr101290Event>();
        parser.OnTr101290Event += events.Add;

        parser.PushBytes(new byte[188 * 2], 188);

        Assert.That(events, Has.Some.Matches<Tr101290Event>(evt =>
            evt.Indicator == Tr101290Indicator.TsSyncLoss &&
            evt.Kind == Tr101290EventKind.Raised));
        Assert.That(events.Count(evt => evt.Indicator == Tr101290Indicator.SyncByteError && evt.Kind == Tr101290EventKind.Raised), Is.EqualTo(2));
    }

    private static byte[] NitSection()
    {
        var bytes = new byte[16];
        bytes[0] = 0x40;
        bytes[1] = 0xB0;
        bytes[2] = 13;
        bytes[4] = 1;
        bytes[5] = 0xC1;
        bytes[8] = 0xF0;
        bytes[10] = 0xF0;
        var crc = Utils.GetCRC32(bytes.AsSpan(0, 12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), crc);
        return bytes;
    }

    private static byte[] PcrPacket(ushort pid, ulong pcr, byte cc)
    {
        var bytes = new byte[188];
        bytes[0] = 0x47;
        bytes[1] = (byte)(pid >> 8);
        bytes[2] = (byte)pid;
        bytes[3] = (byte)(0x20 | cc);
        bytes[4] = 183;
        bytes[5] = 0x10;
        var base33 = pcr / 300;
        var ext = (ushort)(pcr % 300);
        bytes[6] = (byte)(base33 >> 25);
        bytes[7] = (byte)(base33 >> 17);
        bytes[8] = (byte)(base33 >> 9);
        bytes[9] = (byte)(base33 >> 1);
        bytes[10] = (byte)(((base33 & 1) << 7) | ((ulong)ext >> 8));
        bytes[11] = (byte)ext;
        bytes.AsSpan(12).Fill(0xFF);
        return bytes;
    }

    private static byte[] Join(params byte[][] packets)
    {
        var bytes = new byte[packets.Sum(packet => packet.Length)];
        var offset = 0;
        foreach (var packet in packets)
        {
            packet.CopyTo(bytes, offset);
            offset += packet.Length;
        }
        return bytes;
    }
}
