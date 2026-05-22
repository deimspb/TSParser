using TSParser.Descriptors;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Desktop.Models;

namespace TSParser.Desktop.Services;

public enum DetailViewMode
{
    Hex,
    String
}

public static class ParseDisplayFormatter
{
    public static string Format(TableTreeNode? node, DetailViewMode mode)
    {
        if (node?.Payload is null)
            return "";

        return mode switch
        {
            DetailViewMode.String => FormatString(node.Payload),
            _ => ""
        };
    }

    private static string FormatString(object payload) => payload switch
    {
        Table table => table.Print(0),
        Descriptor descriptor => descriptor.Print(0),
        EsInfo es => es.Print(0),
        ushort pid => $"Transport stream PID 0x{pid:X4} ({pid})",
        _ => payload.ToString() ?? ""
    };
}
