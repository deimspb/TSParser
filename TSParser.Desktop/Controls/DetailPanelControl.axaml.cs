using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TSParser.Desktop.Models;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.Controls;

public partial class DetailPanelControl : UserControl
{
    public static readonly StyledProperty<TableTreeNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<DetailPanelControl, TableTreeNode?>(nameof(SelectedNode));

    private DetailViewMode _mode = DetailViewMode.String;
    private TableTreeNode? _lastNode;

    public TableTreeNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public DetailPanelControl()
    {
        InitializeComponent();
        UpdateModeButtons();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedNodeProperty)
        {
            if (!ReferenceEquals(SelectedNode, _lastNode))
            {
                _lastNode = SelectedNode;
                RefreshContent();
            }
        }
    }

    private void OnHexModeClick(object? sender, RoutedEventArgs e) => SetMode(DetailViewMode.Hex);

    private void OnStringModeClick(object? sender, RoutedEventArgs e) => SetMode(DetailViewMode.String);

    private void SetMode(DetailViewMode mode)
    {
        if (_mode == mode)
            return;

        _mode = mode;
        UpdateModeButtons();
        RefreshContent();
    }

    private void UpdateModeButtons()
    {
        HexModeButton.IsChecked = _mode == DetailViewMode.Hex;
        StringModeButton.IsChecked = _mode == DetailViewMode.String;
    }

    private void RefreshContent()
    {
        var node = SelectedNode;
        TitleText.Text = node?.Label ?? "Details";

        if (node is null)
        {
            ShowPlaceholder("Select a table or descriptor in the tree.");
            return;
        }

        if (_mode == DetailViewMode.Hex)
        {
            var bytes = HexDumpFormatter.TryGetRawBytes(node);
            if (bytes is null)
            {
                ShowPlaceholder("No raw bytes available for this node.");
                return;
            }

            if (bytes.Length == 0)
            {
                ShowPlaceholder("(empty)");
                return;
            }

            PlaceholderText.IsVisible = false;
            ContentBorder.IsVisible = true;
            HexView.Bytes = bytes;
            HexView.IsVisible = true;
            StringScroll.IsVisible = false;
            return;
        }

        var content = ParseDisplayFormatter.Format(node, DetailViewMode.String);
        if (string.IsNullOrEmpty(content))
        {
            ShowPlaceholder("No displayable content for this node.");
            return;
        }

        PlaceholderText.IsVisible = false;
        ContentBorder.IsVisible = true;
        HexView.IsVisible = false;
        StringScroll.IsVisible = true;
        ContentText.Text = content;
    }

    private void ShowPlaceholder(string message)
    {
        PlaceholderText.Text = message;
        PlaceholderText.IsVisible = true;
        ContentBorder.IsVisible = false;
        HexView.IsVisible = false;
        StringScroll.IsVisible = false;
    }
}
