using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSClibrary.Profiles;
using DSCplots;
using DSCprototypes.Shared.Graphs;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>
/// The Doses vs scans tab (crcrewso, 2026-11-17: separate the folders of dose versus dose from those of dose versus
/// scans). Its own doses and scans folders, its own one-dimensional criterion, and a run of the library's batch
/// (<see cref="ProfileBatch"/>, as <c>DSCprofiles</c> runs it) over every scan and every dose it pairs with. The rows
/// are filtered on screen; the chosen row's figure is <see cref="SelectedResult"/>, which the view draws with
/// <see cref="ProfilePlot"/>; exporting writes the files <c>DSCprofiles</c> writes. Knows nothing about controls.
/// </summary>
public sealed partial class ScanComparisonViewModel : ObservableObject
{
    private readonly Func<string, string?, Task<string?>> _pickFolder;
    private IReadOnlyList<ScanPairRow> _all = Array.Empty<ScanPairRow>();
    private ProfileBatchResult? _result;
    private RunInputs? _ran;
    private CancellationTokenSource? _cancel;
    private string? _offeredDoses;
    private string? _plotFolder;

    /// <summary>What a run was given, kept for the export and the plot labels.</summary>
    private sealed record RunInputs(string Doses, string Scans, string? PlanFilter, string? ExcludeFolders, ProfileComparisonOptions Options);

    public ScanComparisonViewModel(Func<string, string?, Task<string?>> pickFolder) => _pickFolder = pickFolder;

    public string ConventionNote => ScanRows.ConventionNote;
    public IReadOnlyList<ProfileTest> Tests => ScanRows.Tests;
    public IReadOnlyList<ProfileReference> References => ScanRows.References;
    public IReadOnlyList<string> PlotFormats => ScanRows.PlotFormats;
    public IReadOnlyList<PlotSelection> PlotSelections => ScanRows.PlotSelections;
    public IReadOnlyList<string> Shows => ScanRows.Shows;

    // ---- inputs ----
    [ObservableProperty] private string _dosesFolder = "";
    [ObservableProperty] private string _scansFolder = "";
    [ObservableProperty] private string _planFilter = "";
    [ObservableProperty] private string _excludeFolders = "";

    // The criterion: 1 %/1 mm gamma, measurement as reference, by default (crcrewso, 2026-11-15: "1 % or tighter for
    // analysis and validation").
    [ObservableProperty] private double _dosePercent = 1;
    [ObservableProperty] private double _distanceMm = 1;
    [ObservableProperty] private ProfileTest _test = ProfileTest.Gamma;
    [ObservableProperty] private ProfileReference _reference = ProfileReference.Measured;
    [ObservableProperty] private bool _local;
    [ObservableProperty] private double _cutoffPercent = 10;
    [ObservableProperty] private bool _depthShift;

    public ProfileComparisonOptions Options => ScanRows.Options(DosePercent, DistanceMm, Test, Reference, Local, CutoffPercent, DepthShift);

    partial void OnDosePercentChanged(double value) => NoteCriterionChanged();
    partial void OnDistanceMmChanged(double value) => NoteCriterionChanged();
    partial void OnTestChanged(ProfileTest value) => NoteCriterionChanged();
    partial void OnReferenceChanged(ProfileReference value) => NoteCriterionChanged();
    partial void OnLocalChanged(bool value) => NoteCriterionChanged();
    partial void OnCutoffPercentChanged(double value) => NoteCriterionChanged();
    partial void OnDepthShiftChanged(bool value) => NoteCriterionChanged();

    // ---- run state ----
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(CancelCommand), nameof(ExportCommand), nameof(SaveListedPlotsCommand))]
    private bool _isRunning;
    [ObservableProperty] private string _status = "Choose a folder of doses (RT Dose and RT Plan) and a folder of PTW mcc scans, then run.";
    [ObservableProperty] private string _details = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ExportCommand), nameof(SaveListedPlotsCommand))]
    private bool _hasResult;

    // ---- results ----
    public ObservableCollection<ScanPairRow> Rows { get; } = new();
    public ObservableCollection<string> Unpaired { get; } = new();
    public ObservableCollection<string> Energies { get; } = new() { ScanRows.AnyEnergy };
    public ObservableCollection<string> Fields { get; } = new() { ScanRows.AnyField };
    public ObservableCollection<string> Curves { get; } = new() { ScanRows.AnyCurve };
    [ObservableProperty] private string? _selectedEnergy = ScanRows.AnyEnergy;
    [ObservableProperty] private string? _selectedField = ScanRows.AnyField;
    [ObservableProperty] private string? _selectedCurve = ScanRows.AnyCurve;
    [ObservableProperty] private string _show = ScanRows.AllPairs;
    [ObservableProperty] private string _listed = "";
    [ObservableProperty] private ScanPairRow? _selectedRow;

    /// <summary>The selected row's comparison and its figure's labels; null when there is nothing to draw.</summary>
    [ObservableProperty] private ProfileComparisonResult? _selectedResult;
    [ObservableProperty] private ProfilePlotLabels? _selectedLabels;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SavePlotCommand))] private bool _canSavePlot;

    // ---- export ----
    [ObservableProperty] private string _plotFormat = "png";
    [ObservableProperty] private PlotSelection _exportPlots = PlotSelection.Failing;

    /// <summary>
    /// The dose-versus-dose source folder, offered as this tab's doses folder: taken while the doses box is empty or still
    /// holds the last folder offered, so typing or browsing one here keeps it.
    /// </summary>
    public void OfferDosesFolder(string? sourceFolder)
    {
        if (DosesFolder.Length > 0 && !string.Equals(DosesFolder, _offeredDoses, StringComparison.Ordinal)) return;
        _offeredDoses = sourceFolder ?? "";
        DosesFolder = _offeredDoses;
    }

    /// <summary>The command line's choices: <c>--doses</c>, <c>--scans</c> and <c>--exclude</c>; with both folders, the tab runs.</summary>
    public async Task ApplyAsync(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.DosesFolder is { } doses) DosesFolder = doses;
        if (options.ScanFolder is { } scans) ScansFolder = scans;
        if (options.ExcludeFolders is { } exclude) ExcludeFolders = exclude;
        if (options.ScanFolder is not null && Directory.Exists(DosesFolder)) await RunCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task BrowseDosesAsync()
    {
        var picked = await _pickFolder("Doses: a folder of RT Dose and RT Plan files", MainViewModel.ParentOf(DosesFolder) ?? MainViewModel.ParentOf(ScansFolder));
        if (picked is not null) DosesFolder = picked;
    }

    [RelayCommand]
    private async Task BrowseScansAsync()
    {
        var picked = await _pickFolder("Scans: a folder of PTW mcc files", MainViewModel.ParentOf(ScansFolder) ?? MainViewModel.ParentOf(DosesFolder));
        if (picked is not null) ScansFolder = picked;
    }

    private bool CanRun() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        var options = Options;
        string doses = DosesFolder.Trim(), scans = ScansFolder.Trim();
        string? plan = ScanRows.NullIfBlank(PlanFilter), exclude = ScanRows.NullIfBlank(ExcludeFolders);
        if (ScanRows.Refusal(doses, scans, plan, exclude, options) is { } refusal)
        {
            Status = refusal;
            return;
        }

        IsRunning = true;
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        var progress = new Progress<string>(m => Status = m);
        Status = $"Reading the scans under {scans}";
        try
        {
            var result = await Task.Run(() =>
            {
                var r = ProfileBatch.Run(scans, doses, new ProfileBatchOptions(options, plan, exclude), m => ((IProgress<string>)progress).Report(m), token);
                ScanRows.ReleaseGrids();
                return r;
            }, token);
            SetResult(result, doses, scans, plan, exclude, options);
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled; the rows shown are from the run before.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Status = $"Failed: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cancel.Dispose();
            _cancel = null;
        }
    }

    private bool CanCancel() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cancel?.Cancel();

    /// <summary>Shows a finished batch: its rows, the filter choices, the scans that didn't pair. A run ends here; so can a test.</summary>
    public void SetResult(ProfileBatchResult result, string dosesFolder, string scansFolder, string? planFilter, string? excludeFolders, ProfileComparisonOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;
        _ran = new RunInputs(dosesFolder, scansFolder, planFilter, excludeFolders, options);
        _all = ScanRows.Build(result, scansFolder);

        Unpaired.Clear();
        foreach (var line in ScanRows.Unpaired(result, scansFolder)) Unpaired.Add(line);
        Refill(Energies, ScanRows.Energies(_all));
        Refill(Fields, ScanRows.Fields(_all));
        Refill(Curves, ScanRows.Curves(_all));
        SelectedEnergy = Keep(Energies, SelectedEnergy, ScanRows.AnyEnergy);
        SelectedField = Keep(Fields, SelectedField, ScanRows.AnyField);
        SelectedCurve = Keep(Curves, SelectedCurve, ScanRows.AnyCurve);
        HasResult = true;
        Refilter();
        Status = ScanRows.Summary(result, _all, options);
    }

    private static void Refill(ObservableCollection<string> list, IReadOnlyList<string> items)
    {
        list.Clear();
        foreach (var i in items) list.Add(i);
    }

    private static string Keep(IReadOnlyCollection<string> list, string? current, string any) =>
        current is not null && list.Contains(current) ? current : any;

    partial void OnSelectedEnergyChanged(string? value) => Refilter();
    partial void OnSelectedFieldChanged(string? value) => Refilter();
    partial void OnSelectedCurveChanged(string? value) => Refilter();
    partial void OnShowChanged(string value) => Refilter();

    private void Refilter()
    {
        var keep = ScanRows.Filter(_all, SelectedEnergy, SelectedField, SelectedCurve, Show);
        var previous = SelectedRow;
        Rows.Clear();
        foreach (var r in keep) Rows.Add(r);
        Listed = _all.Count == 0 ? "" : $"{keep.Count} of {_all.Count} pair(s) listed";
        SelectedRow = keep.FirstOrDefault(r => ReferenceEquals(r, previous)) ?? keep.FirstOrDefault();
    }

    partial void OnSelectedRowChanged(ScanPairRow? value)
    {
        if (value?.Pair.Result is { } result && _ran is not null)
        {
            SelectedLabels = ScanRows.Labels(value, _ran.Scans);
            SelectedResult = result;
        }
        else
        {
            SelectedLabels = null;
            SelectedResult = null;
        }
        CanSavePlot = SelectedResult is not null;
        Details = value is null ? "" : ScanRows.Details(value);
    }

    private void NoteCriterionChanged()
    {
        if (_ran is not null && !IsRunning && Options != _ran.Options)
            Status = $"The rows are at {ProfilePlot.Criterion(_ran.Options)}; run again to compare at {ProfilePlot.Criterion(Options)}.";
    }

    /// <summary>Saves the figure on screen, as <see cref="PlotFormat"/>, into a folder the user picks.</summary>
    [RelayCommand(CanExecute = nameof(CanSavePlot))]
    private async Task SavePlotAsync()
    {
        if (SelectedRow is not { } row || SelectedResult is not { } result || SelectedLabels is not { } labels) return;
        var folder = await _pickFolder("Folder for the plot", _plotFolder ?? MainViewModel.ParentOf(ScansFolder));
        if (folder is null) return;
        _plotFolder = folder;
        string path = Path.Combine(folder, ScanRows.PlotFileName(row, PlotFormat));
        try
        {
            ProfilePlot.Save(result, labels, path);
            Status = $"Saved {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = $"The plot couldn't be saved: {ex.Message}";
        }
    }

    private bool CanUseResult() => HasResult && !IsRunning;

    /// <summary>A plot of every row the filters list, into a folder the user picks, named by row as the export names them.</summary>
    [RelayCommand(CanExecute = nameof(CanUseResult))]
    private async Task SaveListedPlotsAsync()
    {
        if (_ran is null || Rows.Count == 0) return;
        var folder = await _pickFolder("Folder for the plots", _plotFolder ?? MainViewModel.ParentOf(ScansFolder));
        if (folder is null) return;
        _plotFolder = folder;
        var jobs = Rows.Select(r => (r.Row, r.Pair)).ToArray();
        string format = PlotFormat, scans = _ran.Scans;
        IsRunning = true;
        try
        {
            var progress = new Progress<string>(m => Status = m);
            var written = await Task.Run(() => ProfilePlotExport.WriteInto(folder, scans, jobs, format, say: m => ((IProgress<string>)progress).Report(m)));
            int skipped = jobs.Length - written.Count;
            Status = $"Saved {written.Count} plot(s) into {folder}" + (skipped > 0 ? $"; {skipped} listed row(s) have no comparison to draw" : "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = $"The plots couldn't be saved: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Writes what <c>DSCprofiles</c> writes for this run, with the plots <see cref="ExportPlots"/> asks for, into a folder the user picks.</summary>
    [RelayCommand(CanExecute = nameof(CanUseResult))]
    private async Task ExportAsync()
    {
        if (_result is null || _ran is null) return;
        var folder = await _pickFolder("Folder for the results", _plotFolder ?? MainViewModel.ParentOf(ScansFolder));
        if (folder is null) return;
        _plotFolder = folder;
        var (result, ran, plots, format) = (_result, _ran, ExportPlots, PlotFormat);
        IsRunning = true;
        try
        {
            var progress = new Progress<string>(m => Status = m);
            Status = await Task.Run(() => ScanRows.Export(folder, ran.Scans, ran.Doses, ran.Options, ran.PlanFilter, ran.ExcludeFolders, result, plots, format,
                m => ((IProgress<string>)progress).Report(m)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = $"The results couldn't be written: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
