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
    public void ShowGraphs() => Tabs.SelectedIndex = 3;

    // The view model exposes the curve as data; drawing it is the view's job, and ScottPlot's
    // AvaPlot is a control, so the hand-over happens here rather than through a binding.
    private void AttachGraph()
    {
        if (_graph is not null) _graph.PropertyChanged -= OnGraphChanged;
        _graph = (DataContext as MainViewModel)?.Graph;
        if (_graph is not null) _graph.PropertyChanged += OnGraphChanged;
        Draw(_graph?.Curves);
    }

    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A mode change with nothing to draw raises no Curves change, so redraw on it as well for the empty-plot title.
        if (e.PropertyName is nameof(GraphViewModel.Curves) or nameof(GraphViewModel.IsScanMode)) Draw(_graph?.Curves);
    }

    /// <summary>
    /// One curve, or one per depth: each drawn as source solid and target dashed in the same colour,
    /// so "All depths" reads as four pairs rather than eight lines. In scan mode the source is the
    /// calculated dose and the target the measurement.
    /// </summary>
    private void Draw(IReadOnlyList<ProfileCurve>? curves)
    {
        var main = DataContext as MainViewModel;
        bool scans = _graph?.IsScanMode == true;
        var plot = GraphPlot.Plot;
        plot.Clear();
        if (curves is null || curves.Count == 0)
        {
            plot.Title(scans ? "No curve: load scans and choose one with a matched dose" : "No curve: run a comparison and choose a beam");
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
                s.LegendText = scans ? $"{tag}dose {curve.SourceName}" : $"{tag}{main?.SourceAlias ?? "Source"} {curve.SourceName}";
                s.LineWidth = 2;
                s.MarkerSize = 0;
                s.Color = colour;

                var t = plot.Add.Scatter(xs, target);
                t.LegendText = scans ? $"{tag}scan {curve.TargetName}" : $"{tag}{main?.TargetAlias ?? "Target"} {curve.TargetName}";
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
