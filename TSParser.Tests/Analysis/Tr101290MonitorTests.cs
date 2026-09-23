using NUnit.Framework;
using System.Buffers.Binary;
using TSParser.Analysis;
using TSParser.Service;
using TSParser.Tables.DvbTables;
using TSParser.Tests.Helpers;
using TSParser.TransportStream;

namespace TSParser.Tests.Analysis;

[TestFixture]
public class Tr101290MonitorTests
{
    [Test]
    public void One_bad_sync_byte_sets_sync_byte_error_only()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObserveMissingSync();

        Assert.That(monitor.GetState(Tr101290Indicator.SyncByteError), Is.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetState(Tr101290Indicator.TsSyncLoss), Is.EqualTo(Tr101290IndicatorState.NotMeasured));
        Assert.That(monitor.GetCount(Tr101290Indicator.SyncByteError), Is.EqualTo(1));
    }

    [Test]
    public void Two_bad_sync_bytes_set_sync_loss_and_five_good_bytes_clear_it()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObserveMissingSync();
        monitor.ObserveMissingSync();

        Assert.That(monitor.GetState(Tr101290Indicator.TsSyncLoss), Is.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetCount(Tr101290Indicator.SyncByteError), Is.EqualTo(2));

        for (var i = 0; i < 5; i++)
            monitor.ObserveGoodSync();

        Assert.That(monitor.GetState(Tr101290Indicator.TsSyncLoss), Is.EqualTo(Tr101290IndicatorState.Ok));
        Assert.That(monitor.GetState(Tr101290Indicator.SyncByteError), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Continuity_gap_is_an_error_and_the_next_sequential_value_clears_it()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x100, cc: 0));
        monitor.ObservePacket(Packet(pid: 0x100, cc: 2));

        Assert.That(monitor.GetState(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePacket(Packet(pid: 0x100, cc: 3));

        Assert.That(monitor.GetState(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Repeated_continuity_without_payload_is_not_an_error()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x100, cc: 4, payload: false));
        monitor.ObservePacket(Packet(pid: 0x100, cc: 4, payload: false));

        Assert.That(monitor.GetState(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(Tr101290IndicatorState.NotMeasured));
    }

    [Test]
    public void Third_identical_payload_packet_is_a_continuity_error()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x100, cc: 1));
        monitor.ObservePacket(Packet(pid: 0x100, cc: 1));
        monitor.ObservePacket(Packet(pid: 0x100, cc: 1));

        Assert.That(monitor.GetState(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetCount(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(1));
    }

    [Test]
    public void Discontinuity_indicator_allows_a_continuity_jump()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x100, cc: 0));
        monitor.ObservePacket(Packet(pid: 0x100, cc: 7, discontinuity: true));

        Assert.That(monitor.GetState(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(Tr101290IndicatorState.NotMeasured));
    }

    [Test]
    public void Null_packets_are_not_checked_for_continuity()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x1FFF, cc: 0));
        monitor.ObservePacket(Packet(pid: 0x1FFF, cc: 5));

        Assert.That(monitor.GetCount(Tr101290Indicator.ContinuityCountError, 0x1FFF), Is.EqualTo(0));
    }

    [Test]
    public void Pat_missing_longer_than_500ms_is_cleared_by_a_valid_current_pat_section()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms500 + Tr101290Limits.Ms40));

        Assert.That(monitor.GetState(Tr101290Indicator.PatError2), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePat(Pat(0x0020));

        Assert.That(monitor.GetState(Tr101290Indicator.PatError2), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Unexpected_pat_table_id_sets_pat_error()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x0000, cc: 0, pusi: true, payloadBytes: [0x00, 0x02]));

        Assert.That(monitor.GetState(Tr101290Indicator.PatError2), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Pmt_missing_longer_than_500ms_after_pat()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePat(Pat(pmtPid: 0x0020));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms500 + Tr101290Limits.Ms40));

        Assert.That(monitor.GetState(Tr101290Indicator.PmtError2, 0x0020), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePmt(Pmt(pmtPid: 0x0020, pcrPid: 0x100, esPid: 0x200));
        Assert.That(monitor.GetState(Tr101290Indicator.PmtError2, 0x0020), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Referenced_pid_missing_longer_than_the_timeout_is_pid_error()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePmt(Pmt(pmtPid: 0x0020, pcrPid: 0x100, esPid: 0x200));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Sec5 + Tr101290Limits.Ms40));

        Assert.That(monitor.GetState(Tr101290Indicator.PidError, 0x200), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePacket(Packet(pid: 0x200, cc: 0));

        Assert.That(monitor.GetState(Tr101290Indicator.PidError, 0x200), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Transport_error_indicator_sets_and_clears()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x100, cc: 0, tei: true));
        Assert.That(monitor.GetState(Tr101290Indicator.TransportError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePacket(Packet(pid: 0x100, cc: 0));
        Assert.That(monitor.GetState(Tr101290Indicator.TransportError, 0x100), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Crc_error_is_cleared_by_a_valid_section_on_the_same_pid()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObserveCrcError(pid: 0x0000, tableId: 0x00);
        Assert.That(monitor.GetState(Tr101290Indicator.CrcError, 0x0000), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePat(Pat(0x0020));
        Assert.That(monitor.GetState(Tr101290Indicator.CrcError, 0x0000), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Pcr_interval_above_100ms_sets_repetition_error()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePmt(Pmt(pmtPid: 0x0020, pcrPid: 0x100, esPid: 0x200));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms100 + 300));

        Assert.That(monitor.GetState(Tr101290Indicator.PcrRepetitionError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Pcr_break_above_100ms_without_the_flag_sets_discontinuity_error()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePmt(Pmt(pmtPid: 0x0020, pcrPid: 0x100, esPid: 0x200));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms100 + Tr101290Limits.Ms40));

        Assert.That(monitor.GetState(Tr101290Indicator.PcrDiscontinuityIndicatorError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetState(Tr101290Indicator.PcrRepetitionError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Discontinuity_flag_is_not_an_error_for_small_or_large_pcr_steps()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePmt(Pmt(pmtPid: 0x0020, pcrPid: 0x100, esPid: 0x200));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms25, discontinuity: true));

        Assert.That(monitor.GetState(Tr101290Indicator.PcrDiscontinuityIndicatorError, 0x100), Is.Not.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms25 + Tr101290Limits.Ms100 + Tr101290Limits.Ms40, discontinuity: true));

        Assert.That(monitor.GetState(Tr101290Indicator.PcrDiscontinuityIndicatorError, 0x100), Is.Not.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Scrambled_packet_without_cat_is_cat_error()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x100, cc: 0, scrambling: 2));
        Assert.That(monitor.GetState(Tr101290Indicator.CatError), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObserveCat(Cat());
        Assert.That(monitor.GetState(Tr101290Indicator.CatError), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Nit_actual_missing_longer_than_10s_and_sections_closer_than_25ms()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObserveNit(Nit(0x40));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms25 / 2));
        monitor.ObserveNit(Nit(0x40));

        Assert.That(monitor.GetState(Tr101290Indicator.SiRepetitionError, 0x0010), Is.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetState(Tr101290Indicator.NitActualError, 0x0010), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Sec10 + Tr101290Limits.Sec2));
        Assert.That(monitor.GetState(Tr101290Indicator.NitActualError, 0x0010), Is.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetCount(Tr101290Indicator.NitActualError, 0x0010), Is.GreaterThan(0));
    }

    [Test]
    public void Unreferenced_pid_is_reported_after_500ms_and_cleared_by_pmt()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Packet(pid: 0x200, cc: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms500 + Tr101290Limits.Ms40));

        Assert.That(monitor.GetState(Tr101290Indicator.UnreferencedPid, 0x200), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePmt(Pmt(pmtPid: 0x0020, pcrPid: 0x100, esPid: 0x200));
        Assert.That(monitor.GetState(Tr101290Indicator.UnreferencedPid, 0x200), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Eit_pf_requires_both_sections()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObserveEit(Eit(0x4E, serviceId: 1, sectionNumber: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms100));
        monitor.ObserveEit(Eit(0x4E, serviceId: 1, sectionNumber: 1));

        Assert.That(monitor.GetState(Tr101290Indicator.EitPfError, 0x0012), Is.EqualTo(Tr101290IndicatorState.Ok));

        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Sec2 + Tr101290Limits.Sec2));
        Assert.That(monitor.GetState(Tr101290Indicator.EitPfError, 0x0012), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Rst_sections_closer_than_25ms_are_an_error()
    {
        var monitor = new Tr101290Monitor();
        var rst = PsiTsPacketFactory.BuildSection(0x71, new byte[9]);
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Packet(pid: 0x0013, cc: 0, pusi: true, payloadBytes: [0x00, .. rst]));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Ms25 / 2));
        monitor.ObservePacket(Packet(pid: 0x0013, cc: 1, pusi: true, payloadBytes: [0x00, .. rst]));

        Assert.That(monitor.GetState(Tr101290Indicator.RstError, 0x0013), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Same_continuity_counter_requires_an_exact_duplicate()
    {
        var monitor = new Tr101290Monitor();

        monitor.ObservePacket(Packet(pid: 0x100, cc: 1, payloadBytes: [0x10]));
        monitor.ObservePacket(Packet(pid: 0x100, cc: 1, payloadBytes: [0x11]));

        Assert.That(monitor.GetState(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Section_timing_does_not_require_parsing_descriptor_payloads()
    {
        var monitor = new Tr101290Monitor();
        var nit = NitWithMalformedDescriptor();

        monitor.ObservePacket(Pcr(0x0100, 0));
        monitor.ObserveSection(0x0010, nit);
        monitor.ObservePacket(Pcr(0x0100, Tr101290Limits.Ms25 / 2));
        monitor.ObserveSection(0x0010, nit);

        Assert.That(
            monitor.GetState(Tr101290Indicator.SiRepetitionError, 0x0010),
            Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Sync_loss_gates_downstream_checks_until_five_good_packets()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Packet(pid: 0x100, cc: 0));
        monitor.ObserveMissingSync();
        monitor.ObserveMissingSync();

        for (byte cc = 5; cc < 9; cc++)
            monitor.ObservePacket(Packet(pid: 0x100, cc: cc));

        Assert.That(monitor.GetCount(Tr101290Indicator.ContinuityCountError, 0x100), Is.Zero);

        monitor.ObservePacket(Packet(pid: 0x100, cc: 9));
        monitor.ObservePacket(Packet(pid: 0x100, cc: 11));

        Assert.That(monitor.GetState(Tr101290Indicator.ContinuityCountError, 0x100), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Si_minimum_interval_is_between_any_sections_of_the_same_table_id()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Pcr(0x100, 0));
        monitor.ObserveNit(Nit(0x40, sectionNumber: 0, lastSectionNumber: 1));
        monitor.ObservePacket(Pcr(0x100, Tr101290Limits.Ms25 / 2));
        monitor.ObserveNit(Nit(0x40, sectionNumber: 1, lastSectionNumber: 1));

        Assert.That(monitor.GetState(Tr101290Indicator.SiRepetitionError, 0x0010), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePacket(Pcr(0x100, Tr101290Limits.Ms25 + Tr101290Limits.Ms25));
        monitor.ObserveNit(Nit(0x40, sectionNumber: 0, lastSectionNumber: 1));

        Assert.That(monitor.GetState(Tr101290Indicator.SiRepetitionError, 0x0010), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    [Test]
    public void Si_maximum_interval_is_tracked_per_subtable_section()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Pcr(0x100, 0));
        monitor.ObserveNit(Nit(0x40, sectionNumber: 0, lastSectionNumber: 1));
        monitor.ObservePacket(Pcr(0x100, Tr101290Limits.Sec5));
        monitor.ObserveNit(Nit(0x40, sectionNumber: 1, lastSectionNumber: 1));
        monitor.ObservePacket(Pcr(0x100, Tr101290Limits.Sec10 + Tr101290Limits.Ms100));

        Assert.That(monitor.GetState(Tr101290Indicator.NitActualError, 0x0010), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Eit_pf_error_is_aggregated_across_known_services()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Pcr(0x100, 0));
        monitor.ObserveEit(Eit(0x4E, serviceId: 1, sectionNumber: 0));
        monitor.ObserveEit(Eit(0x4E, serviceId: 1, sectionNumber: 1));
        monitor.ObserveEit(Eit(0x4E, serviceId: 2, sectionNumber: 0));
        monitor.ObservePacket(Pcr(0x100, Tr101290Limits.Sec2 + Tr101290Limits.Ms100));

        Assert.That(monitor.GetState(Tr101290Indicator.EitPfError, 0x0012), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObserveEit(Eit(0x4E, serviceId: 1, sectionNumber: 0));
        monitor.ObserveEit(Eit(0x4E, serviceId: 1, sectionNumber: 1));

        Assert.That(monitor.GetState(Tr101290Indicator.EitPfError, 0x0012), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Completed_new_pat_and_pmt_versions_remove_stale_referenced_pids()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePat(Pat(0x0020, version: 0, sectionNumber: 0, lastSectionNumber: 1));
        monitor.ObservePat(Pat(0x0030, version: 0, sectionNumber: 1, lastSectionNumber: 1));
        monitor.ObservePmt(Pmt(0x0020, 0x0100, 0x0200));
        monitor.ObservePmt(Pmt(0x0030, 0x0100, 0x0300));

        monitor.ObservePat(Pat(0x0040, version: 1));
        monitor.ObservePmt(Pmt(0x0040, 0x0100, 0x0400));
        monitor.ObservePacket(Pcr(0x0100, 0));
        monitor.ObservePacket(Pcr(0x0100, Tr101290Limits.Sec5 + Tr101290Limits.Ms100));

        Assert.That(monitor.GetState(Tr101290Indicator.PidError, 0x0200), Is.Not.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetState(Tr101290Indicator.PidError, 0x0300), Is.Not.EqualTo(Tr101290IndicatorState.Error));
        Assert.That(monitor.GetState(Tr101290Indicator.PidError, 0x0400), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void File_clock_estimation_starts_only_after_a_reliable_pcr_pair()
    {
        var monitor = new Tr101290Monitor(Tr101290Options.Enable, Tr101290ClockMode.File);
        monitor.ObservePacket(Pcr(0x0100, 0));
        for (byte i = 0; i < 10; i++)
            monitor.ObservePacket(Packet(0x0200, (byte)(i & 0x0F)));

        Assert.That(monitor.GetState(Tr101290Indicator.PatError2), Is.Not.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObservePacket(Pcr(0x0100, Tr101290Limits.Ms100));
        for (var i = 0; i < 60; i++)
            monitor.ObservePacket(Packet(0x0200, (byte)(i & 0x0F)));

        Assert.That(monitor.GetState(Tr101290Indicator.PatError2), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Udp_clock_uses_monotonic_time_when_pcr_is_absent()
    {
        var monitor = new Tr101290Monitor(
            new Tr101290Options { Enabled = true, PidErrorTimeout = TimeSpan.FromMilliseconds(1) },
            Tr101290ClockMode.Udp);
        monitor.ObservePmt(Pmt(0x0020, 0x0100, 0x0200));

        Thread.Sleep(30);
        monitor.ObservePacket(Packet(0x0300, 0));

        Assert.That(monitor.GetState(Tr101290Indicator.PidError, 0x0200), Is.EqualTo(Tr101290IndicatorState.Error));
    }

    [Test]
    public void Sdt_actual_missing_longer_than_2s()
    {
        var monitor = new Tr101290Monitor();
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: 0));
        monitor.ObservePacket(Pcr(pid: 0x100, pcr: Tr101290Limits.Sec2 + Tr101290Limits.Ms40));

        Assert.That(monitor.GetState(Tr101290Indicator.SdtActualError, 0x0011), Is.EqualTo(Tr101290IndicatorState.Error));

        monitor.ObserveSdt(Sdt(0x42));
        Assert.That(monitor.GetState(Tr101290Indicator.SdtActualError, 0x0011), Is.EqualTo(Tr101290IndicatorState.Ok));
    }

    private static TsPacket Pcr(ushort pid, ulong pcr, bool discontinuity = false) =>
        Packet(pid, cc: 0, pcr: pcr, discontinuity: discontinuity);

    private static TsPacket Packet(
        ushort pid,
        byte cc,
        bool payload = true,
        bool tei = false,
        byte scrambling = 0,
        bool discontinuity = false,
        ulong? pcr = null,
        bool pusi = false,
        byte[]? payloadBytes = null)
    {
        var bytes = new byte[188];
        bytes[0] = TsPacket.SYNC_BYTE;
        bytes[1] = (byte)((tei ? 0x80 : 0) | (pusi ? 0x40 : 0) | ((pid >> 8) & 0x1F));
        bytes[2] = (byte)pid;
        if (tei)
        {
            bytes[3] = (byte)(cc & 0x0F);
            return new TsPacket(bytes, 0);
        }

        var useAdaptation = !payload || discontinuity || pcr.HasValue;
        var afc = payload ? (useAdaptation ? 0b11 : 0b01) : 0b10;
        bytes[3] = (byte)((scrambling << 6) | (afc << 4) | (cc & 0x0F));
        var cursor = 4;
        if (useAdaptation)
        {
            var flags = (byte)((discontinuity ? 0x80 : 0) | (pcr.HasValue ? 0x10 : 0));
            var fieldLength = pcr.HasValue ? (byte)7 : (byte)1;
            if (!payload)
                fieldLength = 183;

            bytes[cursor++] = fieldLength;
            bytes[cursor++] = flags;
            if (pcr.HasValue)
            {
                WritePcr(bytes.AsSpan(cursor, 6), pcr.Value);
                cursor += 6;
            }

            var adaptationEnd = 4 + 1 + fieldLength;
            while (cursor < adaptationEnd && cursor < bytes.Length)
                bytes[cursor++] = 0xFF;
        }

        if (payload && payloadBytes is { Length: > 0 })
            payloadBytes.CopyTo(bytes.AsSpan(cursor));

        return new TsPacket(bytes, 0);
    }

    private static void WritePcr(Span<byte> six, ulong pcr)
    {
        var base33 = pcr / 300;
        var ext = (ushort)(pcr % 300);
        six[0] = (byte)((base33 >> 25) & 0xFF);
        six[1] = (byte)((base33 >> 17) & 0xFF);
        six[2] = (byte)((base33 >> 9) & 0xFF);
        six[3] = (byte)((base33 >> 1) & 0xFF);
        six[4] = (byte)(((base33 & 1UL) << 7) | ((ulong)ext >> 8));
        six[5] = (byte)(ext & 0xFF);
    }

    private static PAT Pat(ushort pmtPid, byte version = 0, byte sectionNumber = 0, byte lastSectionNumber = 0)
    {
        var bytes = new byte[16];
        bytes[0] = 0x00;
        bytes[1] = 0xB0;
        bytes[2] = 13;
        bytes[5] = (byte)((version << 1) | 0x01);
        bytes[6] = sectionNumber;
        bytes[7] = lastSectionNumber;
        bytes[9] = 0x01;
        bytes[10] = (byte)((pmtPid >> 8) & 0x1F);
        bytes[11] = (byte)pmtPid;
        return new PAT(bytes);
    }

    private static PMT Pmt(ushort pmtPid, ushort pcrPid, ushort esPid)
    {
        var bytes = new byte[21];
        bytes[0] = 0x02;
        bytes[1] = 0xB0;
        bytes[2] = 18;
        bytes[4] = 0x01;
        bytes[5] = 0x01;
        bytes[8] = (byte)((pcrPid >> 8) & 0x1F);
        bytes[9] = (byte)pcrPid;
        bytes[12] = 0x02;
        bytes[13] = (byte)((esPid >> 8) & 0x1F);
        bytes[14] = (byte)esPid;
        return new PMT(bytes, pmtPid);
    }

    private static NIT Nit(byte tableId, byte sectionNumber = 0, byte lastSectionNumber = 0)
    {
        var bytes = new byte[16];
        bytes[0] = tableId;
        bytes[1] = 0xB0;
        bytes[2] = 13;
        bytes[5] = 0x01;
        bytes[6] = sectionNumber;
        bytes[7] = lastSectionNumber;
        return new NIT(bytes);
    }

    private static SDT Sdt(byte tableId)
    {
        var bytes = new byte[15];
        bytes[0] = tableId;
        bytes[1] = 0xB0;
        bytes[2] = 12;
        bytes[5] = 0x01;
        return new SDT(bytes);
    }

    private static EIT Eit(byte tableId, ushort serviceId, byte sectionNumber)
    {
        var bytes = new byte[18];
        bytes[0] = tableId;
        bytes[1] = 0xB0;
        bytes[2] = 15;
        bytes[3] = (byte)(serviceId >> 8);
        bytes[4] = (byte)serviceId;
        bytes[5] = 0x01;
        bytes[6] = sectionNumber;
        bytes[13] = tableId;
        return new EIT(bytes);
    }

    private static CAT Cat()
    {
        var bytes = new byte[12];
        bytes[0] = 0x01;
        bytes[1] = 0xB0;
        bytes[2] = 9;
        bytes[5] = 0x01;
        return new CAT(bytes);
    }

    private static byte[] NitWithMalformedDescriptor()
    {
        var bytes = new byte[18];
        bytes[0] = 0x40;
        bytes[1] = 0xB0;
        bytes[2] = 15;
        bytes[5] = 0xC1;
        bytes[9] = 2;
        bytes[10] = 0x40;
        bytes[11] = 5; // Descriptor body exceeds the declared two-byte descriptor loop.
        bytes[12] = 0xF0;
        var crc = Utils.GetCRC32(bytes.AsSpan(0, 14));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(14), crc);
        return bytes;
    }
}
