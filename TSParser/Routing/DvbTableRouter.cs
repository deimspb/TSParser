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

using TSParser.Enums;
using TSParser.Service;
using TSParser.Tables.DvbTableFactory;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;
using TSParser.Tables.Scte35;
using TSParser.TransportStream;
using TSParser.TransportStream.T2mi;

namespace TSParser.Routing;

internal sealed class DvbTableRouter
{
    private readonly TsMode _tsMode;
    private readonly DynamicPidRegistry _dynamicPidRegistry;
    private readonly TdtTotFactory _tdtTotFactory = new();
    private readonly SdtBatFactory _sdtBatFactory = new();
    private readonly CatFactory _catFactory = new();
    private readonly NitFactory _nitFactory = new();
    private readonly PatFactory _patFactory = new();
    private readonly EitFactory _eitFactory = new();
    private readonly MipFactory _mipFactory = new();

    public DvbTableRouter(TsMode tsMode, T2miOptions t2miOptions)
    {
        _tsMode = tsMode;
        _dynamicPidRegistry = new DynamicPidRegistry(t2miOptions);

        _patFactory.OnPatReady += PatFactory_OnPatReady;
        _eitFactory.OnEitReady += eit => OnEitReady?.Invoke(eit);
        _catFactory.OnCatReady += cat => OnCatReady?.Invoke(cat);
        _nitFactory.OnNitReady += nit => OnNitReady?.Invoke(nit);
        _mipFactory.OnMipReady += mip => OnMipReady?.Invoke(mip);
        _tdtTotFactory.OnTdtReady += tdt => OnTdtReady?.Invoke(tdt);
        _tdtTotFactory.OnTotReady += tot => OnTotReady?.Invoke(tot);
        _sdtBatFactory.OnSdtReady += sdt => OnSdtReady?.Invoke(sdt);
        _sdtBatFactory.OnBatReady += bat => OnBatReady?.Invoke(bat);

        _dynamicPidRegistry.OnPmtReady += pmt => OnPmtReady?.Invoke(pmt);
        _dynamicPidRegistry.OnAitReady += ait => OnAitReady?.Invoke(ait);
        _dynamicPidRegistry.OnScte35Ready += scte35 => OnScte35Ready?.Invoke(scte35);
        _dynamicPidRegistry.OnEwsReady += ews => OnEwsReady?.Invoke(ews);
        _dynamicPidRegistry.OnEewsReady += eews => OnEewsReady?.Invoke(eews);
        _dynamicPidRegistry.OnT2miPacketReady += packet => OnT2miPacketReady?.Invoke(packet);
        _dynamicPidRegistry.OnT2miPlpDiscovered += plpId => OnT2miPlpDiscovered?.Invoke(plpId);
        _dynamicPidRegistry.OnPlpTsReady += (pid, plpId, data) => OnPlpTsReady?.Invoke(pid, plpId, data);
    }

    public event PatReady? OnPatReady;
    public event PmtReady? OnPmtReady;
    public event CatReady? OnCatReady;
    public event SdtReady? OnSdtReady;
    public event NitReady? OnNitReady;
    public event BatReady? OnBatReady;
    public event EitReady? OnEitReady;
    public event TdtReady? OnTdtReady;
    public event TotReady? OnTotReady;
    public event AitReady? OnAitReady;
    public event MipReady? OnMipReady;
    public event Scte35Ready? OnScte35Ready;
    public event EwsReady? OnEwsReady;
    public event EewsReady? OnEewsReady;
    public event T2miPacketReady? OnT2miPacketReady;
    public event T2miPlpDiscovered? OnT2miPlpDiscovered;
    public event PlpTsReady? OnPlpTsReady;

    public List<ushort> EwsPidList
    {
        get => _dynamicPidRegistry.EwsPidList;
        set => _dynamicPidRegistry.EwsPidList = value;
    }

    public List<ushort> EewsPidList
    {
        get => _dynamicPidRegistry.EewsPidList;
        set => _dynamicPidRegistry.EewsPidList = value;
    }

    public void RegisterT2miPids(IEnumerable<ushort> pids)
    {
        _dynamicPidRegistry.RegisterT2miPids(pids);
    }

    public void RouteTablePacket(TsPacket tsPacket)
    {
        switch (_tsMode)
        {
            case TsMode.DVB:
                RouteDvb(tsPacket);
                return;
            case TsMode.ATSC:
                throw new UnsupportedTsModeException(TsMode.ATSC);
            case TsMode.ISDB:
                throw new UnsupportedTsModeException(TsMode.ISDB);
            default:
                throw new UnsupportedTsModeException(_tsMode);
        }
    }

    public void RouteT2mi(TsPacket tsPacket)
    {
        _dynamicPidRegistry.RouteT2mi(tsPacket);
    }

    private void RouteDvb(TsPacket tsPacket)
    {
        if (tsPacket.TransportErrorIndicator)
        {
            return;
        }

        if (tsPacket.Pid == (short)ReservedPids.NullPacket)
        {
            return;
        }

        switch (tsPacket.Pid)
        {
            case (ushort)ReservedPids.PAT:
                _patFactory.PushTable(tsPacket);
                break;
            case (ushort)ReservedPids.CAT:
                _catFactory.PushTable(tsPacket);
                break;
            case (ushort)ReservedPids.NIT:
                _nitFactory.PushTable(tsPacket);
                break;
            case (ushort)ReservedPids.SDT:
                _sdtBatFactory.PushTable(tsPacket);
                break;
            case (ushort)ReservedPids.EIT:
                _eitFactory.PushTable(tsPacket);
                break;
            case (ushort)ReservedPids.RST:
                Logger.Send(LogStatus.INFO, "Not implement RST table");
                break;
            case (ushort)ReservedPids.TDT:
                _tdtTotFactory.PushTable(tsPacket);
                break;
            case (ushort)ReservedPids.NetworkSync:
                _mipFactory.PushTable(tsPacket);
                break;
            case (ushort)ReservedPids.RNT:
                Logger.Send(LogStatus.INFO, "Not implement RNT table");
                break;
            case (ushort)ReservedPids.LLinbandSignalink:
                Logger.Send(LogStatus.INFO, "Not implement L lindband signal link table");
                break;
            case (ushort)ReservedPids.Measurement:
                Logger.Send(LogStatus.INFO, "Not implement Measurmrnt table");
                break;
            case (ushort)ReservedPids.DIT:
                Logger.Send(LogStatus.INFO, "Not implement DIT table");
                break;
            case (ushort)ReservedPids.SIT:
                Logger.Send(LogStatus.INFO, "Not implement SIT table");
                break;
            default:
                _dynamicPidRegistry.RouteDynamicTables(tsPacket);
                break;
        }

        _dynamicPidRegistry.RouteT2mi(tsPacket);
    }

    private void PatFactory_OnPatReady(PAT pat)
    {
        OnPatReady?.Invoke(pat);
        _dynamicPidRegistry.UpdateFromPat(pat);
    }
}
