using System.Text;
using TSParser.Descriptors;
using TSParser.Desktop.Models;
using TSParser.Tables;
using TSParser.Tables.DvbTables;

namespace TSParser.Desktop.Services;

public static class HexDumpFormatter
{
    public const int BytesPerLine = 16;

    /// <summary>Monospace characters per display column (hex + space).</summary>
    public const int CharsPerColumn = 3;

    /// <summary>Fixed width (px) for one byte column in the hex grid.</summary>
    public const double ByteColumnWidth = 21;

    public static readonly IReadOnlyList<string> ColumnHeaders =
        Enumerable.Range(1, BytesPerLine).Select(i => $"{i:X2}").ToArray();

    public static byte[]? TryGetRawBytes(TableTreeNode? node) =>
        node?.Payload is null ? null : TryGetRawBytes(node.Payload);

    public static byte[]? TryGetRawBytes(object payload) => payload switch
    {
        Table table => table.TableBytes.ToArray(),
        Descriptor descriptor => descriptor.Data.ToArray(),
        _ => null
    };

    public static string FormatOffset(int byteOffset) => $"{byteOffset:X4}";

    public static string[] FormatDataCells(ReadOnlySpan<byte> bytes, int offset, int count)
    {
        var cells = new string[BytesPerLine];
        for (var i = 0; i < BytesPerLine; i++)
            cells[i] = i < count ? $"{bytes[offset + i]:X2}" : "";
        return cells;
    }

    public static string FormatFixedWidthLine(ReadOnlySpan<byte> bytes, int offset, int count)
    {
        var sb = new StringBuilder(BytesPerLine * CharsPerColumn);
        for (var i = 0; i < BytesPerLine; i++)
            sb.Append(i < count ? $"{bytes[offset + i]:X2} " : "   ");

        return sb.ToString();
    }

    public static string BuildDisplayText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return "";

        var sb = new StringBuilder();
        for (var offset = 0; offset < bytes.Length; offset += BytesPerLine)
        {
            if (offset > 0)
                sb.AppendLine();

            var lineLength = Math.Min(BytesPerLine, bytes.Length - offset);
            sb.Append(FormatFixedWidthLine(bytes, offset, lineLength));
        }

        return sb.ToString();
    }

    public static string GetCopyText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return "";

        var sb = new StringBuilder(bytes.Length * 3 - 1);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append($"{bytes[i]:X2}");
        }

        return sb.ToString();
    }

    public static IReadOnlyList<(string Offset, string[] Cells)> BuildLines(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return Array.Empty<(string, string[])>();

        var lineCount = (bytes.Length + BytesPerLine - 1) / BytesPerLine;
        var lines = new (string Offset, string[] Cells)[lineCount];
        for (var offset = 0; offset < bytes.Length; offset += BytesPerLine)
        {
            var lineLength = Math.Min(BytesPerLine, bytes.Length - offset);
            var index = offset / BytesPerLine;
            lines[index] = (FormatOffset(offset), FormatDataCells(bytes, offset, lineLength));
        }

        return lines;
    }
}
