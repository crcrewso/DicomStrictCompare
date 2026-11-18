using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSClibrary.Gamma;
using DSCprototypes.Shared.Viewer;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>
/// The difference viewer's state for a window that binds (Avalonia): two RT Dose files, the gamma criterion, and the
/// address of the page showing their maps (<see cref="DifferenceViewer"/>, <see cref="ViewerServer"/>). The Eto app's
/// ViewerPage does the same in its own style.
/// </summary>
public sealed partial class ViewerViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private string _sourcePath = "";
    [ObservableProperty] private string _targetPath = "";
    [ObservableProperty] private string _doseText = "3";
    [ObservableProperty] private string _distanceText = "2";
    [ObservableProperty] private string _cutoffText = "10";
    [ObservableProperty] private string _status = "Choose two doses of the same field.";
    [ObservableProperty] private Uri? _url;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ComputeCommand))] private bool _isBusy;

    private ViewerServer? _server;
    private CancellationTokenSource? _run;

    /// <summary>Fills in a pair, and optionally its criterion, and computes it: for a result row or a command line.</summary>
    public Task ShowAsync(string source, string target, double? dosePercent = null, double? distanceMm = null, double? cutoffPercent = null)
    {
        SourcePath = source;
        TargetPath = target;
        if (dosePercent is > 0) DoseText = Say(dosePercent.Value);
        if (distanceMm is > 0) DistanceText = Say(distanceMm.Value);
        if (cutoffPercent is > 0) CutoffText = Say(cutoffPercent.Value);
        return ComputeAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCompute))]
    private async Task ComputeAsync()
    {
        if (!TryNumber(DoseText, out double dose) || !TryNumber(DistanceText, out double distance) || !TryNumber(CutoffText, out double cutoff)) return;
        if (!File.Exists(SourcePath) || !File.Exists(TargetPath)) { Status = "Both doses must be existing files."; return; }
        _run?.Cancel();
        var run = _run = new CancellationTokenSource();
        string folder = Path.Combine(Path.GetTempPath(), "DSC-viewer", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        var progress = new Progress<string>(line => Status = line);
        IsBusy = true;
        try
        {
            string source = SourcePath, target = TargetPath;
            var maps = await Task.Run(() => DifferenceViewer.Prepare(source, target, new GammaOptions(dose, distance), cutoff, folder, progress, run.Token));
            if (_server is null) _server = new ViewerServer(maps.Folder); else _server.Show(maps.Folder);
            // A new address each time, so the page reloads with the new maps.
            Url = new Uri(_server.Url + "?t=" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            double pass = maps.GammaEvaluated > 0 ? 100.0 * (maps.GammaEvaluated - maps.GammaFailed) / maps.GammaEvaluated : double.NaN;
            Status = string.Create(CultureInfo.InvariantCulture, $"{maps.Title}: gamma {pass:0.0} % pass ({maps.GammaFailed:N0} of {maps.GammaEvaluated:N0} fail). Frame: {maps.FrameNote}.");
        }
        catch (OperationCanceledException) { Status = "Cancelled."; }
        catch (Exception ex) { Status = "Couldn't compute the maps: " + ex.Message; }
        finally { IsBusy = false; }
    }

    private bool CanCompute() => !IsBusy;

    /// <summary>Invariant culture, and a bad entry is flagged rather than read as 0.</summary>
    private bool TryNumber(string text, out double value)
    {
        bool ok = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0;
        if (!ok) Status = $"\"{text}\" isn't a positive number (use a point for decimals).";
        return ok;
    }

    private static string Say(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _run?.Cancel();
        _server?.Dispose();
    }
}
