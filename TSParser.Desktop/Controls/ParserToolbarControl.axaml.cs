using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TSParser.Desktop.Services;
using TSParser.Desktop.ViewModels;
using TSParser.Desktop.Views.Dialogs;

namespace TSParser.Desktop.Controls;

public partial class ParserToolbarControl : UserControl
{
    private readonly List<NetworkInterfaceService.BindOption> _bindOptions = [];
    private string _multicastEndpoint = "239.1.2.3:1234";
    private bool _sessionBusy;
    private string? _sessionError;

    public ParserToolbarControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainWindowViewModel? Shell => DataContext as MainWindowViewModel;

    public string StatusDisplay =>
        !string.IsNullOrEmpty(_sessionError) ? _sessionError : Shell?.StatusText ?? "Idle";

    public bool SessionBusy => _sessionBusy;

    public bool IsUdpRunning =>
        Shell?.Session.InputMode == TsParserSessionInputMode.Udp && Shell.Session.IsRunning;

    public bool CanPlayUdp =>
        !SessionBusy && !IsUdpRunning && TsParserSessionService.TryParseMulticastEndpoint(_multicastEndpoint, out _, out _);

    public void RequestOpenFile() => OnOpenFileClick(this, new RoutedEventArgs());

    public void FocusUdpSettings()
    {
        SourceSettings.IsExpanded = true;
        Dispatcher.UIThread.Post(() =>
        {
            EndpointBox.Focus();
            EndpointBox.SelectAll();
        });
    }

    public void RequestBitrateSettings() => OnBitrateClick(this, new RoutedEventArgs());

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _bindOptions.Clear();
        _bindOptions.AddRange(Shell?.NetworkInterfaces.GetBindOptions() ?? []);
        BindCombo.ItemsSource = _bindOptions.Select(o => o.Label).ToList();
        BindCombo.SelectedIndex = 0;

        if (!string.IsNullOrEmpty(Shell?.Session.CurrentMulticastEndpoint))
            _multicastEndpoint = Shell.Session.CurrentMulticastEndpoint;

        EndpointBox.Text = _multicastEndpoint;
        EndpointBox.TextChanged += (_, _) =>
        {
            _multicastEndpoint = EndpointBox.Text ?? "";
            RefreshToolbarState();
        };

        if (Shell is INotifyPropertyChanged npc)
            npc.PropertyChanged += OnShellPropertyChanged;

        RefreshToolbarState();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.StatusText))
            RefreshToolbarState();
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is null || SessionBusy)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open transport stream",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MPEG-TS")
                {
                    Patterns = ["*.ts", "*.m2ts", "*.mpg", "*.mpeg"],
                    MimeTypes = ["video/mp2t"]
                }
            ]
        }).ConfigureAwait(true);

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            return;

        _sessionError = null;
        await RunSessionAsync(() => Shell.Session.OpenFileAsync(path)).ConfigureAwait(true);
    }

    private async void OnBitrateClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is null)
            return;

        var owner = GetOwnerWindow();
        if (owner is null)
            return;

        var vm = new BitrateSettingsViewModel(Shell.Session, Shell.BitrateStore);
        var dialog = new BitrateSettingsWindow { DataContext = vm };
        var accepted = await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
            return;

        if (vm.NeedsParserRestart)
        {
            await RunSessionAsync(() => Shell.Session.RestartCurrentSessionAsync()).ConfigureAwait(true);
            Shell.ShowStatus("Bitrate settings applied; current source restarted.");
        }

        Shell.OnChartSettingsChanged();
    }

    private async void OnEwsClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is null)
            return;

        var owner = GetOwnerWindow();
        if (owner is null)
            return;

        var vm = new EwsSettingsViewModel(Shell.Session);
        var dialog = new EwsSettingsWindow { DataContext = vm };
        var accepted = await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
            return;

        if (vm.NeedsRestart)
        {
            await RunSessionAsync(() => Shell.Session.RestartCurrentSessionAsync()).ConfigureAwait(true);
            Shell.ShowStatus("EWS settings applied; current source restarted.");
        }
    }

    private async void OnPlpClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is null)
            return;

        var owner = GetOwnerWindow();
        if (owner is null)
            return;

        var vm = new PlpSettingsViewModel(Shell.Session);
        var dialog = new PlpSettingsWindow { DataContext = vm };
        var accepted = await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
        if (!accepted)
            return;

        if (vm.NeedsRestart)
        {
            await RunSessionAsync(() => Shell.Session.RestartCurrentSessionAsync()).ConfigureAwait(true);
            Shell.ShowStatus("PLP settings applied; current source restarted.");
        }
    }

    private void OnPlayUdpClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is null || !CanPlayUdp)
            return;

        _sessionError = null;
        var bind = GetSelectedBindAddress();
        var endpoint = _multicastEndpoint.Trim();
        _ = RunSessionAsync(() => Shell.Session.StartUdpAsync(endpoint, bind));
    }

    private void OnStopUdpClick(object? sender, RoutedEventArgs e)
    {
        _sessionError = null;
        Shell?.Session.Stop();
        RefreshToolbarState();
    }

    private string? GetSelectedBindAddress()
    {
        var index = BindCombo.SelectedIndex;
        if (index < 0 || index >= _bindOptions.Count)
            return null;

        return _bindOptions[index].Address;
    }

    private async Task RunSessionAsync(Func<Task> start)
    {
        if (SessionBusy)
            return;

        _sessionBusy = true;
        RefreshToolbarState();

        try
        {
            await start().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _sessionError = ex.Message;
                Shell?.ShowStatus(ex.Message);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _sessionBusy = false;
                RefreshToolbarState();
            });
        }
    }

    private Window? GetOwnerWindow() =>
        TopLevel.GetTopLevel(this) as Window
        ?? (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private void RefreshToolbarState()
    {
        var udpRunning = IsUdpRunning;
        var busy = SessionBusy;
        var canPlay = CanPlayUdp;

        OpenFileButton.IsEnabled = !busy;
        BitrateButton.IsEnabled = !busy;
        EwsButton.IsEnabled = !busy;
        PlpButton.IsEnabled = !busy;
        EndpointBox.IsEnabled = !udpRunning;
        BindCombo.IsEnabled = !udpRunning;
        PlayButton.IsEnabled = canPlay;
        StopButton.IsEnabled = Shell?.Session.IsRunning == true;
    }
}
