using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DSClibrary;
using DSClibrary.Studies;
using DSCprototypes.Shared.Graphs;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>One of the fixed depths a dose-to-dose profile is drawn at, or all of them on one plot.</summary>
public sealed record DepthChoice(string Label, double? Mm, bool All = false)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Graphs tab's state for the XAML-family prototypes: the matched pairs of the last run, drawn source over target
/// (depth dose, or a crossplane or inplane profile at dmax, 5, 10 or 20 cm - crcrewso, 2026-11-09). Grids load off the
/// UI thread when a beam is chosen and the last pair is kept. Knows nothing about the plotting control: the view watches
/// <see cref="Curve"/> and draws it. Water-tank scans have their own tab since 2026-11-17
/// (<see cref="ScanComparisonViewModel"/>).
/// </summary>
public sealed partial class GraphViewModel : ObservableObject
{
    private readonly Func<string, DoseMatrixOptimal> _loader;
    private IReadOnlyList<BeamDescriptor> _all = Array.Empty<BeamDescriptor>();
    private LoadedPair? _loaded;
    private int _loadSequence;

    public GraphViewModel(Func<string, DoseMatrixOptimal> loader)
    {
        _loader = loader;
        RebuildDepthChoices();
    }

    /// <summary>How the curves are read out of the grids (<see cref="BeamLines"/>).</summary>
    public string ConventionNote => BeamLines.ConventionNote;

    public IReadOnlyList<CurveKind> Kinds { get; } = new[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane };
    /// <summary>
    /// Dmax first when it is on, so it is the default (crcrewso, 2026-11-09), then each depth, then all of
    /// them on one plot. Built from <see cref="DepthsText"/>, because depths are a setting (F7, 2026-11-14).
    /// </summary>
    public ObservableCollection<DepthChoice> DepthChoices { get; } = new();

    /// <summary>The depths offered, as typed: "dmax, 50, 100, 200 mm" by default.</summary>
    [ObservableProperty] private string _depthsText = DepthSettings.Default.ToString();

    private IReadOnlyList<(string Label, double? Mm)> _depthList = BeamLines.FixedDepths;

    partial void OnDepthsTextChanged(string value) => RebuildDepthChoices();

    private void RebuildDepthChoices()
    {
        DepthSettings settings;
        try { settings = DepthSettings.Parse(DepthsText); }
        catch (FormatException ex) { Status = ex.Message; return; }

        var list = new List<(string Label, double? Mm)>();
        if (settings.IncludeDmax) list.Add(("Dmax", null));
        // Whole centimetres keep the labels the tab has always shown ("10 cm"); anything else is in mm.
        list.AddRange(settings.DepthsMm.Select(d => (d % 10 == 0 ? $"{d / 10:0} cm" : $"{d:0.#} mm", (double?)d)));
        _depthList = list;

        string? previous = Depth?.Label;
        DepthChoices.Clear();
        foreach (var d in list) DepthChoices.Add(new DepthChoice(d.Label, d.Mm));
        DepthChoices.Add(new DepthChoice("All depths", null, All: true));
        Depth = DepthChoices.FirstOrDefault(c => c.Label == previous) ?? DepthChoices[0];
        Redraw();
    }

    /// <summary>The curves to draw: one, or one per depth for "All depths". Null for nothing.</summary>
    [ObservableProperty] private IReadOnlyList<ProfileCurve>? _curves;

    /// <summary>The first curve, for callers that want a single one.</summary>
    public ProfileCurve? Curve => Curves is { Count: > 0 } c ? c[0] : null;

    partial void OnCurvesChanged(IReadOnlyList<ProfileCurve>? value) => OnPropertyChanged(nameof(Curve));
    [ObservableProperty] private string _status = "Run a comparison, then choose a beam.";
    [ObservableProperty] private bool _isLoading;

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

    /// <summary>Applies the command line's graph choices, the curve and depth. Called by the view after any scripted run.</summary>
    public Task ApplyAsync(LaunchOptions options)
    {
        if (options.Curve is { } c && Enum.TryParse<CurveKind>(c, ignoreCase: true, out var kind)) Kind = kind;
        // "All depths" and "5 cm" contain a space, which a launcher may split; the first word is enough.
        if (options.Depth is { } d && DepthChoices.FirstOrDefault(x => x.Label.StartsWith(d.Trim(), StringComparison.OrdinalIgnoreCase)) is { } choice) Depth = choice;
        return Task.CompletedTask;
    }

    /// <summary>Rebuilds the filters from a finished run, or empties them for null.</summary>
    public void SetSession(ComparisonSession? session)
    {
        int hiddenElectrons = 0;
        (_all, hiddenElectrons) = session is null
            ? (Array.Empty<BeamDescriptor>(), 0)
            : PairCatalog.ForGraphs(session.Match.Pairs);
        _loaded = null;
        Curves = null;

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
        Status = _all.Count == 0
            ? (session is null ? "Run a comparison, then choose a beam." : "No photon beams among the matched pairs." + hidden)
            : $"{_all.Count} photon pair(s); {Energies.Count - 1} energies, {FieldSizes.Count - 1} field sizes. Choose a beam.{hidden}";
        Refilter();
    }

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
            Curves = null;
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
        if (_loaded is null) { Curves = null; return; }
        double depth = Depth?.Mm ?? BeamLines.DepthOfMaximum(_loaded);
        DepthText = Kind == CurveKind.DepthDose ? "" : Depth is { All: true } ? DepthsText : (Depth?.Mm is null ? $"dmax = {depth:0.#} mm" : $"{depth:0.#} mm");
        Curves = Depth is { All: true } && Kind != CurveKind.DepthDose ? BeamLines.ProfilesAtAllDepths(_loaded, Kind, _depthList) : new[] { BeamLines.ProfileAt(_loaded, Kind, depth) };
    }
}
