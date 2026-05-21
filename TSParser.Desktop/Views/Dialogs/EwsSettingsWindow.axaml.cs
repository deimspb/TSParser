using Avalonia.Controls;
using Avalonia.Interactivity;
using TSParser.Desktop.ViewModels;

namespace TSParser.Desktop.Views.Dialogs;

public partial class EwsSettingsWindow : Window
{
    public EwsSettingsWindow()
    {
        InitializeComponent();
    }

    private EwsSettingsViewModel? ViewModel => DataContext as EwsSettingsViewModel;

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.TrySave() == true)
            Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) =>
        Close(false);
}
