using Avalonia;
using Avalonia.Controls;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.Controls;

public partial class Tr101290PanelControl : UserControl
{
    public static readonly StyledProperty<Tr101290ErrorStore?> StoreProperty =
        AvaloniaProperty.Register<Tr101290PanelControl, Tr101290ErrorStore?>(nameof(Store));

    public static readonly StyledProperty<int> DataRevisionProperty =
        AvaloniaProperty.Register<Tr101290PanelControl, int>(nameof(DataRevision));

    private Tr101290ViewFilter _filter = Tr101290ViewFilter.All;

    public Tr101290ErrorStore? Store
    {
        get => GetValue(StoreProperty);
        set => SetValue(StoreProperty, value);
    }

    public int DataRevision
    {
        get => GetValue(DataRevisionProperty);
        set => SetValue(DataRevisionProperty, value);
    }

    public Tr101290PanelControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StoreProperty || change.Property == DataRevisionProperty)
            Refresh();
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox filterBox)
            return;

        _filter = filterBox.SelectedIndex switch
        {
            1 => Tr101290ViewFilter.ErrorsOnly,
            2 => Tr101290ViewFilter.Priority1,
            3 => Tr101290ViewFilter.Priority2,
            4 => Tr101290ViewFilter.Priority3,
            _ => Tr101290ViewFilter.All,
        };
        Refresh();
    }

    private void Refresh()
    {
        if (SummaryText is null || IndicatorList is null || JournalList is null)
            return;

        var snapshot = Store?.GetSnapshot(_filter) ?? Tr101290Snapshot.Empty;
        SummaryText.Text = snapshot.Summary;
        IndicatorList.ItemsSource = snapshot.Rows;
        JournalList.ItemsSource = snapshot.Journal;
    }
}
