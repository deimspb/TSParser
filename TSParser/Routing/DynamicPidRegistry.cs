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

using TSParser.Service;
using TSParser.Tables.DvbTableFactory;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Scte35;
using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser.Routing;

internal sealed class DynamicPidRegistry
{
    private readonly bool _t2miEnabled;
    private readonly bool _t2miAutoDetect;
    private readonly bool _t2miDeencapsulate;
    private readonly HashSet<ushort> _explicitT2miPids;
    private readonly Dictionary<ushort, PmtFactory> _pmtFactories = new();
    private Action<ushort, byte>? _sectionCrcFailed;
    private Action<ushort, ReadOnlyMemory<byte>>? _sectionAssembled;
    private readonly Dictionary<ushort, ushort> _programPmtPids = new();
    private readonly Dictionary<ushort, HashSet<ushort>> _programAitPids = new();
    private readonly Dictionary<ushort, HashSet<ushort>> _programScte35Pids = new();
    private readonly Dictionary<ushort, HashSet<ushort>> _programAutoT2miPids = new();
    private readonly Dictionary<ushort, PmtRouteState> _pmtRouteStates = new();
    private readonly HashSet<ushort> _aitPids = new();
    private readonly HashSet<ushort> _scte35Pids = new();
    private readonly HashSet<ushort> _ewsPids = new();
    private readonly HashSet<ushort> _eewsPids = new();
    private readonly Dictionary<ushort, AitFactory> _aitFactories = new();
    private readonly Dictionary<ushort, Scte35Factory> _scte35Factories = new();
    private readonly Dictionary<ushort, EwsFactory> _ewsFactories = new();
    private readonly Dictionary<ushort, EewsFactory> _eewsFactories = new();
    private readonly Dictionary<ushort, T2miDemuxer> _t2miDemuxers = new();
    private bool _ewsPidListEmptyWarningSent;
    private bool _eewsPidListEmptyWarningSent;
    private int _patProgramCount;
    private byte? _patVersion;
    private ushort? _patTransportStreamId;
    private byte _patLastSectionNumber;
    private readonly Dictionary<byte, PatRecord[]> _patSections = new();

    public DynamicPidRegistry(T2miOptions t2miOptions)
    {
        _t2miEnabled = t2miOptions.Enabled;
        _t2miAutoDetect = t2miOptions.AutoDetect;
        _t2miDeencapsulate = t2miOptions.Deencapsulate;
        _explicitT2miPids = t2miOptions.Pids.ToHashSet();
    }

    public event PmtReady? OnPmtReady;
    public event AitReady? OnAitReady;
    public event Scte35Ready? OnScte35Ready;
    public event EwsReady? OnEwsReady;
    public event EewsReady? OnEewsReady;
    public event T2miPacketReady? OnT2miPacketReady;
    public event T2miPlpDiscovered? OnT2miPlpDiscovered;
    public event PlpTsReady? OnPlpTsReady;

    public List<ushort> EwsPidList
    {
        get => _ewsPids.OrderBy(pid => pid).ToList();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _ewsPids.Clear();
            foreach (var pid in value)
            {
                _ewsPids.Add(pid);
            }

            _ewsFactories.Clear();
            _ewsPidListEmptyWarningSent = false;
        }
    }

    public List<ushort> EewsPidList
    {
        get => _eewsPids.OrderBy(pid => pid).ToList();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _eewsPids.Clear();
            foreach (var pid in value)
            {
                _eewsPids.Add(pid);
            }

            _eewsFactories.Clear();
            _eewsPidListEmptyWarningSent = false;
        }
    }

    public void SetSectionCrcFailedHandler(Action<ushort, byte>? handler)
    {
        _sectionCrcFailed = handler;
        foreach (var factory in _pmtFactories.Values)
            factory.SectionCrcFailed = handler;
    }

    public void SetSectionAssembledHandler(Action<ushort, ReadOnlyMemory<byte>>? handler)
    {
        _sectionAssembled = handler;
        foreach (var factory in _pmtFactories.Values)
            factory.SectionAssembled = handler;
    }

    public void RegisterT2miPids(IEnumerable<ushort> pids)
    {
        foreach (var pid in pids)
        {
            RegisterT2miPid(pid);
        }
    }

    public bool IsTrackedPid(ushort pid) =>
        _pmtFactories.ContainsKey(pid)
        || _aitPids.Contains(pid)
        || _scte35Pids.Contains(pid)
        || _ewsPids.Contains(pid)
        || _eewsPids.Contains(pid);

    public bool IsT2miPid(ushort pid) => _t2miDemuxers.ContainsKey(pid);

    public void UpdateFromPat(PAT pat)
    {
        if (!pat.CurrentNextIndicator)
        {
            return;
        }

        if (_patVersion != pat.VersionNumber || _patTransportStreamId != pat.TransportStreamId
            || _patLastSectionNumber != pat.LastSectionNumber)
        {
            _patSections.Clear();
            _patVersion = pat.VersionNumber;
            _patTransportStreamId = pat.TransportStreamId;
            _patLastSectionNumber = pat.LastSectionNumber;
        }

        _patSections[pat.SectionNumber] = pat.PatRecords;
        if (_patSections.Count < _patLastSectionNumber + 1
            || Enumerable.Range(0, _patLastSectionNumber + 1).Any(section => !_patSections.ContainsKey((byte)section)))
        {
            return;
        }

        var desiredPrograms = _patSections.Values
            .SelectMany(records => records)
            .Where(record => record.ProgramNumber != 0)
            .ToDictionary(record => record.ProgramNumber, record => record.Pid);

        _patProgramCount = desiredPrograms.Count;

        foreach (var removedProgram in _programPmtPids.Keys.Except(desiredPrograms.Keys).ToArray())
        {
            _programPmtPids.Remove(removedProgram);
            _programAitPids.Remove(removedProgram);
            _programScte35Pids.Remove(removedProgram);
            _programAutoT2miPids.Remove(removedProgram);
            _pmtRouteStates.Remove(removedProgram);
        }

        foreach (var pair in desiredPrograms)
        {
            if (_programPmtPids.TryGetValue(pair.Key, out var oldPid) && oldPid != pair.Value)
            {
                _programAitPids.Remove(pair.Key);
                _programScte35Pids.Remove(pair.Key);
                _programAutoT2miPids.Remove(pair.Key);
                _pmtRouteStates.Remove(pair.Key);
            }

            _programPmtPids[pair.Key] = pair.Value;
        }

        var desiredPmtPids = desiredPrograms.Values.ToHashSet();
        foreach (var stalePid in _pmtFactories.Keys.Except(desiredPmtPids).ToArray())
            _pmtFactories.Remove(stalePid);

        foreach (var pid in desiredPmtPids)
        {
            if (_pmtFactories.ContainsKey(pid))
                continue;

            var factory = new PmtFactory
            {
                CurrentPid = pid,
                SectionCrcFailed = _sectionCrcFailed,
                SectionAssembled = _sectionAssembled,
            };
            factory.OnPmtReady += PmtFactory_OnPmtReady;
            _pmtFactories[pid] = factory;
        }

        ReconcileDynamicTablePids();
        if (_patProgramCount != 1)
        {
            _programAutoT2miPids.Clear();
        }
        ReconcileT2miPids();
    }

    public void ResetStreamState()
    {
        _pmtFactories.Clear();
        _programPmtPids.Clear();
        _programAitPids.Clear();
        _programScte35Pids.Clear();
        _programAutoT2miPids.Clear();
        _pmtRouteStates.Clear();
        _aitPids.Clear();
        _scte35Pids.Clear();
        _aitFactories.Clear();
        _scte35Factories.Clear();
        _ewsFactories.Clear();
        _eewsFactories.Clear();
        _patSections.Clear();
        _patVersion = null;
        _patTransportStreamId = null;
        _patProgramCount = 0;
        _ewsPidListEmptyWarningSent = false;
        _eewsPidListEmptyWarningSent = false;

        _t2miDemuxers.Clear();
        RegisterT2miPids(_explicitT2miPids);
    }

    public void RouteDynamicTables(TsPacket tsPacket)
    {
        RoutePmt(tsPacket);
        RouteAit(tsPacket);
        RouteScte35(tsPacket);
        RouteEws(tsPacket);
        RouteEews(tsPacket);
    }

    public void RouteT2mi(TsPacket tsPacket)
    {
        if (!_t2miEnabled || tsPacket.TransportErrorIndicator || tsPacket.Pid == 0xFFFF)
        {
            return;
        }

        if (_t2miDemuxers.TryGetValue(tsPacket.Pid, out var demuxer))
        {
            demuxer.PushPacket(tsPacket);
        }
    }

    private void PmtFactory_OnPmtReady(PMT pmt)
    {
        OnPmtReady?.Invoke(pmt);

        if (!pmt.CurrentNextIndicator
            || !_programPmtPids.TryGetValue(pmt.ProgramNumber, out var expectedPid)
            || expectedPid != pmt.TablePid)
        {
            return;
        }

        if (!_pmtRouteStates.TryGetValue(pmt.ProgramNumber, out var routeState)
            || routeState.Version != pmt.VersionNumber
            || routeState.LastSectionNumber != pmt.LastSectionNumber)
        {
            routeState = new PmtRouteState(pmt.VersionNumber, pmt.LastSectionNumber);
            _pmtRouteStates[pmt.ProgramNumber] = routeState;
        }

        routeState.Sections[pmt.SectionNumber] = pmt.EsInfoList.ToArray();
        if (routeState.Sections.Count < pmt.LastSectionNumber + 1
            || Enumerable.Range(0, pmt.LastSectionNumber + 1).Any(section => !routeState.Sections.ContainsKey((byte)section)))
        {
            return;
        }

        var allEs = routeState.Sections.OrderBy(pair => pair.Key).SelectMany(pair => pair.Value).ToArray();
        _programAitPids[pmt.ProgramNumber] = allEs
            .Where(es => es.StreamType == 0x05 && es.EsDescriptorList.Exists(desc => desc.DescriptorTag == 0x6F))
            .Select(es => es.ElementaryPid)
            .ToHashSet();
        _programScte35Pids[pmt.ProgramNumber] = allEs
            .Where(es => es.StreamType == 0x86)
            .Select(es => es.ElementaryPid)
            .ToHashSet();
        ReconcileDynamicTablePids();

        if (_t2miEnabled && _t2miAutoDetect && _patProgramCount == 1 && allEs.Length == 1
            && allEs[0].StreamType == 0x06)
        {
            _programAutoT2miPids[pmt.ProgramNumber] = [allEs[0].ElementaryPid];
        }
        else
        {
            _programAutoT2miPids.Remove(pmt.ProgramNumber);
        }
        ReconcileT2miPids();
    }

    private void ReconcileDynamicTablePids()
    {
        var desiredAit = _programAitPids.Values.SelectMany(pids => pids).ToHashSet();
        var desiredScte35 = _programScte35Pids.Values.SelectMany(pids => pids).ToHashSet();

        foreach (var pid in _aitPids.Except(desiredAit).ToArray())
        {
            _aitPids.Remove(pid);
            _aitFactories.Remove(pid);
        }
        foreach (var pid in desiredAit.Where(pid => _aitPids.Add(pid)))
        {
            var factory = new AitFactory { CurrentPid = pid };
            factory.OnAitReady += AitFactory_OnAitReady;
            _aitFactories[pid] = factory;
        }

        foreach (var pid in _scte35Pids.Except(desiredScte35).ToArray())
        {
            _scte35Pids.Remove(pid);
            _scte35Factories.Remove(pid);
        }
        foreach (var pid in desiredScte35.Where(pid => _scte35Pids.Add(pid)))
        {
            var factory = new Scte35Factory { CurrentPid = pid };
            factory.OnScte35Ready += Scte35Factory_OnScte35Ready;
            _scte35Factories[pid] = factory;
        }
    }

    private void ReconcileT2miPids()
    {
        if (!_t2miEnabled)
            return;

        var desired = _explicitT2miPids
            .Concat(_programAutoT2miPids.Values.SelectMany(pids => pids))
            .ToHashSet();
        foreach (var stalePid in _t2miDemuxers.Keys.Except(desired).ToArray())
            _t2miDemuxers.Remove(stalePid);
        RegisterT2miPids(desired);
    }

    private sealed class PmtRouteState(byte version, byte lastSectionNumber)
    {
        public byte Version { get; } = version;
        public byte LastSectionNumber { get; } = lastSectionNumber;
        public Dictionary<byte, EsInfo[]> Sections { get; } = new();
    }

    private void RoutePmt(TsPacket tsPacket)
    {
        if (_pmtFactories.TryGetValue(tsPacket.Pid, out var factory))
        {
            factory.PushTable(tsPacket);
        }
    }

    private void RouteAit(TsPacket tsPacket)
    {
        if (_aitFactories.TryGetValue(tsPacket.Pid, out var factory))
        {
            factory.PushTable(tsPacket);
        }
    }

    private void RouteScte35(TsPacket tsPacket)
    {
        if (_scte35Factories.TryGetValue(tsPacket.Pid, out var factory))
        {
            factory.PushTable(tsPacket);
        }
    }

    private void RouteEws(TsPacket tsPacket)
    {
        if (_ewsPids.Count == 0)
        {
            if (!_ewsPidListEmptyWarningSent)
            {
                Logger.Send(LogStatus.WARNING, "EWS pid list is empty, set EWS pid list to get EWS tables");
                _ewsPidListEmptyWarningSent = true;
            }

            return;
        }

        _ewsPidListEmptyWarningSent = false;

        if (!_ewsPids.Contains(tsPacket.Pid))
        {
            return;
        }

        if (!_ewsFactories.TryGetValue(tsPacket.Pid, out var factory))
        {
            factory = new EwsFactory
            {
                CurrentPid = tsPacket.Pid
            };
            factory.OnEwsReady += EwsFactory_OnEwsReady;
            _ewsFactories[tsPacket.Pid] = factory;
        }

        factory.PushTable(tsPacket);
    }

    private void RouteEews(TsPacket tsPacket)
    {
        if (_eewsPids.Count == 0)
        {
            if (!_eewsPidListEmptyWarningSent)
            {
                Logger.Send(LogStatus.WARNING, "EEWS pid list is empty, set EEWS pid list to get EEWS tables");
                _eewsPidListEmptyWarningSent = true;
            }

            return;
        }

        _eewsPidListEmptyWarningSent = false;

        if (!_eewsPids.Contains(tsPacket.Pid))
        {
            return;
        }

        if (!_eewsFactories.TryGetValue(tsPacket.Pid, out var factory))
        {
            factory = new EewsFactory
            {
                CurrentPid = tsPacket.Pid
            };
            factory.OnEewsReady += EewsFactory_OnEewsReady;
            _eewsFactories[tsPacket.Pid] = factory;
        }

        factory.PushTable(tsPacket);
    }

    private void RegisterT2miPid(ushort pid)
    {
        if (_t2miDemuxers.ContainsKey(pid))
        {
            return;
        }

        var demuxer = new T2miDemuxer(pid, _t2miDeencapsulate);
        demuxer.PacketReady += T2miDemuxer_OnPacketReady;
        demuxer.PlpDiscovered += T2miDemuxer_OnPlpDiscovered;
        demuxer.PlpTsReady += (plpId, tsData) => OnPlpTsReady?.Invoke(pid, plpId, tsData);
        _t2miDemuxers[pid] = demuxer;
        Logger.Send(LogStatus.INFO, $"T2-MI demuxer registered on PID 0x{pid:X4}");
    }

    private void AitFactory_OnAitReady(AIT ait)
    {
        OnAitReady?.Invoke(ait);
    }

    private void Scte35Factory_OnScte35Ready(SCTE35 scte35)
    {
        OnScte35Ready?.Invoke(scte35);
    }

    private void EwsFactory_OnEwsReady(EWS ews)
    {
        OnEwsReady?.Invoke(ews);
    }

    private void EewsFactory_OnEewsReady(EEWS eews)
    {
        OnEewsReady?.Invoke(eews);
    }

    private void T2miDemuxer_OnPacketReady(T2miPacket packet)
    {
        OnT2miPacketReady?.Invoke(packet);
    }

    private void T2miDemuxer_OnPlpDiscovered(byte plpId)
    {
        OnT2miPlpDiscovered?.Invoke(plpId);
    }
}
