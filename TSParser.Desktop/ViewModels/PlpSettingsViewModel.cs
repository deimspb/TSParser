using CommunityToolkit.Mvvm.ComponentModel;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.ViewModels;

public sealed partial class PlpSettingsViewModel : ViewModelBase
{
    private readonly TsParserSessionService _session;

    public PlpSettingsViewModel(TsParserSessionService session)
    {
        _session = session;
        LoadFromSession();
    }

    public bool NeedsRestart => _session.HasActiveSource;

    [ObservableProperty]
    private string _plpPidsText = "";

    [ObservableProperty]
    private string? _errorMessage;

    public void LoadFromSession()
    {
        var s = _session.Settings;
        PlpPidsText = FormatPidList(s.T2miPids);
        ErrorMessage = null;
    }

    public bool TrySave()
    {
        ErrorMessage = null;

        if (!TsPidListParser.TryParseList(PlpPidsText, out var pids, out var err))
        {
            ErrorMessage = $"PLP: {err}";
            return false;
        }

        _session.Settings.T2miEnabled = pids.Count > 0;
        _session.Settings.SetT2miPids(pids);
        return true;
    }

    private static string FormatPidList(IReadOnlyList<ushort> pids) =>
        pids.Count == 0 ? "" : string.Join(", ", pids.Select(p => $"0x{p:X4}"));
}
