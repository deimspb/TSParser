namespace BlessManifest;

internal static class HexFormat
{
    public static string Byte(byte value) => $"0x{value:X2}";

    public static string UInt32(uint value) => $"0x{value:X}";
}
