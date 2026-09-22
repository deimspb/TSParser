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

using TSParser.Tables.DvbTables;
using TSParser.TransportStream;

namespace TSParser.Analysis;

/// <summary>
/// ETSI TR 101 290 V1.4.1 monitor for the outer MPEG-TS.
/// Interval checks use the PCR of the PMT PCR PID, or the first PID that carries a PCR until a PMT arrives.
/// PCR accuracy, PTS, and the T-STD buffer model are not measured.
/// </summary>
public sealed class Tr101290Monitor
{
    private readonly ulong _pidTimeoutTicks;
    private readonly Dictionary<IndicatorKey, IndicatorSlot> _slots = new();
    private readonly Dictionary<ushort, ContinuityState> _continuity = new();
    private readonly Dictionary<ushort, PcrState> _pcr = new();
    private readonly Dictionary<byte, SectionMark> _sections = new();
    private readonly Dictionary<ushort, PidWatch> _referenced = new();
    private readonly Dictionary<ushort, SectionMark> _unreferenced = new();
    private readonly Dictionary<(byte TableId, ushort ServiceId), EitPfMark> _eitPf = new();
    private readonly HashSet<ushort> _pmtPids = new();
    private readonly HashSet<ushort> _referencedPids = new();
    private readonly HashSet<ushort> _discontinuityPending = new();

    private ulong _packetNumber;
    private ulong _lastPacketNumber;
    private int _consecutiveGoodSync;
    private int _consecutiveBadSync;
    private ushort? _clockPid;
    private ulong? _now;
    private bool _clockLocked;
    private ulong _lockedAt;
    private bool _catSeen;
    private readonly SectionMark _patPackets = new();
    private readonly Dictionary<ushort, PmtWatch> _pmtSections = new();

    public Tr101290Monitor(Tr101290Options? options = null)
    {
        options ??= Tr101290Options.Enable;
        _pidTimeoutTicks = Tr101290Limits.Ticks(options.PidErrorTimeout);
    }

    public event Action<Tr101290Event>? OnEvent;

    public void Reset()
    {
        _slots.Clear();
        _continuity.Clear();
        _pcr.Clear();
        _sections.Clear();
        _referenced.Clear();
        _unreferenced.Clear();
        _eitPf.Clear();
        _pmtPids.Clear();
        _referencedPids.Clear();
        _discontinuityPending.Clear();
        _pmtSections.Clear();
        _patPackets.Reset();
        _packetNumber = 0;
        _lastPacketNumber = 0;
        _consecutiveGoodSync = 0;
        _consecutiveBadSync = 0;
        _clockPid = null;
        _now = null;
        _clockLocked = false;
        _lockedAt = 0;
        _catSeen = false;
    }

    public Tr101290IndicatorState GetState(Tr101290Indicator indicator, ushort? pid = null)
    {
        if (!_slots.TryGetValue(Key(indicator, pid), out var slot) || !slot.Published)
            return Tr101290IndicatorState.NotMeasured;

        return slot.Reasons == Tr101290Fault.None
            ? Tr101290IndicatorState.Ok
            : Tr101290IndicatorState.Error;
    }

    public ulong GetCount(Tr101290Indicator indicator, ushort? pid = null)
    {
        return _slots.TryGetValue(Key(indicator, pid), out var slot) ? slot.Count : 0;
    }

    public void ObserveMissingSync()
    {
        StampPacket();
        _consecutiveGoodSync = 0;
        _consecutiveBadSync++;
        Fault(
            Tr101290Indicator.SyncByteError,
            pid: null,
            Tr101290Fault.Continuity,
            "sync byte is not 0x47",
            countEach: true);

        if (_consecutiveBadSync >= 2)
        {
            Fault(
                Tr101290Indicator.TsSyncLoss,
                pid: null,
                Tr101290Fault.Missing,
                "two or more consecutive sync bytes are incorrect",
                countEach: false);
        }
    }

    public void ObserveGoodSync()
    {
        StampPacket();
        NoteGoodSync();
    }

    public void ObservePacket(TsPacket packet)
    {
        StampPacket();
        NoteGoodSync();

        if (packet.Pid == 0xFFFF)
            return;

        if (packet.TransportErrorIndicator)
        {
            NotePidPresence(packet.Pid);
            Fault(
                Tr101290Indicator.TransportError,
                packet.Pid,
                Tr101290Fault.Transport,
                "transport_error_indicator is set",
                countEach: true);
            return;
        }

        Heal(Tr101290Indicator.TransportError, packet.Pid, Tr101290Fault.Transport, "transport_error_indicator clear", surfaceOk: false);

        if (packet.Pid == 0x1FFF)
            return;

        NotePidPresence(packet.Pid);
        CheckContinuity(packet);
        CheckScrambling(packet);
        CheckPcr(packet);
        CheckTableId(packet);

        if (packet.Pid == 0x0000)
            NotePatPacket();
    }

    public void ObserveCrcError(ushort pid, byte tableId)
    {
        Fault(
            Tr101290Indicator.CrcError,
            pid,
            Tr101290Fault.Crc,
            $"CRC error in table_id 0x{tableId:X2}",
            countEach: true);
    }

    public void ObservePat(PAT pat)
    {
        NoteValidSection(pat.TablePid);
        _pmtPids.Clear();
        foreach (var record in pat.PatRecords)
        {
            if (record.ProgramNumber == 0)
                continue;

            _pmtPids.Add(record.Pid);
            var watch = Pmt(record.Pid);
            if (!watch.Announced.Seen)
                StampArrival(watch.Announced);
        }

        DropNewlyReferenced();
    }

    public void ObservePmt(PMT pmt)
    {
        NoteValidSection(pmt.TablePid);
        NotePeriodic(Pmt(pmt.TablePid).Section, Tr101290Indicator.PmtError2, pmt.TablePid, countsAsSiRepetition: false, applyMinimum: false);

        if (pmt.PcrPid != 0x1FFF)
        {
            ActivatePcr(pmt.PcrPid);
            ReferencePid(pmt.PcrPid);
            AdoptPmtClock(pmt.PcrPid);
        }

        foreach (var es in pmt.EsInfoList)
            ReferencePid(es.ElementaryPid);

        DropNewlyReferenced();
    }

    public void ObserveCat(CAT cat)
    {
        if (cat.TableId != 0x01)
            return;

        NoteValidSection(cat.TablePid);
        _catSeen = true;
        Heal(Tr101290Indicator.CatError, pid: null, Tr101290Fault.Missing, "CAT present", surfaceOk: true);
        Heal(Tr101290Indicator.CatError, pid: null, Tr101290Fault.TableId, "CAT table_id 0x01", surfaceOk: true);
    }

    public void ObserveNit(NIT nit)
    {
        NoteValidSection(nit.TablePid);
        if (nit.TableId == 0x40)
            NotePeriodic(Section(0x40), Tr101290Indicator.NitActualError, nit.TablePid, countsAsSiRepetition: true);
        else if (nit.TableId == 0x41)
            NotePeriodic(Section(0x41), Tr101290Indicator.NitOtherError, nit.TablePid, countsAsSiRepetition: true);
    }

    public void ObserveSdt(SDT sdt)
    {
        NoteValidSection(sdt.TablePid);
        if (sdt.TableId == 0x42)
            NotePeriodic(Section(0x42), Tr101290Indicator.SdtActualError, sdt.TablePid, countsAsSiRepetition: true);
        else if (sdt.TableId == 0x46)
            NotePeriodic(Section(0x46), Tr101290Indicator.SdtOtherError, sdt.TablePid, countsAsSiRepetition: true);
    }

    public void ObserveBat(BAT bat)
    {
        NoteValidSection(bat.TablePid);
        NotePeriodic(Section(bat.TableId), indicator: null, bat.TablePid, countsAsSiRepetition: true);
    }

    public void ObserveEit(EIT eit)
    {
        NoteValidSection(eit.TablePid);
        Tr101290Indicator? indicator = eit.TableId switch
        {
            0x4E => Tr101290Indicator.EitActualError,
            0x4F => Tr101290Indicator.EitOtherError,
            _ => null,
        };
        NotePeriodic(Section(eit.TableId), indicator, eit.TablePid, countsAsSiRepetition: true);

        if (eit.TableId is 0x4E or 0x4F && eit.SectionNumber is 0 or 1)
            NoteEitPf(eit.TableId, eit.ServiceId, eit.SectionNumber);
    }

    public void ObserveTdt(TDT tdt)
    {
        NotePeriodic(Section(0x70), Tr101290Indicator.TdtError, tdt.TablePid, countsAsSiRepetition: true);
    }

    public void ObserveTot(TOT tot)
    {
        NoteValidSection(tot.TablePid);
        NotePeriodic(Section(0x73), indicator: null, tot.TablePid, countsAsSiRepetition: true);
    }

    private void StampPacket() => _lastPacketNumber = _packetNumber++;

    private void NoteGoodSync()
    {
        _consecutiveBadSync = 0;
        _consecutiveGoodSync++;
        Heal(Tr101290Indicator.SyncByteError, pid: null, Tr101290Fault.Continuity, "sync byte is 0x47", surfaceOk: true);
        if (_consecutiveGoodSync >= 5)
        {
            Heal(
                Tr101290Indicator.TsSyncLoss,
                pid: null,
                Tr101290Fault.Missing,
                "five consecutive sync bytes received",
                surfaceOk: true);
        }
    }

    private void CheckContinuity(TsPacket packet)
    {
        var hasPayload = packet.AdaptationFieldControl is 0b01 or 0b11;
        var adaptationOnly = packet.AdaptationFieldControl == 0b10;
        if (!hasPayload && !adaptationOnly)
            return;

        var discontinuity = packet.HasAdaptationField && packet.Adaptation_field.DiscontinuityIndicator;
        if (!_continuity.TryGetValue(packet.Pid, out var state))
        {
            _continuity[packet.Pid] = new ContinuityState
            {
                HasLast = true,
                Last = packet.ContinuityCounter,
                Copies = hasPayload ? 1 : 0,
            };
            return;
        }

        if (discontinuity)
        {
            state.Last = packet.ContinuityCounter;
            state.Copies = hasPayload ? 1 : 0;
            Heal(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "discontinuity_indicator set", surfaceOk: false);
            return;
        }

        if (!hasPayload)
        {
            if (packet.ContinuityCounter != state.Last)
            {
                Fault(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "continuity counter changed without payload", countEach: true);
            }
            else
            {
                Heal(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "continuity counter held without payload", surfaceOk: false);
            }

            state.Last = packet.ContinuityCounter;
            state.Copies = 0;
            return;
        }

        if (packet.ContinuityCounter == state.Last)
        {
            state.Copies++;
            if (state.Copies > 2)
            {
                Fault(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "packet repeated more than twice", countEach: true);
            }
            else
            {
                Heal(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "duplicate packet", surfaceOk: false);
            }

            return;
        }

        var expected = (byte)((state.Last + 1) & 0x0F);
        var broken = packet.ContinuityCounter != expected;
        state.Last = packet.ContinuityCounter;
        state.Copies = 1;
        if (broken)
        {
            Fault(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "continuity counter is not sequential", countEach: true);
        }
        else
        {
            Heal(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "continuity counter sequential", surfaceOk: false);
        }
    }

    private void CheckScrambling(TsPacket packet)
    {
        if (packet.TransportScramblingControl != 0)
        {
            if (packet.Pid == 0x0000)
            {
                Fault(Tr101290Indicator.PatError2, pid: null, Tr101290Fault.Scrambling, "PID 0x0000 is scrambled", countEach: false);
            }

            if (_pmtPids.Contains(packet.Pid))
            {
                Fault(Tr101290Indicator.PmtError2, packet.Pid, Tr101290Fault.Scrambling, "PMT PID is scrambled", countEach: false);
            }

            if (!_catSeen)
            {
                Fault(Tr101290Indicator.CatError, pid: null, Tr101290Fault.Missing, "scrambled packet and no CAT", countEach: false);
            }

            return;
        }

        if (packet.Pid == 0x0000)
            Heal(Tr101290Indicator.PatError2, pid: null, Tr101290Fault.Scrambling, "PID 0x0000 is not scrambled", surfaceOk: false);

        if (_pmtPids.Contains(packet.Pid))
            Heal(Tr101290Indicator.PmtError2, packet.Pid, Tr101290Fault.Scrambling, "PMT PID is not scrambled", surfaceOk: false);
    }

    private void CheckPcr(TsPacket packet)
    {
        var hasPcr = packet.HasAdaptationField && packet.Adaptation_field.PCRFlag;
        var discontinuity = packet.HasAdaptationField && packet.Adaptation_field.DiscontinuityIndicator;
        if (discontinuity)
            _discontinuityPending.Add(packet.Pid);

        if (!hasPcr)
            return;

        var flagged = _discontinuityPending.Remove(packet.Pid) || discontinuity;
        if (!_pcr.TryGetValue(packet.Pid, out var state))
        {
            state = new PcrState();
            _pcr[packet.Pid] = state;
        }

        var pcr = packet.Adaptation_field.PcrValue;
        var hasDelta = state.HasLast;
        var delta = hasDelta ? Tr101290Limits.ForwardDelta(pcr, state.Last) : 0UL;
        state.HasLast = true;
        state.Last = pcr;

        if (state.Active && hasDelta)
            EvaluatePcrErrors(packet.Pid, delta, flagged);

        if (_clockPid is null)
            _clockPid = packet.Pid;

        if (packet.Pid != _clockPid)
            return;

        if (flagged && hasDelta)
            UpdateClock(pcr, rebase: true);
        else
            UpdateClock(pcr, rebase: false);
    }

    private void EvaluatePcrErrors(ushort pid, ulong delta, bool discontinuity)
    {
        if (discontinuity)
        {
            Heal(Tr101290Indicator.PcrRepetitionError, pid, Tr101290Fault.Interval, "PCR discontinuity flagged", surfaceOk: false);
        }
        else if (delta > Tr101290Limits.Ms40)
        {
            Fault(Tr101290Indicator.PcrRepetitionError, pid, Tr101290Fault.Interval, "PCR interval greater than 40 ms", countEach: false);
        }
        else
        {
            Heal(Tr101290Indicator.PcrRepetitionError, pid, Tr101290Fault.Interval, "PCR interval within 40 ms", surfaceOk: true);
        }

        if (!discontinuity && delta > Tr101290Limits.Ms100)
        {
            Fault(
                Tr101290Indicator.PcrDiscontinuityIndicatorError,
                pid,
                Tr101290Fault.Discontinuity,
                "PCR discontinuity without discontinuity_indicator",
                countEach: false);
        }
        else
        {
            Heal(
                Tr101290Indicator.PcrDiscontinuityIndicatorError,
                pid,
                Tr101290Fault.Discontinuity,
                "PCR step matches discontinuity_indicator",
                surfaceOk: false);
        }

        if (discontinuity && delta <= Tr101290Limits.Ms100)
        {
            Fault(
                Tr101290Indicator.PcrDiscontinuityIndicatorError,
                pid,
                Tr101290Fault.FalseDiscontinuity,
                "discontinuity_indicator set without a PCR break greater than 100 ms",
                countEach: false);
        }
        else
        {
            Heal(
                Tr101290Indicator.PcrDiscontinuityIndicatorError,
                pid,
                Tr101290Fault.FalseDiscontinuity,
                "discontinuity_indicator matches the PCR step",
                surfaceOk: true);
        }
    }

    private void CheckTableId(TsPacket packet)
    {
        if (!TryPeekTableId(packet, out var tableId))
            return;

        if (IsAllowedTableId(packet.Pid, tableId))
        {
            ClearUnexpectedTable(packet.Pid);
            if (packet.Pid == 0x0013 && tableId == 0x71)
                NotePeriodic(Section(0x71), Tr101290Indicator.RstError, packet.Pid, countsAsSiRepetition: false);

            return;
        }

        var indicator = TableIndicator(packet.Pid);
        if (indicator is not Tr101290Indicator known)
            return;

        var pid = known is Tr101290Indicator.PatError2 or Tr101290Indicator.CatError ? (ushort?)null : packet.Pid;
        Fault(known, pid, Tr101290Fault.TableId, $"unexpected table_id 0x{tableId:X2}", countEach: false);
    }

    private void ClearUnexpectedTable(ushort pid)
    {
        var indicator = TableIndicator(pid);
        if (indicator is not Tr101290Indicator known)
            return;

        var keyPid = known is Tr101290Indicator.PatError2 or Tr101290Indicator.CatError ? (ushort?)null : pid;
        Heal(known, keyPid, Tr101290Fault.TableId, "table_id is allowed", surfaceOk: false);
    }

    private Tr101290Indicator? TableIndicator(ushort pid)
    {
        if (pid == 0x0000) return Tr101290Indicator.PatError2;
        if (pid == 0x0001) return Tr101290Indicator.CatError;
        if (pid == 0x0010) return Tr101290Indicator.NitActualError;
        if (pid == 0x0011) return Tr101290Indicator.SdtActualError;
        if (pid == 0x0012) return Tr101290Indicator.EitActualError;
        if (pid == 0x0013) return Tr101290Indicator.RstError;
        if (pid == 0x0014) return Tr101290Indicator.TdtError;
        if (_pmtPids.Contains(pid)) return Tr101290Indicator.PmtError2;
        return null;
    }

    private bool IsAllowedTableId(ushort pid, byte tableId)
    {
        if (pid != 0x0000 && tableId == 0x72)
            return true;

        return pid switch
        {
            0x0000 => tableId == 0x00,
            0x0001 => tableId == 0x01,
            0x0010 => tableId is 0x40 or 0x41,
            0x0011 => tableId is 0x42 or 0x46 or 0x4A,
            0x0012 => tableId is >= 0x4E and <= 0x6F,
            0x0013 => tableId == 0x71,
            0x0014 => tableId is 0x70 or 0x73,
            _ => _pmtPids.Contains(pid) && tableId == 0x02,
        };
    }

    private static bool TryPeekTableId(TsPacket packet, out byte tableId)
    {
        tableId = 0;
        if (!packet.PayloadUnitStartIndicator || packet.TransportScramblingControl != 0 || packet.Payload.Length < 2)
            return false;

        var index = 1 + packet.Payload[0];
        if (index >= packet.Payload.Length)
            return false;

        tableId = packet.Payload[index];
        return true;
    }

    private void NotePatPacket()
    {
        StampArrival(_patPackets);
        if (_clockLocked)
            Heal(Tr101290Indicator.PatError2, pid: null, Tr101290Fault.Interval, "PID 0x0000 present", surfaceOk: true);
    }

    private void NotePidPresence(ushort pid)
    {
        if (_referenced.TryGetValue(pid, out var watch))
        {
            StampArrival(watch.LastPacket);
            watch.Seen = true;
            Heal(Tr101290Indicator.PidError, pid, Tr101290Fault.Missing, "referenced PID present", surfaceOk: false);
        }

        if (pid <= 0x001F || _pmtPids.Contains(pid) || _referencedPids.Contains(pid))
        {
            if (_unreferenced.Remove(pid))
                Heal(Tr101290Indicator.UnreferencedPid, pid, Tr101290Fault.Unreferenced, "PID is referenced", surfaceOk: false);

            return;
        }

        if (!_unreferenced.TryGetValue(pid, out var mark))
        {
            mark = new SectionMark();
            _unreferenced[pid] = mark;
        }

        if (!mark.Seen)
            StampArrival(mark);
    }

    private void ReferencePid(ushort pid)
    {
        _referencedPids.Add(pid);
        if (!_referenced.TryGetValue(pid, out var watch))
        {
            watch = new PidWatch();
            _referenced[pid] = watch;
        }

        if (!watch.Announced.Seen)
            StampArrival(watch.Announced);
    }

    private void ActivatePcr(ushort pid)
    {
        if (!_pcr.TryGetValue(pid, out var state))
        {
            state = new PcrState();
            _pcr[pid] = state;
        }

        state.Active = true;
    }

    private void AdoptPmtClock(ushort pid)
    {
        if (_clockPid == pid)
            return;

        _clockPid = pid;
        SuspendIntervals();
    }

    private void DropNewlyReferenced()
    {
        foreach (var pid in _unreferenced.Keys.Where(IsReferencedNow).ToArray())
        {
            _unreferenced.Remove(pid);
            Heal(Tr101290Indicator.UnreferencedPid, pid, Tr101290Fault.Unreferenced, "PID is referenced", surfaceOk: false);
        }
    }

    private bool IsReferencedNow(ushort pid) =>
        pid <= 0x001F || pid == 0x1FFF || _pmtPids.Contains(pid) || _referencedPids.Contains(pid);

    private void NoteValidSection(ushort pid)
    {
        Heal(Tr101290Indicator.CrcError, pid, Tr101290Fault.Crc, "section CRC ok", surfaceOk: false);
    }

    private void NotePeriodic(SectionMark mark, Tr101290Indicator? indicator, ushort pid, bool countsAsSiRepetition, bool applyMinimum = true)
    {
        if (applyMinimum && _clockLocked && _now is ulong now && mark.HasAt && now != mark.At)
        {
            var delta = Tr101290Limits.ForwardDelta(now, mark.At);
            if (delta < Tr101290Limits.Ms25)
            {
                if (indicator is Tr101290Indicator specific)
                {
                    Fault(specific, PidFor(specific, pid), Tr101290Fault.Repetition, "section interval less than 25 ms", countEach: false);
                }

                if (countsAsSiRepetition)
                {
                    Fault(Tr101290Indicator.SiRepetitionError, pid, Tr101290Fault.Repetition, "SI section interval less than 25 ms", countEach: false);
                }
            }
            else
            {
                if (indicator is Tr101290Indicator specific)
                {
                    Heal(specific, PidFor(specific, pid), Tr101290Fault.Repetition, "section interval at least 25 ms", surfaceOk: true);
                }

                if (countsAsSiRepetition)
                {
                    Heal(Tr101290Indicator.SiRepetitionError, pid, Tr101290Fault.Repetition, "SI section interval at least 25 ms", surfaceOk: true);
                }
            }
        }

        StampArrival(mark);
        if (indicator is Tr101290Indicator arrived && _clockLocked)
            Heal(arrived, PidFor(arrived, pid), Tr101290Fault.Interval, "section present", surfaceOk: true);
    }

    private void NoteEitPf(byte tableId, ushort serviceId, byte sectionNumber)
    {
        var key = (tableId, serviceId);
        if (!_eitPf.TryGetValue(key, out var mark))
        {
            mark = new EitPfMark();
            _eitPf[key] = mark;
        }

        var slot = sectionNumber == 0 ? mark.Section0 : mark.Section1;
        StampArrival(slot);
        EvaluateEitPf(tableId, serviceId, mark);
    }

    private void EvaluateEitPf(byte tableId, ushort serviceId, EitPfMark mark)
    {
        if (!_clockLocked || _now is not ulong now)
            return;

        var fresh0 = mark.Section0.HasAt && Tr101290Limits.ForwardDelta(now, mark.Section0.At) <= Tr101290Limits.Sec2;
        var fresh1 = mark.Section1.HasAt && Tr101290Limits.ForwardDelta(now, mark.Section1.At) <= Tr101290Limits.Sec2;
        var pid = (ushort)0x0012;
        if (fresh0 && fresh1)
        {
            Heal(Tr101290Indicator.EitPfError, pid, Tr101290Fault.Pair, "EIT P/F sections 0 and 1 are both present", surfaceOk: true);
            return;
        }

        var aged = (mark.Section0.HasAt && !fresh0) || (mark.Section1.HasAt && !fresh1);
        if (aged && (mark.Section0.Seen || mark.Section1.Seen))
        {
            Fault(
                Tr101290Indicator.EitPfError,
                pid,
                Tr101290Fault.Pair,
                $"EIT P/F table 0x{tableId:X2} service {serviceId} is missing section 0 or 1",
                countEach: false);
        }
    }

    private void UpdateClock(ulong pcr, bool rebase)
    {
        if (!_clockLocked)
        {
            _now = pcr;
            _clockLocked = true;
            _lockedAt = pcr;
            StampPending(pcr);
            return;
        }

        _now = pcr;
        if (rebase)
        {
            _lockedAt = pcr;
            Restamp(pcr);
            return;
        }

        CheckTimeouts();
    }

    private void CheckTimeouts()
    {
        if (_now is not ulong now)
            return;

        var sinceLock = Tr101290Limits.ForwardDelta(now, _lockedAt);
        if (!_patPackets.HasAt || Tr101290Limits.ForwardDelta(now, _patPackets.At) > Tr101290Limits.Ms500)
        {
            if (sinceLock > Tr101290Limits.Ms500)
                Fault(Tr101290Indicator.PatError2, pid: null, Tr101290Fault.Interval, "PID 0x0000 missing for more than 500 ms", countEach: false);
        }
        else
        {
            Heal(Tr101290Indicator.PatError2, pid: null, Tr101290Fault.Interval, "PID 0x0000 present", surfaceOk: true);
        }

        foreach (var (pid, watch) in _pmtSections)
        {
            var sectionFresh = watch.Section.HasAt
                && Tr101290Limits.ForwardDelta(now, watch.Section.At) <= Tr101290Limits.Ms500;
            if (sectionFresh)
            {
                Heal(Tr101290Indicator.PmtError2, pid, Tr101290Fault.Interval, "PMT section present", surfaceOk: true);
                continue;
            }

            var basis = watch.Section.HasAt ? watch.Section : watch.Announced;
            if (basis.HasAt && Tr101290Limits.ForwardDelta(now, basis.At) > Tr101290Limits.Ms500)
                Fault(Tr101290Indicator.PmtError2, pid, Tr101290Fault.Interval, "PMT section missing for more than 500 ms", countEach: false);
        }

        foreach (var (pid, watch) in _referenced)
        {
            var basis = watch.Seen && watch.LastPacket.HasAt ? watch.LastPacket : watch.Announced;
            if (!basis.HasAt)
                continue;

            if (Tr101290Limits.ForwardDelta(now, basis.At) > _pidTimeoutTicks)
                Fault(Tr101290Indicator.PidError, pid, Tr101290Fault.Missing, "referenced PID missing", countEach: false);
            else if (watch.Seen)
                Heal(Tr101290Indicator.PidError, pid, Tr101290Fault.Missing, "referenced PID present", surfaceOk: false);
        }

        foreach (var (pid, mark) in _unreferenced)
        {
            if (IsReferencedNow(pid))
                continue;

            if (mark.HasAt && Tr101290Limits.ForwardDelta(now, mark.At) > Tr101290Limits.Ms500)
                Fault(Tr101290Indicator.UnreferencedPid, pid, Tr101290Fault.Unreferenced, "PID is not referenced by PMT", countEach: false);
        }

        CheckRequiredSection(0x40, Tr101290Indicator.NitActualError, Tr101290Limits.Sec10, required: true, pid: 0x0010);
        CheckRequiredSection(0x41, Tr101290Indicator.NitOtherError, Tr101290Limits.Sec10, required: false, pid: 0x0010);
        CheckRequiredSection(0x42, Tr101290Indicator.SdtActualError, Tr101290Limits.Sec2, required: true, pid: 0x0011);
        CheckRequiredSection(0x46, Tr101290Indicator.SdtOtherError, Tr101290Limits.Sec10, required: false, pid: 0x0011);
        CheckRequiredSection(0x4E, Tr101290Indicator.EitActualError, Tr101290Limits.Sec2, required: true, pid: 0x0012);
        CheckRequiredSection(0x4F, Tr101290Indicator.EitOtherError, Tr101290Limits.Sec10, required: false, pid: 0x0012);
        CheckRequiredSection(0x70, Tr101290Indicator.TdtError, Tr101290Limits.Sec30, required: true, pid: 0x0014);

        foreach (var pair in _eitPf)
            EvaluateEitPf(pair.Key.TableId, pair.Key.ServiceId, pair.Value);
    }

    private void CheckRequiredSection(byte tableId, Tr101290Indicator indicator, ulong max, bool required, ushort pid)
    {
        if (_now is not ulong now)
            return;

        _sections.TryGetValue(tableId, out var mark);
        if (mark is not { Seen: true })
        {
            if (required && Tr101290Limits.ForwardDelta(now, _lockedAt) > max)
                Fault(indicator, pid, Tr101290Fault.Interval, "section missing", countEach: false);

            return;
        }

        if (!mark.HasAt)
            return;

        if (Tr101290Limits.ForwardDelta(now, mark.At) > max)
            Fault(indicator, pid, Tr101290Fault.Interval, "section interval too long", countEach: false);
        else
            Heal(indicator, pid, Tr101290Fault.Interval, "section interval within limit", surfaceOk: true);
    }

    private void SuspendIntervals()
    {
        _clockLocked = false;
        _now = null;
        Suspend(_patPackets);
        foreach (var watch in _pmtSections.Values)
        {
            Suspend(watch.Announced);
            Suspend(watch.Section);
        }
        foreach (var mark in _sections.Values)
            Suspend(mark);
        foreach (var mark in _unreferenced.Values)
            Suspend(mark);
        foreach (var watch in _referenced.Values)
        {
            Suspend(watch.Announced);
            Suspend(watch.LastPacket);
        }

        foreach (var pf in _eitPf.Values)
        {
            Suspend(pf.Section0);
            Suspend(pf.Section1);
        }
    }

    private void StampPending(ulong now)
    {
        StampIfPending(_patPackets, now);
        foreach (var watch in _pmtSections.Values)
        {
            StampIfPending(watch.Announced, now);
            StampIfPending(watch.Section, now);
        }
        foreach (var mark in _sections.Values)
            StampIfPending(mark, now);
        foreach (var mark in _unreferenced.Values)
            StampIfPending(mark, now);
        foreach (var watch in _referenced.Values)
        {
            StampIfPending(watch.Announced, now);
            StampIfPending(watch.LastPacket, now);
        }

        foreach (var pf in _eitPf.Values)
        {
            StampIfPending(pf.Section0, now);
            StampIfPending(pf.Section1, now);
        }
    }

    private void Restamp(ulong now)
    {
        Restamp(_patPackets, now);
        foreach (var watch in _pmtSections.Values)
        {
            Restamp(watch.Announced, now);
            Restamp(watch.Section, now);
        }
        foreach (var mark in _sections.Values)
            Restamp(mark, now);
        foreach (var mark in _unreferenced.Values)
            Restamp(mark, now);
        foreach (var watch in _referenced.Values)
        {
            Restamp(watch.Announced, now);
            Restamp(watch.LastPacket, now);
        }

        foreach (var pf in _eitPf.Values)
        {
            Restamp(pf.Section0, now);
            Restamp(pf.Section1, now);
        }

        _lockedAt = now;
    }

    private static void Suspend(SectionMark mark)
    {
        if (!mark.Seen && !mark.HasAt)
            return;

        mark.Pending = true;
        mark.HasAt = false;
    }

    private static void StampIfPending(SectionMark mark, ulong now)
    {
        if (!mark.Pending && mark.HasAt)
            return;

        if (!mark.Seen && !mark.Pending)
            return;

        mark.At = now;
        mark.HasAt = true;
        mark.Pending = false;
        mark.Seen = true;
    }

    private static void Restamp(SectionMark mark, ulong now)
    {
        if (!mark.Seen && !mark.HasAt)
            return;

        mark.At = now;
        mark.HasAt = true;
        mark.Pending = false;
        mark.Seen = true;
    }

    private void StampArrival(SectionMark mark)
    {
        mark.Seen = true;
        if (_now is ulong now)
        {
            mark.At = now;
            mark.HasAt = true;
            mark.Pending = false;
            return;
        }

        mark.Pending = true;
        mark.HasAt = false;
    }

    private SectionMark Section(byte tableId)
    {
        if (!_sections.TryGetValue(tableId, out var mark))
        {
            mark = new SectionMark();
            _sections[tableId] = mark;
        }

        return mark;
    }

    private PmtWatch Pmt(ushort pid)
    {
        if (!_pmtSections.TryGetValue(pid, out var watch))
        {
            watch = new PmtWatch();
            _pmtSections[pid] = watch;
        }

        return watch;
    }

    private static ushort? PidFor(Tr101290Indicator indicator, ushort pid) =>
        indicator is Tr101290Indicator.PatError2 or Tr101290Indicator.CatError ? null : pid;

    private void Fault(Tr101290Indicator indicator, ushort? pid, Tr101290Fault reason, string detail, bool countEach)
    {
        var key = Key(indicator, pid);
        if (!_slots.TryGetValue(key, out var slot))
        {
            slot = new IndicatorSlot();
            _slots[key] = slot;
        }

        var wasError = slot.Reasons != Tr101290Fault.None;
        slot.Reasons |= reason;
        slot.Detail = detail;
        slot.PacketNumber = _lastPacketNumber;
        slot.Published = true;
        if (wasError && !countEach)
            return;

        slot.Count++;
        if (!wasError || slot.Count <= 3 || slot.Count % 100 == 0)
            Emit(key, slot, Tr101290EventKind.Raised);
    }

    private void Heal(Tr101290Indicator indicator, ushort? pid, Tr101290Fault reason, string detail, bool surfaceOk)
    {
        var key = Key(indicator, pid);
        if (!_slots.TryGetValue(key, out var slot))
        {
            if (!surfaceOk)
                return;

            slot = new IndicatorSlot();
            _slots[key] = slot;
        }

        var wasError = slot.Reasons != Tr101290Fault.None;
        slot.Reasons &= ~reason;
        if (slot.Reasons != Tr101290Fault.None)
            return;

        if (!wasError && (!surfaceOk || slot.Published))
            return;

        slot.Detail = detail;
        slot.PacketNumber = _lastPacketNumber;
        slot.Published = true;
        Emit(key, slot, Tr101290EventKind.Cleared);
    }

    private void Emit(IndicatorKey key, IndicatorSlot slot, Tr101290EventKind kind)
    {
        OnEvent?.Invoke(new Tr101290Event(
            Tr101290Names.PriorityOf(key.Indicator),
            key.Indicator,
            kind,
            key.HasPid ? key.Pid : null,
            slot.PacketNumber,
            slot.Count,
            slot.Detail));
    }

    private static IndicatorKey Key(Tr101290Indicator indicator, ushort? pid) =>
        new(indicator, pid ?? 0, pid.HasValue);

    private readonly record struct IndicatorKey(Tr101290Indicator Indicator, ushort Pid, bool HasPid);

    [Flags]
    private enum Tr101290Fault : uint
    {
        None = 0,
        Interval = 1 << 0,
        Scrambling = 1 << 1,
        TableId = 1 << 2,
        Repetition = 1 << 3,
        Continuity = 1 << 4,
        Transport = 1 << 5,
        Crc = 1 << 6,
        Missing = 1 << 7,
        Discontinuity = 1 << 8,
        FalseDiscontinuity = 1 << 9,
        Unreferenced = 1 << 10,
        Pair = 1 << 11,
    }

    private sealed class IndicatorSlot
    {
        public Tr101290Fault Reasons;
        public ulong Count;
        public ulong PacketNumber;
        public string Detail = "";
        public bool Published;
    }

    private sealed class ContinuityState
    {
        public bool HasLast;
        public byte Last;
        public int Copies;
    }

    private sealed class PcrState
    {
        public bool Active;
        public bool HasLast;
        public ulong Last;
    }

    private sealed class SectionMark
    {
        public bool Seen;
        public bool Pending;
        public bool HasAt;
        public ulong At;

        public void Reset()
        {
            Seen = false;
            Pending = false;
            HasAt = false;
            At = 0;
        }
    }

    private sealed class PmtWatch
    {
        public SectionMark Announced { get; } = new();
        public SectionMark Section { get; } = new();
    }

    private sealed class PidWatch
    {
        public bool Seen;
        public SectionMark Announced { get; } = new();
        public SectionMark LastPacket { get; } = new();
    }

    private sealed class EitPfMark
    {
        public SectionMark Section0 { get; } = new();
        public SectionMark Section1 { get; } = new();
    }
}
