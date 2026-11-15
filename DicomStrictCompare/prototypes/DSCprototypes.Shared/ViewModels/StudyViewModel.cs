using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSClibrary;
using DSClibrary.Matching;
using DSClibrary.Studies;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.Studies;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>One dose set row in the study's set list (F1).</summary>
public sealed partial class DoseSetViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _folder = "";
    [ObservableProperty] private string _algorithm = "";
    [ObservableProperty] private string _tpsVersion = "";
    [ObservableProperty] private DoseReporting _reporting = DoseReporting.NotStated;
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _planFilter = "";

    public static IReadOnlyList<DoseReporting> ReportingChoices { get; } = Enum.GetValues<DoseReporting>();

    public DoseSet ToSet() => new(Name, Folder)
    {
        Algorithm = Blank(Algorithm),
        TpsVersion = Blank(TpsVersion),
        Reporting = Reporting,
        Notes = Blank(Notes),
        PlanFilter = Blank(PlanFilter),
    };

    public static DoseSetViewModel From(DoseSet s) => new()
    {
        Name = s.Name, Folder = s.Folder, Algorithm = s.Algorithm ?? "", TpsVersion = s.TpsVersion ?? "",
        Reporting = s.Reporting, Notes = s.Notes ?? "", PlanFilter = s.PlanFilter ?? "",
    };

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// One comparison row (F2). Rules are typed as text so they can be pasted: label and field rules as
/// <c>pattern -&gt; replacement</c> separated by semicolons, overrides as <c>source = target</c>.
/// </summary>
public sealed partial class SetComparisonViewModel : ObservableObject
{
    [ObservableProperty] private string _sourceSet = "";
    [ObservableProperty] private string _targetSet = "";
    [ObservableProperty] private bool _matchByUid;
    [ObservableProperty] private string _labelRules = "";
    [ObservableProperty] private string _fieldRules = "";
    [ObservableProperty] private string _overrides = "";

    public SetComparison ToComparison()
    {
        var strategy = MatchByUid ? DoseMatchStrategy.PlanAndBeamUid : DoseMatchStrategy.Identifier;
        return new SetComparison(SourceSet, TargetSet, new PairingRules(strategy,
            ParseRules(LabelRules, "label"), ParseOverrides(Overrides), ParseRules(FieldRules, "field")));
    }

    public static SetComparisonViewModel From(SetComparison c) => new()
    {
        SourceSet = c.SourceSet,
        TargetSet = c.TargetSet,
        MatchByUid = c.Rules.Strategy == DoseMatchStrategy.PlanAndBeamUid,
        LabelRules = string.Join("; ", c.Rules.LabelRules.Select(r => r.ToString())),
        FieldRules = string.Join("; ", c.Rules.FieldRules.Select(r => r.ToString())),
        Overrides = string.Join("; ", c.Rules.LabelOverrides.Select(o => $"{o.Key} = {o.Value}")),
    };

    /// <summary>Fills in the rule that pairs an AAA plan with its AXB copy.</summary>
    [RelayCommand]
    private void UseAcrossAlgorithms()
    {
        MatchByUid = false;
        LabelRules = "(AAA|AXB) -> {algorithm}";
    }

    public static IReadOnlyList<LabelRule> ParseRules(string text, string what)
    {
        var rules = new List<LabelRule>();
        foreach (string part in (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int arrow = part.IndexOf("->", StringComparison.Ordinal);
            if (arrow <= 0) throw new FormatException($"The {what} rule '{part}' needs the form 'pattern -> replacement'.");
            rules.Add(new LabelRule(part[..arrow].Trim(), part[(arrow + 2)..].Trim()));
        }
        return rules;
    }

    public static IReadOnlyDictionary<string, string> ParseOverrides(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) throw new FormatException($"The override '{part}' needs the form 'source label = target label'.");
            map[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }
        return map;
    }
}

/// <summary>One row of an index table as the view shows it: the row label, then one text per column.</summary>
public sealed record IndexRowView(string Label, IReadOnlyList<string> Cells);

/// <summary>
/// The Study tab (design/comparison-studies.md): sets and comparisons (F1, F2), a pairing preview,
/// a run whose rows carry plan checks and dose warnings (F3, F4), index tables (F5), and every set's
/// curves for one field at the study's depths (F7). Criteria come from the main window's list, so
/// one set of criteria serves both tabs.
/// </summary>
public sealed partial class StudyViewModel : ObservableObject
{
    private readonly Func<string, string?, Task<string?>> _pickFolder;
    private readonly Func<IReadOnlyList<DtaSpec>> _criteria;
    private readonly Func<string, DoseMatrixOptimal> _loader;
    private CancellationTokenSource? _cancel;
    private IReadOnlyList<LoadedDose>? _loaded;
    private int _loadSequence;

    public StudyViewModel(Func<string, string?, Task<string?>> pickFolder, Func<IReadOnlyList<DtaSpec>> criteria,
        Func<string, DoseMatrixOptimal>? loader = null)
    {
        _pickFolder = pickFolder;
        _criteria = criteria;
        _loader = loader ?? ComparisonRunner.LoadGrid;
    }

    // ---- setup (F1, F2, F7 depths) ----
    public ObservableCollection<DoseSetViewModel> Sets { get; } = new();
    public ObservableCollection<SetComparisonViewModel> Comparisons { get; } = new();
    [ObservableProperty] private DoseSetViewModel? _selectedSet;
    [ObservableProperty] private SetComparisonViewModel? _selectedComparison;
    [ObservableProperty] private string _depthsText = DepthSettings.Default.ToString();
    [ObservableProperty] private string _studyFile = "";

    // ---- running ----
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(PreviewCommand), nameof(RunCommand), nameof(CancelCommand), nameof(ExportCommand))]
    private bool _isRunning;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private StudySession? _session;
    [ObservableProperty] private string _status = "Add two or more dose sets and a comparison between them.";
    [ObservableProperty] private string _log = "";

    // ---- pairing preview and results (F2-F5) ----
    public ObservableCollection<string> PairingLines { get; } = new();
    public ObservableCollection<StudyRow> Rows { get; } = new();

    // ---- index tables (F5) ----
    public ObservableCollection<string> IndexComparisons { get; } = new();
    public ObservableCollection<DtaSpec> IndexCriteria { get; } = new();
    public IReadOnlyList<BeamAxis> Axes { get; } = Enum.GetValues<BeamAxis>();
    [ObservableProperty] private string? _selectedIndexComparison;
    [ObservableProperty] private DtaSpec? _selectedIndexCriterion;
    [ObservableProperty] private BeamAxis _rowAxis = BeamAxis.FieldSize;
    [ObservableProperty] private BeamAxis _columnAxis = BeamAxis.Energy;
    [ObservableProperty] private IndexTable? _index;
    [ObservableProperty] private IReadOnlyList<IndexRowView> _indexRows = Array.Empty<IndexRowView>();

    // ---- curves (F7) ----
    public ObservableCollection<StudyField> Fields { get; } = new();
    public ObservableCollection<string> ReferenceSets { get; } = new();
    public IReadOnlyList<CurveKind> Kinds { get; } = new[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane };
    [ObservableProperty] private StudyField? _selectedField;
    [ObservableProperty] private string? _referenceSet;
    [ObservableProperty] private CurveKind _kind = CurveKind.DepthDose;
    [ObservableProperty] private MultiCurveSet? _curves;
    [ObservableProperty] private string _curveStatus = "Run the study, then choose a field.";
    [ObservableProperty] private bool _isLoadingCurves;

    [RelayCommand]
    private void AddSet() => Sets.Add(new DoseSetViewModel { Name = $"Set {Sets.Count + 1}" });

    [RelayCommand]
    private void RemoveSet()
    {
        if (SelectedSet is { } s) Sets.Remove(s);
    }

    [RelayCommand]
    private async Task BrowseSetFolderAsync()
    {
        if (SelectedSet is not { } s) { Status = "Choose a set first."; return; }
        var picked = await _pickFolder($"Folder for {s.Name}", MainViewModel.ParentOf(s.Folder) ?? MainViewModel.ParentOf(Sets.FirstOrDefault(x => x.Folder.Length > 0)?.Folder));
        if (picked is not null) s.Folder = picked;
    }

    [RelayCommand]
    private void AddComparison()
    {
        var c = new SetComparisonViewModel
        {
            SourceSet = Sets.ElementAtOrDefault(0)?.Name ?? "",
            TargetSet = Sets.ElementAtOrDefault(Comparisons.Count + 1)?.Name ?? Sets.ElementAtOrDefault(1)?.Name ?? "",
        };
        Comparisons.Add(c);
    }

    [RelayCommand]
    private void RemoveComparison()
    {
        if (SelectedComparison is { } c) Comparisons.Remove(c);
    }

    /// <summary>Builds the study as typed, or says what is wrong with it.</summary>
    public Study? BuildStudy(out string? problem)
    {
        try
        {
            var study = new Study(Sets.Select(s => s.ToSet()), Comparisons.Select(c => c.ToComparison()), DepthSettings.Parse(DepthsText));
            var problems = study.Problems().ToList();
            problems.AddRange(study.Sets.Where(s => !Directory.Exists(s.Folder)).Select(s => $"{s.Name}: the folder '{s.Folder}' does not exist."));
            problem = problems.Count == 0 ? null : string.Join(" ", problems);
            return problem is null ? study : null;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            problem = ex.Message;
            return null;
        }
    }

    [RelayCommand]
    private void SaveStudy()
    {
        try
        {
            var study = new Study(Sets.Select(s => s.ToSet()), Comparisons.Select(c => c.ToComparison()), DepthSettings.Parse(DepthsText));
            if (string.IsNullOrWhiteSpace(StudyFile)) { Status = "Give a file name for the study (for example D:\\studies\\aaa-vs-axb.json)."; return; }
            study.Save(StudyFile);
            Status = $"Saved the study to {StudyFile}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not save the study: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenStudy()
    {
        try
        {
            var study = Study.Load(StudyFile);
            Sets.Clear();
            foreach (var s in study.Sets) Sets.Add(DoseSetViewModel.From(s));
            Comparisons.Clear();
            foreach (var c in study.Comparisons) Comparisons.Add(SetComparisonViewModel.From(c));
            DepthsText = study.Depths.ToString();
            Status = $"Opened {StudyFile}: {study.Sets.Count} set(s), {study.Comparisons.Count} comparison(s).";
        }
        catch (Exception ex)
        {
            Status = $"Could not open the study: {ex.Message}";
        }
    }

    private bool CanRun() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task PreviewAsync() => ExecuteAsync(compare: false);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunAsync() => ExecuteAsync(compare: true);

    private async Task ExecuteAsync(bool compare)
    {
        var study = BuildStudy(out var problem);
        if (study is null) { Status = problem ?? "The study cannot run."; return; }
        var criteria = _criteria();
        if (compare && criteria.Count == 0) { Status = "Add at least one criterion on the Criteria list."; return; }

        IsRunning = true;
        Log = "";
        Session = null;
        _cancel = new CancellationTokenSource();
        var progress = new Progress<string>(line => { Log += line + Environment.NewLine; Status = line; });
        try
        {
            var session = compare
                ? await StudyRunner.RunAsync(study, criteria, progress, _cancel.Token)
                : await StudyRunner.PreviewAsync(study, progress, _cancel.Token);
            Show(session);
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

    private void Show(StudySession session)
    {
        Session = session;
        PairingLines.Clear();
        foreach (var p in session.Previews)
        {
            PairingLines.Add(p.Summary);
            foreach (var w in p.SetWarnings) PairingLines.Add($"    set warning: {w}");
            foreach (var m in p.NearMisses) PairingLines.Add($"    near miss: {m}");
            foreach (var u in p.Match.UnmatchedSource.Take(200)) PairingLines.Add($"    source unmatched: {u}");
            foreach (var u in p.Match.UnmatchedTarget.Take(200)) PairingLines.Add($"    target unmatched: {u}");
            foreach (var a in p.Match.AmbiguousKeys) PairingLines.Add($"    ambiguous: {a}");
            foreach (var d in p.Pairs.Where(x => x.PlanCheck.Checked && !x.PlanCheck.Agrees).Take(200))
                PairingLines.Add($"    plan differs: {d.Facts.SourcePlan} / {d.Facts.Field}: {d.PlanCheck.Summary}");
        }

        Rows.Clear();
        foreach (var r in session.Rows) Rows.Add(r);

        IndexComparisons.Clear();
        foreach (var p in session.Previews) IndexComparisons.Add(p.Comparison.Name);
        IndexCriteria.Clear();
        foreach (var c in session.Criteria) IndexCriteria.Add(c);
        SelectedIndexComparison = IndexComparisons.FirstOrDefault();
        SelectedIndexCriterion = IndexCriteria.FirstOrDefault();
        RebuildIndex();

        Fields.Clear();
        foreach (var f in StudyFieldCatalog.Build(session)) Fields.Add(f);
        SelectedField = Fields.FirstOrDefault();
    }

    private bool CanCancel() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cancel?.Cancel();

    private bool CanExport() => Session is not null && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (Session is null) return;
        var folder = await _pickFolder("Folder for the study spreadsheets", MainViewModel.ParentOf(Sets.FirstOrDefault()?.Folder));
        if (folder is null) return;
        try
        {
            var written = StudyCsv.Write(Session, folder);
            Status = $"Wrote {written.Count} file(s) to {folder}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not write the spreadsheets: {ex.Message}";
        }
    }

    // ---- index tables ----

    partial void OnSelectedIndexComparisonChanged(string? value) => RebuildIndex();
    partial void OnSelectedIndexCriterionChanged(DtaSpec? value) => RebuildIndex();
    partial void OnRowAxisChanged(BeamAxis value) => RebuildIndex();
    partial void OnColumnAxisChanged(BeamAxis value) => RebuildIndex();

    private void RebuildIndex()
    {
        if (Session is null || Session.IsPreview || SelectedIndexComparison is null || SelectedIndexCriterion is null)
        {
            Index = null;
            IndexRows = Array.Empty<IndexRowView>();
            return;
        }
        var table = IndexTable.Build(Session.Rows, SelectedIndexComparison, SelectedIndexCriterion, RowAxis, ColumnAxis);
        Index = table;
        IndexRows = table.RowLabels.Select((label, r) => new IndexRowView(label,
            Enumerable.Range(0, table.ColumnLabels.Count).Select(c => table[r, c]?.Text ?? "").ToArray())).ToArray();
    }

    // ---- curves ----

    partial void OnSelectedFieldChanged(StudyField? value)
    {
        string? previous = ReferenceSet;
        ReferenceSets.Clear();
        if (value is not null) foreach (var d in value.Doses) ReferenceSets.Add(d.SetName);
        ReferenceSet = ReferenceSets.Contains(previous ?? "") ? previous : ReferenceSets.FirstOrDefault();
        _ = LoadFieldAsync(value);
    }

    partial void OnReferenceSetChanged(string? value) => Redraw();
    partial void OnKindChanged(CurveKind value) => Redraw();
    partial void OnDepthsTextChanged(string value) => Redraw();

    private async Task LoadFieldAsync(StudyField? field)
    {
        int sequence = ++_loadSequence;
        _loaded = null;
        if (field is null) { Curves = null; return; }
        IsLoadingCurves = true;
        CurveStatus = $"Loading {field.Doses.Count} dose(s) for {field.Label}";
        try
        {
            var loaded = await Task.Run(() => MultiCurves.Load(field.Doses, _loader));
            if (sequence != _loadSequence) return;
            _loaded = loaded;
            Redraw();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence) { Curves = null; CurveStatus = $"Could not load the field: {ex.GetType().Name}: {ex.Message}"; }
        }
        finally
        {
            if (sequence == _loadSequence) IsLoadingCurves = false;
        }
    }

    private void Redraw()
    {
        if (SelectedField is null || _loaded is null) { Curves = null; return; }
        DepthSettings depths;
        try { depths = DepthSettings.Parse(DepthsText); }
        catch (FormatException ex) { CurveStatus = ex.Message; return; }

        int reference = Math.Max(0, SelectedField.Doses.ToList().FindIndex(d => d.SetName == ReferenceSet));
        var set = MultiCurves.For(SelectedField.Doses, _loaded, Kind, depths, reference);
        Curves = set;
        var notes = new List<string> { $"{SelectedField.Facts.Energy}, {SelectedField.Facts.FieldSize}, {SelectedField.Facts.Machine}" };
        if (Kind != CurveKind.DepthDose) notes.Add($"dmax on {SelectedField.Doses[reference].SetName}'s axis {set.DmaxMm.ToString("0.#", CultureInfo.InvariantCulture)} mm");
        notes.AddRange(set.Skipped.Select(s => "skipped " + s));
        notes.AddRange(SelectedField.Notes);
        if (_loaded.Any(l => !l.IsocentreFromPlan)) notes.Add("a set has no plan: its grid centre stands in for the isocentre");
        CurveStatus = string.Join("; ", notes);
    }
}
