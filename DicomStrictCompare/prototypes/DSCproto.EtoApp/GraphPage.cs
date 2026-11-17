using DSClibrary;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.ViewModels;
using Eto.Drawing;
using Eto.Forms;
using Label = Eto.Forms.Label;   // ScottPlot has a Label type of its own

namespace DSCproto.EtoApp;

/// <summary>
/// The Graphs tab, Eto style: controls are fields, state lives in the panel, and the shared catalogue
/// and beam-line helpers do the thinking. Dose versus dose over the run's matched pairs (crcrewso,
/// 2026-11-09): depth dose, or a crossplane or inplane profile at dmax, 5, 10 or 20 cm or all four on one
/// plot. Grids load on a worker and the last pair is kept. Water-tank scans have their own tab since
/// 2026-11-17 (<see cref="ScanPage"/>).
/// </summary>
public sealed class GraphPage : Panel
{
    private readonly Func<string, DoseMatrixOptimal> _loader;
    private readonly Func<(string Source, string Target)> _aliases;

    private readonly ProgressBar _busy = new() { Indeterminate = true, Visible = false, Width = 120 };
    private readonly DropDown _energy = new() { Width = 150 };
    private readonly DropDown _fieldSize = new() { Width = 150 };
    private readonly DropDown _beam = new() { Width = 460 };
    private readonly DropDown _kind = new() { Width = 150 };
    private readonly DropDown _depth = new() { Width = 130, Enabled = false };
    private readonly Label _depthText = new() { Width = 160, VerticalAlignment = VerticalAlignment.Center };

    private readonly ScottPlot.Eto.EtoPlot _plot = new();
    private readonly Label _status = new() { Text = "Run a comparison, then choose a beam.", Wrap = WrapMode.Word };
    private readonly Label _note = new() { Text = BeamLines.ConventionNote, Wrap = WrapMode.Word, TextColor = Colors.Gray, Font = SystemFonts.Default(9) };

    private IReadOnlyList<BeamDescriptor> _all = Array.Empty<BeamDescriptor>();
    private LoadedPair? _loaded;
    private int _loadSequence;
    private bool _refilling;

    private static readonly DepthChoice[] DepthChoices = BeamLines.FixedDepths.Select(d => new DepthChoice(d.Label, d.Mm))
        .Append(new DepthChoice("All depths", null, All: true)).ToArray();

    public GraphPage(Func<string, DoseMatrixOptimal> loader, Func<(string Source, string Target)> aliases)
    {
        _loader = loader;
        _aliases = aliases;

        _kind.DataStore = new object[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane };
        _kind.SelectedIndex = 0;
        _depth.DataStore = DepthChoices;
        _depth.SelectedIndex = 0;   // dmax is the default (crcrewso, 2026-11-09)
        _beam.ItemTextBinding = Binding.Property((BeamDescriptor b) => b.Label);

        _energy.SelectedIndexChanged += (_, _) => Refilter();
        _fieldSize.SelectedIndexChanged += (_, _) => Refilter();
        _beam.SelectedIndexChanged += (_, _) => { if (!_refilling) _ = LoadPairAsync(_beam.SelectedValue as BeamDescriptor); };
        _kind.SelectedIndexChanged += (_, _) => { _depth.Enabled = Kind != CurveKind.DepthDose && _loaded is not null; Redraw(); };
        _depth.SelectedIndexChanged += (_, _) => Redraw();

        // One vertical section per row: a DynamicLayout shares column widths within a section
        // (measured 2026-11-09: in one section the field-size box took the slider's width).
        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(0, 6, 0, 0) };
        layout.BeginVertical();
        layout.AddRow(Caption("Energy"), _energy, Caption("Field size"), _fieldSize, Caption("Beam"), _beam, _busy, null);
        layout.EndVertical();
        layout.BeginVertical();
        layout.AddRow(Caption("Curve"), _kind, Caption("Depth"), _depth, _depthText, null);
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

    /// <summary>The command line's graph choices: curve and depth.</summary>
    public void Apply(LaunchOptions options)
    {
        if (options.Curve is { } c && Enum.TryParse<CurveKind>(c, ignoreCase: true, out var kind))
            _kind.SelectedIndex = Array.IndexOf(new[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane }, kind);
        if (options.Depth is { } d)
        {
            int i = Array.FindIndex(DepthChoices, x => x.Label.StartsWith(d.Trim(), StringComparison.OrdinalIgnoreCase));
            if (i >= 0) _depth.SelectedIndex = i;
        }
    }

    private CurveKind Kind => _kind.SelectedValue is CurveKind k ? k : CurveKind.DepthDose;
    private DepthChoice Depth => _depth.SelectedValue as DepthChoice ?? DepthChoices[0];

    /// <summary>Rebuilds the filters from a finished run, or empties them for null.</summary>
    public void SetSession(ComparisonSession? session)
    {
        int hiddenElectrons = 0;
        (_all, hiddenElectrons) = session is null
            ? (Array.Empty<BeamDescriptor>(), 0)
            : PairCatalog.ForGraphs(session.Match.Pairs);
        _loaded = null;

        _refilling = true;
        _energy.DataStore = new[] { PairCatalog.AnyEnergy }.Concat(PairCatalog.Energies(_all)).ToArray();
        _fieldSize.DataStore = new[] { PairCatalog.AnyFieldSize }.Concat(PairCatalog.FieldSizes(_all)).ToArray();
        _energy.SelectedIndex = 0;
        _fieldSize.SelectedIndex = 0;
        _refilling = false;

        bool any = _all.Count > 0;
        _energy.Enabled = _fieldSize.Enabled = _beam.Enabled = _kind.Enabled = any;
        string hidden = hiddenElectrons > 0 ? $" {hiddenElectrons} electron beam(s) not shown (photons only for now)." : "";
        _status.Text = any
            ? $"{_all.Count} photon pair(s); {_energy.DataStore.Count() - 1} energies, {_fieldSize.DataStore.Count() - 1} field sizes. Choose a beam.{hidden}"
            : (session is null ? "Run a comparison, then choose a beam." : "No photon beams among the matched pairs." + hidden);
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

        _ = LoadPairAsync(_beam.SelectedValue as BeamDescriptor);
    }

    private async Task LoadPairAsync(BeamDescriptor? beam)
    {
        int sequence = ++_loadSequence;
        if (beam is null)
        {
            _loaded = null;
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

            _loaded = loaded;
            _depth.Enabled = Kind != CurveKind.DepthDose;
            string iso = loaded.IsocentreFromPlan
                ? $"isocentre from the plan at ({loaded.IsoX:0.#}, {loaded.IsoY:0.#}, {loaded.IsoZ:0.#}) mm"
                : "no plan: the grid centre stands in for the isocentre";
            _status.Text = $"{beam.Energy}, {beam.FieldSize}; {loaded.Source.DimensionX}x{loaded.Source.DimensionY}x{loaded.Source.DimensionZ} "
                         + $"@ {loaded.Source.XRes:0.##} mm vs {loaded.Target.DimensionX}x{loaded.Target.DimensionY}x{loaded.Target.DimensionZ} "
                         + $"@ {loaded.Target.XRes:0.##} mm; {iso}; dmax on the source axis {BeamLines.DepthOfMaximum(loaded):0.#} mm";
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
        double dmax = BeamLines.DepthOfMaximum(_loaded);
        double depth = Depth.Mm ?? dmax;
        _depthText.Text = Kind == CurveKind.DepthDose ? "" : Depth.All ? $"dmax {dmax:0.#}, 50, 100, 200 mm" : Depth.Mm is null ? $"dmax = {depth:0.#} mm" : $"{depth:0.#} mm";
        Draw(Depth.All && Kind != CurveKind.DepthDose
            ? BeamLines.ProfilesAtAllDepths(_loaded, Kind)
            : new[] { BeamLines.ProfileAt(_loaded, Kind, depth) });
    }

    /// <summary>One curve, or one per depth: source solid and target dashed in one colour per curve.</summary>
    private void Draw(IReadOnlyList<ProfileCurve>? curves)
    {
        var plot = _plot.Plot;
        plot.Clear();
        if (curves is null || curves.Count == 0)
        {
            plot.Title("No curve: run a comparison and choose a beam");
            plot.XLabel("");
            plot.YLabel("");
            plot.Legend.IsVisible = false;
        }
        else
        {
            var (sourceAlias, targetAlias) = _aliases();
            var palette = new ScottPlot.Palettes.Category10();
            for (int i = 0; i < curves.Count; i++)
            {
                var curve = curves[i];
                var (xs, source, target) = curve.Overlap();
                if (xs.Length == 0) continue;
                string tag = curve.Tag.Length > 0 ? curve.Tag + ": " : "";
                var colour = palette.GetColor(i);

                var s = plot.Add.Scatter(xs, source);
                s.LegendText = $"{tag}{sourceAlias} {curve.SourceName}";
                s.LineWidth = 2;
                s.MarkerSize = 0;
                s.Color = colour;

                var t = plot.Add.Scatter(xs, target);
                t.LegendText = $"{tag}{targetAlias} {curve.TargetName}";
                t.LineWidth = 2;
                t.MarkerSize = 0;
                t.LinePattern = ScottPlot.LinePattern.Dashed;
                t.Color = colour;
            }
            var first = curves[0];
            plot.Title(curves.Count > 1 ? first.Title.Split(" at ")[0] + " at every depth" : first.Title);
            plot.XLabel(first.AxisLabel);
            plot.YLabel(first.ValueLabel);
            plot.Legend.IsVisible = true;
            plot.Legend.Alignment = ScottPlot.Alignment.UpperRight;
            plot.Axes.AutoScale();
        }
        _plot.Refresh();
    }
}
