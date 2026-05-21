using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TSParser.Desktop.Models;
using TSParser.Tables.DvbTables;

namespace TSParser.Desktop.Controls;

public partial class TableTreeNodeControl : UserControl
{
    public static readonly StyledProperty<TableTreeNode?> NodeProperty =
        AvaloniaProperty.Register<TableTreeNodeControl, TableTreeNode?>(nameof(Node));

    public TableTreeNode? Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public TableTreeNodeControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NodeProperty)
            RefreshPresentation();
    }

    public void RefreshPresentation()
    {
        var node = Node;
        if (node is null)
            return;

        LabelButton.Content = node.Label;

        var showToggle = node.Kind switch
        {
            TableTreeNodeKind.Version => MayHaveDescriptors(node) || node.Children.Count > 0,
            _ => node.Children.Count > 0
        };

        ToggleButton.IsVisible = showToggle;
        ToggleSpacer.IsVisible = !showToggle;
        if (showToggle)
            ToggleButton.Content = node.IsExpanded ? "▼" : "▶";

        ChildrenPanel.IsVisible = node.IsExpanded && node.Children.Count > 0;

        LabelButton.Classes.Clear();
        switch (node.Kind)
        {
            case TableTreeNodeKind.Category:
                LabelButton.Classes.Add("table-tree-label");
                LabelButton.Classes.Add("category");
                break;
            case TableTreeNodeKind.Stream:
                LabelButton.Classes.Add("table-tree-label");
                LabelButton.Classes.Add("stream");
                break;
            case TableTreeNodeKind.Version:
                LabelButton.Classes.Add("table-tree-label");
                LabelButton.Classes.Add(node.IsActive ? "active" : "stale");
                break;
            case TableTreeNodeKind.Pid when node.IsMissingFromStream:
                LabelButton.Classes.Add("table-tree-label");
                LabelButton.Classes.Add("pid-missing");
                break;
            default:
                LabelButton.Classes.Add("table-tree-label");
                break;
        }

        if (FindTreeHost()?.SelectedNodeId == node.Id)
            LabelButton.Classes.Add("selected");
    }

    private static bool MayHaveDescriptors(TableTreeNode versionNode) =>
        versionNode.Table switch
        {
            PAT => false,
            TDT => false,
            _ => true
        };

    private TableTreeControl? FindTreeHost() =>
        this.GetVisualAncestors().OfType<TableTreeControl>().FirstOrDefault();

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        if (Node is null)
            return;

        FindTreeHost()?.HandleToggleExpand(Node, !Node.IsExpanded);
    }

    private void OnSelectClick(object? sender, RoutedEventArgs e)
    {
        if (Node is null)
            return;

        FindTreeHost()?.HandleNodeSelected(Node);
    }
}
