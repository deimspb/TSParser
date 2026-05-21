using Avalonia;
using Avalonia.Controls;
using ScottPlot;
using TSParser.Desktop.Services;

namespace TSParser.Desktop.Controls;

public partial class BitrateChartControl : UserControl
{
    private const double YAxisPaddingRatio = 0.12;
    private const double MinYAxisPaddingMbps = 0.5;
    private const double XAxisPaddingRatio = 0.02;
    private const double MinXAxisPadding = 0.25;

    private int _appliedRevision = -1;
    private int _appliedConfigRevision = -1;
    private int _lastFitRevisionSeen = -1;

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

        ChartPlot.IsVisible = hasData;
        PlaceholderText.IsVisible = !hasData;

        if (!hasData)
        {
            _appliedRevision = -1;
            ChartPlot.Plot.Clear();
            ChartPlot.Refresh();
            return;
        }

        var fitChanged = FitRevision != _lastFitRevisionSeen;
        _lastFitRevisionSeen = FitRevision;

        var configChanged = ChartConfigRevision != _appliedConfigRevision;
        if (!fitChanged && !configChanged && DataRevision == _appliedRevision)
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

        ChartPlot.Plot.Clear();

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

        ChartPlot.Plot.Clear();

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

        var line = ChartPlot.Plot.Add.ScatterLine(xs, ys);
        line.LegendText = name;
        line.Color = Color.FromHex(colorHex);
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
            ChartPlot.Refresh();
            return;
        }

        var xRange = ComputeXRange(allX);
        var yRange = ComputeYRange(allY);

        ChartPlot.Plot.Title(title);
        ChartPlot.Plot.Axes.Bottom.Label.Text = xLabel;
        ChartPlot.Plot.Axes.Left.Label.Text = "Mbps";
        ChartPlot.Plot.Axes.SetLimits(xRange.Min, xRange.Max, yRange.Min, yRange.Max);
        ChartPlot.Plot.Legend.IsVisible = true;
        ChartPlot.Plot.Legend.Orientation = Orientation.Horizontal;
        ChartPlot.Plot.Legend.Alignment = Alignment.UpperCenter;
        ChartPlot.Refresh();
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
