namespace TSParser.Desktop.Services;

/// <summary>Builds the main window title from the active parser input source.</summary>
internal static class WindowTitleFormatter
{
    public const string AppName = "TSParser";

    public static string Format(TsParserSessionInputMode mode, string? fileDisplayName, string? multicastEndpoint) =>
        mode switch
        {
            TsParserSessionInputMode.File when !string.IsNullOrEmpty(fileDisplayName) =>
                $"{AppName} - {fileDisplayName}",
            TsParserSessionInputMode.Udp when !string.IsNullOrEmpty(multicastEndpoint) =>
                $"{AppName} - {multicastEndpoint}",
            _ => AppName
        };
}
