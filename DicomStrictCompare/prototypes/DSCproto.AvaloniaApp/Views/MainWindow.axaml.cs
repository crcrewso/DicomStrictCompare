using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.ViewModels;
using ScottPlot;

namespace DSCproto.AvaloniaApp.Views;

public partial class MainWindow : Window
{
    private GraphViewModel? _graph;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachGraph();
    }

    /// <summary>
    /// The one platform service the view model needs. Avalonia reaches native pickers through the
    /// TopLevel's StorageProvider, which is why this lives in the view and is handed down as a delegate.
    /// </summary>
    public async Task<string?> PickFolderAsync(string title, string? startFolder)
    {
        // The start folder is handed over as a storage folder, which is how Avalonia's abstraction
        // stays portable to platforms where a path is not a thing a picker can be told.
        IStorageFolder? start = startFolder is null ? null : await StorageProvider.TryGetFolderFromPathAsync(startFolder);
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        return folders.Count == 1 ? folders[0].TryGetLocalPath() : null;
    }

    /// <summary>Switches to the Graphs tab; used by a scripted run that wants a curve on screen.</summary>
    public void ShowGraphs()
    {
        Workflows.SelectedItem = DosesTab;
        Tabs.SelectedIndex = 3;
    }

    /// <summary>Switches to the Doses vs scans tab; used when the command line names a scans folder.</summary>
    public void ShowScans() => Workflows.SelectedItem = ScansTab;

    // The view model exposes the curve as data; drawing it is the view's job, and ScottPlot's
    // AvaPlot is a control, so the hand-over happens here rather than through a binding.
    private void AttachGraph()
    {
        if (_graph is not null) _graph.PropertyChanged -= OnGraphChanged;
        _graph = (DataContext as MainViewModel)?.Graph;
        if (_graph is not null) _graph.PropertyChanged += OnGraphChanged;
        Draw(_graph?.Curves);

        if (_scans is not null) _scans.PropertyChanged -= OnScansChanged;
        _scans = (DataContext as MainViewModel)?.Scans;
        if (_scans is not null) _scans.PropertyChanged += OnScansChanged;
        DrawScan();

        if (_study is not null) _study.PropertyChanged -= OnStudyChanged;
        _study = (DataContext as MainViewModel)?.Study;
        if (_study is not null) _study.PropertyChanged += OnStudyChanged;
        DrawStudy(_study?.Curves);
        BuildIndexColumns(_study?.Index);
    }

    private StudyViewModel? _study;
    private ScanComparisonViewModel? _scans;

    private void OnStudyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StudyViewModel.Curves)) DrawStudy(_study?.Curves);
        if (e.PropertyName == nameof(StudyViewModel.Index)) BuildIndexColumns(_study?.Index);
    }

    /// <summary>
    /// An index table's columns depend on the data (one per energy, say), so they are built here when
    /// the table changes. Each cell binds to its position in <see cref="IndexRowView.Cells"/>.
    /// </summary>
    private void BuildIndexColumns(DSCprototypes.Shared.Studies.IndexTable? table)
    {
        IndexGrid.Columns.Clear();
        if (table is null) return;
        IndexGrid.Columns.Add(new DataGridTextColumn
        {
            Header = DSCprototypes.Shared.Studies.IndexTable.Say(table.RowAxis) + " \\ " + DSCprototypes.Shared.Studies.IndexTable.Say(table.ColumnAxis),
            Binding = new Avalonia.Data.Binding(nameof(IndexRowView.Label)),
        });
        for (int i = 0; i < table.ColumnLabels.Count; i++)
            IndexGrid.Columns.Add(new DataGridTextColumn
            {
                Header = table.ColumnLabels[i],
                Binding = new Avalonia.Data.Binding($"{nameof(IndexRowView.Cells)}[{i}]"),
            });
    }

    /// <summary>
    /// Every set's curve for one field: one colour per set; with several depths, one line style per
    /// depth. The lower plot is each set minus the reference, in percent of the reference maximum.
    /// </summary>
    private void DrawStudy(MultiCurveSet? set)
    {
        var plot = StudyCurvePlot.Plot;
        var diff = StudyDifferencePlot.Plot;
        plot.Clear();
        diff.Clear();
        if (set is null || set.Curves.Count == 0)
        {
            plot.Title("No curve: run the study and choose a field");
            plot.Legend.IsVisible = false;
            diff.Legend.IsVisible = false;
        }
        else
        {
            var palette = new ScottPlot.Palettes.Category10();
            var patterns = new[] { LinePattern.Solid, LinePattern.Dashed, LinePattern.Dotted, LinePattern.DenselyDashed };
            for (int c = 0; c < set.Curves.Count; c++)
            {
                var curve = set.Curves[c];
                string tag = set.Curves.Count > 1 ? curve.Tag + ": " : "";
                for (int s = 0; s < curve.Series.Count; s++)
                {
                    var (xs, ys) = Defined(curve.Positions, curve.Series[s].Values);
                    if (xs.Length == 0) continue;
                    var line = plot.Add.Scatter(xs, ys);
                    line.LegendText = tag + curve.Series[s].Name;
                    line.LineWidth = 2;
                    line.MarkerSize = 0;
                    line.Color = palette.GetColor(s);
                    line.LinePattern = patterns[c % patterns.Length];
                }
                for (int d = 0; d < curve.Differences.Count; d++)
                {
                    var (xs, ys) = Defined(curve.Positions, curve.Differences[d].Values);
                    if (xs.Length == 0) continue;
                    var line = diff.Add.Scatter(xs, ys);
                    line.LegendText = tag + curve.Differences[d].Name;
                    line.LineWidth = 1.5f;
                    line.MarkerSize = 0;
                    line.Color = palette.GetColor(d + 1);
                    line.LinePattern = patterns[c % patterns.Length];
                }
            }
            var first = set.Curves[0];
            plot.Title(set.Curves.Count > 1 ? first.Title.Split(" at ")[0] + " at each depth" : first.Title);
            plot.XLabel(first.AxisLabel);
            plot.YLabel(first.ValueLabel);
            plot.Legend.IsVisible = true;
            plot.Legend.Alignment = Alignment.UpperRight;
            plot.Axes.AutoScale();
            diff.Title("Difference from the reference (% of the reference maximum)");
            diff.XLabel(first.AxisLabel);
            diff.YLabel("%");
            diff.Legend.IsVisible = first.Differences.Count > 0;
            diff.Legend.Alignment = Alignment.UpperRight;
            diff.Axes.AutoScale();
        }
        StudyCurvePlot.Refresh();
        StudyDifferencePlot.Refresh();

        static (double[] Xs, double[] Ys) Defined(double[] xs, double[] ys)
        {
            var keep = Enumerable.Range(0, xs.Length).Where(i => !double.IsNaN(ys[i])).ToArray();
            return (keep.Select(i => xs[i]).ToArray(), keep.Select(i => ys[i]).ToArray());
        }
    }

    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GraphViewModel.Curves)) Draw(_graph?.Curves);
    }

    private void OnScansChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScanComparisonViewModel.SelectedResult) or nameof(ScanComparisonViewModel.HasResult)) DrawScan();
    }

    /// <summary>
    /// Doses vs scans: the chosen pair's figure, drawn by the same code that saves it (DSCplots.ProfilePlot), into this
    /// control's multiplot, curves above and each tested point below.
    /// </summary>
    private void DrawScan()
    {
        if (_scans?.SelectedResult is { } result && _scans.SelectedLabels is { } labels)
            DSCplots.ProfilePlot.Fill(ScanPlot.Multiplot, result, labels);
        else
            DSCplots.ProfilePlot.Empty(ScanPlot.Multiplot, _scans?.HasResult == true ? "No comparison for this row" : "Run, then choose a pair");
        ScanPlot.Refresh();
    }

    /// <summary>
    /// One curve, or one per depth: each drawn as source solid and target dashed in the same colour,
    /// so "All depths" reads as four pairs rather than eight lines.
    /// </summary>
    private void Draw(IReadOnlyList<ProfileCurve>? curves)
    {
        var main = DataContext as MainViewModel;
        var plot = GraphPlot.Plot;
        plot.Clear();
        if (curves is null || curves.Count == 0)
        {
            plot.Title("No curve: run a comparison and choose a beam");
            plot.XLabel("");
            plot.YLabel("");
            plot.Legend.IsVisible = false;
        }
        else
        {
            var palette = new ScottPlot.Palettes.Category10();
            for (int i = 0; i < curves.Count; i++)
            {
                var curve = curves[i];
                var (xs, source, target) = curve.Overlap();
                if (xs.Length == 0) continue;
                string tag = curve.Tag.Length > 0 ? curve.Tag + ": " : "";
                var colour = palette.GetColor(i);

                var s = plot.Add.Scatter(xs, source);
                s.LegendText = $"{tag}{main?.SourceAlias ?? "Source"} {curve.SourceName}";
                s.LineWidth = 2;
                s.MarkerSize = 0;
                s.Color = colour;

                var t = plot.Add.Scatter(xs, target);
                t.LegendText = $"{tag}{main?.TargetAlias ?? "Target"} {curve.TargetName}";
                t.LineWidth = 2;
                t.MarkerSize = 0;
                t.LinePattern = LinePattern.Dashed;
                t.Color = colour;
            }
            var first = curves[0];
            plot.Title(curves.Count > 1 ? first.Title.Split(" at ")[0] + " at every depth" : first.Title);
            plot.XLabel(first.AxisLabel);
            plot.YLabel(first.ValueLabel);
            plot.Legend.IsVisible = true;
            plot.Legend.Alignment = Alignment.UpperRight;
            plot.Axes.AutoScale();
        }
        GraphPlot.Refresh();
    }
}
