using Avalonia.Controls;
using TSParser.Desktop.ViewModels;

namespace TSParser.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _closeAfterDispose;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;

        TableTree.NodeSelected += node =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SelectTreeNode(node);
        };
        ServicesTree.NodeSelected += node =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SelectTreeNode(node);
        };
        ServicesTree.SetFilter(4);
    }

    private void OnEmptyOpenFileClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ParserToolbar.RequestOpenFile();

    private void OnEmptyConnectUdpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ParserToolbar.FocusUdpSettings();

    private void OnChartSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ParserToolbar.RequestBitrateSettings();

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeAfterDispose)
            return;

        if (DataContext is not IAsyncDisposable disposable)
            return;

        e.Cancel = true;
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(true);
        }
        finally
        {
            _closeAfterDispose = true;
            Close();
        }
    }
}

