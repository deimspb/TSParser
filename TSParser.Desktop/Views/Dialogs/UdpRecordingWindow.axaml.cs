using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TSParser.Desktop.Views.Dialogs;

public partial class UdpRecordingWindow : Window
{
    public UdpRecordingWindow() => InitializeComponent();

    public UdpRecordingOptions? Result { get; private set; }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Record UDP transport stream",
            SuggestedFileName = $"udp-{DateTime.Now:yyyyMMdd-HHmmss}.ts",
            DefaultExtension = "ts",
            ShowOverwritePrompt = false,
            FileTypeChoices =
            [
                new FilePickerFileType("MPEG-TS") { Patterns = ["*.ts"], MimeTypes = ["video/mp2t"] }
            ]
        }).ConfigureAwait(true);

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            PathBox.Text = path;
    }

    private void OnLimitKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LimitBox is not null)
            LimitBox.Text = LimitKindBox.SelectedIndex == 0 ? "10" : "1024";
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;
        var path = PathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowError("Choose a destination file.");
            return;
        }

        if (!double.TryParse(LimitBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || value <= 0)
        {
            ShowError("Enter a positive limit.");
            return;
        }

        try
        {
            Result = LimitKindBox.SelectedIndex == 0
                ? new UdpRecordingOptions { FilePath = path, MaxDuration = TimeSpan.FromMinutes(value) }
                : new UdpRecordingOptions { FilePath = path, MaxBytes = checked((long)(value * 1024 * 1024)) };
            Close(true);
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
        {
            ShowError("The limit is too large.");
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
