using System.Text;
using TSParser.Descriptors;
using TSParser.Tables;
using TSParser.Tables.DvbTables;
using TSParser.Web.Models;

namespace TSParser.Web.Services;

public enum DetailViewMode
{
    Hex,
    String
}

public static class ParseDisplayFormatter
{
    private const int BytesPerLine = 16;

    public static string Format(TableTreeNode? node, DetailViewMode mode)
    {
        if (node?.Payload is null)
            return "";

        return mode switch
        {
            DetailViewMode.Hex => FormatHex(node.Payload),
            DetailViewMode.String => FormatString(node.Payload),
            _ => ""
        };
    }

    private static string FormatHex(object payload) => payload switch
    {
        Table table => FormatHexBytes(table.TableBytes),
        Descriptor descriptor => FormatHexBytes(descriptor.Data),
        _ => "No raw bytes available for this node."
    };

    private static string FormatString(object payload) => payload switch
    {
        Table table => table.Print(0),
        Descriptor descriptor => descriptor.Print(0),
        EsInfo es => es.Print(0),
        ushort pid => $"Transport stream PID 0x{pid:X4} ({pid})",
        _ => payload.ToString() ?? ""
    };

    private static string FormatHexBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return "(empty)";

        var sb = new StringBuilder(bytes.Length * 4);
        for (var offset = 0; offset < bytes.Length; offset += BytesPerLine)
        {
            var lineLength = Math.Min(BytesPerLine, bytes.Length - offset);
            sb.Append($"{offset:X4}  ");
            for (var i = 0; i < lineLength; i++)
                sb.Append($"{bytes[offset + i]:X2} ");

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
