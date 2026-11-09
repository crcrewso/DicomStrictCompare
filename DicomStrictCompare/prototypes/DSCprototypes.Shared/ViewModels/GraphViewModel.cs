using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DSClibrary;
using DSCprototypes.Shared.Graphs;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>
/// The Graphs tab's state for the XAML-family prototypes: filters by energy and field size, the
/// beam chosen from what is left, the kind of curve and the depth, and the curve itself. Grids are
/// loaded when a beam is chosen, off the UI thread, and the last pair is kept so moving the depth
/// slider costs only a line sample. Knows nothing about the plotting control: the view watches
/// <see cref="Curve"/> and draws it.
/// </summary>
public sealed partial class GraphViewModel : ObservableObject
{
    private readonly Func<string, DoseMatrixOptimal> _loader;
    private IReadOnlyList<BeamDescriptor> _all = Array.Empty<BeamDescriptor>();
    private LoadedPair? _loaded;
    private int _loadSequence;

    public GraphViewModel(Func<string, DoseMatrixOptimal> loader) => _loader = loader;

    public string ConventionNote => BeamLines.ConventionNote;

    public ObservableCollection<string> Energies { get; } = new();
    public ObservableCollection<string> FieldSizes { get; } = new();
    public ObservableCollection<BeamDescriptor> Beams { get; } = new();
    public IReadOnlyList<CurveKind> Kinds { get; } = new[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane };

    [ObservableProperty] private string? _selectedEnergy;
    [ObservableProperty] private string? _selectedFieldSize;
    [ObservableProperty] private BeamDescriptor? _selectedBeam;
    [ObservableProperty] private CurveKind _kind = CurveKind.DepthDose;
    [ObservableProperty] private double[] _depths = Array.Empty<double>();
    [ObservableProperty] private int _depthIndex;
    [ObservableProperty] private double _depthMaxIndex;
    [ObservableProperty] private string _depthText = "";
    [ObservableProperty] private bool _depthEnabled;
    [ObservableProperty] private ProfileCurve? _curve;
    [ObservableProperty] private string _status = "Run a comparison, then choose a beam.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasBeams;

    /// <summary>Rebuilds the filters from a finished run, or empties them for null.</summary>
    public void SetSession(ComparisonSession? session)
    {
        _all = session is null ? Array.Empty<BeamDescriptor>() : PairCatalog.Describe(session.Match.Pairs);
        _loaded = null;
        Curve = null;

        Energies.Clear();
        FieldSizes.Clear();
        Energies.Add(PairCatalog.AnyEnergy);
        foreach (var e in PairCatalog.Energies(_all)) Energies.Add(e);
        FieldSizes.Add(PairCatalog.AnyFieldSize);
        foreach (var f in PairCatalog.FieldSizes(_all)) FieldSizes.Add(f);

        SelectedEnergy = Energies[0];
        SelectedFieldSize = FieldSizes[0];
        HasBeams = _all.Count > 0;
        Status = _all.Count == 0
            ? "Run a comparison, then choose a beam."
            : $"{_all.Count} matched pair(s); {Energies.Count - 1} energies, {FieldSizes.Count - 1} field sizes. Choose a beam.";
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

    partial void OnSelectedBeamChanged(BeamDescriptor? value) => _ = LoadAsync(value);

    partial void OnKindChanged(CurveKind value)
    {
        DepthEnabled = value != CurveKind.DepthDose && Depths.Length > 0;
        Redraw();
    }

    partial void OnDepthIndexChanged(int value)
    {
        OnPropertyChanged(nameof(DepthSlider));
        Redraw();
    }

    /// <summary>The depth index as a double, which is what a Slider binds to; rounded on the way back.</summary>
    public double DepthSlider
    {
        get => DepthIndex;
        set
        {
            int index = (int)Math.Round(value);
            if (index != DepthIndex) DepthIndex = index;
        }
    }

    private async Task LoadAsync(BeamDescriptor? beam)
    {
        int sequence = ++_loadSequence;
        if (beam is null)
        {
            _loaded = null;
            Curve = null;
            return;
        }

        IsLoading = true;
        Status = $"Loading {beam.Label}";
        try
        {
            var loaded = await Task.Run(() => BeamLines.Load(beam.Pair, _loader));
            if (sequence != _loadSequence) return;   // the user moved on while this pair was loading

            double previousDepth = Depths.Length > 0 && DepthIndex < Depths.Length ? Depths[DepthIndex] : 100.0;
            _loaded = loaded;
            Depths = BeamLines.Depths(loaded);
            DepthMaxIndex = Math.Max(0, Depths.Length - 1);
            DepthIndex = NearestIndex(Depths, previousDepth);
            DepthEnabled = Kind != CurveKind.DepthDose && Depths.Length > 0;

            string iso = loaded.IsocentreFromPlan
                ? $"isocentre from the plan at ({loaded.IsoX:0.#}, {loaded.IsoY:0.#}, {loaded.IsoZ:0.#}) mm"
                : "no plan: the grid centre stands in for the isocentre";
            Status = $"{beam.Energy}, {beam.FieldSize}; {loaded.Source.DimensionX}x{loaded.Source.DimensionY}x{loaded.Source.DimensionZ} "
                   + $"@ {loaded.Source.XRes:0.##} mm vs {loaded.Target.DimensionX}x{loaded.Target.DimensionY}x{loaded.Target.DimensionZ} "
                   + $"@ {loaded.Target.XRes:0.##} mm; {iso}";
            Redraw();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence)
            {
                _loaded = null;
                Curve = null;
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
        if (_loaded is null) { Curve = null; return; }
        double depth = Depths.Length > 0 ? Depths[Math.Clamp(DepthIndex, 0, Depths.Length - 1)] : 0;
        DepthText = Kind == CurveKind.DepthDose ? "" : $"{depth:0.#} mm";
        Curve = BeamLines.ProfileAt(_loaded, Kind, depth);
    }

    private static int NearestIndex(double[] values, double target)
    {
        if (values.Length == 0) return 0;
        int best = 0;
        for (int i = 1; i < values.Length; i++)
            if (Math.Abs(values[i] - target) < Math.Abs(values[best] - target)) best = i;
        return best;
    }
}
