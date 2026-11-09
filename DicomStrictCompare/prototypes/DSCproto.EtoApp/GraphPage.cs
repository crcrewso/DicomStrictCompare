using DSClibrary;
using DSClibrary.Results.DICOM;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.ViewModels;
using Eto.Drawing;
using Eto.Forms;
using Label = Eto.Forms.Label;   // ScottPlot has a Label type of its own

namespace DSCproto.EtoApp;

/// <summary>
/// The Graphs tab, Eto style: controls are fields, state lives in the panel, and the shared catalogue
/// and beam-line helpers do the thinking. Two modes (Cody, 2026-11-09): dose versus dose over the
/// run's matched pairs - depth dose, or a crossplane or inplane profile at dmax, 5, 10 or 20 cm or all
/// four on one plot - and dose versus water-tank scans, matched by energy, filter and field size.
/// Grids load on a worker and the last one is kept.
/// </summary>
public sealed class GraphPage : Panel
{
    private readonly Func<string, DoseMatrixOptimal> _loader;
    private readonly Func<(string Source, string Target)> _aliases;
    private readonly Func<string, string?, string?> _pickFolder;
    private readonly Func<string> _sourceFolder;

    // mode
    private readonly DropDown _mode = new() { Width = 170 };
    private readonly ProgressBar _busy = new() { Indeterminate = true, Visible = false, Width = 120 };

    // dose versus dose
    private readonly Panel _dosePanel = new();
    private readonly DropDown _energy = new() { Width = 150 };
    private readonly DropDown _fieldSize = new() { Width = 150 };
    private readonly DropDown _beam = new() { Width = 460 };
    private readonly DropDown _kind = new() { Width = 150 };
    private readonly DropDown _depth = new() { Width = 130, Enabled = false };
    private readonly Label _depthText = new() { Width = 160, VerticalAlignment = VerticalAlignment.Center };

    // dose versus scans
    private readonly Panel _scanPanel = new() { Visible = false };
    private readonly TextBox _scanFolder = new() { Width = 520, PlaceholderText = "Folder of PTW mcc files (searched with its subfolders)" };
    private readonly Button _browseScans = new() { Text = "Browse" };
    private readonly Button _loadScans = new() { Text = "Load scans" };
    private readonly DropDown _scanEnergy = new() { Width = 150, Enabled = false };
    private readonly DropDown _scanFieldSize = new() { Width = 150, Enabled = false };
    private readonly DropDown _scan = new() { Width = 460, Enabled = false };
    private readonly DropDown _dose = new() { Width = 620, Enabled = false };

    private readonly ScottPlot.Eto.EtoPlot _plot = new();
    private readonly Label _status = new() { Text = "Run a comparison, then choose a beam.", Wrap = WrapMode.Word };
    private readonly Label _note = new() { Text = BeamLines.ConventionNote, Wrap = WrapMode.Word, TextColor = Colors.Gray, Font = SystemFonts.Default(9) };

    private IReadOnlyList<BeamDescriptor> _all = Array.Empty<BeamDescriptor>();
    private IReadOnlyList<ScanDescriptor> _allScans = Array.Empty<ScanDescriptor>();
    private IReadOnlyList<RTDoseWithPlan> _sourceDoses = Array.Empty<RTDoseWithPlan>();
    private LoadedPair? _loaded;
    private LoadedDose? _loadedDose;
    private int _loadSequence;
    private bool _refilling;

    private static readonly DepthChoice[] DepthChoices = BeamLines.FixedDepths.Select(d => new DepthChoice(d.Label, d.Mm))
        .Append(new DepthChoice("All depths", null, All: true)).ToArray();

    public GraphPage(
        Func<string, DoseMatrixOptimal> loader,
        Func<(string Source, string Target)> aliases,
        Func<string, string?, string?> pickFolder,
        Func<string> sourceFolder)
    {
        _loader = loader;
        _aliases = aliases;
        _pickFolder = pickFolder;
        _sourceFolder = sourceFolder;

        _mode.DataStore = new object[] { GraphMode.DoseVersusDose, GraphMode.DoseVersusScans };
        _mode.SelectedIndex = 0;
        _kind.DataStore = new object[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane };
        _kind.SelectedIndex = 0;
        _depth.DataStore = DepthChoices;
        _depth.SelectedIndex = 0;   // dmax is the default (Cody, 2026-11-09)
        _beam.ItemTextBinding = Binding.Property((BeamDescriptor b) => b.Label);
        _scan.ItemTextBinding = Binding.Property((ScanDescriptor s) => s.Label);
        _dose.ItemTextBinding = Binding.Property((DoseCandidate d) => d.Label);

        _mode.SelectedIndexChanged += (_, _) =>
        {
            bool scans = IsScanMode;
            _dosePanel.Visible = !scans;
            _scanPanel.Visible = scans;
            if (scans) RedrawScan(); else Redraw();
        };
        _energy.SelectedIndexChanged += (_, _) => Refilter();
        _fieldSize.SelectedIndexChanged += (_, _) => Refilter();
        _beam.SelectedIndexChanged += (_, _) => { if (!_refilling) _ = LoadPairAsync(_beam.SelectedValue as BeamDescriptor); };
        _kind.SelectedIndexChanged += (_, _) => { _depth.Enabled = Kind != CurveKind.DepthDose && _loaded is not null; Redraw(); };
        _depth.SelectedIndexChanged += (_, _) => Redraw();

        _browseScans.Click += (_, _) =>
        {
            var picked = _pickFolder("Folder of PTW mcc scans", MainViewModel.ParentOf(_scanFolder.Text) ?? MainViewModel.ParentOf(_sourceFolder()));
            if (picked is not null) _scanFolder.Text = picked;
        };
        _loadScans.Click += async (_, _) => await LoadScansAsync();
        _scanEnergy.SelectedIndexChanged += (_, _) => RefilterScans();
        _scanFieldSize.SelectedIndexChanged += (_, _) => RefilterScans();
        _scan.SelectedIndexChanged += (_, _) => { if (!_refilling) OnScanChanged(); };
        _dose.SelectedIndexChanged += (_, _) => { if (!_refilling) _ = LoadDoseAsync(_dose.SelectedValue as DoseCandidate); };

        // One vertical section per row: a DynamicLayout shares column widths within a section
        // (measured 2026-11-09: in one section the field-size box took the slider's width).
        var doseLayout = new DynamicLayout { DefaultSpacing = new Size(8, 6) };
        doseLayout.BeginVertical();
        doseLayout.AddRow(Caption("Energy"), _energy, Caption("Field size"), _fieldSize, Caption("Beam"), _beam, null);
        doseLayout.EndVertical();
        doseLayout.BeginVertical();
        doseLayout.AddRow(Caption("Curve"), _kind, Caption("Depth"), _depth, _depthText, null);
        doseLayout.EndVertical();
        _dosePanel.Content = doseLayout;

        var scanLayout = new DynamicLayout { DefaultSpacing = new Size(8, 6) };
        scanLayout.BeginVertical();
        scanLayout.AddRow(Caption("Scans"), _scanFolder, _browseScans, _loadScans, null);
        scanLayout.EndVertical();
        scanLayout.BeginVertical();
        scanLayout.AddRow(Caption("Energy"), _scanEnergy, Caption("Field size"), _scanFieldSize, Caption("Scan"), _scan, null);
        scanLayout.EndVertical();
        scanLayout.BeginVertical();
        scanLayout.AddRow(Caption("Matched dose"), _dose, null);
        scanLayout.EndVertical();
        _scanPanel.Content = scanLayout;

        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(0, 6, 0, 0) };
        layout.BeginVertical();
        layout.AddRow(Caption("Compare"), _mode, _busy, null);
        layout.EndVertical();
        layout.BeginVertical();
        layout.Add(_dosePanel);
        layout.Add(_scanPanel);
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

    /// <summary>The command line's graph choices: curve and depth, or a scan folder to switch to and load.</summary>
    public async Task ApplyAsync(LaunchOptions options)
    {
        if (options.Curve is { } c && Enum.TryParse<CurveKind>(c, ignoreCase: true, out var kind))
            _kind.SelectedIndex = Array.IndexOf(new[] { CurveKind.DepthDose, CurveKind.Crossplane, CurveKind.Inplane }, kind);
        if (options.Depth is { } d)
        {
            int i = Array.FindIndex(DepthChoices, x => x.Label.StartsWith(d.Trim(), StringComparison.OrdinalIgnoreCase));
            if (i >= 0) _depth.SelectedIndex = i;
        }
        if (options.ScanFolder is { } scans)
        {
            _mode.SelectedIndex = 1;
            _scanFolder.Text = scans;
            await LoadScansAsync();
        }
    }

    private bool IsScanMode => _mode.SelectedValue is GraphMode.DoseVersusScans;
    private CurveKind Kind => _kind.SelectedValue is CurveKind k ? k : CurveKind.DepthDose;
    private DepthChoice Depth => _depth.SelectedValue as DepthChoice ?? DepthChoices[0];

    // ---- dose versus dose ----

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
        if (!IsScanMode)
            _status.Text = any
                ? $"{_all.Count} photon pair(s); {_energy.DataStore.Count() - 1} energies, {_fieldSize.DataStore.Count() - 1} field sizes. Choose a beam.{hidden}"
                : (session is null ? "Run a comparison, then choose a beam." : "No photon beams among the matched pairs." + hidden);

        if (session is not null) { _sourceDoses = session.Source.Doses; RefilterScans(); }
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
            if (!IsScanMode) Draw(null);
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
        if (IsScanMode) return;
        if (_loaded is null) { Draw(null); return; }
        double dmax = BeamLines.DepthOfMaximum(_loaded);
        double depth = Depth.Mm ?? dmax;
        _depthText.Text = Kind == CurveKind.DepthDose ? "" : Depth.All ? $"dmax {dmax:0.#}, 50, 100, 200 mm" : Depth.Mm is null ? $"dmax = {depth:0.#} mm" : $"{depth:0.#} mm";
        Draw(Depth.All && Kind != CurveKind.DepthDose
            ? BeamLines.ProfilesAtAllDepths(_loaded, Kind)
            : new[] { BeamLines.ProfileAt(_loaded, Kind, depth) });
    }

    // ---- dose versus scans ----

    private async Task LoadScansAsync()
    {
        if (!Directory.Exists(_scanFolder.Text)) { _status.Text = "The scan folder must exist."; return; }
        int sequence = ++_loadSequence;
        _busy.Visible = true;
        _status.Text = $"Reading scans under {_scanFolder.Text}";
        try
        {
            string folder = _scanFolder.Text;
            string source = _sourceFolder();
            bool needDoses = _sourceDoses.Count == 0 && Directory.Exists(source);
            var (result, doses) = await Task.Run(() =>
            {
                var scans = ScanCatalog.Load(folder);
                var ds = needDoses ? RTFolderReader.Read(source, recursive: true).Doses : null;
                return (scans, ds);
            });
            if (sequence != _loadSequence) return;
            if (doses is not null) _sourceDoses = doses;
            _allScans = result.Scans;

            _refilling = true;
            _scanEnergy.DataStore = new[] { ScanCatalog.AnyEnergy }.Concat(ScanCatalog.Energies(_allScans)).ToArray();
            _scanFieldSize.DataStore = new[] { ScanCatalog.AnyFieldSize }.Concat(ScanCatalog.FieldSizes(_allScans)).ToArray();
            _scanEnergy.SelectedIndex = 0;
            _scanFieldSize.SelectedIndex = 0;
            _refilling = false;
            _scanEnergy.Enabled = _scanFieldSize.Enabled = _scan.Enabled = _dose.Enabled = _allScans.Count > 0;

            var hidden = new List<string>();
            if (result.HiddenElectrons > 0) hidden.Add($"{result.HiddenElectrons} electron");
            if (result.HiddenWedges > 0) hidden.Add($"{result.HiddenWedges} wedge");
            if (result.HiddenOther > 0) hidden.Add($"{result.HiddenOther} other");
            _status.Text = $"{_allScans.Count} photon open-field scan(s) in {result.Files} file(s)"
                         + (hidden.Count > 0 ? $"; not shown: {string.Join(", ", hidden)}" : "")
                         + (result.Errors.Count > 0 ? $"; {result.Errors.Count} file(s) failed to parse" : "")
                         + $"; matched against {_sourceDoses.Count} source dose(s)" + (doses is not null ? " read from the source folder" : "");
            RefilterScans();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence) _status.Text = $"Could not read the scans: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            if (sequence == _loadSequence) _busy.Visible = false;
        }
    }

    private void RefilterScans()
    {
        if (_refilling) return;
        var keep = ScanCatalog.Filter(_allScans, _scanEnergy.SelectedValue as string, _scanFieldSize.SelectedValue as string);
        var previous = _scan.SelectedValue as ScanDescriptor;
        _refilling = true;
        _scan.DataStore = keep;
        int index = keep.ToList().FindIndex(s => ReferenceEquals(s, previous));
        if (index < 0) index = keep.ToList().FindIndex(s => ScanCatalog.MatchingDoses(s, _sourceDoses).Count > 0);   // open on a curve, not on "no match"
        _scan.SelectedIndex = keep.Count == 0 ? -1 : Math.Max(0, index);
        _refilling = false;
        OnScanChanged();
    }

    private void OnScanChanged()
    {
        var scan = _scan.SelectedValue as ScanDescriptor;
        var previousDose = (_dose.SelectedValue as DoseCandidate)?.Dose;
        var candidates = scan is null ? Array.Empty<DoseCandidate>() : ScanCatalog.MatchingDoses(scan, _sourceDoses);
        _refilling = true;
        _dose.DataStore = candidates;
        int index = candidates.ToList().FindIndex(c => ReferenceEquals(c.Dose, previousDose));
        _dose.SelectedIndex = candidates.Count == 0 ? -1 : Math.Max(0, index);
        _refilling = false;

        if (candidates.Count == 0)
        {
            if (IsScanMode) Draw(null);
            if (scan is not null)
                _status.Text = _sourceDoses.Count == 0
                    ? "No source doses to match against: set the source folder (and run, or load the scans again)."
                    : $"No source dose matches {scan.Energy} {scan.FieldSize} (same energy, filter and field size).";
            return;
        }
        _ = LoadDoseAsync(_dose.SelectedValue as DoseCandidate);
    }

    private async Task LoadDoseAsync(DoseCandidate? candidate)
    {
        if (candidate is null) return;
        if (_loadedDose is not null && ReferenceEquals(_loadedDose.Dose, candidate.Dose)) { RedrawScan(); return; }

        int sequence = ++_loadSequence;
        _busy.Visible = true;
        _status.Text = $"Loading {candidate.Label}";
        try
        {
            var loaded = await Task.Run(() => BeamLines.LoadDose(candidate.Dose, _loader));
            if (sequence != _loadSequence) return;
            _loadedDose = loaded;
            _status.Text = $"{candidate.Label}; {loaded.Grid.DimensionX}x{loaded.Grid.DimensionY}x{loaded.Grid.DimensionZ} @ {loaded.Grid.XRes:0.##} mm; "
                         + (loaded.IsocentreFromPlan ? $"isocentre from the plan at ({loaded.IsoX:0.#}, {loaded.IsoY:0.#}, {loaded.IsoZ:0.#}) mm" : "no plan: the grid centre stands in for the isocentre");
            RedrawScan();
        }
        catch (Exception ex)
        {
            if (sequence == _loadSequence)
            {
                _loadedDose = null;
                Draw(null);
                _status.Text = $"Could not load the dose: {ex.GetType().Name}: {ex.Message}";
            }
        }
        finally
        {
            if (sequence == _loadSequence) _busy.Visible = false;
        }
    }

    private void RedrawScan()
    {
        if (!IsScanMode) return;
        var scan = _scan.SelectedValue as ScanDescriptor;
        var candidate = _dose.SelectedValue as DoseCandidate;
        if (scan is null || candidate is null || _loadedDose is null || !ReferenceEquals(_loadedDose.Dose, candidate.Dose)) { Draw(null); return; }
        Draw(new[] { BeamLines.ScanOverDose(scan, _loadedDose) });
    }

    // ---- drawing ----

    /// <summary>One curve, or one per depth: source solid and target dashed in one colour per curve.</summary>
    private void Draw(IReadOnlyList<ProfileCurve>? curves)
    {
        bool scans = IsScanMode;
        var plot = _plot.Plot;
        plot.Clear();
        if (curves is null || curves.Count == 0)
        {
            plot.Title(scans ? "No curve: load scans and choose one with a matched dose" : "No curve: run a comparison and choose a beam");
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
                s.LegendText = scans ? $"{tag}dose {curve.SourceName}" : $"{tag}{sourceAlias} {curve.SourceName}";
                s.LineWidth = 2;
                s.MarkerSize = 0;
                s.Color = colour;

                var t = plot.Add.Scatter(xs, target);
                t.LegendText = scans ? $"{tag}scan {curve.TargetName}" : $"{tag}{targetAlias} {curve.TargetName}";
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
