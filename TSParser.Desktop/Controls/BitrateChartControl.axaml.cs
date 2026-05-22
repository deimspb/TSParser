using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ScottPlot;
using ScottPlot.Plottables;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.Controls;

public partial class BitrateChartControl : UserControl
{
    private const double YAxisPaddingRatio = 0.12;
    private const double MinYAxisPaddingMbps = 0.5;
    private const double XAxisPaddingRatio = 0.02;
    private const double MinXAxisPadding = 0.25;

    private const double MinPlotDimension = 8;

    private int _appliedRevision = -1;
    private int _appliedConfigRevision = -1;
    private int _lastFitRevisionSeen = -1;
    private bool _renderPending;
    private readonly List<(string Name, string ColorHex)> _legendEntries = [];
    private readonly List<ChartSeries> _series = [];
    private string _xAxisUnit = "";
    private VerticalLine? _probeLine;
    private bool _probeHandlersAttached;

    public static readonly StyledProperty<BitrateHistoryStore?> StoreProperty =
        AvaloniaProperty.Register<BitrateChartControl, BitrateHistoryStore?>(nameof(Store));

    public static readonly StyledProperty<int> DataRevisionProperty =
        AvaloniaProperty.Register<BitrateChartControl, int>(nameof(DataRevision));

    public static readonly StyledProperty<int> ChartConfigRevisionProperty =
        AvaloniaProperty.Register<BitrateChartControl, int>(nameof(ChartConfigRevision));

    public static readonly StyledProperty<int> FitRevisionProperty =
        AvaloniaProperty.Register<BitrateChartControl, int>(nameof(FitRevision));

    public BitrateHistoryStore? Store
    {
        get => GetValue(StoreProperty);
        set => SetValue(StoreProperty, value);
    }

    public int DataRevision
    {
        get => GetValue(DataRevisionProperty);
        set => SetValue(DataRevisionProperty, value);
    }

    public int ChartConfigRevision
    {
        get => GetValue(ChartConfigRevisionProperty);
        set => SetValue(ChartConfigRevisionProperty, value);
    }

    public int FitRevision
    {
        get => GetValue(FitRevisionProperty);
        set => SetValue(FitRevisionProperty, value);
    }

    public BitrateChartControl()
    {
        InitializeComponent();
        PlotHost.LayoutUpdated += OnPlotHostLayoutUpdated;
        EnsureProbePointerHandlers();
    }

    private void EnsureProbePointerHandlers()
    {
        if (_probeHandlersAttached)
            return;

        _probeHandlersAttached = true;
        ChartPlot.PointerMoved += OnChartPointerMoved;
        ChartPlot.PointerExited += OnChartPointerExited;
        PlotHost.PointerExited += OnChartPointerExited;
    }

    private void OnPlotHostLayoutUpdated(object? sender, EventArgs e)
    {
        if (_renderPending)
            TryRender();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StoreProperty
            || change.Property == DataRevisionProperty
            || change.Property == ChartConfigRevisionProperty
            || change.Property == FitRevisionProperty)
        {
            TryRender();
        }
    }

    private void TryRender()
    {
        if (Store is null)
            return;

        var snapshot = Store.GetSnapshot();
        var hasData = snapshot.HasChartData;

        PlaceholderText.Text = BuildPlaceholderText(snapshot, hasData);
        ChartTitle.Text = BuildChartTitle(snapshot);
        ChartStats.Text = BuildStatsText(snapshot);

        if (!hasData)
        {
            _appliedRevision = -1;
            _renderPending = false;
            ChartPlot.IsVisible = false;
            PlaceholderText.IsVisible = true;
            ClearPlotAndLegend();
            return;
        }

        if (!HasValidPlotBounds())
        {
            _renderPending = true;
            ChartPlot.IsVisible = false;
            PlaceholderText.IsVisible = true;
            PlaceholderText.Text = "Rendering bitrate chart…";
            return;
        }

        _renderPending = false;
        ChartPlot.IsVisible = true;
        PlaceholderText.IsVisible = false;

        var fitChanged = FitRevision != _lastFitRevisionSeen;
        _lastFitRevisionSeen = FitRevision;

        var configChanged = ChartConfigRevision != _appliedConfigRevision;
        if (!_renderPending && !fitChanged && !configChanged && DataRevision == _appliedRevision)
            return;

        _appliedRevision = DataRevision;
        _appliedConfigRevision = ChartConfigRevision;

        switch (snapshot.Mode)
        {
            case BitrateChartMode.File when snapshot.HasChartData:
                BuildFileChart(snapshot);
                break;
            case BitrateChartMode.Udp when snapshot.HasChartData:
                BuildUdpChart(snapshot);
                break;
        }
    }

    private void BuildFileChart(BitrateChartSnapshot snapshot)
    {
        var usePercent = snapshot.FileLengthBytes is > 0;
        var allY = new List<double>();
        var allX = new List<double>();

        ClearPlotAndLegend();

        if (snapshot.ShowStreamOnChart && snapshot.FilePoints.Count > 0)
        {
            var xs = new double[snapshot.FilePoints.Count];
            var totalYs = new double[snapshot.FilePoints.Count];
            var usefulYs = new double[snapshot.FilePoints.Count];

            for (var i = 0; i < snapshot.FilePoints.Count; i++)
            {
                var point = snapshot.FilePoints[i];
                xs[i] = ToFileX(point.ByteOffset, snapshot.FileLengthBytes, usePercent);
                var totalMbps = point.TotalBitsPerSecond / 1_000_000d;
                var usefulMbps = point.UsefulBitsPerSecond / 1_000_000d;
                totalYs[i] = totalMbps;
                usefulYs[i] = usefulMbps;
                allY.Add(totalMbps);
                allY.Add(usefulMbps);
            }

            AddLine("Stream total", xs, totalYs, "#4682b4");
            AddLine("Stream useful", xs, usefulYs, "#228b22");
            allX.AddRange(xs);
        }

        if (snapshot.ShowIndividualPidsOnChart)
            AddPidFileTraces(snapshot, usePercent, allY, allX);

        if (snapshot.ShowPidSumOnChart && snapshot.SumFilePoints.Count > 0)
        {
            var xs = new double[snapshot.SumFilePoints.Count];
            var ys = new double[snapshot.SumFilePoints.Count];
            for (var i = 0; i < snapshot.SumFilePoints.Count; i++)
            {
                var point = snapshot.SumFilePoints[i];
                xs[i] = ToFileX(point.ByteOffset, snapshot.FileLengthBytes, usePercent);
                var mbps = point.BitsPerSecond / 1_000_000d;
                ys[i] = mbps;
                allY.Add(mbps);
            }

            AddLine("Selected sum", xs, ys, BitrateChartColors.SumLine, lineWidth: 3);
            allX.AddRange(xs);
        }

        FinalizeChart(
            allY,
            allX,
            usePercent ? "File position (%)" : "File offset (MB)",
            usePercent ? "Bitrate vs file position" : "Bitrate vs byte offset");
    }

    private void BuildUdpChart(BitrateChartSnapshot snapshot)
    {
        var allY = new List<double>();
        var allX = new List<double>();
        DateTime? origin = null;

        ClearPlotAndLegend();

        if (snapshot.ShowStreamOnChart && snapshot.UdpPoints.Count > 0)
        {
            origin = snapshot.UdpPoints[0].TimestampUtc;
            var xs = new double[snapshot.UdpPoints.Count];
            var totalYs = new double[snapshot.UdpPoints.Count];
            var usefulYs = new double[snapshot.UdpPoints.Count];

            for (var i = 0; i < snapshot.UdpPoints.Count; i++)
            {
                var point = snapshot.UdpPoints[i];
                xs[i] = (point.TimestampUtc - origin.Value).TotalSeconds;
                var totalMbps = point.TotalBitsPerSecond / 1_000_000d;
                var usefulMbps = point.UsefulBitsPerSecond / 1_000_000d;
                totalYs[i] = totalMbps;
                usefulYs[i] = usefulMbps;
                allY.Add(totalMbps);
                allY.Add(usefulMbps);
            }

            AddLine("Stream total", xs, totalYs, "#4682b4");
            AddLine("Stream useful", xs, usefulYs, "#228b22");
            allX.AddRange(xs);
        }

        if (snapshot.ShowIndividualPidsOnChart)
            AddPidUdpTraces(snapshot, ref origin, allY, allX);

        if (snapshot.ShowPidSumOnChart && snapshot.SumUdpPoints.Count > 0)
        {
            origin ??= snapshot.SumUdpPoints[0].TimestampUtc;
            var xs = new double[snapshot.SumUdpPoints.Count];
            var ys = new double[snapshot.SumUdpPoints.Count];
            for (var i = 0; i < snapshot.SumUdpPoints.Count; i++)
            {
                var point = snapshot.SumUdpPoints[i];
                xs[i] = (point.TimestampUtc - origin.Value).TotalSeconds;
                var mbps = point.BitsPerSecond / 1_000_000d;
                ys[i] = mbps;
                allY.Add(mbps);
            }

            AddLine("Selected sum", xs, ys, BitrateChartColors.SumLine, lineWidth: 3);
            allX.AddRange(xs);
        }

        FinalizeChart(allY, allX, "Time (s)", "Live bitrate");
    }

    private void AddPidFileTraces(
        BitrateChartSnapshot snapshot,
        bool usePercent,
        List<double> allY,
        List<double> allX)
    {
        var index = 0;
        foreach (var pid in snapshot.ChartPids)
        {
            if (!snapshot.PidFilePoints.TryGetValue(pid, out var points) || points.Count == 0)
            {
                index++;
                continue;
            }

            var xs = new double[points.Count];
            var ys = new double[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                xs[i] = ToFileX(point.ByteOffset, snapshot.FileLengthBytes, usePercent);
                var mbps = point.BitsPerSecond / 1_000_000d;
                ys[i] = mbps;
                allY.Add(mbps);
            }

            AddLine($"PID 0x{pid:X4}", xs, ys, BitrateChartColors.ForIndex(index));
            allX.AddRange(xs);
            index++;
        }
    }

    private void AddPidUdpTraces(
        BitrateChartSnapshot snapshot,
        ref DateTime? origin,
        List<double> allY,
        List<double> allX)
    {
        var index = 0;
        foreach (var pid in snapshot.ChartPids)
        {
            if (!snapshot.PidUdpPoints.TryGetValue(pid, out var points) || points.Count == 0)
            {
                index++;
                continue;
            }

            origin ??= points[0].TimestampUtc;
            var xs = new double[points.Count];
            var ys = new double[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                xs[i] = (point.TimestampUtc - origin.Value).TotalSeconds;
                var mbps = point.BitsPerSecond / 1_000_000d;
                ys[i] = mbps;
                allY.Add(mbps);
            }

            AddLine($"PID 0x{pid:X4}", xs, ys, BitrateChartColors.ForIndex(index));
            allX.AddRange(xs);
            index++;
        }
    }

    private void AddLine(string name, double[] xs, double[] ys, string colorHex, int lineWidth = 2)
    {
        if (xs.Length == 0)
            return;

        _legendEntries.Add((name, colorHex));
        _series.Add(new ChartSeries(name, colorHex, xs, ys));

        var line = ChartPlot.Plot.Add.ScatterLine(xs, ys);
        line.Color = ScottPlot.Color.FromHex(colorHex);
        line.LineWidth = lineWidth;
    }

    private void FinalizeChart(
        List<double> allY,
        List<double> allX,
        string xLabel,
        string title)
    {
        if (allY.Count == 0 || allX.Count == 0)
        {
            ChartPlot.IsVisible = false;
            PlaceholderText.IsVisible = true;
            return;
        }

        if (!HasValidPlotBounds())
        {
            _renderPending = true;
            ChartPlot.IsVisible = false;
            PlaceholderText.IsVisible = true;
            return;
        }

        var xRange = SanitizeAxisRange(ComputeXRange(allX));
        var yRange = SanitizeAxisRange(ComputeYRange(allY));

        ChartPlot.Plot.Title(title);
        ChartPlot.Plot.Axes.Bottom.Label.Text = xLabel;
        ChartPlot.Plot.Axes.Left.Label.Text = "Mbps";
        _xAxisUnit = xLabel;

        ChartPlot.Plot.Axes.SetLimits(xRange.Min, xRange.Max, yRange.Min, yRange.Max);
        ApplyExternalLegend();
        EnsureProbePlottables();
        HideProbe();
        SafeRefreshChart();
    }

    private void ClearPlotAndLegend()
    {
        ChartPlot.Plot.Clear();
        ChartPlot.Plot.HideLegend();
        _legendEntries.Clear();
        _series.Clear();
        _probeLine = null;
        LegendHost.Children.Clear();
        LegendHost.IsVisible = false;
        HideProbe();
    }

    private void EnsureProbePlottables()
    {
        _probeLine ??= ChartPlot.Plot.Add.VerticalLine(0);
        _probeLine.IsVisible = false;
        _probeLine.IsDraggable = false;
        _probeLine.LineWidth = 1.5f;
        _probeLine.LinePattern = LinePattern.Dashed;
        _probeLine.Color = ScottPlot.Color.FromHex("#6c757d");
    }

    private void OnChartPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ChartPlot.IsVisible || _series.Count == 0 || _probeLine is null)
        {
            HideProbe();
            return;
        }

        var position = e.GetCurrentPoint(ChartPlot).Position;
        if (position.X < 0 || position.Y < 0
            || position.X > ChartPlot.Bounds.Width || position.Y > ChartPlot.Bounds.Height)
        {
            HideProbe();
            return;
        }

        var pixel = new Pixel((float)position.X, (float)position.Y);
        var mouseCoords = ChartPlot.Plot.GetCoordinates(pixel);
        var x = mouseCoords.X;
        var limits = ChartPlot.Plot.Axes.GetLimits();

        if (x < limits.Left || x > limits.Right)
        {
            HideProbe();
            return;
        }

        _probeLine.X = x;
        _probeLine.IsVisible = true;

        var lines = new List<string> { $"{FormatProbeX(x)}" };
        var anyValue = false;

        foreach (var series in _series)
        {
            if (!TryInterpolateY(series.Xs, series.Ys, x, out var yMbps))
                continue;

            anyValue = true;
            lines.Add($"{series.Name}: {yMbps:F2} Mbps");
        }

        if (!anyValue)
        {
            HideProbe();
            return;
        }

        ProbeText.Text = string.Join(Environment.NewLine, lines);
        ProbeOverlay.IsVisible = true;
        SafeRefreshChart();
    }

    private void OnChartPointerExited(object? sender, PointerEventArgs e) => HideProbe();

    private void HideProbe()
    {
        if (_probeLine is not null)
            _probeLine.IsVisible = false;

        ProbeOverlay.IsVisible = false;

        if (ChartPlot.IsVisible)
            SafeRefreshChart();
    }

    private string FormatProbeX(double x)
    {
        if (_xAxisUnit.Contains('%', StringComparison.Ordinal))
            return $"X: {x:F2} %";

        if (_xAxisUnit.Contains("(s)", StringComparison.OrdinalIgnoreCase))
            return $"X: {x:F2} s";

        if (_xAxisUnit.Contains("(MB)", StringComparison.OrdinalIgnoreCase))
            return $"X: {x:F2} MB";

        return $"X: {x:F2}";
    }

    private static bool TryInterpolateY(double[] xs, double[] ys, double x, out double y)
    {
        y = 0;
        if (xs.Length == 0 || ys.Length != xs.Length)
            return false;

        if (x < xs[0] || x > xs[^1])
            return false;

        if (xs.Length == 1)
        {
            y = ys[0];
            return true;
        }

        var index = Array.BinarySearch(xs, x);
        if (index >= 0)
        {
            y = ys[index];
            return true;
        }

        index = ~index;
        if (index <= 0 || index >= xs.Length)
            return false;

        var x0 = xs[index - 1];
        var x1 = xs[index];
        var y0 = ys[index - 1];
        var y1 = ys[index];
        var t = (x - x0) / (x1 - x0);
        y = y0 + t * (y1 - y0);
        return true;
    }

    private sealed record ChartSeries(string Name, string ColorHex, double[] Xs, double[] Ys);

    private void ApplyExternalLegend()
    {
        LegendHost.Children.Clear();

        foreach (var (name, colorHex) in _legendEntries)
        {
            var swatch = new Border
            {
                Width = 16,
                Height = 3,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush(Avalonia.Media.Color.Parse(colorHex)),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };

            var item = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(0, 0, 14, 0),
                Children =
                {
                    swatch,
                    new TextBlock { Text = name },
                },
            };

            LegendHost.Children.Add(item);
        }

        LegendHost.IsVisible = _legendEntries.Count > 0;
    }

    private bool HasValidPlotBounds()
    {
        // PlotHost stays in the visual tree; ChartPlot.Bounds stay 0 while IsVisible=false.
        var w = PlotHost.Bounds.Width;
        var h = PlotHost.Bounds.Height;
        return w >= MinPlotDimension && h >= MinPlotDimension
               && !double.IsNaN(w) && !double.IsNaN(h);
    }

    private static (double Min, double Max) SanitizeAxisRange((double Min, double Max) range) =>
        double.IsFinite(range.Min) && double.IsFinite(range.Max) && range.Max > range.Min
            ? range
            : (0, 1);

    private void SafeRefreshChart()
    {
        try
        {
            ChartPlot.Refresh();
        }
        catch
        {
            _renderPending = false;
            ChartPlot.IsVisible = false;
            PlaceholderText.IsVisible = true;
            PlaceholderText.Text = "Bitrate chart failed to render.";
        }
    }

    private static double ToFileX(long byteOffset, long? fileLengthBytes, bool usePercent) =>
        usePercent && fileLengthBytes is > 0
            ? 100d * byteOffset / fileLengthBytes.Value
            : byteOffset / 1_000_000d;

    private static (double Min, double Max) ComputeXRange(IEnumerable<double> values)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;

        foreach (var x in values)
        {
            if (x < min) min = x;
            if (x > max) max = x;
        }

        if (double.IsInfinity(min) || double.IsInfinity(max))
            return (0, 1);

        return ExpandAxisRange(min, max, XAxisPaddingRatio, MinXAxisPadding);
    }

    private static (double Min, double Max) ComputeYRange(IEnumerable<double> values)
    {
        var max = double.NegativeInfinity;

        foreach (var value in values)
        {
            if (value > max)
                max = value;
        }

        if (double.IsNegativeInfinity(max))
            return (0, 1);

        if (max <= 0)
            return (0, 1);

        var topPad = Math.Max(max * YAxisPaddingRatio, MinYAxisPaddingMbps);
        return (0, max + topPad);
    }

    private static (double Min, double Max) ExpandAxisRange(double min, double max, double paddingRatio, double minPadding)
    {
        if (Math.Abs(max - min) < 1e-9)
        {
            var pad = Math.Max(Math.Abs(max) * paddingRatio, minPadding);
            return (min - pad, max + pad);
        }

        var span = max - min;
        var margin = Math.Max(span * paddingRatio, minPadding);
        return (min - margin, max + margin);
    }

    private static string BuildPlaceholderText(BitrateChartSnapshot snapshot, bool hasData)
    {
        if (hasData)
            return snapshot.Mode switch
            {
                BitrateChartMode.File => "Rendering bitrate chart…",
                BitrateChartMode.Udp => "Rendering live bitrate chart…",
                _ => ""
            };

        if (snapshot.Mode == BitrateChartMode.File)
        {
            if (snapshot.ShowPidSumOnChart && snapshot.ChartPids.Count >= 2 && !snapshot.HasPidSamples)
                return "No PID bitrate samples yet. Select PIDs in Bitrate… and restart the file (or wait for windows if parsing).";

            if (snapshot.HasStoredSamples)
                return "No data for the selected chart options. Enable Stream and/or Per-PID lines, or ensure at least two PIDs have samples for the sum.";

            return "Waiting for bitrate windows during file parse…";
        }

        if (snapshot.Mode == BitrateChartMode.Udp)
        {
            if (snapshot.ShowPidSumOnChart && snapshot.ChartPids.Count >= 2 && !snapshot.HasPidSamples)
                return "No PID bitrate samples yet. Select PIDs in Bitrate… and restart UDP play.";

            if (snapshot.HasStoredSamples)
                return "No data for the selected chart options.";

            return "Waiting for live bitrate samples…";
        }

        if (snapshot.ChartPids.Count > 0 || snapshot.ShowStreamOnChart)
            return "Open a file or start UDP to collect bitrate samples.";

        return "Open Bitrate… to choose stream and/or PID traces, then parse a file or UDP stream.";
    }

    private static string BuildChartTitle(BitrateChartSnapshot snapshot)
    {
        if (snapshot.ChartPids.Count == 0)
            return "Transport bitrate";

        return snapshot.ShowStreamOnChart
            ? "Transport and PID bitrate"
            : "PID bitrate";
    }

    private static string BuildStatsText(BitrateChartSnapshot snapshot)
    {
        var parts = new List<string>();

        if (snapshot.ShowStreamOnChart && snapshot.Mode == BitrateChartMode.File && snapshot.FilePoints.Count > 0)
        {
            var avgTotal = snapshot.AverageTotalMegabitsPerSecond;
            var avgUseful = snapshot.AverageUsefulMegabitsPerSecond;
            if (avgTotal.HasValue && avgUseful.HasValue)
                parts.Add($"Stream avg {avgTotal.Value:F2} / {avgUseful.Value:F2} Mbps");
        }

        if (snapshot.ShowStreamOnChart && snapshot.Mode == BitrateChartMode.Udp && snapshot.UdpPoints.Count > 0)
        {
            var latest = snapshot.UdpPoints[^1];
            parts.Add($"Stream {latest.TotalBitsPerSecond / 1_000_000d:F2} / {latest.UsefulBitsPerSecond / 1_000_000d:F2} Mbps");
        }

        if (snapshot.ChartPids.Count > 0)
        {
            var pidCount = snapshot.Mode switch
            {
                BitrateChartMode.File => snapshot.PidFilePoints.Count(kv => kv.Value.Count > 0),
                BitrateChartMode.Udp => snapshot.PidUdpPoints.Count(kv => kv.Value.Count > 0),
                _ => 0
            };
            parts.Add($"{pidCount}/{snapshot.ChartPids.Count} PID traces");
        }

        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }
}
