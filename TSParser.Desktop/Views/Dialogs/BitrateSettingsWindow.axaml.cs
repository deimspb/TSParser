using Avalonia.Controls;
using Avalonia.Interactivity;
using TSParser.Desktop.ViewModels;

namespace TSParser.Desktop.Views.Dialogs;

public partial class BitrateSettingsWindow : Window
{
    public BitrateSettingsWindow()
    {
        InitializeComponent();
    }

    private BitrateSettingsViewModel? ViewModel => DataContext as BitrateSettingsViewModel;

    private void OnRefreshPidsClick(object? sender, RoutedEventArgs e) =>
        ViewModel?.RefreshObservedPids();

    private void OnSelectAllPidsClick(object? sender, RoutedEventArgs e) =>
        ViewModel?.SelectAllChartPids();

    private void OnClearPidsClick(object? sender, RoutedEventArgs e) =>
        ViewModel?.ClearChartPids();

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.TrySave() == true)
            Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) =>
        Close(false);
}
