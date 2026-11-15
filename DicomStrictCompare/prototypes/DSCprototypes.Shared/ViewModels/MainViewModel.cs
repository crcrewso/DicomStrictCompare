using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSClibrary.Matching;
using DSClibrary.Comparing;
using DSClibrary.Studies;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>One criterion row in the editable criteria list.</summary>
public sealed partial class CriterionViewModel : ObservableObject
{
    [ObservableProperty] private double _tolerancePercent = 2.0;
    [ObservableProperty] private double _distanceMm = 2.0;
    [ObservableProperty] private double _thresholdPercent = 10.0;
    [ObservableProperty] private bool _global = true;
    [ObservableProperty] private bool _gamma;
    [ObservableProperty] private BeamsCompared _appliesTo = BeamsCompared.Both;

    /// <summary>The choices for the "Applies to" column.</summary>
    public static IReadOnlyList<BeamsCompared> AppliesToChoices { get; } = Enum.GetValues<BeamsCompared>();

    public CriterionViewModel() { }

    public CriterionViewModel(DtaSpec spec)
    {
        _tolerancePercent = spec.TolerancePercent;
        _distanceMm = spec.DistanceMm;
        _thresholdPercent = spec.ThresholdPercent;
        _global = spec.Global;
        _gamma = spec.Gamma;
        _appliesTo = spec.AppliesTo;
    }

    public DtaSpec ToSpec() => new(TolerancePercent, DistanceMm, ThresholdPercent, Global, Gamma, AppliesTo);
}

/// <summary>
/// The whole window's state, shared by the Avalonia and Uno prototypes because both are MVVM
/// frameworks and the state is the same. It knows nothing about controls: it exposes properties and
/// commands and the view binds to them. The only platform touch, picking a folder, is injected.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>(title, suggested start folder or null) to the folder picked or null.</summary>
    private readonly Func<string, string?, Task<string?>> _pickFolder;
    private CancellationTokenSource? _cancel;
    private string? _lastPicked;
    private string? _lastExportFolder;

    public MainViewModel(Func<string, string?, Task<string?>> pickFolder)
    {
        _pickFolder = pickFolder;
        foreach (var d in DtaSpec.Defaults) Criteria.Add(new CriterionViewModel(d));
        Graph = new GraphViewModel(ComparisonRunner.LoadGrid, pickFolder);
        Study = new StudyViewModel(pickFolder, () => Criteria.Select(c => c.ToSpec()).ToList(),
            runSettings: () => RunSettings, applyRunSettings: Apply);
    }

    /// <summary>The Study tab: several dose sets and the comparisons between them (design/comparison-studies.md).</summary>
    public StudyViewModel Study { get; }

    partial void OnSourceFolderChanged(string value) => Graph.SetSourceFolder(value);

    /// <summary>The Graphs tab: curves out of the matched pairs of the last run (Cody, 2026-11-09: "implement the graph feature").</summary>
    public GraphViewModel Graph { get; }

    /// <summary>
    /// Where a Browse dialog should open: the parent of this field's previous choice (Cody,
    /// 2026-11-08), else the parent of the other folder, else the last folder picked anywhere.
    /// </summary>
    public static string? ParentOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            string full = Path.GetFullPath(path.Trim());
            string? parent = Path.GetDirectoryName(full);
            if (parent is not null && Directory.Exists(parent)) return parent;
            return Directory.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    private string? StartFor(string? own, string? other) => ParentOf(own) ?? ParentOf(other) ?? _lastPicked;

    private async Task<string?> PickAsync(string title, string? start)
    {
        var picked = await _pickFolder(title, start);
        if (picked is not null) _lastPicked = picked;
        return picked;
    }

    [ObservableProperty] private string _sourceFolder = "";
    [ObservableProperty] private string _targetFolder = "";
    [ObservableProperty] private string _sourceAlias = "Reference";
    [ObservableProperty] private string _targetAlias = "Target";
    [ObservableProperty] private bool _matchByUid = true;

    /// <summary>
    /// The "Max" setting: Global Max, or CAX where the source plan's jaws are on axis (falling back to Global Max).
    /// Used by both the Comparison and Study tabs, like the criteria.
    /// </summary>
    [ObservableProperty] private MaximumKind _maximum = MaximumKind.GlobalMax;
    public IReadOnlyList<MaximumKind> MaximumChoices { get; } = Enum.GetValues<MaximumKind>();

    /// <summary>The comparison engine; HandWritten, the reference, unless chosen. Used by both tabs.</summary>
    [ObservableProperty] private ComparerKind _engine = ComparerKind.HandWritten;
    public IReadOnlyList<ComparerKind> EngineChoices { get; } = Enum.GetValues<ComparerKind>();

    /// <summary>
    /// CPU threads: every count from 1 to every logical processor, SMT included, defaulting to the recommended one,
    /// one per usable core (Cody, 2026-11-15). Saved as null when the recommended count is chosen, so a study opened
    /// on another machine uses that machine's.
    /// </summary>
    [ObservableProperty] private ThreadChoice _threads = AllThreadChoices[CpuTopology.PhysicalCores - 1];
    public IReadOnlyList<ThreadChoice> ThreadChoices => AllThreadChoices;
    private static readonly ThreadChoice[] AllThreadChoices = Enumerable.Range(1, CpuTopology.MaxThreads)
        .Select(n => new ThreadChoice(n, n == CpuTopology.PhysicalCores)).ToArray();
    private static ThreadChoice ChoiceFor(int? threads) =>
        AllThreadChoices[Math.Clamp(threads ?? CpuTopology.PhysicalCores, 1, AllThreadChoices.Length) - 1];

    /// <summary>
    /// Float on the GPU engines, a user's choice (Cody, 2026-11-15): some GPUs run double far slower, for example
    /// NVIDIA GeForce RTX 20 and 30 series. Float may differ from the reference on a few points in millions.
    /// </summary>
    [ObservableProperty] private bool _gpuFloat;

    /// <summary>Pairs compared at once; 0 chooses from free memory (BatchComparer.PairsAtOnce).</summary>
    [ObservableProperty] private int _pairsAtOnce;

    /// <summary>Read each target only where the criteria and the edge check use it (not for gamma). Same counts.</summary>
    [ObservableProperty] private bool _partialTargetReads;

    /// <summary>Files read at once into a queue ahead of the comparison; 0 has each worker read its own pair.</summary>
    [ObservableProperty] private int _readers;

    /// <summary>Loaded pairs the queue holds, when Readers is above 0.</summary>
    [ObservableProperty] private int _pairsHeld = 2;

    public ComparerSettings ComparerSettings => new(Engine, Threads.Recommended ? null : Threads.Count,
        Precision: GpuFloat ? GpuPrecision.Float : GpuPrecision.Double);
    public StudyRunSettings RunSettings => new(Maximum, ComparerSettings, PairsAtOnce > 0 ? PairsAtOnce : null,
        PartialTargetReads, Readers > 0 ? Readers : null, Readers > 0 ? PairsHeld : null);

    /// <summary>Takes a study file's run settings, so opening a study restores the engine it was run with.</summary>
    public void Apply(StudyRunSettings run)
    {
        Maximum = run.Maximum;
        Engine = run.ComparerOrDefault.Kind;
        Threads = ChoiceFor(run.ComparerOrDefault.Threads);
        GpuFloat = run.ComparerOrDefault.Precision == GpuPrecision.Float;
        PairsAtOnce = run.PairsAtOnce ?? 0;
        PartialTargetReads = run.PartialTargetReads;
        Readers = run.Readers ?? 0;
        PairsHeld = run.PairsHeld ?? 2;
    }
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

    /// <summary>Pre-fills the window from the command line. Nothing runs here.</summary>
    public void Apply(LaunchOptions options)
    {
        if (options.SourceFolder is not null) SourceFolder = options.SourceFolder;
        if (options.TargetFolder is not null) TargetFolder = options.TargetFolder;
        if (options.SourceAlias is not null) SourceAlias = options.SourceAlias;
        if (options.TargetAlias is not null) TargetAlias = options.TargetAlias;
        if (options.AddGamma) Criteria.Add(new CriterionViewModel(DtaSpec.DefaultGamma));
    }

    /// <summary>
    /// If the command line asked for it, runs the comparison and, when an export folder was given,
    /// writes the spreadsheet there without a picker. Called by the view once its window is open, so
    /// a scripted run exercises the same path a click does.
    /// </summary>
    public async Task AutoRunIfRequestedAsync(LaunchOptions options)
    {
        if (!options.CanAutoRun) return;
        await RunCommand.ExecuteAsync(null);
        if (options.ExportFolder is not null && Session is not null)
        {
            var written = CsvReport.Write(Session, options.ExportFolder);
            Status = Session.Summary + " - wrote " + string.Join(" and ", written.Select(Path.GetFileName));
        }
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var picked = await PickAsync("Source (reference) folder", StartFor(SourceFolder, TargetFolder));
        if (picked is not null) SourceFolder = picked;
    }

    [RelayCommand]
    private async Task BrowseTargetAsync()
    {
        var picked = await PickAsync("Target folder", StartFor(TargetFolder, SourceFolder));
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
        Graph.SetSession(null);
        _cancel = new CancellationTokenSource();

        var request = new ComparisonRequest(
            SourceFolder, TargetFolder, SourceAlias, TargetAlias,
            Criteria.Select(c => c.ToSpec()).ToList(), Strategy,
            DegreeOfParallelism: PairsAtOnce > 0 ? PairsAtOnce : null, Maximum: Maximum, Comparer: ComparerSettings,
            PartialTargetReads: PartialTargetReads, Queue: RunSettings.Queue);

        // IProgress<T> marshals back to the thread that created it, so the runner can report from its worker.
        var progress = new Progress<string>(line => { Log += line + Environment.NewLine; Status = line; });

        try
        {
            var session = await ComparisonRunner.RunAsync(request, progress, _cancel.Token);
            foreach (var row in session.Rows) Rows.Add(row);
            foreach (var u in session.Unmatched) Unmatched.Add(u.ToString());
            foreach (var a in session.Match.AmbiguousKeys) Unmatched.Add(a);
            Session = session;
            Graph.SetSession(session);
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
        var folder = await PickAsync("Folder for the spreadsheet", StartFor(_lastExportFolder, SourceFolder));
        if (folder is null) return;
        _lastExportFolder = folder;
        var written = CsvReport.Write(Session, folder);
        Status = "Wrote " + string.Join(" and ", written.Select(Path.GetFileName));
    }
}

/// <summary>One entry of the Threads list: a count, and whether it is the recommended one.</summary>
public sealed record ThreadChoice(int Count, bool Recommended)
{
    public override string ToString() => Recommended ? $"{Count} (recommended)" : Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
