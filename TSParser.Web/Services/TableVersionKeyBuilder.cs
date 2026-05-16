using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Tables.Mip;

namespace TSParser.Web.Services;

internal static class TableVersionKeyBuilder
{
    public static string GetCategoryTitle(TsTableKind kind, Table table) => kind switch
    {
        TsTableKind.Pat => "PAT",
        TsTableKind.Pmt => "PMT",
        TsTableKind.Cat => "CAT",
        TsTableKind.Nit => table.TableId == 0x40 ? "NIT (actual)" : "NIT (other)",
        TsTableKind.Sdt => table.TableId == 0x42 ? "SDT (actual)" : "SDT (other)",
        TsTableKind.Bat => "BAT",
        TsTableKind.Eit => "EIT",
        TsTableKind.Tdt => "TDT",
        TsTableKind.Tot => "TOT",
        TsTableKind.Mip => "MIP",
        TsTableKind.Ait => "AIT",
        TsTableKind.Scte35 => "SCTE-35",
        TsTableKind.Ews => "EWS",
        TsTableKind.Eews => "EEWS",
        _ => kind.ToString()
    };

  public static bool UsesFlatVersions(TsTableKind kind) =>
        kind is TsTableKind.Pat or TsTableKind.Cat or TsTableKind.Tdt;

    public static string GetVersionPrefix(TsTableKind kind) => kind switch
    {
        TsTableKind.Pat => "PAT",
        TsTableKind.Pmt => "PMT",
        TsTableKind.Cat => "CAT",
        TsTableKind.Nit => "NIT",
        TsTableKind.Sdt => "SDT",
        TsTableKind.Bat => "BAT",
        TsTableKind.Eit => "EIT",
        TsTableKind.Tdt => "TDT",
        TsTableKind.Tot => "TOT",
        TsTableKind.Mip => "MIP",
        TsTableKind.Ait => "AIT",
        TsTableKind.Scte35 => "SCTE35",
        TsTableKind.Ews => "EWS",
        TsTableKind.Eews => "EEWS",
        _ => kind.ToString()
    };

    public static string GetStreamKey(TsTableKind kind, Table table) => kind switch
    {
        TsTableKind.Pat => "PAT",
        TsTableKind.Cat => "CAT",
        TsTableKind.Tdt => "TDT",
        TsTableKind.Pmt when table is PMT pmt =>
            $"PMT:pid=0x{pmt.TablePid:X4}:prog={pmt.ProgramNumber}",
        TsTableKind.Nit when table is NIT nit =>
            $"NIT:tid=0x{nit.TableId:X2}:net={nit.NetworkId}:sec={nit.SectionNumber}/{nit.LastSectionNumber}",
        TsTableKind.Sdt when table is SDT sdt =>
            $"SDT:tid=0x{sdt.TableId:X2}:ts={sdt.TransportStreamId}:onid={sdt.OriginalNetworkId}:sec={sdt.SectionNumber}/{sdt.LastSectionNumber}",
        TsTableKind.Bat when table is BAT bat =>
            $"BAT:bid={bat.BouquetId}:sec={bat.SectionNumber}/{bat.LastSectionNumber}",
        TsTableKind.Eit when table is EIT eit =>
            $"EIT:tid=0x{eit.TableId:X2}:svc={eit.ServiceId}:sec={eit.SectionNumber}/{eit.LastSectionNumber}",
        TsTableKind.Tot =>
            $"TOT:sec={table.SectionNumber}/{table.LastSectionNumber}",
        TsTableKind.Mip when table is MIP mip =>
            $"MIP:pid=0x{mip.TablePid:X4}",
        TsTableKind.Ait when table is AIT ait =>
            $"AIT:pid=0x{ait.TablePid:X4}:sec={ait.SectionNumber}/{ait.LastSectionNumber}",
        TsTableKind.Scte35 when table is SCTE35 scte =>
            $"SCTE35:pid=0x{scte.TablePid:X4}:sec={scte.SectionNumber}/{scte.LastSectionNumber}",
        TsTableKind.Ews when table is EWS ews =>
            $"EWS:pid=0x{ews.TablePid:X4}:sec={ews.SectionNumber}/{ews.LastSectionNumber}",
        TsTableKind.Eews when table is EEWS eews =>
            $"EEWS:pid=0x{eews.TablePid:X4}:sec={eews.SectionNumber}/{eews.LastSectionNumber}",
        _ => $"{kind}:{table.TableId:X2}:{table.SectionNumber}"
    };

    public static string GetStreamLabel(TsTableKind kind, Table table) => kind switch
    {
        TsTableKind.Pat or TsTableKind.Cat or TsTableKind.Tdt => "",
        TsTableKind.Pmt when table is PMT pmt =>
            $"0x{pmt.TablePid:X4} (prog {pmt.ProgramNumber})",
        TsTableKind.Nit when table is NIT nit =>
            $"net {nit.NetworkId} sec {nit.SectionNumber}/{nit.LastSectionNumber}",
        TsTableKind.Sdt when table is SDT sdt =>
            $"ts {sdt.TransportStreamId} sec {sdt.SectionNumber}/{sdt.LastSectionNumber}",
        TsTableKind.Bat when table is BAT bat =>
            $"bouquet {bat.BouquetId} sec {bat.SectionNumber}/{bat.LastSectionNumber}",
        TsTableKind.Eit when table is EIT eit =>
            $"0x{eit.TableId:X2} svc {eit.ServiceId} sec {eit.SectionNumber}/{eit.LastSectionNumber}",
        TsTableKind.Tot =>
            $"sec {table.SectionNumber}/{table.LastSectionNumber}",
        TsTableKind.Mip when table is MIP mip =>
            $"PID 0x{mip.TablePid:X4}",
        TsTableKind.Ait when table is AIT ait =>
            $"PID 0x{ait.TablePid:X4} sec {ait.SectionNumber}/{ait.LastSectionNumber}",
        TsTableKind.Scte35 when table is SCTE35 scte =>
            $"PID 0x{scte.TablePid:X4} sec {scte.SectionNumber}/{scte.LastSectionNumber}",
        TsTableKind.Ews when table is EWS ews =>
            $"PID 0x{ews.TablePid:X4} sec {ews.SectionNumber}/{ews.LastSectionNumber}",
        TsTableKind.Eews when table is EEWS eews =>
            $"PID 0x{eews.TablePid:X4} sec {eews.SectionNumber}/{eews.LastSectionNumber}",
        _ => GetStreamKey(kind, table)
    };
}
