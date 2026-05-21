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

        var content = ParseDisplayFormatter.Format(node, _mode);
        var showPlaceholder = node is null || string.IsNullOrEmpty(content);

        PlaceholderText.IsVisible = showPlaceholder;
        ContentBox.IsVisible = !showPlaceholder;

        if (!showPlaceholder)
            ContentBox.Text = content;
    }
}
