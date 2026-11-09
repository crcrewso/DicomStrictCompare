using DSClibrary;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using Eto.Drawing;
using Eto.Forms;
using Label = Eto.Forms.Label;   // ScottPlot has a Label type of its own

namespace DSCproto.EtoApp;

/// <summary>
/// The Graphs tab, Eto style: the controls are fields, the state lives in the panel, and the shared
/// catalogue and beam-line helpers do the thinking. Filters by energy and field size, a beam from
/// what is left, the kind of curve, and a depth slider (Cody, 2026-11-09: "implement the graph
/// feature in Avalonia and Eto"). Grids load on a worker when a beam is chosen and the last pair is
/// kept, so the slider only re-samples a line.
/// </summary>
public sealed class GraphPage : Panel
{
    private readonly Func<string, DoseMatrixOptimal> _loader;
    private readonly Func<(string Source, string Target)> _aliases;

    private readonly DropDown _energy = new() { Width = 150 };
    private readonly DropDown _fieldSize = new() { Width = 150 };
    private readonly DropDown _beam = new() { Width = 460 };
    private readonly DropDown _kind = new() { Width = 150 };
    private readonly Slider _depth = new() { MinValue = 0, MaxValue = 0, Width = 380, Enabled = false, SnapToTick = true, TickFrequency = 1 };
    private readonly Label _depthText = new() { Width = 70, VerticalAlignment = VerticalAlignment.Center };
    private readonly ProgressBar _busy = new() { Indeterminate = true, Visible = false, Width = 120 };
    private readonly ScottPlot.Eto.EtoPlot _plot = new();
    private readonly Label _status = new() { Text = "Run a comparison, then choose a beam.", Wrap = WrapMode.Word };
    private readonly Label _note = new() { Text = BeamLines.ConventionNote, Wrap = WrapMode.Word, TextColor = Colors.Gray, Font = SystemFonts.Default(9) };

    private IReadOnlyList<BeamDescriptor> _all = Array.Empty<BeamDescriptor>();
    private LoadedPair? _loaded;
    private double[] _depths = Array.Empty<double>();
    private int _loadSequence;
    private bool _refilling;

    public GraphPage(Func<string, DoseMatrixOptimal> loader, Func<(string Source, string Target)> aliases)
    {
        _loader = loader;
        _aliases = aliases;

        _kind.DataStore = new object[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane };
        _kind.SelectedIndex = 0;
        _beam.ItemTextBinding = Binding.Property((BeamDescriptor b) => b.Label);

        _energy.SelectedIndexChanged += (_, _) => Refilter();
        _fieldSize.SelectedIndexChanged += (_, _) => Refilter();
        _beam.SelectedIndexChanged += (_, _) => { if (!_refilling) _ = LoadAsync(_beam.SelectedValue as BeamDescriptor); };
        _kind.SelectedIndexChanged += (_, _) => { _depth.Enabled = Kind != CurveKind.DepthDose && _depths.Length > 0; Redraw(); };
        _depth.ValueChanged += (_, _) => Redraw();

        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(0, 6, 0, 0) };
        // One vertical section per row: a DynamicLayout shares column widths within a section, and
        // these two rows have nothing in common (measured 2026-11-09: in one section the field-size
        // box took the slider's width and the beam box was pushed off the right edge).
        layout.BeginVertical();
        layout.AddRow(Caption("Energy"), _energy, Caption("Field size"), _fieldSize, Caption("Beam"), _beam, null);
        layout.EndVertical();
        layout.BeginVertical();
        layout.AddRow(Caption("Curve"), _kind, Caption("Depth"), _depth, _depthText, _busy, null);
        layout.EndVertical();
        layout.BeginVertical(yscale: true);
        layout.Add(_plot, xscale: true, yscale: true);
        layout.EndVertical();
        layout.BeginVertical();
        layout.Add(_status);
        layout.Add(_note);
        layout.EndVertical();
        Content = layout;

        Draw(null);
        SetSession(null);
    }

    private static Label Caption(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private CurveKind Kind => _kind.SelectedValue is CurveKind k ? k : CurveKind.DepthDose;

    /// <summary>Rebuilds the filters from a finished run, or empties them for null.</summary>
    public void SetSession(ComparisonSession? session)
    {
        _all = session is null ? Array.Empty<BeamDescriptor>() : PairCatalog.Describe(session.Match.Pairs);
        _loaded = null;
        _depths = Array.Empty<double>();

        _refilling = true;
        _energy.DataStore = new[] { PairCatalog.AnyEnergy }.Concat(PairCatalog.Energies(_all)).ToArray();
        _fieldSize.DataStore = new[] { PairCatalog.AnyFieldSize }.Concat(PairCatalog.FieldSizes(_all)).ToArray();
        _energy.SelectedIndex = 0;
        _fieldSize.SelectedIndex = 0;
        _refilling = false;

        bool any = _all.Count > 0;
        _energy.Enabled = _fieldSize.Enabled = _beam.Enabled = _kind.Enabled = any;
        _status.Text = any
            ? $"{_all.Count} matched pair(s); {_energy.DataStore.Count() - 1} energies, {_fieldSize.DataStore.Count() - 1} field sizes. Choose a beam."
            : "Run a comparison, then choose a beam.";
        Refilter();
    }

    private void Refilter()
    {
        if (_refilling) return;
        var keep = PairCatalog.Filter(_all, _energy.SelectedValue as string, _fieldSize.SelectedValue as string);
        var previous = _beam.SelectedValue as BeamDescriptor;

        _refilling = true;
        _beam.DataStore = keep;
        int index = keep.ToList().FindIndex(b => ReferenceEquals(b.Pair, previous?.Pair));
        _beam.SelectedIndex = keep.Count == 0 ? -1 : Math.Max(0, index);
        _refilling = false;

        _ = LoadAsync(_beam.SelectedValue as BeamDescriptor);
    }

    private async Task LoadAsync(BeamDescriptor? beam)
    {
        int sequence = ++_loadSequence;
        if (beam is null)
        {
            _loaded = null;
            _depths = Array.Empty<double>();
            _depth.Enabled = false;
            Draw(null);
            return;
        }
        if (_loaded is not null && ReferenceEquals(_loaded.Pair, beam.Pair)) return;

        _busy.Visible = true;
        _status.Text = $"Loading {beam.Label}";
        try
        {
            var loaded = await Task.Run(() => BeamLines.Load(beam.Pair, _loader));
            if (sequence != _loadSequence) return;

            double previousDepth = _depths.Length > 0 && _depth.Value < _depths.Length ? _depths[_depth.Value] : 100.0;
            _loaded = loaded;
            _depths = BeamLines.Depths(loaded);
            _depth.MaxValue = Math.Max(0, _depths.Length - 1);
            _depth.Value = NearestIndex(_depths, previousDepth);
            _depth.Enabled = Kind != CurveKind.DepthDose && _depths.Length > 0;

            string iso = loaded.IsocentreFromPlan
                ? $"isocentre from the plan at ({loaded.IsoX:0.#}, {loaded.IsoY:0.#}, {loaded.IsoZ:0.#}) mm"
                : "no plan: the grid centre stands in for the isocentre";
            _status.Text = $"{beam.Energy}, {beam.FieldSize}; {loaded.Source.DimensionX}x{loaded.Source.DimensionY}x{loaded.Source.DimensionZ} "
                         + $"@ {loaded.Source.XRes:0.##} mm vs {loaded.Target.DimensionX}x{loaded.Target.DimensionY}x{loaded.Target.DimensionZ} "
                         + $"@ {loaded.Target.XRes:0.##} mm; {iso}";
            Redraw();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence)
            {
                _loaded = null;
                Draw(null);
                _status.Text = $"Could not load the pair: {ex.GetType().Name}: {ex.Message}";
            }
        }
        finally
        {
            if (sequence == _loadSequence) _busy.Visible = false;
        }
    }

    private void Redraw()
    {
        if (_loaded is null) { Draw(null); return; }
        double depth = _depths.Length > 0 ? _depths[Math.Clamp(_depth.Value, 0, _depths.Length - 1)] : 0;
        _depthText.Text = Kind == CurveKind.DepthDose ? "" : $"{depth:0.#} mm";
        Draw(BeamLines.ProfileAt(_loaded, Kind, depth));
    }

    private void Draw(ProfileCurve? curve)
    {
        var plot = _plot.Plot;
        plot.Clear();
        if (curve is null)
        {
            plot.Title("No curve: run a comparison and choose a beam");
            plot.XLabel("");
            plot.YLabel("");
            plot.Legend.IsVisible = false;
        }
        else
        {
            var (sourceAlias, targetAlias) = _aliases();
            var (xs, source, target) = curve.Overlap();
            if (xs.Length > 0)
            {
                var s = plot.Add.Scatter(xs, source);
                s.LegendText = $"{sourceAlias}: {curve.SourceName}";
                s.LineWidth = 2;
                s.MarkerSize = 0;

                var t = plot.Add.Scatter(xs, target);
                t.LegendText = $"{targetAlias}: {curve.TargetName}";
                t.LineWidth = 2;
                t.MarkerSize = 0;
                t.LinePattern = ScottPlot.LinePattern.Dashed;
            }
            plot.Title(curve.Title);
            plot.XLabel(curve.AxisLabel);
            plot.YLabel(curve.ValueLabel);
            plot.Legend.IsVisible = true;
            plot.Legend.Alignment = ScottPlot.Alignment.UpperRight;
            plot.Axes.AutoScale();
        }
        _plot.Refresh();
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
