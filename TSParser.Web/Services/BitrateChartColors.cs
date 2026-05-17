namespace TSParser.Web.Services;

internal static class BitrateChartColors
{
    private static readonly string[] Palette =
    [
        "#e45756",
        "#f58518",
        "#ffc20a",
        "#54a24b",
        "#4c78a8",
        "#b279a2",
        "#72b7b2",
        "#9d755d",
        "#bab0ac",
        "#5c6bc0",
        "#26a69a",
        "#ab47bc",
    ];

    public static string ForIndex(int index) => Palette[((index % Palette.Length) + Palette.Length) % Palette.Length];

    public static string SumLine => "#212529";
}
