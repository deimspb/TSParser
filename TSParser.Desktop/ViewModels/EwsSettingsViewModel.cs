using CommunityToolkit.Mvvm.ComponentModel;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.ViewModels;

public sealed partial class EwsSettingsViewModel : ViewModelBase
{
    private readonly TsParserSessionService _session;

    public EwsSettingsViewModel(TsParserSessionService session)
    {
        _session = session;
        LoadFromSession();
    }

    public bool NeedsRestart => _session.HasActiveSource;

    [ObservableProperty]
    private string _ewsPidsText = "";

    [ObservableProperty]
    private string _eewsPidsText = "";

    [ObservableProperty]
    private string? _errorMessage;

    public void LoadFromSession()
    {
        var s = _session.Settings;
        EwsPidsText = FormatPidList(s.EwsPids);
        EewsPidsText = FormatPidList(s.EewsPids);
        ErrorMessage = null;
    }

    public bool TrySave()
    {
        ErrorMessage = null;

        if (!TsPidListParser.TryParseList(EwsPidsText, out var ews, out var err))
        {
            ErrorMessage = $"EWS: {err}";
            return false;
        }

        if (!TsPidListParser.TryParseList(EewsPidsText, out var eews, out err))
        {
            ErrorMessage = $"EEWS: {err}";
            return false;
        }

        _session.Settings.SetEwsPids(ews);
        _session.Settings.SetEewsPids(eews);
        return true;
    }

    private static string FormatPidList(IReadOnlyList<ushort> pids) =>
        pids.Count == 0 ? "" : string.Join(", ", pids.Select(p => $"0x{p:X4}"));
}
