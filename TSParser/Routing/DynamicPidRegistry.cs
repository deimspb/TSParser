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
    private PmtFactory[] _pmtFactories = Array.Empty<PmtFactory>();
    private ushort[] _pmtPids = Array.Empty<ushort>();
    private readonly List<ushort> _aitPids = new();
    private readonly List<ushort> _scte35Pids = new();
    private List<ushort> _ewsPids = new();
    private List<ushort> _eewsPids = new();
    private readonly List<AitFactory> _aitFactories = new();
    private readonly List<Scte35Factory> _scte35Factories = new();
    private readonly List<EwsFactory> _ewsFactories = new();
    private readonly List<EewsFactory> _eewsFactories = new();
    private readonly List<T2miDemuxer> _t2miDemuxers = new();
    private bool _ewsPidListEmptyWarningSent;
    private bool _eewsPidListEmptyWarningSent;
    private int _patProgramCount;

    public DynamicPidRegistry(T2miOptions t2miOptions)
    {
        _t2miEnabled = t2miOptions.Enabled;
        _t2miAutoDetect = t2miOptions.AutoDetect;
        _t2miDeencapsulate = t2miOptions.Deencapsulate;
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
        get => _ewsPids;
        set
        {
            _ewsPids = value ?? throw new ArgumentNullException(nameof(value));
            _ewsFactories.Clear();
            _ewsPidListEmptyWarningSent = false;
        }
    }

    public List<ushort> EewsPidList
    {
        get => _eewsPids;
        set
        {
            _eewsPids = value ?? throw new ArgumentNullException(nameof(value));
            _eewsFactories.Clear();
            _eewsPidListEmptyWarningSent = false;
        }
    }

    public void RegisterT2miPids(IEnumerable<ushort> pids)
    {
        foreach (var pid in pids)
        {
            RegisterT2miPid(pid);
        }
    }

    public void UpdateFromPat(PAT pat)
    {
        if (_t2miEnabled && _t2miAutoDetect)
        {
            _patProgramCount = pat.PatRecords.Count(pr => pr.Pid != 0x16);
        }

        _pmtPids = (from pr in pat.PatRecords where pr.Pid != 0x16 select pr.Pid).ToArray();
        Array.Sort(_pmtPids);

        _pmtFactories = new PmtFactory[_pmtPids.Length];
        for (var i = 0; i < _pmtPids.Length; i++)
        {
            _pmtFactories[i] = new PmtFactory
            {
                CurrentPid = _pmtPids[i]
            };
            _pmtFactories[i].OnPmtReady += PmtFactory_OnPmtReady;
        }
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

        for (var i = 0; i < _t2miDemuxers.Count; i++)
        {
            if (_t2miDemuxers[i].Pid == tsPacket.Pid)
            {
                _t2miDemuxers[i].PushPacket(tsPacket);
                return;
            }
        }
    }

    private void PmtFactory_OnPmtReady(PMT pmt)
    {
        OnPmtReady?.Invoke(pmt);

        var aitIdx = pmt.EsInfoList.FindIndex(es => es.StreamType == 0x05);
        if (aitIdx >= 0 && pmt.EsInfoList[aitIdx].EsDescriptorList.Exists(desc => desc.DescriptorTag == 0x6F))
        {
            var aitPid = pmt.EsInfoList[aitIdx].ElementaryPid;
            if (!_aitPids.Contains(aitPid))
            {
                _aitPids.Add(aitPid);
                var aitFactory = new AitFactory
                {
                    CurrentPid = aitPid
                };
                aitFactory.OnAitReady += AitFactory_OnAitReady;
                _aitFactories.Add(aitFactory);
            }
        }

        var scte35Idx = pmt.EsInfoList.FindIndex(es => es.StreamType == 0x86);
        if (scte35Idx >= 0)
        {
            var scte35Pid = pmt.EsInfoList[scte35Idx].ElementaryPid;
            if (!_scte35Pids.Contains(scte35Pid))
            {
                _scte35Pids.Add(scte35Pid);
                var scte35Factory = new Scte35Factory
                {
                    CurrentPid = scte35Pid
                };
                scte35Factory.OnScte35Ready += Scte35Factory_OnScte35Ready;
                _scte35Factories.Add(scte35Factory);
            }
        }

        if (_t2miEnabled && _t2miAutoDetect && _patProgramCount == 1 && pmt.EsInfoList.Count == 1)
        {
            var es = pmt.EsInfoList[0];
            if (es.StreamType == 0x06)
            {
                RegisterT2miPid(es.ElementaryPid);
            }
        }
    }

    private void RoutePmt(TsPacket tsPacket)
    {
        if (_pmtPids.Length == 0)
        {
            return;
        }

        var idx = Array.IndexOf(_pmtPids, tsPacket.Pid);
        if (idx >= 0)
        {
            _pmtFactories[idx].PushTable(tsPacket);
        }
    }

    private void RouteAit(TsPacket tsPacket)
    {
        var idx = _aitPids.IndexOf(tsPacket.Pid);
        if (idx >= 0)
        {
            _aitFactories[idx].PushTable(tsPacket);
        }
    }

    private void RouteScte35(TsPacket tsPacket)
    {
        var idx = _scte35Pids.IndexOf(tsPacket.Pid);
        if (idx >= 0)
        {
            _scte35Factories[idx].PushTable(tsPacket);
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

        var idx = _ewsPids.IndexOf(tsPacket.Pid);
        if (idx < 0)
        {
            return;
        }

        if (idx < _ewsFactories.Count)
        {
            _ewsFactories[idx].PushTable(tsPacket);
            return;
        }

        var ewsFactory = new EwsFactory
        {
            CurrentPid = _ewsPids[idx]
        };
        ewsFactory.OnEwsReady += EwsFactory_OnEwsReady;
        _ewsFactories.Add(ewsFactory);
        ewsFactory.PushTable(tsPacket);
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

        var idx = _eewsPids.IndexOf(tsPacket.Pid);
        if (idx < 0)
        {
            return;
        }

        if (idx < _eewsFactories.Count)
        {
            _eewsFactories[idx].PushTable(tsPacket);
            return;
        }

        var eewsFactory = new EewsFactory
        {
            CurrentPid = _eewsPids[idx]
        };
        eewsFactory.OnEewsReady += EewsFactory_OnEewsReady;
        _eewsFactories.Add(eewsFactory);
        eewsFactory.PushTable(tsPacket);
    }

    private void RegisterT2miPid(ushort pid)
    {
        if (_t2miDemuxers.Exists(d => d.Pid == pid))
        {
            return;
        }

        var demuxer = new T2miDemuxer(pid, _t2miDeencapsulate);
        demuxer.PacketReady += T2miDemuxer_OnPacketReady;
        demuxer.PlpDiscovered += T2miDemuxer_OnPlpDiscovered;
        demuxer.PlpTsReady += (plpId, tsData) => OnPlpTsReady?.Invoke(pid, plpId, tsData);
        _t2miDemuxers.Add(demuxer);
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
