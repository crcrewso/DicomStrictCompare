using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSClibrary;
using DSClibrary.Results.DICOM;
using DSCprototypes.Shared.Graphs;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>What the Graphs tab compares. Profile to profile is deferred (Cody, 2026-11-09).</summary>
public enum GraphMode
{
    /// <summary>The matched pairs of the last run: source over target.</summary>
    DoseVersusDose,
    /// <summary>A folder of water-tank scans over the source folder's doses, matched by energy, filter and field size.</summary>
    DoseVersusScans
}

/// <summary>One of the fixed depths a dose-to-dose profile is drawn at, or all of them on one plot.</summary>
public sealed record DepthChoice(string Label, double? Mm, bool All = false)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Graphs tab's state for the XAML-family prototypes. Two modes: dose versus dose draws the
/// matched pairs of the last run (depth dose, or a crossplane or inplane profile at dmax, 5, 10 or
/// 20 cm - Cody, 2026-11-09); dose versus scans reads a folder of PTW mcc files and overlays each
/// scan on the source dose whose beam matches it by energy, filter and field size. Grids load off
/// the UI thread when a beam or dose is chosen and the last one is kept. Knows nothing about the
/// plotting control: the view watches <see cref="Curve"/> and draws it.
/// </summary>
public sealed partial class GraphViewModel : ObservableObject
{
    private readonly Func<string, DoseMatrixOptimal> _loader;
    private readonly Func<string, string?, Task<string?>> _pickFolder;
    private IReadOnlyList<BeamDescriptor> _all = Array.Empty<BeamDescriptor>();
    private IReadOnlyList<ScanDescriptor> _allScans = Array.Empty<ScanDescriptor>();
    private IReadOnlyList<RTDoseWithPlan> _sourceDoses = Array.Empty<RTDoseWithPlan>();
    private string? _sourceFolder;
    private LoadedPair? _loaded;
    private LoadedDose? _loadedDose;
    private int _loadSequence;

    public GraphViewModel(Func<string, DoseMatrixOptimal> loader, Func<string, string?, Task<string?>>? pickFolder = null)
    {
        _loader = loader;
        _pickFolder = pickFolder ?? ((_, _) => Task.FromResult<string?>(null));
        Depth = DepthChoices[0];
    }

    public string ConventionNote => BeamLines.ConventionNote;

    public IReadOnlyList<GraphMode> Modes { get; } = new[] { GraphMode.DoseVersusDose, GraphMode.DoseVersusScans };
    public IReadOnlyList<CurveKind> Kinds { get; } = new[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane };
    /// <summary>Dmax first, so it is the default (Cody, 2026-11-09), then 5, 10, 20 cm, then all four on one plot.</summary>
    public IReadOnlyList<DepthChoice> DepthChoices { get; } = BeamLines.FixedDepths.Select(d => new DepthChoice(d.Label, d.Mm))
        .Append(new DepthChoice("All depths", null, All: true)).ToArray();

    // ---- shared ----
    [ObservableProperty] private GraphMode _mode = GraphMode.DoseVersusDose;
    [ObservableProperty] private bool _isDoseMode = true;
    [ObservableProperty] private bool _isScanMode;

    /// <summary>The curves to draw: one, or one per depth for "All depths". Null for nothing.</summary>
    [ObservableProperty] private IReadOnlyList<ProfileCurve>? _curves;

    /// <summary>The first curve, for callers that want a single one.</summary>
    public ProfileCurve? Curve => Curves is { Count: > 0 } c ? c[0] : null;

    partial void OnCurvesChanged(IReadOnlyList<ProfileCurve>? value) => OnPropertyChanged(nameof(Curve));
    [ObservableProperty] private string _status = "Run a comparison, then choose a beam.";
    [ObservableProperty] private bool _isLoading;

    // ---- dose versus dose ----
    public ObservableCollection<string> Energies { get; } = new();
    public ObservableCollection<string> FieldSizes { get; } = new();
    public ObservableCollection<BeamDescriptor> Beams { get; } = new();
    [ObservableProperty] private string? _selectedEnergy;
    [ObservableProperty] private string? _selectedFieldSize;
    [ObservableProperty] private BeamDescriptor? _selectedBeam;
    [ObservableProperty] private CurveKind _kind = CurveKind.DepthDose;
    [ObservableProperty] private DepthChoice? _depth;
    [ObservableProperty] private string _depthText = "";
    [ObservableProperty] private bool _depthEnabled;
    [ObservableProperty] private bool _hasBeams;

    // ---- dose versus scans ----
    public ObservableCollection<string> ScanEnergies { get; } = new();
    public ObservableCollection<string> ScanFieldSizes { get; } = new();
    public ObservableCollection<ScanDescriptor> Scans { get; } = new();
    public ObservableCollection<DoseCandidate> DoseCandidates { get; } = new();
    [ObservableProperty] private string _scanFolder = "";
    [ObservableProperty] private string? _selectedScanEnergy;
    [ObservableProperty] private string? _selectedScanFieldSize;
    [ObservableProperty] private ScanDescriptor? _selectedScan;
    [ObservableProperty] private DoseCandidate? _selectedDose;
    [ObservableProperty] private bool _hasScans;
    [ObservableProperty] private string _scanSummary = "";

    /// <summary>
    /// Applies the command line's graph choices: the curve and depth for dose versus dose, or the
    /// scan folder (which switches the mode and loads it). Called by the view after any scripted run.
    /// </summary>
    public async Task ApplyAsync(LaunchOptions options)
    {
        if (options.Curve is { } c && Enum.TryParse<CurveKind>(c, ignoreCase: true, out var kind)) Kind = kind;
        // "All depths" and "5 cm" contain a space, which a launcher may split; the first word is enough.
        if (options.Depth is { } d && DepthChoices.FirstOrDefault(x => x.Label.StartsWith(d.Trim(), StringComparison.OrdinalIgnoreCase)) is { } choice) Depth = choice;
        if (options.ScanFolder is { } scans)
        {
            Mode = GraphMode.DoseVersusScans;
            ScanFolder = scans;
            await LoadScansCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Rebuilds the dose-versus-dose filters from a finished run, or empties them for null.</summary>
    public void SetSession(ComparisonSession? session)
    {
        int hiddenElectrons = 0;
        (_all, hiddenElectrons) = session is null
            ? (Array.Empty<BeamDescriptor>(), 0)
            : PairCatalog.ForGraphs(session.Match.Pairs);
        _loaded = null;
        if (IsDoseMode) Curves = null;

        Energies.Clear();
        FieldSizes.Clear();
        Energies.Add(PairCatalog.AnyEnergy);
        foreach (var e in PairCatalog.Energies(_all)) Energies.Add(e);
        FieldSizes.Add(PairCatalog.AnyFieldSize);
        foreach (var f in PairCatalog.FieldSizes(_all)) FieldSizes.Add(f);

        SelectedEnergy = Energies[0];
        SelectedFieldSize = FieldSizes[0];
        HasBeams = _all.Count > 0;
        string hidden = hiddenElectrons > 0 ? $" {hiddenElectrons} electron beam(s) not shown (photons only for now)." : "";
        if (IsDoseMode)
            Status = _all.Count == 0
                ? (session is null ? "Run a comparison, then choose a beam." : "No photon beams among the matched pairs." + hidden)
                : $"{_all.Count} photon pair(s); {Energies.Count - 1} energies, {FieldSizes.Count - 1} field sizes. Choose a beam.{hidden}";

        // The source folder's doses are what scans are matched against, so a run refreshes them too.
        if (session is not null) SetSourceDoses(session.Request.SourceFolder, session.Source.Doses);
        Refilter();
    }

    /// <summary>The doses a scan can be matched to. Taken from a run, or read from the source folder when there was none.</summary>
    public void SetSourceDoses(string? folder, IReadOnlyList<RTDoseWithPlan> doses)
    {
        _sourceFolder = folder;
        _sourceDoses = doses;
        RefilterScans();
    }

    /// <summary>Tells the view model where the source folder is, so scans can be matched before any run.</summary>
    public void SetSourceFolder(string? folder)
    {
        if (string.Equals(folder, _sourceFolder, StringComparison.OrdinalIgnoreCase)) return;
        _sourceFolder = folder;
        _sourceDoses = Array.Empty<RTDoseWithPlan>();
    }

    partial void OnModeChanged(GraphMode value)
    {
        IsDoseMode = value == GraphMode.DoseVersusDose;
        IsScanMode = value == GraphMode.DoseVersusScans;
        Curves = null;
        if (IsDoseMode) Redraw(); else RedrawScan();
    }

    // ---- dose versus dose ----

    partial void OnSelectedEnergyChanged(string? value) => Refilter();
    partial void OnSelectedFieldSizeChanged(string? value) => Refilter();

    private void Refilter()
    {
        var keep = PairCatalog.Filter(_all, SelectedEnergy, SelectedFieldSize);
        var previous = SelectedBeam;
        Beams.Clear();
        foreach (var b in keep) Beams.Add(b);
        SelectedBeam = keep.FirstOrDefault(b => ReferenceEquals(b.Pair, previous?.Pair)) ?? keep.FirstOrDefault();
    }

    partial void OnSelectedBeamChanged(BeamDescriptor? value) => _ = LoadPairAsync(value);

    partial void OnKindChanged(CurveKind value)
    {
        DepthEnabled = value != CurveKind.DepthDose && _loaded is not null;
        Redraw();
    }

    partial void OnDepthChanged(DepthChoice? value) => Redraw();

    private async Task LoadPairAsync(BeamDescriptor? beam)
    {
        int sequence = ++_loadSequence;
        if (beam is null)
        {
            _loaded = null;
            if (IsDoseMode) Curves = null;
            return;
        }

        IsLoading = true;
        Status = $"Loading {beam.Label}";
        try
        {
            var loaded = await Task.Run(() => BeamLines.Load(beam.Pair, _loader));
            if (sequence != _loadSequence) return;   // the user moved on while this pair was loading

            _loaded = loaded;
            DepthEnabled = Kind != CurveKind.DepthDose;
            string iso = loaded.IsocentreFromPlan
                ? $"isocentre from the plan at ({loaded.IsoX:0.#}, {loaded.IsoY:0.#}, {loaded.IsoZ:0.#}) mm"
                : "no plan: the grid centre stands in for the isocentre";
            Status = $"{beam.Energy}, {beam.FieldSize}; {loaded.Source.DimensionX}x{loaded.Source.DimensionY}x{loaded.Source.DimensionZ} "
                   + $"@ {loaded.Source.XRes:0.##} mm vs {loaded.Target.DimensionX}x{loaded.Target.DimensionY}x{loaded.Target.DimensionZ} "
                   + $"@ {loaded.Target.XRes:0.##} mm; {iso}; dmax on the source axis {BeamLines.DepthOfMaximum(loaded):0.#} mm";
            Redraw();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence)
            {
                _loaded = null;
                Curves = null;
                Status = $"Could not load the pair: {ex.GetType().Name}: {ex.Message}";
            }
        }
        finally
        {
            if (sequence == _loadSequence) IsLoading = false;
        }
    }

    private void Redraw()
    {
        if (!IsDoseMode) return;
        if (_loaded is null) { Curves = null; return; }
        double depth = Depth?.Mm ?? BeamLines.DepthOfMaximum(_loaded);
        DepthText = Kind == CurveKind.DepthDose ? "" : Depth is { All: true } ? $"dmax {depth:0.#}, 50, 100, 200 mm" : (Depth?.Mm is null ? $"dmax = {depth:0.#} mm" : $"{depth:0.#} mm");
        Curves = Depth is { All: true } && Kind != CurveKind.DepthDose ? BeamLines.ProfilesAtAllDepths(_loaded, Kind) : new[] { BeamLines.ProfileAt(_loaded, Kind, depth) };
    }

    // ---- dose versus scans ----

    [RelayCommand]
    private async Task BrowseScansAsync()
    {
        var picked = await _pickFolder("Folder of PTW mcc scans", MainViewModel.ParentOf(ScanFolder) ?? MainViewModel.ParentOf(_sourceFolder));
        if (picked is not null) ScanFolder = picked;
    }

    [RelayCommand]
    private async Task LoadScansAsync()
    {
        if (!Directory.Exists(ScanFolder))
        {
            Status = "The scan folder must exist.";
            return;
        }
        int sequence = ++_loadSequence;
        IsLoading = true;
        Status = $"Reading scans under {ScanFolder}";
        try
        {
            string? sourceFolder = _sourceFolder;
            bool needDoses = _sourceDoses.Count == 0 && sourceFolder is not null && Directory.Exists(sourceFolder);
            var (result, doses) = await Task.Run(() =>
            {
                var scans = ScanCatalog.Load(ScanFolder);
                var ds = needDoses ? RTFolderReader.Read(sourceFolder!, recursive: true).Doses : null;
                return (scans, ds);
            });
            if (sequence != _loadSequence) return;
            if (doses is not null) _sourceDoses = doses;

            _allScans = result.Scans;
            ScanEnergies.Clear();
            ScanFieldSizes.Clear();
            ScanEnergies.Add(ScanCatalog.AnyEnergy);
            foreach (var e in ScanCatalog.Energies(_allScans)) ScanEnergies.Add(e);
            ScanFieldSizes.Add(ScanCatalog.AnyFieldSize);
            foreach (var f in ScanCatalog.FieldSizes(_allScans)) ScanFieldSizes.Add(f);
            SelectedScanEnergy = ScanEnergies[0];
            SelectedScanFieldSize = ScanFieldSizes[0];
            HasScans = _allScans.Count > 0;

            var hidden = new List<string>();
            if (result.HiddenElectrons > 0) hidden.Add($"{result.HiddenElectrons} electron");
            if (result.HiddenWedges > 0) hidden.Add($"{result.HiddenWedges} wedge");
            if (result.HiddenOther > 0) hidden.Add($"{result.HiddenOther} other");
            ScanSummary = $"{_allScans.Count} photon open-field scan(s) in {result.Files} file(s)"
                        + (hidden.Count > 0 ? $"; not shown: {string.Join(", ", hidden)}" : "")
                        + (result.Errors.Count > 0 ? $"; {result.Errors.Count} file(s) failed to parse" : "")
                        + $"; matched against {_sourceDoses.Count} source dose(s)" + (doses is not null ? " read from the source folder" : "");
            Status = ScanSummary;
            RefilterScans();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence) Status = $"Could not read the scans: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            if (sequence == _loadSequence) IsLoading = false;
        }
    }

    partial void OnSelectedScanEnergyChanged(string? value) => RefilterScans();
    partial void OnSelectedScanFieldSizeChanged(string? value) => RefilterScans();

    private void RefilterScans()
    {
        var keep = ScanCatalog.Filter(_allScans, SelectedScanEnergy, SelectedScanFieldSize);
        var previous = SelectedScan;
        Scans.Clear();
        foreach (var s in keep) Scans.Add(s);
        // Keep the current scan if it is still listed; otherwise the first scan that has a dose to
        // show against, so the tab opens on a curve and not on "no source dose matches".
        SelectedScan = keep.FirstOrDefault(s => ReferenceEquals(s, previous))
            ?? keep.FirstOrDefault(s => ScanCatalog.MatchingDoses(s, _sourceDoses).Count > 0)
            ?? keep.FirstOrDefault();
        if (SelectedScan is not null && ReferenceEquals(SelectedScan, previous)) OnSelectedScanChanged(SelectedScan);
    }

    partial void OnSelectedScanChanged(ScanDescriptor? value)
    {
        var previousDose = SelectedDose?.Dose;
        DoseCandidates.Clear();
        if (value is not null)
            foreach (var c in ScanCatalog.MatchingDoses(value, _sourceDoses)) DoseCandidates.Add(c);
        SelectedDose = DoseCandidates.FirstOrDefault(c => ReferenceEquals(c.Dose, previousDose)) ?? DoseCandidates.FirstOrDefault();
        if (SelectedDose is null && IsScanMode)
        {
            Curves = null;
            if (value is not null)
                Status = _sourceDoses.Count == 0
                    ? "No source doses to match against: set the source folder (and run, or load the scans again)."
                    : $"No source dose matches {value.Energy} {value.FieldSize} (same energy, filter and field size).";
        }
        else if (SelectedDose is not null && ReferenceEquals(SelectedDose.Dose, previousDose))
        {
            RedrawScan();
        }
    }

    partial void OnSelectedDoseChanged(DoseCandidate? value) => _ = LoadDoseAsync(value);

    private async Task LoadDoseAsync(DoseCandidate? candidate)
    {
        if (candidate is null) return;
        if (_loadedDose is not null && ReferenceEquals(_loadedDose.Dose, candidate.Dose)) { RedrawScan(); return; }

        int sequence = ++_loadSequence;
        IsLoading = true;
        Status = $"Loading {candidate.Label}";
        try
        {
            var loaded = await Task.Run(() => BeamLines.LoadDose(candidate.Dose, _loader));
            if (sequence != _loadSequence) return;
            _loadedDose = loaded;
            Status = $"{candidate.Label}; {loaded.Grid.DimensionX}x{loaded.Grid.DimensionY}x{loaded.Grid.DimensionZ} @ {loaded.Grid.XRes:0.##} mm; "
                   + (loaded.IsocentreFromPlan ? $"isocentre from the plan at ({loaded.IsoX:0.#}, {loaded.IsoY:0.#}, {loaded.IsoZ:0.#}) mm" : "no plan: the grid centre stands in for the isocentre");
            RedrawScan();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence)
            {
                _loadedDose = null;
                Curves = null;
                Status = $"Could not load the dose: {ex.GetType().Name}: {ex.Message}";
            }
        }
        finally
        {
            if (sequence == _loadSequence) IsLoading = false;
        }
    }

    private void RedrawScan()
    {
        if (!IsScanMode) return;
        if (SelectedScan is null || _loadedDose is null || SelectedDose is null || !ReferenceEquals(_loadedDose.Dose, SelectedDose.Dose))
        {
            Curves = null;
            return;
        }
        Curves = new[] { BeamLines.ScanOverDose(SelectedScan, _loadedDose) };
    }
}
