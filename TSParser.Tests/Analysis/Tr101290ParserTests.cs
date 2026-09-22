using NUnit.Framework;
using TSParser;
using TSParser.Analysis;
using TSParser.Enums;
using TSParser.Tests.Helpers;

namespace TSParser.Tests.Analysis;

[TestFixture]
public class Tr101290ParserTests
{
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
}
