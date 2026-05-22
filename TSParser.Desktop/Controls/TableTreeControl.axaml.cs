using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TSParser.Desktop.Models;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.Controls;

public partial class TableTreeControl : UserControl
{
    public static readonly StyledProperty<TableVersionStore?> StoreProperty =
        AvaloniaProperty.Register<TableTreeControl, TableVersionStore?>(nameof(Store));

    public static readonly StyledProperty<long> TreeRevisionProperty =
        AvaloniaProperty.Register<TableTreeControl, long>(nameof(TreeRevision));

    public static readonly StyledProperty<Guid?> SelectedNodeIdProperty =
        AvaloniaProperty.Register<TableTreeControl, Guid?>(nameof(SelectedNodeId));

    public event Action<TableTreeNode>? NodeSelected;

    public TableVersionStore? Store
    {
        get => GetValue(StoreProperty);
        set => SetValue(StoreProperty, value);
    }

    public long TreeRevision
    {
        get => GetValue(TreeRevisionProperty);
        set => SetValue(TreeRevisionProperty, value);
    }

    public Guid? SelectedNodeId
    {
        get => GetValue(SelectedNodeIdProperty);
        set => SetValue(SelectedNodeIdProperty, value);
    }

    public TableTreeControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TreeRevisionProperty || change.Property == StoreProperty)
            RefreshNodes();
        else if (change.Property == SelectedNodeIdProperty)
            RefreshNodePresentation();
    }

    public void HandleNodeSelected(TableTreeNode node)
    {
        Store?.SetSelectedNode(node.Id);
        NodeSelected?.Invoke(node);
        RefreshNodePresentation();
    }

    public void HandleToggleExpand(TableTreeNode node, bool expanded, TableTreeNodeControl source)
    {
        Store?.SetExpanded(node.Id, expanded);
        source.ReloadChildrenPanel();
        source.RefreshPresentation();
    }

    private void RefreshNodes()
    {
        var categories = Store?.RootCategories ?? Array.Empty<TableTreeNode>();
        var isEmpty = categories.Count == 0;

        EmptyText.IsVisible = isEmpty;
        RootItems.IsVisible = !isEmpty;
        RootItems.ItemsSource = isEmpty ? null : categories.ToList();
        RefreshNodePresentation();
    }

    private void RefreshNodePresentation()
    {
        foreach (var child in this.GetVisualDescendants().OfType<TableTreeNodeControl>())
            child.RefreshPresentation();
    }
}
