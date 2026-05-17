using TSParser.Descriptors;
using TSParser.Descriptors.Dvb;
using TSParser.Enums;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;
using TSParser.Tables.Scte35;

namespace TSParser.Web.Services;

/// <summary>Maps observed transport-stream PIDs to human-readable labels (PAT/PMT/SDT + reserved PIDs).</summary>
internal sealed class StreamPidCatalog
{
    private readonly object _sync = new();
    private readonly HashSet<ushort> _observed = [];
    private readonly Dictionary<ushort, string> _descriptions = new();
    private readonly Dictionary<ushort, string> _serviceNamesByProgram = new();

    public void Clear()
    {
        lock (_sync)
        {
            _observed.Clear();
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

                        SetDescription(record.Pid, "PMT");
                    }
                    break;

                case TsTableKind.Cat:
                    SetReserved(ReservedPids.CAT, "CAT");
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
                    SetDescription(mip.TablePid, "MIP");
                    break;

                case TsTableKind.Ait when table is AIT ait:
                    SetDescription(ait.TablePid, "AIT");
                    break;

                case TsTableKind.Scte35 when table is SCTE35 scte:
                    SetDescription(scte.TablePid, "SCTE-35");
                    break;

                case TsTableKind.Ews when table is EWS ews:
                    SetDescription(ews.TablePid, "EWS");
                    break;

                case TsTableKind.Eews when table is EEWS eews:
                    SetDescription(eews.TablePid, "EEWS");
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

    public IReadOnlyList<(ushort Pid, string Label)> GetSortedEntries()
    {
        lock (_sync)
        {
            return _observed
                .OrderBy(p => p)
                .Select(p => (p, FormatPidLabel(p, ResolveDescription(p))))
                .ToList();
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
        SetDescription(pmt.TablePid, $"PMT{pmtSuffix}");

        if (pmt.PcrPid is not 0 and not 0x1FFF)
            SetDescription(pmt.PcrPid, $"PCR{pmtSuffix}");

        if (pmt.EsInfoList is not null)
        {
            foreach (var es in pmt.EsInfoList)
            {
                var typeName = GetShortStreamTypeName(es.StreamType);
                SetDescription(es.ElementaryPid, $"{typeName}{pmtSuffix}");
            }
        }
    }

    private string ResolveDescription(ushort pid)
    {
        if (_descriptions.TryGetValue(pid, out var known))
            return known;

        if (TryGetReservedDescription(pid, out var reserved))
            return reserved;

        return "private_sections MPEG2";
    }

    private static bool TryGetReservedDescription(ushort pid, out string description)
    {
        if (Enum.IsDefined(typeof(ReservedPids), (int)pid))
        {
            description = (ReservedPids)pid switch
            {
                ReservedPids.PAT => "PAT",
                ReservedPids.CAT => "CAT",
                ReservedPids.NIT => "NIT",
                ReservedPids.SDT => "SDT/BAT",
                ReservedPids.EIT => "EIT",
                ReservedPids.TDT => "TOT/TDT",
                ReservedPids.NullPacket => "NULL Packets (Stuffing)",
                ReservedPids.NetworkSync => "MIP",
                ReservedPids.RST => "RST",
                ReservedPids.RNT => "RNT",
                ReservedPids.LLinbandSignalink => "Linkage",
                ReservedPids.Measurement => "Measurement",
                ReservedPids.DIT => "DIT",
                ReservedPids.SIT => "SIT",
                _ => ((ReservedPids)pid).ToString()
            };
            return true;
        }

        description = "";
        return false;
    }

    private void SetReserved(ReservedPids pid, string description) =>
        SetDescription((ushort)pid, description);

    private void SetDescription(ushort pid, string description) =>
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

    private static string FormatPidLabel(ushort pid, string description) =>
        $"pid: 0x{pid:X} ({pid}) => {description}";

    private static string GetShortStreamTypeName(byte streamType) => streamType switch
    {
        0x01 => "Video MPEG1",
        0x02 => "Video MPEG2",
        0x03 => "Audio MPEG1",
        0x04 => "Audio MPEG2",
        0x0F => "Audio AAC",
        0x11 => "Audio MPEG4",
        0x1B => "Video H.264 (AVC)",
        0x24 => "Video H.265 (HEVC)",
        0x86 => "SCTE-35",
        0x05 => "AIT",
        _ => $"Stream 0x{streamType:X2}"
    };
}
