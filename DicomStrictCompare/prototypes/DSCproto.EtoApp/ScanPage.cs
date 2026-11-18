using DSClibrary.Profiles;
using DSCplots;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.ViewModels;
using Eto.Drawing;
using Eto.Forms;
using Label = Eto.Forms.Label;   // ScottPlot has a Label type of its own

namespace DSCproto.EtoApp;

/// <summary>
/// The Doses vs scans tab, Eto style (crcrewso, 2026-11-17: separate the folders of dose versus dose from those of dose
/// versus scans): controls are fields and state lives in the panel; the shared <see cref="ScanRows"/> runs the library's
/// batch (<see cref="ProfileBatch"/>, as DSCprofiles runs it), labels and filters its rows, and exports. The chosen row's
/// figure is drawn by <see cref="ProfilePlot"/>, the same figure saved.
/// </summary>
public sealed class ScanPage : Panel
{
    private readonly Func<string, string?, string?> _pickFolder;

    private readonly TextBox _doses = new() { PlaceholderText = "Folder of RT Dose and RT Plan files (searched with its subfolders)",
        ToolTip = "Filled in from the Doses vs doses source folder until you choose a folder here" };
    private readonly TextBox _scans = new() { PlaceholderText = "Folder of PTW mcc files (searched with its subfolders)" };
    private readonly TextBox _planFilter = new() { Width = 220, PlaceholderText = "regex, optional", ToolTip = "Only doses whose plan label matches this regular expression, ignoring case" };
    private readonly TextBox _exclude = new() { Width = 220, PlaceholderText = "regex, for example ^zz", ToolTip = "Scan files under a folder whose name matches this regular expression are left out" };

    // The criterion: 1 %/1 mm gamma, measurement as reference, by default (crcrewso, 2026-11-15: "1 % or tighter for analysis and validation").
    private readonly NumericStepper _dosePercent = new() { Value = 1, MinValue = 0.001, DecimalPlaces = 1, MaximumDecimalPlaces = 3, Increment = 0.5, Width = 70 };
    private readonly NumericStepper _distanceMm = new() { Value = 1, MinValue = 0, DecimalPlaces = 1, MaximumDecimalPlaces = 3, Increment = 0.5, Width = 70, ToolTip = "0 with the strict test: dose difference only" };
    private readonly DropDown _test = new() { Width = 100, ToolTip = HelpText.ScanTest };
    private readonly DropDown _reference = new() { Width = 120, ToolTip = HelpText.ScanReference };
    private readonly CheckBox _local = new() { Text = "Local", ToolTip = "Dose difference as a percentage of the reference point, not of the reference maximum" };
    private readonly NumericStepper _cutoff = new() { Value = 10, MinValue = 0, MaxValue = 100, DecimalPlaces = 0, MaximumDecimalPlaces = 2, Width = 70, ToolTip = "Points below this percentage of the reference maximum aren't tested" };
    private readonly CheckBox _depthShift = new() { Text = "Depth shift", ToolTip = "Depth doses only: read the dose at depth + s for s within 3 mm in 0.1 mm steps, the best fit beyond dmax" };
    private readonly Button _run = new() { Text = "Run" };
    private readonly Button _cancel = new() { Text = "Cancel", Enabled = false };
    private readonly ProgressBar _busy = new() { Indeterminate = true, Visible = false, Width = 120 };

    private readonly CheckListButton _energy = new(new CheckList("energies")) { Enabled = false };
    private readonly CheckListButton _field = new(new CheckList("field sizes")) { Enabled = false };
    private readonly DropDown _curve = new() { Width = 130, Enabled = false };
    private readonly DropDown _show = new() { Width = 130, Enabled = false };
    private readonly TextBox _find = new() { Width = 200, Enabled = false, PlaceholderText = "e.g. 10FFF 6x6", ToolTip = HelpText.ScanFind };
    private readonly Label _listed = new() { VerticalAlignment = VerticalAlignment.Center };

    private readonly GridView _grid = new() { AllowMultipleSelection = false };
    private readonly ListBox _unpaired = new();
    private readonly ScottPlot.Eto.EtoPlot _plot = new();

    private readonly DropDown _format = new() { Width = 70 };
    private readonly Button _savePlot = new() { Text = "Save plot", Enabled = false };
    private readonly Button _saveListed = new() { Text = "Save plots of listed pairs", Enabled = false, ToolTip = "One image per pair the filters list, named by its row" };
    private readonly DropDown _exportPlots = new() { Width = 100 };
    private readonly Button _export = new() { Text = "Export results (CSV)", Enabled = false,
        ToolTip = "The files DSCprofiles writes: profiles-results.csv, profiles-unpaired.csv, profiles-settings.txt, and the plots in profiles-plots" };

    private readonly Label _status = new() { Text = "Choose a folder of doses (RT Dose and RT Plan) and a folder of PTW mcc scans, then run.", Wrap = WrapMode.Word };
    private readonly Label _details = new() { Wrap = WrapMode.Word, TextColor = Colors.Gray, Font = SystemFonts.Default(9) };
    private readonly Label _note = new() { Text = ScanRows.ConventionNote, Wrap = WrapMode.Word, TextColor = Colors.Gray, Font = SystemFonts.Default(9) };

    private IReadOnlyList<ScanPairRow> _all = Array.Empty<ScanPairRow>();
    private ProfileBatchResult? _result;
    private (string Doses, string Scans, string? Plan, string? Exclude, ProfileComparisonOptions Options)? _ran;
    private CancellationTokenSource? _cancelSource;
    private string? _offeredDoses;
    private string? _plotFolder;
    private bool _refilling;

    public ScanPage(Func<string, string?, string?> pickFolder)
    {
        _pickFolder = pickFolder;
        _test.DataStore = ScanRows.Tests.Cast<object>().ToArray();
        _test.SelectedIndex = 0;
        _reference.DataStore = ScanRows.References.Cast<object>().ToArray();
        _reference.SelectedIndex = 0;
        _format.DataStore = ScanRows.PlotFormats;
        _format.SelectedIndex = 0;
        _exportPlots.DataStore = ScanRows.PlotSelections.Cast<object>().ToArray();
        _exportPlots.SelectedIndex = 0;
        _show.DataStore = ScanRows.Shows;
        _show.SelectedIndex = 0;
        BuildGrid();

        var browseDoses = new Button { Text = "Browse" };
        browseDoses.Click += (_, _) => Browse(_doses, "Doses: a folder of RT Dose and RT Plan files", _scans);
        var browseScans = new Button { Text = "Browse" };
        browseScans.Click += (_, _) => Browse(_scans, "Scans: a folder of PTW mcc files", _doses);
        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _cancelSource?.Cancel();
        foreach (var d in new[] { _curve, _show }) d.SelectedIndexChanged += (_, _) => Refilter();
        _energy.List.Changed += (_, _) => { RefillFields(); Refilter(); };
        _field.List.Changed += (_, _) => Refilter();
        _find.TextChanged += (_, _) => Refilter();
        _grid.SelectionChanged += (_, _) => ShowSelected();
        _savePlot.Click += (_, _) => SavePlot();
        _saveListed.Click += async (_, _) => await SaveListedAsync();
        _export.Click += async (_, _) => await ExportAsync();
        _dosePercent.ValueChanged += (_, _) => NoteCriterionChanged();
        _distanceMm.ValueChanged += (_, _) => NoteCriterionChanged();
        _test.SelectedIndexChanged += (_, _) => NoteCriterionChanged();
        _reference.SelectedIndexChanged += (_, _) => NoteCriterionChanged();
        _local.CheckedChanged += (_, _) => NoteCriterionChanged();
        _cutoff.ValueChanged += (_, _) => NoteCriterionChanged();
        _depthShift.CheckedChanged += (_, _) => NoteCriterionChanged();

        // One vertical section per part with its own columns: a DynamicLayout shares column widths within a section.
        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(6, 12, 6, 12) };
        layout.BeginVertical();
        void FolderRow(string caption, TextBox folder, Button browse, string patternCaption, TextBox pattern)
        {
            layout.BeginHorizontal();
            layout.Add(Caption(caption, 60));
            layout.Add(folder, xscale: true);
            layout.Add(browse);
            layout.Add(Caption(patternCaption, 120));
            layout.Add(pattern);
            layout.EndHorizontal();
        }
        FolderRow("Doses", _doses, browseDoses, "Plan label filter", _planFilter);
        FolderRow("Scans", _scans, browseScans, "Skip scan folders", _exclude);
        layout.EndVertical();
        layout.BeginVertical();
        layout.AddRow(new Label { Text = "Criterion", Font = SystemFonts.Bold(), VerticalAlignment = VerticalAlignment.Center },
            Caption("Dose %"), _dosePercent, Caption("Distance mm"), _distanceMm, Caption("Test"), _test, Caption("Reference"), _reference,
            _local, Caption("Cutoff %"), _cutoff, _depthShift, _run, _cancel, _busy, null);
        layout.EndVertical();
        layout.BeginVertical();
        layout.AddRow(Caption("Energy"), _energy, Caption("Field"), _field, Caption("Curve"), _curve, Caption("Show"), _show, Caption("Find"), _find, _listed, null);
        layout.EndVertical();

        var tables = new TabControl();
        tables.Pages.Add(new TabPage(_grid) { Text = "Pairs" });
        tables.Pages.Add(new TabPage(_unpaired) { Text = "Unpaired" });
        var splitter = new Splitter
        {
            Orientation = Orientation.Horizontal,
            Position = 560,
            Panel1 = tables,
            Panel2 = _plot,
        };
        layout.BeginVertical(yscale: true);
        layout.Add(splitter, xscale: true, yscale: true);
        layout.EndVertical();

        layout.BeginVertical();
        layout.AddRow(Caption("Format"), _format, _savePlot, _saveListed, Caption("Plots in the export"), _exportPlots, _export, null);
        layout.EndVertical();
        layout.BeginVertical();
        layout.Add(_status);
        layout.Add(_details);
        layout.Add(_note);
        layout.EndVertical();
        Content = layout;

        DrawSelected(null, null);
    }

    private static Label Caption(string text, int? width = null)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        if (width is { } w) label.Width = w;
        return label;
    }

    private void Browse(TextBox target, string title, TextBox other)
    {
        var picked = _pickFolder(title, MainViewModel.ParentOf(target.Text) ?? MainViewModel.ParentOf(other.Text));
        if (picked is not null) target.Text = picked;
    }

    private void BuildGrid()
    {
        GridColumn Col(string header, Func<ScanPairRow, string> get, int width, string? tip = null) => new()
        {
            HeaderText = header, HeaderToolTip = tip, Width = width,
            DataCell = new TextBoxCell { Binding = Binding.Delegate(get) },
        };
        GridCopy.Attach(_grid);
        _grid.Columns.Add(Col("Row", r => r.Row.ToString(System.Globalization.CultureInfo.InvariantCulture), 50));
        _grid.Columns.Add(Col("Scan", r => r.Scan, 260));
        _grid.Columns.Add(Col("Curve", r => r.Curve, 80));
        _grid.Columns.Add(Col("Depth mm", r => r.Depth, 70));
        _grid.Columns.Add(Col("Energy", r => r.Energy, 70));
        _grid.Columns.Add(Col("Field", r => r.Field, 140));
        _grid.Columns.Add(Col("Dose", r => r.DoseFile, 200));
        _grid.Columns.Add(Col("Pass %", r => r.PassRate, 60, HelpText.PassRate));
        _grid.Columns.Add(Col("Points", r => r.Points, 80, HelpText.ScanPoints));
        _grid.Columns.Add(Col("Max γ", r => r.MaxGamma, 60, HelpText.ScanMaxGamma));
        _grid.Columns.Add(Col("Outcome", r => r.OutcomeText, 160, HelpText.ScanOutcome));
        _grid.Columns.Add(Col("Header", r => r.HeaderFlag, 70, HelpText.ScanHeader));
    }

    /// <summary>
    /// The dose-versus-dose source folder, offered as this tab's doses folder: taken while the doses box is empty or still
    /// holds the last folder offered, so typing or browsing one here keeps it.
    /// </summary>
    public void OfferDosesFolder(string? sourceFolder)
    {
        if (_doses.Text.Length > 0 && !string.Equals(_doses.Text, _offeredDoses, StringComparison.Ordinal)) return;
        _offeredDoses = sourceFolder ?? "";
        _doses.Text = _offeredDoses;
    }

    /// <summary>The command line's choices: <c>--doses</c> and <c>--scans</c>; with both, the tab runs.</summary>
    public async Task ApplyAsync(LaunchOptions options)
    {
        if (options.DosesFolder is { } doses) _doses.Text = doses;
        if (options.ScanFolder is { } scans) _scans.Text = scans;
        if (options.ScanFolder is not null && Directory.Exists(_doses.Text)) await RunAsync();
    }

    private ProfileComparisonOptions Options => ScanRows.Options(
        (double)_dosePercent.Value, (double)_distanceMm.Value,
        _test.SelectedValue is ProfileTest t ? t : ProfileTest.Gamma,
        _reference.SelectedValue is ProfileReference r ? r : ProfileReference.Measured,
        _local.Checked == true, (double)_cutoff.Value, _depthShift.Checked == true);

    private async Task RunAsync()
    {
        var options = Options;
        string doses = _doses.Text.Trim(), scans = _scans.Text.Trim();
        string? plan = ScanRows.NullIfBlank(_planFilter.Text), exclude = ScanRows.NullIfBlank(_exclude.Text);
        if (ScanRows.Refusal(doses, scans, plan, exclude, options) is { } refusal) { _status.Text = refusal; return; }

        SetBusy(true);
        _cancelSource = new CancellationTokenSource();
        var token = _cancelSource.Token;
        _status.Text = $"Reading the scans under {scans}";
        try
        {
            void Say(string m) => Application.Instance.AsyncInvoke(() => _status.Text = m);
            var result = await Task.Run(() =>
            {
                var r = ProfileBatch.Run(scans, doses, new ProfileBatchOptions(options, plan, exclude), Say, token);
                ScanRows.ReleaseGrids();
                return r;
            }, token);
            ShowResult(result, (doses, scans, plan, exclude, options));
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Cancelled; the rows shown are from the run before.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _status.Text = $"Failed: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
            _cancelSource.Dispose();
            _cancelSource = null;
        }
    }

    private void SetBusy(bool busy)
    {
        _run.Enabled = !busy;
        _cancel.Enabled = busy;
        _busy.Visible = busy;
        _saveListed.Enabled = _export.Enabled = !busy && _result is not null;
    }

    private void ShowResult(ProfileBatchResult result, (string, string, string?, string?, ProfileComparisonOptions) ran)
    {
        _result = result;
        _ran = ran;
        _all = ScanRows.Build(result, ran.Item2);
        _unpaired.Items.Clear();
        foreach (var line in ScanRows.Unpaired(result, ran.Item2)) _unpaired.Items.Add(line);

        _refilling = true;
        _energy.List.SetChoices(ScanRows.Energies(_all).Skip(1));
        Refill(_curve, ScanRows.Curves(_all));
        _refilling = false;
        RefillFields();
        _energy.Enabled = _field.Enabled = _curve.Enabled = _show.Enabled = _find.Enabled = true;
        Refilter();
        _status.Text = ScanRows.Summary(result, _all, ran.Item5);
    }

    /// <summary>The field sizes measured at the ticked energies only, as the Avalonia window offers them.</summary>
    private void RefillFields()
    {
        var energies = _energy.List.Checked;
        _field.List.SetChoices(ScanRows.Fields(energies is null ? _all : _all.Where(r => energies.Contains(r.Energy))).Skip(1));
    }

    private static void Refill(DropDown list, IReadOnlyList<string> items)
    {
        string? previous = list.SelectedValue as string;
        list.DataStore = items;
        int i = previous is null ? -1 : items.ToList().IndexOf(previous);
        list.SelectedIndex = Math.Max(0, i);
    }

    private void Refilter()
    {
        if (_refilling) return;
        var keep = ScanRows.Filter(_all, _energy.List.Checked, _field.List.Checked, _curve.SelectedValue as string, _show.SelectedValue as string, _find.Text);
        var previous = _grid.SelectedItem as ScanPairRow;
        _grid.DataStore = keep;
        _listed.Text = _all.Count == 0 ? "" : $"{keep.Count} of {_all.Count} pair(s) listed";
        int index = keep.ToList().FindIndex(r => ReferenceEquals(r, previous));
        if (keep.Count > 0) _grid.SelectRow(Math.Max(0, index));
        ShowSelected();
    }

    private void ShowSelected()
    {
        var row = _grid.SelectedItem as ScanPairRow;
        var result = row?.Pair.Result;
        var labels = row is not null && result is not null && _ran is { } ran ? ScanRows.Labels(row, ran.Scans) : null;
        _details.Text = row is null ? "" : ScanRows.Details(row);
        _savePlot.Enabled = result is not null;
        DrawSelected(result, labels);
    }

    private void DrawSelected(ProfileComparisonResult? result, ProfilePlotLabels? labels)
    {
        if (result is not null && labels is not null) ProfilePlot.Fill(_plot.Multiplot, result, labels);
        else ProfilePlot.Empty(_plot.Multiplot, _result is null ? "Run, then choose a pair" : "No comparison for this row");
        _plot.Refresh();
    }

    private void NoteCriterionChanged()
    {
        if (_ran is { } ran && _run.Enabled && Options != ran.Options)
            _status.Text = $"The rows are at {ProfilePlot.Criterion(ran.Options)}; run again to compare at {ProfilePlot.Criterion(Options)}.";
    }

    private string Format => _format.SelectedValue as string ?? "png";

    private void SavePlot()
    {
        if (_grid.SelectedItem is not ScanPairRow row || row.Pair.Result is not { } result || _ran is not { } ran) return;
        var folder = _pickFolder("Folder for the plot", _plotFolder ?? MainViewModel.ParentOf(_scans.Text));
        if (folder is null) return;
        _plotFolder = folder;
        string path = Path.Combine(folder, ScanRows.PlotFileName(row, Format));
        try
        {
            ProfilePlot.Save(result, ScanRows.Labels(row, ran.Scans), path);
            _status.Text = $"Saved {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Text = $"The plot couldn't be saved: {ex.Message}";
        }
    }

    private async Task SaveListedAsync()
    {
        var rows = (_grid.DataStore ?? Array.Empty<object>()).OfType<ScanPairRow>().ToArray();
        if (rows.Length == 0 || _ran is not { } ran) return;
        var folder = _pickFolder("Folder for the plots", _plotFolder ?? MainViewModel.ParentOf(_scans.Text));
        if (folder is null) return;
        _plotFolder = folder;
        var jobs = rows.Select(r => (r.Row, r.Pair)).ToArray();
        string format = Format;
        SetBusy(true);
        try
        {
            void Say(string m) => Application.Instance.AsyncInvoke(() => _status.Text = m);
            var written = await Task.Run(() => ProfilePlotExport.WriteInto(folder, ran.Scans, jobs, format, say: Say));
            int skipped = jobs.Length - written.Count;
            _status.Text = $"Saved {written.Count} plot(s) into {folder}" + (skipped > 0 ? $"; {skipped} listed row(s) have no comparison to draw" : "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Text = $"The plots couldn't be saved: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExportAsync()
    {
        if (_result is not { } result || _ran is not { } ran) return;
        var folder = _pickFolder("Folder for the results", _plotFolder ?? MainViewModel.ParentOf(_scans.Text));
        if (folder is null) return;
        _plotFolder = folder;
        var plots = _exportPlots.SelectedValue is PlotSelection p ? p : PlotSelection.Failing;
        string format = Format;
        SetBusy(true);
        try
        {
            void Say(string m) => Application.Instance.AsyncInvoke(() => _status.Text = m);
            _status.Text = await Task.Run(() => ScanRows.Export(folder, ran.Scans, ran.Doses, ran.Options, ran.Plan, ran.Exclude, result, plots, format, Say));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Text = $"The results couldn't be written: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }
}
