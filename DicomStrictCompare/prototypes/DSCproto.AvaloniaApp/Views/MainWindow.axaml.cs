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
        Draw(_graph?.Curve);
    }

    private void OnGraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphViewModel.Curve)) Draw(_graph?.Curve);
    }

    private void Draw(ProfileCurve? curve)
    {
        var main = DataContext as MainViewModel;
        var plot = GraphPlot.Plot;
        plot.Clear();
        if (curve is null)
        {
            plot.Title("No curve: run a comparison and choose a beam");
            plot.XLabel("");
            plot.YLabel("");
            plot.Legend.IsVisible = false;
        }
        else
        {
            var (xs, source, target) = curve.Overlap();
            if (xs.Length > 0)
            {
                var s = plot.Add.Scatter(xs, source);
                s.LegendText = $"{main?.SourceAlias ?? "Source"}: {curve.SourceName}";
                s.LineWidth = 2;
                s.MarkerSize = 0;

                var t = plot.Add.Scatter(xs, target);
                t.LegendText = $"{main?.TargetAlias ?? "Target"}: {curve.TargetName}";
                t.LineWidth = 2;
                t.MarkerSize = 0;
                t.LinePattern = LinePattern.Dashed;
            }
            plot.Title(curve.Title);
            plot.XLabel(curve.AxisLabel);
            plot.YLabel(curve.ValueLabel);
            plot.Legend.IsVisible = true;
            plot.Legend.Alignment = Alignment.UpperRight;
            plot.Axes.AutoScale();
        }
        GraphPlot.Refresh();
    }
}
