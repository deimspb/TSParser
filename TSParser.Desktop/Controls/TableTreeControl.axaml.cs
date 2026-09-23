using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
        if (EmptyText is null || RootItems is null)
            return;

        var categories = (Store?.RootCategories ?? Array.Empty<TableTreeNode>())
            .Where(MatchesTypeFilter)
            .Where(MatchesSearch)
            .ToList();
        var isEmpty = categories.Count == 0;

        EmptyText.IsVisible = isEmpty;
        RootItems.IsVisible = !isEmpty;
        RootItems.ItemsSource = isEmpty ? null : categories;
        RefreshNodePresentation();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => RefreshNodes();

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e) => RefreshNodes();

    public void SetFilter(int selectedIndex)
    {
        TypeFilter.SelectedIndex = selectedIndex;
        RefreshNodes();
    }

    private bool MatchesTypeFilter(TableTreeNode node) => TypeFilter?.SelectedIndex switch
    {
        1 => node.Label != "PIDs" && !node.Label.Contains("PLP", StringComparison.OrdinalIgnoreCase),
        2 => node.Label == "PIDs",
        3 => node.Label.Contains("PLP", StringComparison.OrdinalIgnoreCase),
        4 => node.Label.StartsWith("PMT", StringComparison.OrdinalIgnoreCase)
             || node.Label.StartsWith("SDT", StringComparison.OrdinalIgnoreCase)
             || node.Label.Contains("PLP", StringComparison.OrdinalIgnoreCase),
        _ => true
    };

    private bool MatchesSearch(TableTreeNode node)
    {
        var query = SearchBox?.Text?.Trim();
        if (string.IsNullOrEmpty(query))
            return true;

        if (ContainsMatch(node, query))
        {
            ExpandMatchingBranches(node, query);
            return true;
        }

        return false;
    }

    private static bool ContainsMatch(TableTreeNode node, string query) =>
        node.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
        || node.Children.Any(child => ContainsMatch(child, query));

    private static bool ExpandMatchingBranches(TableTreeNode node, string query)
    {
        var childMatch = false;
        foreach (var child in node.Children)
            childMatch |= ExpandMatchingBranches(child, query);

        var selfMatch = node.Label.Contains(query, StringComparison.OrdinalIgnoreCase);
        if (childMatch)
            node.IsExpanded = true;
        return selfMatch || childMatch;
    }

    private void RefreshNodePresentation()
    {
        foreach (var child in this.GetVisualDescendants().OfType<TableTreeNodeControl>())
            child.RefreshPresentation();
    }
}
