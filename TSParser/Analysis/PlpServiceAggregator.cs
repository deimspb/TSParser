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

using TSParser.Descriptors.Dvb;
using TSParser.Tables.DvbTables;

namespace TSParser.Analysis;

/// <summary>
/// Collects PAT, SDT, and PMT for each inner MPEG-TS stream identified by T2-MI source PID and PLP ID.
/// </summary>
public sealed class PlpServiceAggregator
{
    private readonly object _sync = new();
    private readonly Dictionary<(ushort T2miPid, byte PlpId), Dictionary<ushort, MutablePlpService>> _byPlp = new();
    private readonly HashSet<(ushort T2miPid, byte PlpId)> _plpTsReceived = [];

    public void ApplyPat(ushort t2miPid, byte plpId, PAT pat)
    {
        if (pat.PatRecords is null)
            return;

        lock (_sync)
        {
            var services = GetOrCreatePlpServices(t2miPid, plpId);
            foreach (var record in pat.PatRecords)
            {
                if (record.ProgramNumber == 0)
                    continue;

                var service = GetOrCreateService(services, record.ProgramNumber);
                service.PmtPid = record.Pid;
            }
        }
    }

    public void ApplySdt(ushort t2miPid, byte plpId, SDT sdt)
    {
        if (sdt.SdtItemsList is null)
            return;

        lock (_sync)
        {
            var services = GetOrCreatePlpServices(t2miPid, plpId);
            foreach (var item in sdt.SdtItemsList)
            {
                var service = GetOrCreateService(services, item.ServiceId);
                service.ServiceId = item.ServiceId;

                var descriptor = PlpServiceDescriptorHelper.TryGetServiceDescriptor(item.SdtItemDescriptorList);
                if (descriptor is null)
                    continue;

                ApplyServiceDescriptor(service, descriptor);
            }
        }
    }

    public void ApplyPmt(ushort t2miPid, byte plpId, PMT pmt)
    {
        lock (_sync)
        {
            var services = GetOrCreatePlpServices(t2miPid, plpId);
            var service = GetOrCreateService(services, pmt.ProgramNumber);
            service.PcrPid = pmt.PcrPid;
            service.ElementaryStreams = PlpServiceDescriptorHelper.MapElementaryStreams(pmt.EsInfoList);

            if (string.IsNullOrWhiteSpace(service.ServiceName))
            {
                var descriptor = PlpServiceDescriptorHelper.TryGetServiceDescriptor(pmt.PmtDescriptorList);
                if (descriptor is not null)
                    ApplyServiceDescriptor(service, descriptor);
            }
        }
    }

    public void MarkPlpTsReceived(ushort t2miPid, byte plpId)
    {
        lock (_sync)
            _plpTsReceived.Add((t2miPid, plpId));
    }

    public bool HasReceivedPlpTs(ushort t2miPid, byte plpId)
    {
        lock (_sync)
            return _plpTsReceived.Contains((t2miPid, plpId));
    }

    public IReadOnlyList<(ushort T2miPid, byte PlpId)> GetPlpKeys()
    {
        lock (_sync)
        {
            var keys = new HashSet<(ushort, byte)>(_plpTsReceived);
            keys.UnionWith(_byPlp.Keys);
            return keys.OrderBy(k => k.Item1).ThenBy(k => k.Item2).ToList();
        }
    }

    public IReadOnlyList<PlpServiceInfo> GetServices(ushort t2miPid, byte plpId)
    {
        lock (_sync)
        {
            if (!_byPlp.TryGetValue((t2miPid, plpId), out var services) || services.Count == 0)
                return [];

            return services.Values
                .Select(s => s.ToSnapshot())
                .OrderBy(s => s.ProgramNumber)
                .ToList();
        }
    }

    private Dictionary<ushort, MutablePlpService> GetOrCreatePlpServices(ushort t2miPid, byte plpId)
    {
        var key = (t2miPid, plpId);
        if (!_byPlp.TryGetValue(key, out var services))
        {
            services = new Dictionary<ushort, MutablePlpService>();
            _byPlp[key] = services;
        }

        return services;
    }

    private static MutablePlpService GetOrCreateService(Dictionary<ushort, MutablePlpService> services, ushort programNumber)
    {
        if (!services.TryGetValue(programNumber, out var service))
        {
            service = new MutablePlpService { ProgramNumber = programNumber };
            services[programNumber] = service;
        }

        return service;
    }

    private static void ApplyServiceDescriptor(MutablePlpService service, ServiceDescriptor_0x48 descriptor)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.ServiceName))
            service.ServiceName = descriptor.ServiceName.Trim();

        service.ServiceType = descriptor.ServiceType;
        service.ServiceTypeName = descriptor.ServiceTypeName;

        if (!string.IsNullOrWhiteSpace(descriptor.ServiceProviderName))
            service.ServiceProviderName = descriptor.ServiceProviderName.Trim();
    }

    private sealed class MutablePlpService
    {
        public ushort ProgramNumber { get; init; }
        public ushort? PmtPid { get; set; }
        public ushort? ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public byte? ServiceType { get; set; }
        public string? ServiceTypeName { get; set; }
        public string? ServiceProviderName { get; set; }
        public ushort? PcrPid { get; set; }
        public IReadOnlyList<PlpElementaryStreamInfo> ElementaryStreams { get; set; } = [];

        public PlpServiceInfo ToSnapshot() => new()
        {
            ProgramNumber = ProgramNumber,
            PmtPid = PmtPid,
            ServiceId = ServiceId,
            ServiceName = ServiceName,
            ServiceType = ServiceType,
            ServiceTypeName = ServiceTypeName,
            ServiceProviderName = ServiceProviderName,
            PcrPid = PcrPid,
            ElementaryStreams = ElementaryStreams,
        };
    }
}
