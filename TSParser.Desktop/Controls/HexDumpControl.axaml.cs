using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.Controls;

public partial class HexDumpControl : UserControl
{
    public static readonly StyledProperty<byte[]?> BytesProperty =
        AvaloniaProperty.Register<HexDumpControl, byte[]?>(nameof(Bytes));

    private byte[] _bytes = [];

    public byte[]? Bytes
    {
        get => GetValue(BytesProperty);
        set => SetValue(BytesProperty, value);
    }

    public HexDumpControl()
    {
        InitializeComponent();
        ColumnHeaderItems.ItemsSource = HexDumpFormatter.ColumnHeaders;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BytesProperty)
            RebuildView();
    }

    private void RebuildView()
    {
        _bytes = Bytes ?? [];
        OffsetPanel.Children.Clear();

        if (_bytes.Length == 0)
        {
            DataText.Text = "";
            return;
        }

        var lines = HexDumpFormatter.BuildLines(_bytes);
        foreach (var (offset, _) in lines)
        {
            OffsetPanel.Children.Add(new TextBlock
            {
                Classes = { "hex-dump-offset" },
                Text = offset
            });
        }

        DataText.Text = HexDumpFormatter.BuildDisplayText(_bytes);
        DataScroll.Offset = new Vector(0, 0);
        OffsetScroll.Offset = new Vector(0, 0);
    }

    private void OnDataScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        OffsetScroll.Offset = new Vector(0, DataScroll.Offset.Y);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || (e.KeyModifiers & KeyModifiers.Control) == 0)
            return;

        if (!string.IsNullOrEmpty(DataText.SelectedText))
            return;

        CopyHexToClipboard();
        e.Handled = true;
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e) => CopyHexToClipboard();

    private async void CopyHexToClipboard()
    {
        if (_bytes.Length == 0)
            return;

        var text = HexDumpFormatter.GetCopyText(_bytes);
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
    }
}
