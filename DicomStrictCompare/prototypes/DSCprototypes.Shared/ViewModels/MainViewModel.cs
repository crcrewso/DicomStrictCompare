using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSClibrary.Matching;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>One criterion row in the editable criteria list.</summary>
public sealed partial class CriterionViewModel : ObservableObject
{
    [ObservableProperty] private double _tolerancePercent = 2.0;
    [ObservableProperty] private double _distanceMm = 2.0;
    [ObservableProperty] private double _thresholdPercent = 10.0;
    [ObservableProperty] private bool _global = true;

    public CriterionViewModel() { }

    public CriterionViewModel(DtaSpec spec)
    {
        _tolerancePercent = spec.TolerancePercent;
        _distanceMm = spec.DistanceMm;
        _thresholdPercent = spec.ThresholdPercent;
        _global = spec.Global;
    }

    public DtaSpec ToSpec() => new(TolerancePercent, DistanceMm, ThresholdPercent, Global);
}

/// <summary>
/// The whole window's state, shared by the Avalonia and Uno prototypes because both are MVVM
/// frameworks and the state is the same. It knows nothing about controls: it exposes properties and
/// commands and the view binds to them. The only platform touch, picking a folder, is injected.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Func<string, Task<string?>> _pickFolder;
    private CancellationTokenSource? _cancel;

    public MainViewModel(Func<string, Task<string?>> pickFolder)
    {
        _pickFolder = pickFolder;
        foreach (var d in DtaSpec.Defaults) Criteria.Add(new CriterionViewModel(d));
    }

    [ObservableProperty] private string _sourceFolder = "";
    [ObservableProperty] private string _targetFolder = "";
    [ObservableProperty] private string _sourceAlias = "Reference";
    [ObservableProperty] private string _targetAlias = "Target";
    [ObservableProperty] private bool _matchByUid = true;
    [ObservableProperty] private string _status = "Choose two folders of RT Dose and RT Plan files.";
    [ObservableProperty] private string _log = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(CancelCommand), nameof(ExportCommand))]
    private bool _isRunning;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private ComparisonSession? _session;
    [ObservableProperty] private CriterionViewModel? _selectedCriterion;

    public ObservableCollection<CriterionViewModel> Criteria { get; } = new();
    public ObservableCollection<ResultRow> Rows { get; } = new();
    public ObservableCollection<string> Unmatched { get; } = new();

    public DoseMatchStrategy Strategy => MatchByUid ? DoseMatchStrategy.PlanAndBeamUid : DoseMatchStrategy.Identifier;

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var picked = await _pickFolder("Source (reference) folder");
        if (picked is not null) SourceFolder = picked;
    }

    [RelayCommand]
    private async Task BrowseTargetAsync()
    {
        var picked = await _pickFolder("Target folder");
        if (picked is not null) TargetFolder = picked;
    }

    [RelayCommand]
    private void AddCriterion() => Criteria.Add(new CriterionViewModel());

    [RelayCommand]
    private void RemoveCriterion()
    {
        if (SelectedCriterion is { } c) Criteria.Remove(c);
    }

    private bool CanRun() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (!Directory.Exists(SourceFolder) || !Directory.Exists(TargetFolder))
        {
            Status = "Both folders must exist.";
            return;
        }
        if (Criteria.Count == 0)
        {
            Status = "Add at least one criterion.";
            return;
        }

        IsRunning = true;
        Rows.Clear();
        Unmatched.Clear();
        Log = "";
        Session = null;
        _cancel = new CancellationTokenSource();

        var request = new ComparisonRequest(
            SourceFolder, TargetFolder, SourceAlias, TargetAlias,
            Criteria.Select(c => c.ToSpec()).ToList(), Strategy);

        // IProgress<T> marshals back to the thread that created it, so the runner can report from its worker.
        var progress = new Progress<string>(line => { Log += line + Environment.NewLine; Status = line; });

        try
        {
            var session = await ComparisonRunner.RunAsync(request, progress, _cancel.Token);
            foreach (var row in session.Rows) Rows.Add(row);
            foreach (var u in session.Unmatched) Unmatched.Add(u.ToString());
            foreach (var a in session.Match.AmbiguousKeys) Unmatched.Add(a);
            Session = session;
            Status = session.Summary;
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled.";
        }
        catch (Exception ex)
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

    private bool CanExport() => Session is not null && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (Session is null) return;
        var folder = await _pickFolder("Folder for the spreadsheet");
        if (folder is null) return;
        var written = CsvReport.Write(Session, folder);
        Status = "Wrote " + string.Join(" and ", written.Select(Path.GetFileName));
    }
}
