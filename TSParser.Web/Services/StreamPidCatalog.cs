using TSParser.Descriptors;
using TSParser.Descriptors.Custom;
using TSParser.Descriptors.Dvb;
using TSParser.Enums;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;
using TSParser.Tables.Scte35;
using TSParser.Web.Models;

namespace TSParser.Web.Services;

/// <summary>Maps SI-defined and observed PIDs for the tree UI (StreamParser pidList semantics).</summary>
internal sealed class StreamPidCatalog
{
    private readonly object _sync = new();
    private readonly HashSet<ushort> _observed = [];
    private readonly HashSet<ushort> _expected = [];
    private readonly Dictionary<ushort, string> _descriptions = new();
    private readonly Dictionary<ushort, string> _serviceNamesByProgram = new();

    public void Clear()
    {
        lock (_sync)
        {
            _observed.Clear();
            _expected.Clear();
            _descriptions.Clear();
            _serviceNamesByProgram.Clear();
        }
    }

    public void ApplyTable(TsTableKind kind, Table table)
    {
        lock (_sync)
        {
            switch (kind)
            {
                case TsTableKind.Pat when table is PAT pat:
                    SetReserved(ReservedPids.PAT, "PAT");
                    foreach (var record in pat.PatRecords)
                    {
                        if (record.ProgramNumber == 0)
                            continue;

                        ExpectPid(record.Pid, "PMT");
                    }
                    break;

                case TsTableKind.Cat when table is CAT cat:
                    SetReserved(ReservedPids.CAT, "CAT");
                    ApplyCatDescriptors(cat);
                    break;

                case TsTableKind.Nit:
                    SetReserved(ReservedPids.NIT, "NIT");
                    break;

                case TsTableKind.Sdt when table is SDT sdt:
                    SetReserved(ReservedPids.SDT, "SDT/BAT");
                    if (sdt.SdtItemsList is not null)
                    {
                        foreach (var item in sdt.SdtItemsList)
                        {
                            var name = TryGetServiceName(item.SdtItemDescriptorList);
                            if (!string.IsNullOrWhiteSpace(name))
                                _serviceNamesByProgram[item.ServiceId] = name;
                        }
                    }
                    break;

                case TsTableKind.Bat:
                    SetReserved(ReservedPids.SDT, "SDT/BAT");
                    break;

                case TsTableKind.Eit:
                    SetReserved(ReservedPids.EIT, "EIT");
                    break;

                case TsTableKind.Tdt:
                case TsTableKind.Tot:
                    SetReserved(ReservedPids.TDT, "TOT/TDT");
                    break;

                case TsTableKind.Pmt when table is PMT pmt:
                    ApplyPmt(pmt);
                    break;

                case TsTableKind.Mip when table is MIP mip:
                    SetPidRole(mip.TablePid, "MIP");
                    break;

                case TsTableKind.Ait when table is AIT ait:
                    SetPidRole(ait.TablePid, "AIT");
                    break;

                case TsTableKind.Scte35 when table is SCTE35 scte:
                    SetPidRole(scte.TablePid, "SCTE-35");
                    break;

                case TsTableKind.Ews when table is EWS ews:
                    SetPidRole(ews.TablePid, "EWS");
                    break;

                case TsTableKind.Eews when table is EEWS eews:
                    SetPidRole(eews.TablePid, "EEWS");
                    break;
            }
        }
    }

    public bool SyncObserved(IEnumerable<ushort> pids)
    {
        lock (_sync)
        {
            var added = false;
            foreach (var pid in pids)
                added |= _observed.Add(pid);

            return added;
        }
    }

    public IReadOnlyList<PidTreeEntry> GetSortedEntries()
    {
        lock (_sync)
        {
            var allPids = new HashSet<ushort>(_observed);
            allPids.UnionWith(_expected);

            var entries = allPids
                .OrderBy(p => p)
                .Select(p => new PidTreeEntry
                {
                    Pid = p,
                    TypeDescription = _descriptions.GetValueOrDefault(p),
                    IsObservedInStream = _observed.Contains(p),
                    IsMissingFromStream = _expected.Contains(p) && !_observed.Contains(p)
                })
                .ToList();

            return entries;
        }
    }

    public int ObservedCount
    {
        get
        {
            lock (_sync)
                return _observed.Count;
        }
    }

    private void ApplyPmt(PMT pmt)
    {
        var serviceName = TryGetServiceName(pmt.PmtDescriptorList)
                          ?? _serviceNamesByProgram.GetValueOrDefault(pmt.ProgramNumber, "");

        var pmtSuffix = string.IsNullOrWhiteSpace(serviceName) ? "" : $" - {serviceName}";
        ExpectPid(pmt.TablePid, $"PMT{pmtSuffix}");

        if (pmt.PcrPid is not 0 and not 0x1FFF)
            ExpectPid(pmt.PcrPid, $"PCR{pmtSuffix}");

        if (pmt.PmtDescriptorList is not null)
            ApplyCaDescriptors(pmt.PmtDescriptorList, "ECM");

        if (pmt.EsInfoList is not null)
        {
            foreach (var es in pmt.EsInfoList)
            {
                var esLabel = GetEsTypeLabel(es);
                if (esLabel is not null)
                    ExpectPid(es.ElementaryPid, $"{esLabel}{pmtSuffix}");
                else
                {
                    ExpectPid(es.ElementaryPid, null);
                }

                if (es.EsDescriptorList is not null)
                    ApplyCaDescriptors(es.EsDescriptorList, "ECM");
            }
        }
    }

    private void ApplyCatDescriptors(CAT cat)
    {
        if (cat.CatDescriptorList is not null)
            ApplyCaDescriptors(cat.CatDescriptorList, "EMM");
    }

    private void ApplyCaDescriptors(IEnumerable<Descriptor> descriptors, string role)
    {
        foreach (var descriptor in descriptors)
        {
            if (descriptor.DescriptorTag != 0x09)
                continue;

            var caPid = descriptor switch
            {
                CaDescriptorCustom_0x09 custom => custom.CaPid,
                CaDescriptor_0x09 standard => standard.CaPid,
                _ => (ushort)0
            };

            if (caPid is not 0 and not 0x1FFF)
                ExpectPid(caPid, role);
        }
    }

    /// <summary>PMT ES label: AIT only with 0x6F; otherwise stream type name from SI (never guessed AIT for 0x05).</summary>
    private static string? GetEsTypeLabel(EsInfo es)
    {
        if (HasDescriptorTag(es.EsDescriptorList, 0x6F))
            return "AIT";

        return es.StreamTypeName;
    }

    private static bool HasDescriptorTag(IEnumerable<Descriptor>? descriptors, byte tag)
    {
        if (descriptors is null)
            return false;

        foreach (var descriptor in descriptors)
        {
            if (descriptor?.DescriptorTag == tag)
                return true;
        }

        return false;
    }

    private void ExpectPid(ushort pid, string? description)
    {
        _expected.Add(pid);
        if (description is not null)
            SetPidRole(pid, description);
    }

    private void SetReserved(ReservedPids pid, string description)
    {
        var value = (ushort)pid;
        _expected.Add(value);
        SetPidRole(value, description);
    }

    private void SetPidRole(ushort pid, string description) =>
        _descriptions[pid] = description;

    private static string? TryGetServiceName(IEnumerable<Descriptor>? descriptors)
    {
        if (descriptors is null)
            return null;

        foreach (var descriptor in descriptors)
        {
            if (descriptor is null)
                continue;

            if (descriptor is ServiceDescriptor_0x48 service &&
                !string.IsNullOrWhiteSpace(service.ServiceName))
                return service.ServiceName.Trim();
        }

        return null;
    }
}
