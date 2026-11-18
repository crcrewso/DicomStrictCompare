using System.Globalization;
using DSClibrary.Gamma;
using DSCprototypes.Shared.Viewer;
using Eto.Drawing;
using Eto.Forms;

namespace DSCproto.EtoApp;

/// <summary>
/// The difference viewer (prototype, crcrewso 2026-11-18): two RT Dose files, their gamma and % difference maps on the
/// source grid, shown by NiiVue in a WebView, in the beam frame (x and y along the collimator-0 jaws, z along the beam).
/// The page is served from this machine only (<see cref="ViewerServer"/>); "Open in browser" shows the same page where
/// the WebView lacks WebGL2.
/// </summary>
public sealed class ViewerPage : Panel
{
    private readonly TextBox _source = new() { PlaceholderText = "Source (reference) RT Dose" };
    private readonly TextBox _target = new() { PlaceholderText = "Target RT Dose" };
    private readonly TextBox _dose = new() { Text = "3", Width = 50 };
    private readonly TextBox _distance = new() { Text = "2", Width = 50 };
    private readonly TextBox _cutoff = new() { Text = "10", Width = 50 };
    private readonly Button _compute = new() { Text = "Compute and view" };
    private readonly Button _browser = new() { Text = "Open in browser", Enabled = false };
    private readonly ProgressBar _busy = new() { Indeterminate = true, Visible = false, Width = 120 };
    private readonly Label _status = new() { Text = "Choose two doses of the same field.", Wrap = WrapMode.Word };
    private readonly WebView _web = new();
    private ViewerServer? _server;
    private CancellationTokenSource? _run;

    public ViewerPage()
    {
        _compute.Click += (_, _) => _ = ComputeAsync();
        _browser.Click += (_, _) => { if (_server is not null) Application.Instance.Open(_server.Url); };
        _dose.ToolTip = "Gamma dose criterion, % of the source maximum (global)";
        _distance.ToolTip = "Gamma distance criterion, mm";
        _cutoff.ToolTip = "Points below this % of the source maximum are not evaluated";

        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(6) };
        // A TableLayout, so the path boxes take the width and the browse buttons keep theirs.
        layout.Add(new TableLayout
        {
            Spacing = new Size(8, 6),
            Rows =
            {
                new TableRow(Caption("Source"), new TableCell(_source, true), Browse(_source)),
                new TableRow(Caption("Target"), new TableCell(_target, true), Browse(_target)),
            },
        });
        layout.BeginVertical();
        layout.AddRow(Caption("Gamma"), _dose, Caption("% /"), _distance, Caption("mm, cutoff"), _cutoff, Caption("%"), _compute, _busy, _browser, null);
        layout.EndVertical();
        layout.BeginVertical(yscale: true);
        layout.Add(_web, xscale: true, yscale: true);
        layout.EndVertical();
        layout.Add(_status);
        Content = layout;
    }

    /// <summary>Fills in a pair, and optionally its criterion, and computes it: for a result row or a command line.</summary>
    public Task ShowAsync(string source, string target, double? dosePercent = null, double? distanceMm = null, double? cutoffPercent = null)
    {
        _source.Text = source;
        _target.Text = target;
        if (dosePercent is > 0) _dose.Text = dosePercent.Value.ToString("R", CultureInfo.InvariantCulture);
        if (distanceMm is > 0) _distance.Text = distanceMm.Value.ToString("R", CultureInfo.InvariantCulture);
        if (cutoffPercent is > 0) _cutoff.Text = cutoffPercent.Value.ToString("R", CultureInfo.InvariantCulture);
        return ComputeAsync();
    }

    private async Task ComputeAsync()
    {
        if (!TryNumber(_dose, out double dose) || !TryNumber(_distance, out double distance) || !TryNumber(_cutoff, out double cutoff)) return;
        if (!File.Exists(_source.Text) || !File.Exists(_target.Text)) { _status.Text = "Both doses must be existing files."; return; }
        _run?.Cancel();
        var run = _run = new CancellationTokenSource();
        string folder = Path.Combine(Path.GetTempPath(), "DSC-viewer", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        var progress = new Progress<string>(line => Application.Instance.AsyncInvoke(() => _status.Text = line));
        _busy.Visible = true;
        _compute.Enabled = false;
        try
        {
            string source = _source.Text, target = _target.Text;
            var maps = await Task.Run(() => DifferenceViewer.Prepare(source, target, new GammaOptions(dose, distance), cutoff, folder, progress, run.Token));
            if (_server is null) _server = new ViewerServer(maps.Folder); else _server.Show(maps.Folder);
            _web.Url = new Uri(_server.Url);
            _browser.Enabled = true;
            double pass = maps.GammaEvaluated > 0 ? 100.0 * (maps.GammaEvaluated - maps.GammaFailed) / maps.GammaEvaluated : double.NaN;
            _status.Text = string.Create(CultureInfo.InvariantCulture, $"{maps.Title}: gamma {pass:0.0} % pass ({maps.GammaFailed:N0} of {maps.GammaEvaluated:N0} fail). Frame: {maps.FrameNote}.");
        }
        catch (OperationCanceledException) { _status.Text = "Cancelled."; }
        catch (Exception ex) { _status.Text = "Couldn't compute the maps: " + ex.Message; }
        finally
        {
            _busy.Visible = false;
            _compute.Enabled = true;
        }
    }

    /// <summary>Invariant culture, and a bad entry is flagged rather than read as 0.</summary>
    private bool TryNumber(TextBox box, out double value)
    {
        bool ok = double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0;
        if (!ok) _status.Text = $"\"{box.Text}\" isn't a positive number (use a point for decimals).";
        return ok;
    }

    private Button Browse(TextBox into)
    {
        var button = new Button { Text = "…", Width = 32 };
        button.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog { Title = "RT Dose", Filters = { new FileFilter("DICOM", ".dcm", ".DCM"), new FileFilter("All files", "*") } };
            if (File.Exists(into.Text)) dialog.Directory = new Uri(Path.GetDirectoryName(into.Text)!);
            if (dialog.ShowDialog(this) == DialogResult.Ok) into.Text = dialog.FileName;
        };
        return button;
    }

    private static Label Caption(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _run?.Cancel(); _server?.Dispose(); }
        base.Dispose(disposing);
    }
}
