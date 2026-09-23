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

using System.Diagnostics;
using System.Buffers.Binary;
using TSParser.Service;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.TransportStream;

namespace TSParser.Analysis;

/// <summary>
/// ETSI TR 101 290 V1.4.1 monitor for the outer MPEG-TS.
/// Interval checks use a stable PMT PCR PID (or the first PCR before PMT topology is known),
/// with monotonic UDP time as fallback and a PCR-derived packet-rate estimate for file input.
/// PCR accuracy, PTS, and the T-STD buffer model are not measured.
/// </summary>
public sealed class Tr101290Monitor
{
    private readonly Tr101290ClockMode _clockMode;
    private readonly ulong _pidTimeoutTicks;
    private readonly Dictionary<IndicatorKey, IndicatorSlot> _slots = new();
    private readonly Dictionary<ushort, ContinuityState> _continuity = new();
    private readonly Dictionary<ushort, PcrState> _pcr = new();
    private readonly Dictionary<SectionKey, SectionMark> _sections = new();
    private readonly Dictionary<byte, SectionMark> _tableIdArrivals = new();
    private readonly Dictionary<ushort, PsiSectionAssembler> _sectionAssemblers = new();
    private readonly Dictionary<ushort, PidWatch> _referenced = new();
    private readonly Dictionary<ushort, SectionMark> _unreferenced = new();
    private readonly Dictionary<(byte TableId, ushort ServiceId), EitPfMark> _eitPf = new();
    private readonly HashSet<ushort> _pmtPids = new();
    private readonly HashSet<ushort> _referencedPids = new();
    private readonly HashSet<ushort> _discontinuityPending = new();
    private readonly Dictionary<byte, PAT> _patTopologySections = new();
    private readonly Dictionary<byte, uint> _patSectionCrcs = new();
    private readonly Dictionary<ushort, PmtTopology> _pmtTopologies = new();
    private readonly Dictionary<(ushort Pid, byte SectionNumber), uint> _pmtSectionCrcs = new();
    private readonly Dictionary<ushort, HashSet<ushort>> _pmtReferences = new();
    private readonly Dictionary<ushort, HashSet<SectionKey>> _siRepetitionFaults = new();
    private readonly Dictionary<(Tr101290Indicator Indicator, ushort? Pid), HashSet<SectionKey>> _indicatorRepetitionFaults = new();

    private ulong _packetNumber;
    private ulong _lastPacketNumber;
    private int _consecutiveGoodSync;
    private int _consecutiveBadSync;
    private ushort? _clockPid;
    private ulong? _now;
    private bool _clockLocked;
    private ulong _lockedAt;
    private bool _catSeen;
    private bool _syncLost;
    private long _lastMonotonicTimestamp;
    private double? _fileTicksPerPacket;
    private ulong? _lastClockPacketNumber;
    private ulong? _lastClockPcr;
    private ulong _lastClockLogical;
    private ulong _lastTimeoutCheck;
    private byte? _patTopologyVersion;
    private ushort? _patTopologyTransportStreamId;
    private byte _patTopologyLastSection;
    private readonly SectionMark _patPackets = new();
    private readonly Dictionary<ushort, PmtWatch> _pmtSections = new();

    public Tr101290Monitor(Tr101290Options? options = null)
        : this(options, Tr101290ClockMode.Push)
    {
    }

    internal Tr101290Monitor(Tr101290Options? options, Tr101290ClockMode clockMode)
    {
        options ??= Tr101290Options.Enable;
        _clockMode = clockMode;
        _pidTimeoutTicks = Tr101290Limits.Ticks(options.PidErrorTimeout);
        _lastMonotonicTimestamp = Stopwatch.GetTimestamp();
        if (_clockMode == Tr101290ClockMode.Udp)
        {
            _clockLocked = true;
            _now = 0;
        }
    }

    public event Action<Tr101290Event>? OnEvent;

    public void Reset()
    {
        _slots.Clear();
        _continuity.Clear();
        _pcr.Clear();
        _sections.Clear();
        _tableIdArrivals.Clear();
        _sectionAssemblers.Clear();
        _referenced.Clear();
        _unreferenced.Clear();
        _eitPf.Clear();
        _pmtPids.Clear();
        _referencedPids.Clear();
        _discontinuityPending.Clear();
        _patTopologySections.Clear();
        _patSectionCrcs.Clear();
        _pmtTopologies.Clear();
        _pmtSectionCrcs.Clear();
        _pmtReferences.Clear();
        _siRepetitionFaults.Clear();
        _indicatorRepetitionFaults.Clear();
        _pmtSections.Clear();
        _patPackets.Reset();
        _packetNumber = 0;
        _lastPacketNumber = 0;
        _consecutiveGoodSync = 0;
        _consecutiveBadSync = 0;
        _clockPid = null;
        _now = _clockMode == Tr101290ClockMode.Udp ? 0 : null;
        _clockLocked = _clockMode == Tr101290ClockMode.Udp;
        _lockedAt = 0;
        _catSeen = false;
        _syncLost = false;
        _lastMonotonicTimestamp = Stopwatch.GetTimestamp();
        _fileTicksPerPacket = null;
        _lastClockPacketNumber = null;
        _lastClockPcr = null;
        _lastClockLogical = 0;
        _lastTimeoutCheck = 0;
        _patTopologyVersion = null;
        _patTopologyTransportStreamId = null;
        _patTopologyLastSection = 0;
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
            if (!_syncLost)
            {
                _syncLost = true;
                _continuity.Clear();
                _sectionAssemblers.Clear();
            }

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
        var rawPacket = packet.RawPacket is { Length: > 0 } raw
            ? raw.AsSpan()
            : ReadOnlySpan<byte>.Empty;
        ObservePacket(packet, rawPacket, observeSections: true);
    }

    internal void ObservePacket(TsPacket packet, ReadOnlySpan<byte> rawPacket, bool observeSections)
    {
        StampPacket();
        NoteGoodSync();

        if (_syncLost)
            return;

        AdvanceFallbackClock();

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
        CheckContinuity(packet, rawPacket);
        CheckScrambling(packet);
        CheckPcr(packet);
        CheckTableId(packet);

        if (observeSections)
            ObserveSections(packet);
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
        if (!pat.CurrentNextIndicator)
            return;

        NotePatSection();
        UpdatePatTopology(pat);
    }

    private void UpdatePatTopology(PAT pat)
    {

        if (_patTopologyVersion != pat.VersionNumber
            || _patTopologyTransportStreamId != pat.TransportStreamId
            || _patTopologyLastSection != pat.LastSectionNumber)
        {
            _patTopologySections.Clear();
            _patSectionCrcs.Clear();
            _patTopologyVersion = pat.VersionNumber;
            _patTopologyTransportStreamId = pat.TransportStreamId;
            _patTopologyLastSection = pat.LastSectionNumber;
        }

        _patTopologySections[pat.SectionNumber] = pat;
        if (!HasAllSections(_patTopologySections.Keys, pat.LastSectionNumber))
            return;

        var desiredPmtPids = _patTopologySections.Values
            .SelectMany(section => section.PatRecords)
            .Where(record => record.ProgramNumber != 0)
            .Select(record => record.Pid)
            .ToHashSet();

        foreach (var stalePid in _pmtPids.Except(desiredPmtPids).ToArray())
            RemovePmt(stalePid);

        foreach (var pid in desiredPmtPids)
        {
            _pmtPids.Add(pid);
            var watch = Pmt(pid);
            if (!watch.Announced.Seen)
                StampArrival(watch.Announced);
        }

        DropNewlyReferenced();
    }

    public void ObservePmt(PMT pmt)
    {
        NoteValidSection(pmt.TablePid);
        if (!pmt.CurrentNextIndicator)
            return;

        NotePeriodic(Pmt(pmt.TablePid).Section(pmt.SectionNumber), Tr101290Indicator.PmtError2, pmt.TablePid, countsAsSiRepetition: false, applyMinimum: false);
        UpdatePmtTopology(pmt);
    }

    private void UpdatePmtTopology(PMT pmt)
    {

        if (!_pmtTopologies.TryGetValue(pmt.TablePid, out var topology)
            || topology.ProgramNumber != pmt.ProgramNumber
            || topology.Version != pmt.VersionNumber
            || topology.LastSectionNumber != pmt.LastSectionNumber)
        {
            topology = new PmtTopology(pmt.ProgramNumber, pmt.VersionNumber, pmt.LastSectionNumber);
            _pmtTopologies[pmt.TablePid] = topology;
            Pmt(pmt.TablePid).Sections.Clear();
            foreach (var key in _pmtSectionCrcs.Keys.Where(key => key.Pid == pmt.TablePid).ToArray())
                _pmtSectionCrcs.Remove(key);
        }

        topology.Sections[pmt.SectionNumber] = pmt;
        if (!HasAllSections(topology.Sections.Keys, pmt.LastSectionNumber))
            return;

        var references = topology.Sections.Values
            .SelectMany(section => section.EsInfoList.Select(es => es.ElementaryPid)
                .Append(section.PcrPid).Where(pid => pid != 0x1FFF))
            .ToHashSet();
        _pmtReferences[pmt.TablePid] = references;
        ReconcileReferencedPids();
        ReconcilePcrPids();

        var pcrPid = topology.Sections.Values.Select(section => section.PcrPid).FirstOrDefault(pid => pid != 0x1FFF);
        if (pcrPid != 0)
        {
            ActivatePcr(pcrPid);
            AdoptPmtClock(pcrPid);
        }

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
            NotePeriodic(Section(nit.TableId, nit.NetworkId, nit.SectionNumber), Tr101290Indicator.NitActualError, nit.TablePid, countsAsSiRepetition: true);
        else if (nit.TableId == 0x41)
            NotePeriodic(Section(nit.TableId, nit.NetworkId, nit.SectionNumber), Tr101290Indicator.NitOtherError, nit.TablePid, countsAsSiRepetition: true);
    }

    public void ObserveSdt(SDT sdt)
    {
        NoteValidSection(sdt.TablePid);
        if (sdt.TableId == 0x42)
            NotePeriodic(Section(sdt.TableId, sdt.TransportStreamId, sdt.SectionNumber), Tr101290Indicator.SdtActualError, sdt.TablePid, countsAsSiRepetition: true);
        else if (sdt.TableId == 0x46)
            NotePeriodic(Section(sdt.TableId, sdt.TransportStreamId, sdt.SectionNumber), Tr101290Indicator.SdtOtherError, sdt.TablePid, countsAsSiRepetition: true);
    }

    public void ObserveBat(BAT bat)
    {
        NoteValidSection(bat.TablePid);
        NotePeriodic(Section(bat.TableId, bat.BouquetId, bat.SectionNumber), indicator: null, bat.TablePid, countsAsSiRepetition: true);
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
        NotePeriodic(Section(eit.TableId, eit.ServiceId, eit.SectionNumber), indicator, eit.TablePid, countsAsSiRepetition: true);

        if (eit.TableId is 0x4E or 0x4F && eit.SectionNumber is 0 or 1)
            NoteEitPf(eit.TableId, eit.ServiceId, eit.SectionNumber);
    }

    public void ObserveTdt(TDT tdt)
    {
        NotePeriodic(Section(0x70, 0, 0), Tr101290Indicator.TdtError, tdt.TablePid, countsAsSiRepetition: true);
    }

    public void ObserveTot(TOT tot)
    {
        NoteValidSection(tot.TablePid);
        NotePeriodic(Section(0x73, 0, 0), indicator: null, tot.TablePid, countsAsSiRepetition: true);
    }

    private void StampPacket() => _lastPacketNumber = _packetNumber++;

    private void NoteGoodSync()
    {
        _consecutiveBadSync = 0;
        _consecutiveGoodSync++;
        Heal(Tr101290Indicator.SyncByteError, pid: null, Tr101290Fault.Continuity, "sync byte is 0x47", surfaceOk: true);
        if (_consecutiveGoodSync >= 5)
        {
            _syncLost = false;
            Heal(
                Tr101290Indicator.TsSyncLoss,
                pid: null,
                Tr101290Fault.Missing,
                "five consecutive sync bytes received",
                surfaceOk: true);
        }
    }

    private void CheckContinuity(TsPacket packet, ReadOnlySpan<byte> rawPacket)
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
                LastPacket = CopyPacket(rawPacket),
            };
            return;
        }

        if (discontinuity)
        {
            state.Last = packet.ContinuityCounter;
            state.Copies = hasPayload ? 1 : 0;
            SetLastPacket(state, rawPacket);
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

            if (packet.ContinuityCounter != state.Last)
                state.Last = packet.ContinuityCounter;
            return;
        }

        if (packet.ContinuityCounter == state.Last)
        {
            if (state.LastPacket is null || rawPacket.IsEmpty || !rawPacket.SequenceEqual(state.LastPacket))
            {
                state.Copies = 1;
                SetLastPacket(state, rawPacket);
                Fault(Tr101290Indicator.ContinuityCountError, packet.Pid, Tr101290Fault.Continuity, "same continuity counter with different packet bytes", countEach: true);
                return;
            }

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
        SetLastPacket(state, rawPacket);
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

        UpdateClockFromPcr(pcr, hasDelta ? delta : null, flagged);
    }

    private void EvaluatePcrErrors(ushort pid, ulong delta, bool discontinuity)
    {
        if (discontinuity)
        {
            Heal(Tr101290Indicator.PcrRepetitionError, pid, Tr101290Fault.Interval, "PCR discontinuity flagged", surfaceOk: false);
        }
        else if (delta > Tr101290Limits.Ms100)
        {
            Fault(Tr101290Indicator.PcrRepetitionError, pid, Tr101290Fault.Interval, "PCR interval greater than 100 ms", countEach: false);
        }
        else
        {
            Heal(Tr101290Indicator.PcrRepetitionError, pid, Tr101290Fault.Interval, "PCR interval within 100 ms", surfaceOk: true);
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

    }

    private void ObserveSections(TsPacket packet)
    {
        if (!packet.HasPayload || packet.Payload.Length == 0 || packet.TransportScramblingControl != 0)
            return;

        if (!IsMonitoredSectionPid(packet.Pid))
            return;

        if (!_sectionAssemblers.TryGetValue(packet.Pid, out var assembler))
        {
            assembler = new PsiSectionAssembler(packet.Pid);
            _sectionAssemblers[packet.Pid] = assembler;
        }

        foreach (var memory in assembler.PushPacket(packet))
            ObserveSection(packet.Pid, memory.Span);
    }

    internal void ObserveSection(ushort pid, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 3)
            return;

        var tableId = bytes[0];
        uint crc = 0;
        if (HasSectionCrc(bytes))
        {
            if (bytes.Length < 4)
                return;

            crc = BinaryPrimitives.ReadUInt32BigEndian(bytes[^4..]);
            if (Utils.GetCRC32(bytes[..^4]) != crc)
            {
                ObserveCrcError(pid, tableId);
                return;
            }

            NoteValidSection(pid);
        }

        if (!IsAllowedTableId(pid, tableId))
            return;

        try
        {
            var syntaxSection = (bytes[1] & 0x80) != 0;
            if (syntaxSection && bytes.Length < 8)
                return;

            var tableIdExtension = syntaxSection ? BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]) : (ushort)0;
            var sectionNumber = syntaxSection ? bytes[6] : (byte)0;
            switch (tableId)
            {
                case 0x00: ObservePatSection(bytes, crc); break;
                case 0x01:
                    _catSeen = true;
                    Heal(Tr101290Indicator.CatError, pid: null, Tr101290Fault.Missing, "CAT present", surfaceOk: true);
                    Heal(Tr101290Indicator.CatError, pid: null, Tr101290Fault.TableId, "CAT table_id 0x01", surfaceOk: true);
                    break;
                case 0x02: ObservePmtSection(pid, bytes, crc); break;
                case 0x40:
                    NotePeriodic(Section(tableId, tableIdExtension, sectionNumber), Tr101290Indicator.NitActualError, pid, countsAsSiRepetition: true);
                    break;
                case 0x41:
                    NotePeriodic(Section(tableId, tableIdExtension, sectionNumber), Tr101290Indicator.NitOtherError, pid, countsAsSiRepetition: true);
                    break;
                case 0x42:
                    NotePeriodic(Section(tableId, tableIdExtension, sectionNumber), Tr101290Indicator.SdtActualError, pid, countsAsSiRepetition: true);
                    break;
                case 0x46:
                    NotePeriodic(Section(tableId, tableIdExtension, sectionNumber), Tr101290Indicator.SdtOtherError, pid, countsAsSiRepetition: true);
                    break;
                case 0x4A:
                    NotePeriodic(Section(tableId, tableIdExtension, sectionNumber), indicator: null, pid, countsAsSiRepetition: true);
                    break;
                case >= 0x4E and <= 0x6F:
                    ObserveEitSection(tableId, tableIdExtension, sectionNumber, pid);
                    break;
                case 0x70: NotePeriodic(Section(0x70, 0, 0), Tr101290Indicator.TdtError, pid, countsAsSiRepetition: true); break;
                case 0x71: NotePeriodic(Section(0x71, 0, 0), Tr101290Indicator.RstError, pid, countsAsSiRepetition: false); break;
                case 0x73: NotePeriodic(Section(0x73, 0, 0), indicator: null, pid, countsAsSiRepetition: true); break;
            }
        }
        catch (Exception ex) when (ex is SectionParseException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            // A malformed section must not disturb monitoring of later sections.
        }
    }

    private static byte[]? CopyPacket(ReadOnlySpan<byte> rawPacket) =>
        rawPacket.IsEmpty ? null : rawPacket.ToArray();

    private static void SetLastPacket(ContinuityState state, ReadOnlySpan<byte> rawPacket)
    {
        if (rawPacket.IsEmpty)
        {
            state.LastPacket = null;
            return;
        }

        if (state.LastPacket is not { } destination || destination.Length != rawPacket.Length)
        {
            state.LastPacket = rawPacket.ToArray();
            return;
        }

        rawPacket.CopyTo(destination);
    }

    private void ObservePatSection(ReadOnlySpan<byte> bytes, uint crc)
    {
        var current = (bytes[5] & 0x01) != 0;
        if (!current)
            return;

        NotePatSection();
        var transportStreamId = BinaryPrimitives.ReadUInt16BigEndian(bytes[3..]);
        var version = (byte)((bytes[5] & 0x3E) >> 1);
        var sectionNumber = bytes[6];
        var lastSectionNumber = bytes[7];
        if (_patTopologyVersion != version
            || _patTopologyTransportStreamId != transportStreamId
            || _patTopologyLastSection != lastSectionNumber)
        {
            _patTopologySections.Clear();
            _patSectionCrcs.Clear();
            _patTopologyVersion = version;
            _patTopologyTransportStreamId = transportStreamId;
            _patTopologyLastSection = lastSectionNumber;
        }

        if (_patSectionCrcs.TryGetValue(sectionNumber, out var previousCrc) && previousCrc == crc)
            return;

        var pat = new PAT(bytes);
        _patSectionCrcs[sectionNumber] = crc;
        UpdatePatTopology(pat);
    }

    private void ObservePmtSection(ushort pid, ReadOnlySpan<byte> bytes, uint crc)
    {
        if ((bytes[5] & 0x01) == 0)
            return;

        var sectionNumber = bytes[6];
        NotePeriodic(Pmt(pid).Section(sectionNumber), Tr101290Indicator.PmtError2, pid, countsAsSiRepetition: false, applyMinimum: false);
        var key = (pid, sectionNumber);
        if (_pmtSectionCrcs.TryGetValue(key, out var previousCrc) && previousCrc == crc)
            return;

        var pmt = new PMT(bytes, pid);
        UpdatePmtTopology(pmt);
        _pmtSectionCrcs[key] = crc;
    }

    private void ObserveEitSection(byte tableId, ushort serviceId, byte sectionNumber, ushort pid)
    {
        Tr101290Indicator? indicator = tableId switch
        {
            0x4E => Tr101290Indicator.EitActualError,
            0x4F => Tr101290Indicator.EitOtherError,
            _ => null,
        };
        NotePeriodic(Section(tableId, serviceId, sectionNumber), indicator, pid, countsAsSiRepetition: true);
        if (tableId is 0x4E or 0x4F && sectionNumber is 0 or 1)
            NoteEitPf(tableId, serviceId, sectionNumber);
    }

    private bool IsMonitoredSectionPid(ushort pid) => pid is 0x0000 or 0x0001 or 0x0010 or 0x0011 or 0x0012 or 0x0013 or 0x0014
        || _pmtPids.Contains(pid);

    private static bool HasSectionCrc(ReadOnlySpan<byte> bytes) => bytes[0] switch
    {
        0x70 or 0x71 or 0x72 => false,
        0x73 => true,
        _ => (bytes[1] & 0x80) != 0,
    };

    private void CheckTableId(TsPacket packet)
    {
        if (!TryPeekTableId(packet, out var tableId))
            return;

        if (IsAllowedTableId(packet.Pid, tableId))
        {
            ClearUnexpectedTable(packet.Pid);
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

    private void NotePatSection()
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

    private static bool HasAllSections(IEnumerable<byte> sections, byte lastSectionNumber)
    {
        var present = sections.ToHashSet();
        return Enumerable.Range(0, lastSectionNumber + 1).All(section => present.Contains((byte)section));
    }

    private void RemovePmt(ushort pid)
    {
        _pmtPids.Remove(pid);
        _pmtSections.Remove(pid);
        _pmtTopologies.Remove(pid);
        _pmtReferences.Remove(pid);
        foreach (var key in _pmtSectionCrcs.Keys.Where(key => key.Pid == pid).ToArray())
            _pmtSectionCrcs.Remove(key);
        Heal(Tr101290Indicator.PmtError2, pid, Tr101290Fault.Interval | Tr101290Fault.Scrambling | Tr101290Fault.TableId, "PMT PID removed by current PAT", surfaceOk: false);
        ReconcileReferencedPids();
        ReconcilePcrPids();
    }

    private void ReconcileReferencedPids()
    {
        var desired = _pmtReferences.Values.SelectMany(pids => pids).ToHashSet();
        foreach (var stalePid in _referencedPids.Except(desired).ToArray())
        {
            _referencedPids.Remove(stalePid);
            _referenced.Remove(stalePid);
            if (_pcr.TryGetValue(stalePid, out var pcr))
                pcr.Active = false;
            Heal(Tr101290Indicator.PidError, stalePid, Tr101290Fault.Missing, "PID no longer referenced by current PMT", surfaceOk: false);
        }

        foreach (var pid in desired)
            ReferencePid(pid);
    }

    private void ReconcilePcrPids()
    {
        foreach (var state in _pcr.Values)
            state.Active = false;

        var desired = _pmtTopologies
            .Where(pair => _pmtReferences.ContainsKey(pair.Key))
            .SelectMany(pair => pair.Value.Sections.Values)
            .Select(section => section.PcrPid)
            .Where(pid => pid != 0x1FFF)
            .Distinct();
        foreach (var pid in desired)
            ActivatePcr(pid);
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
        if (_clockPid == pid
            || (_clockPid is ushort current && _pcr.TryGetValue(current, out var currentState) && currentState.Active))
            return;

        _clockPid = pid;
        _lastClockPcr = null;
        _lastClockPacketNumber = null;
        _lastClockLogical = _now ?? 0;
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
        var intervalMark = applyMinimum && mark.Key is SectionKey sectionKey
            ? Minimum(sectionKey.TableId)
            : mark;
        if (applyMinimum && _clockLocked && _now is ulong now && intervalMark.HasAt && now != intervalMark.At)
        {
            var delta = Tr101290Limits.ForwardDelta(now, intervalMark.At);
            if (delta < Tr101290Limits.Ms25)
            {
                if (indicator is Tr101290Indicator specific)
                {
                    AddRepetitionFault(specific, PidFor(specific, pid), intervalMark);
                    Fault(specific, PidFor(specific, pid), Tr101290Fault.Repetition, "section interval less than 25 ms", countEach: false);
                }

                if (countsAsSiRepetition)
                {
                    if (intervalMark.Key is SectionKey key)
                    {
                        if (!_siRepetitionFaults.TryGetValue(pid, out var faults))
                        {
                            faults = new HashSet<SectionKey>();
                            _siRepetitionFaults[pid] = faults;
                        }
                        faults.Add(key);
                    }
                    Fault(Tr101290Indicator.SiRepetitionError, pid, Tr101290Fault.Repetition, "SI section interval less than 25 ms", countEach: false);
                }
            }
            else
            {
                if (indicator is Tr101290Indicator specific)
                {
                    RemoveRepetitionFault(specific, PidFor(specific, pid), intervalMark);
                }

                if (countsAsSiRepetition)
                {
                    if (intervalMark.Key is SectionKey key)
                    {
                        if (_siRepetitionFaults.TryGetValue(pid, out var faults))
                            faults.Remove(key);
                    }
                    if (!_siRepetitionFaults.TryGetValue(pid, out var remaining) || remaining.Count == 0)
                        Heal(Tr101290Indicator.SiRepetitionError, pid, Tr101290Fault.Repetition, "SI section interval at least 25 ms", surfaceOk: true);
                }
            }
        }

        StampArrival(intervalMark);
        if (!ReferenceEquals(intervalMark, mark))
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
        EvaluateEitPfAggregate();
    }

    private void AddRepetitionFault(Tr101290Indicator indicator, ushort? pid, SectionMark mark)
    {
        if (mark.Key is not SectionKey key)
            return;

        var aggregateKey = (indicator, pid);
        if (!_indicatorRepetitionFaults.TryGetValue(aggregateKey, out var faults))
        {
            faults = new HashSet<SectionKey>();
            _indicatorRepetitionFaults[aggregateKey] = faults;
        }

        faults.Add(key);
    }

    private void RemoveRepetitionFault(Tr101290Indicator indicator, ushort? pid, SectionMark mark)
    {
        var aggregateKey = (indicator, pid);
        if (mark.Key is SectionKey key && _indicatorRepetitionFaults.TryGetValue(aggregateKey, out var faults))
        {
            faults.Remove(key);
            if (faults.Count > 0)
                return;
        }

        Heal(indicator, pid, Tr101290Fault.Repetition, "section interval at least 25 ms", surfaceOk: true);
    }

    private void EvaluateEitPfAggregate()
    {
        if (!_clockLocked || _now is not ulong now)
            return;

        var pid = (ushort)0x0012;
        var hasIncompletePair = false;
        foreach (var (key, mark) in _eitPf)
        {
            var fresh0 = mark.Section0.HasAt && Tr101290Limits.ForwardDelta(now, mark.Section0.At) <= Tr101290Limits.Sec2;
            var fresh1 = mark.Section1.HasAt && Tr101290Limits.ForwardDelta(now, mark.Section1.At) <= Tr101290Limits.Sec2;
            var aged = (mark.Section0.HasAt && !fresh0) || (mark.Section1.HasAt && !fresh1);
            if (aged && (mark.Section0.Seen || mark.Section1.Seen) && !(fresh0 && fresh1))
            {
                Fault(
                    Tr101290Indicator.EitPfError,
                    pid,
                    Tr101290Fault.Pair,
                    $"EIT P/F table 0x{key.TableId:X2} service {key.ServiceId} is missing section 0 or 1",
                    countEach: false);
                return;
            }

            if (!fresh0 || !fresh1)
                hasIncompletePair = true;
        }

        if (_eitPf.Count > 0 && !hasIncompletePair)
            Heal(Tr101290Indicator.EitPfError, pid, Tr101290Fault.Pair, "all known EIT P/F section pairs are present", surfaceOk: true);
    }

    private void AdvanceFallbackClock()
    {
        if (_clockMode == Tr101290ClockMode.Udp)
        {
            var timestamp = Stopwatch.GetTimestamp();
            var elapsed = timestamp - _lastMonotonicTimestamp;
            _lastMonotonicTimestamp = timestamp;
            if (elapsed > 0)
            {
                var ticks = (ulong)((double)elapsed * Tr101290Limits.TickHz / Stopwatch.Frequency);
                AdvanceLogicalClock(ticks);
            }
        }
        else if (_clockMode == Tr101290ClockMode.File && _fileTicksPerPacket is double ticksPerPacket)
        {
            AdvanceLogicalClock((ulong)Math.Max(0, ticksPerPacket));
        }
    }

    private void UpdateClockFromPcr(ulong pcr, ulong? delta, bool rebase)
    {
        if (!_clockLocked)
        {
            _now = 0;
            _clockLocked = true;
            _lockedAt = 0;
            StampPending(0);
            _lastClockPcr = pcr;
            _lastClockPacketNumber = _lastPacketNumber;
            _lastClockLogical = 0;
            return;
        }

        if (rebase)
        {
            var now = _now ?? 0;
            _lastClockPcr = pcr;
            _lastClockPacketNumber = _lastPacketNumber;
            _lastClockLogical = now;
            return;
        }

        if (delta is ulong pcrDelta)
        {
            if (_clockMode == Tr101290ClockMode.File
                && _lastClockPacketNumber is ulong lastPacket
                && _lastPacketNumber > lastPacket
                && pcrDelta > 0
                && pcrDelta <= Tr101290Limits.Sec10)
            {
                _fileTicksPerPacket = (double)pcrDelta / (_lastPacketNumber - lastPacket);
            }

            var target = _lastClockLogical > ulong.MaxValue - pcrDelta
                ? ulong.MaxValue
                : _lastClockLogical + pcrDelta;
            var now = _now ?? 0;
            if (target > now)
                AdvanceLogicalClock(target - now);
        }

        _lastClockPcr = pcr;
        _lastClockPacketNumber = _lastPacketNumber;
        _lastClockLogical = _now ?? 0;
    }

    private void AdvanceLogicalClock(ulong delta)
    {
        if (!_clockLocked || delta == 0)
            return;

        var current = _now ?? 0;
        _now = current > ulong.MaxValue - delta ? ulong.MaxValue : current + delta;
        var now = _now.Value;
        if (Tr101290Limits.ForwardDelta(now, _lastTimeoutCheck) >= Tr101290Limits.Ms10)
        {
            _lastTimeoutCheck = now;
            CheckTimeouts();
        }
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
            var sectionFresh = watch.Sections.Count > 0
                && watch.Sections.Values.All(mark => mark.HasAt
                    && Tr101290Limits.ForwardDelta(now, mark.At) <= Tr101290Limits.Ms500);
            if (sectionFresh)
            {
                Heal(Tr101290Indicator.PmtError2, pid, Tr101290Fault.Interval, "PMT section present", surfaceOk: true);
                continue;
            }

            var staleSection = watch.Sections.Values.FirstOrDefault(mark => mark.HasAt
                && Tr101290Limits.ForwardDelta(now, mark.At) > Tr101290Limits.Ms500);
            var basis = staleSection ?? watch.Announced;
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

        EvaluateEitPfAggregate();
    }

    private void CheckRequiredSection(byte tableId, Tr101290Indicator indicator, ulong max, bool required, ushort pid)
    {
        if (_now is not ulong now)
            return;

        var marks = _sections.Where(pair => pair.Key.TableId == tableId).Select(pair => pair.Value).ToArray();
        if (marks.Length == 0)
        {
            if (required && Tr101290Limits.ForwardDelta(now, _lockedAt) > max)
                Fault(indicator, pid, Tr101290Fault.Interval, "section missing", countEach: false);

            return;
        }

        if (marks.Any(mark => !mark.HasAt))
            return;

        if (marks.Any(mark => Tr101290Limits.ForwardDelta(now, mark.At) > max))
            Fault(indicator, pid, Tr101290Fault.Interval, "section interval too long", countEach: false);
        else
            Heal(indicator, pid, Tr101290Fault.Interval, "section interval within limit", surfaceOk: true);
    }

    private void StampPending(ulong now)
    {
        StampIfPending(_patPackets, now);
        foreach (var watch in _pmtSections.Values)
        {
            StampIfPending(watch.Announced, now);
            foreach (var mark in watch.Sections.Values)
                StampIfPending(mark, now);
        }
        foreach (var mark in _sections.Values)
            StampIfPending(mark, now);
        foreach (var mark in _tableIdArrivals.Values)
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

    private SectionMark Section(byte tableId, ushort tableIdExtension, byte sectionNumber)
    {
        var key = new SectionKey(tableId, tableIdExtension, sectionNumber);
        if (!_sections.TryGetValue(key, out var mark))
        {
            mark = new SectionMark();
            mark.Key = key;
            _sections[key] = mark;
        }

        return mark;
    }

    private SectionMark Minimum(byte tableId)
    {
        if (!_tableIdArrivals.TryGetValue(tableId, out var mark))
        {
            mark = new SectionMark { Key = new SectionKey(tableId, 0, 0) };
            _tableIdArrivals[tableId] = mark;
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
    private readonly record struct SectionKey(byte TableId, ushort TableIdExtension, byte SectionNumber);

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
        Unreferenced = 1 << 9,
        Pair = 1 << 10,
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
        public byte[]? LastPacket;
    }

    private sealed class PcrState
    {
        public bool Active;
        public bool HasLast;
        public ulong Last;
    }

    private sealed class SectionMark
    {
        public SectionKey? Key;
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
        public Dictionary<byte, SectionMark> Sections { get; } = new();

        public SectionMark Section(byte sectionNumber)
        {
            if (!Sections.TryGetValue(sectionNumber, out var mark))
            {
                mark = new SectionMark();
                Sections[sectionNumber] = mark;
            }

            return mark;
        }
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

    private sealed class PmtTopology(ushort programNumber, byte version, byte lastSectionNumber)
    {
        public ushort ProgramNumber { get; } = programNumber;
        public byte Version { get; } = version;
        public byte LastSectionNumber { get; } = lastSectionNumber;
        public Dictionary<byte, PMT> Sections { get; } = new();
    }
}

internal enum Tr101290ClockMode
{
    Push,
    File,
    Udp,
}
