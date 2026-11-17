using System.Collections.ObjectModel;
using DSClibrary.Comparing;
using DSClibrary.Matching;
using DSCprototypes.Shared;
using DSCprototypes.Shared.ViewModels;
using Eto.Drawing;
using Eto.Forms;

namespace DSCproto.EtoApp;

/// <summary>
/// The window, written the way Eto is meant to be written: controls are fields, layout is built in
/// code with DynamicLayout, and the form itself handles its events. There is no separate view model;
/// the state the window needs (the last session, the criteria) is held here and the shared runner does
/// the work on a background thread.
/// </summary>
public sealed class MainForm : Form
{
    // A row of the editable criteria grid. Eto's GridView binds to plain properties.
    private sealed class CriterionRow
    {
        public double TolerancePercent { get; set; } = 2.0;
        public double DistanceMm { get; set; } = 2.0;
        public double ThresholdPercent { get; set; } = 10.0;
        public bool Global { get; set; } = true;
        public bool Gamma { get; set; }
        public BeamsCompared AppliesTo { get; set; } = BeamsCompared.Both;
        public string AppliesToText
        {
            get => AppliesTo.ToString();
            set => AppliesTo = Enum.TryParse<BeamsCompared>(value, out var b) ? b : BeamsCompared.Both;
        }
        public DtaSpec ToSpec() => new(TolerancePercent, DistanceMm, ThresholdPercent, Global, Gamma, AppliesTo);
        public static CriterionRow From(DtaSpec s) => new()
        {
            TolerancePercent = s.TolerancePercent, DistanceMm = s.DistanceMm,
            ThresholdPercent = s.ThresholdPercent, Global = s.Global, Gamma = s.Gamma, AppliesTo = s.AppliesTo,
        };
    }

    private readonly TextBox _sourceFolder = new() { PlaceholderText = "Folder of RT Dose and RT Plan files" };
    private readonly TextBox _targetFolder = new() { PlaceholderText = "Folder of RT Dose and RT Plan files" };
    private readonly TextBox _sourceAlias = new() { Text = "Reference", Width = 160 };
    private readonly TextBox _targetAlias = new() { Text = "Target", Width = 160 };
    // Each folder's algorithm, for doses whose plan label and beam name name none (crcrewso, 2026-11-16).
    private readonly TextBox _sourceAlgorithm = new() { PlaceholderText = "if names lack it", Width = 140 };
    private readonly TextBox _targetAlgorithm = new() { PlaceholderText = "if names lack it", Width = 140 };
    private readonly TextBox _algorithmNames = new() { Text = AlgorithmTokens.Default.ToString(), Width = 420,
        ToolTip = "Algorithm = token, token; ... Tokens are matched anywhere in a name, ignoring case." };
    private readonly RadioButtonList _strategy = new()
    {
        Orientation = Orientation.Horizontal,
        Items = { MatchModes.Window[0].Label, MatchModes.Window[1].Label, MatchModes.Window[2].Label },
        SelectedIndex = 0,
    };

    private readonly ObservableCollection<CriterionRow> _criteria = new();
    private readonly GridView _criteriaGrid = new() { Height = 200 };
    private readonly ObservableCollection<ResultRow> _rows = new();
    private readonly GridView _resultsGrid = new();
    private readonly ListBox _unmatched = new();
    private readonly TextArea _log = new() { ReadOnly = true, Wrap = true, Font = Fonts.Monospace(9) };
    private readonly Label _status = new() { Text = "Choose two folders of RT Dose and RT Plan files." };
    private readonly ProgressBar _progress = new() { Indeterminate = true, Visible = false, Width = 160 };

    private readonly Button _run = new() { Text = "Run comparison" };
    private readonly Button _cancel = new() { Text = "Cancel", Enabled = false };
    private readonly Button _export = new() { Text = "Export spreadsheet (CSV)", Enabled = false };

    private ComparisonSession? _session;
    private CancellationTokenSource? _cancelSource;
    private readonly GraphPage _graphs;
    private readonly ScanPage _scans;
    private readonly TabControl _tabs = new();
    // Two comparisons with nothing in common but the doses (crcrewso, 2026-11-17): each tab its own inputs, run and results.
    private readonly TabControl _workflows = new();

    // Kept with a comparison, because they can change results: Max and GPU float.
    private readonly DropDown _maximum = new() { ToolTip = "GlobalMax: the source grid's maximum. CaxMax: the 3 x 3 average at the CAX maximum, used only when the source plan's jaws are on axis, otherwise Global Max." };
    private readonly CheckBox _gpuFloat = new() { Text = "GPU float", ToolTip = "GPU engines only. Float may differ from the reference on a few points in millions; double matches it exactly." };

    // This machine's settings (MachineSettings, kept in its file and saved as they change). None changes a count.
    private readonly DropDown _engine = new() { ToolTip = "HandWritten is the original and the reference. The others apply the same rules faster and give the same counts." };
    private readonly DropDown _threads = new() { ToolTip = "CPU threads: within a pair for MultiThread, pairs at once for HandWritten and SingleThread. The recommended count leaves out SMT siblings and low-power E-cores." };
    private readonly NumericStepper _pairsAtOnce = new() { MinValue = 0, MaxValue = 256, Width = 90, ToolTip = "0 chooses from free memory." };
    private readonly CheckBox _partialTargetReads = new() { ToolTip = "Read each target only where the criteria and the edge check use it. The counts are the same either way." };
    private readonly NumericStepper _readers = new() { MinValue = 0, MaxValue = 64, Width = 90, ToolTip = "Files read at once, ahead of the comparison; fewer if memory is short. 0 has each worker read its own pair." };
    private readonly NumericStepper _pairsHeld = new() { MinValue = 1, MaxValue = 64, Width = 90, ToolTip = "Loaded pairs waiting for a worker, when Readers is above 0." };
    private readonly int[] _threadCounts = MachineSettings.ThreadCounts(CpuTopology.MaxThreads, CpuTopology.PhysicalCores).ToArray();
    private bool _loadingSettings;

    public MainForm()
    {
        _graphs = new GraphPage(ComparisonRunner.LoadGrid, () => (_sourceAlias.Text, _targetAlias.Text));
        _scans = new ScanPage(PickFolder);
        // The source folder is offered to the Doses vs scans tab until a doses folder is chosen there.
        _sourceFolder.TextChanged += (_, _) => _scans.OfferDosesFolder(_sourceFolder.Text);
        Title = "DicomStrictCompare - Eto.Forms prototype";
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(900, 600);

        foreach (var d in DtaSpec.Defaults) _criteria.Add(CriterionRow.From(d));
        BuildCriteriaGrid();
        BuildResultsGrid();
        WireEvents();
        BuildSettings();

        // DynamicLayout: rows and columns described in code, with scaling flags instead of a grid of
        // sizes. BeginVertical/BeginHorizontal is Eto's own layout idiom.
        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(12) };

        // A DynamicLayout shares column widths across every row of one vertical section, so each
        // part of the window with its own column structure gets its own BeginVertical/EndVertical:
        // the folder rows, the splitter, and the action bar. (Measured 2026-11-08: without that, the
        // splitter landed in the status line's first column and the tabs never reached the right edge.)
        layout.BeginVertical();
        void FolderRow(string caption, TextBox folder, string dialogTitle, TextBox alias, TextBox algorithm)
        {
            layout.BeginHorizontal();
            layout.Add(new Label { Text = caption, Width = 130, VerticalAlignment = VerticalAlignment.Center });
            layout.Add(folder, xscale: true);
            layout.Add(BrowseButton(folder, dialogTitle, ReferenceEquals(folder, _sourceFolder) ? _targetFolder : _sourceFolder));
            layout.Add(new Label { Text = "Alias", VerticalAlignment = VerticalAlignment.Center });
            layout.Add(alias);
            layout.Add(new Label { Text = "Algorithm", VerticalAlignment = VerticalAlignment.Center });
            layout.Add(algorithm);
            layout.EndHorizontal();
        }
        FolderRow("Source (reference)", _sourceFolder, "Source (reference) folder", _sourceAlias, _sourceAlgorithm);
        FolderRow("Target", _targetFolder, "Target folder", _targetAlias, _targetAlgorithm);
        layout.BeginHorizontal();
        layout.Add(new Label { Text = "Match doses by", Width = 130, VerticalAlignment = VerticalAlignment.Center });
        layout.Add(_strategy, xscale: true);
        layout.EndHorizontal();
        layout.EndVertical();

        var criteriaPanel = new DynamicLayout { DefaultSpacing = new Size(8, 6) };
        criteriaPanel.AddRow(new Label { Text = "Criteria", Font = SystemFonts.Bold() });
        criteriaPanel.AddRow(_criteriaGrid);
        var add = new Button { Text = "Add" };
        var remove = new Button { Text = "Remove" };
        add.Click += (_, _) => _criteria.Add(new CriterionRow());
        remove.Click += (_, _) => { if (_criteriaGrid.SelectedItem is CriterionRow r) _criteria.Remove(r); };
        criteriaPanel.AddRow(new TableLayout(add, remove, null));
        criteriaPanel.AddRow(new TableLayout(new TableRow(new Label { Text = "Max", VerticalAlignment = VerticalAlignment.Center }, _maximum, _gpuFloat, null)));
        criteriaPanel.Add(null, yscale: true);

        var tabs = _tabs;
        tabs.Pages.Add(new TabPage(_resultsGrid) { Text = "Results" });
        tabs.Pages.Add(new TabPage(_unmatched) { Text = "Unmatched" });
        tabs.Pages.Add(new TabPage(_log) { Text = "Log" });
        tabs.Pages.Add(new TabPage(_graphs) { Text = "Graphs", Padding = new Padding(6) });
        tabs.Pages.Add(Placeholder("Detector geometry",
            "Placeholder: detector geometry correction for PTW scans. IDetectorCorrection exists in DSClibrary; no physical kernel is implemented yet (backlog D4), so there is nothing honest to switch on here."));
        tabs.Pages.Add(Placeholder("Archive",
            "Placeholder: PNG export of each comparison for the record (an archiving output, not a display mechanism - crcrewso, 2026-11-08). Rendered from the retained ComparisonOutcome, independent of this window."));
        tabs.Pages.Add(SettingsPage());

        var splitter = new Splitter
        {
            Orientation = Orientation.Horizontal,
            Position = 340,
            FixedPanel = SplitterFixedPanel.Panel1,
            Panel1 = criteriaPanel,
            Panel2 = tabs,
        };

        // The section itself must scale vertically, or it takes its preferred height and pushes the
        // action bar below the window (measured 2026-11-08).
        layout.BeginVertical(yscale: true);
        layout.Add(splitter, xscale: true, yscale: true);
        layout.EndVertical();

        layout.BeginVertical();
        layout.BeginHorizontal();
        _status.Wrap = WrapMode.None;
        layout.Add(_status, xscale: true);
        layout.Add(_progress);
        layout.Add(_run);
        layout.Add(_cancel);
        layout.Add(_export);
        layout.EndHorizontal();
        layout.EndVertical();

        _workflows.Pages.Add(new TabPage(layout) { Text = "Doses vs doses" });
        _workflows.Pages.Add(new TabPage(_scans) { Text = "Doses vs scans" });
        Content = _workflows;

        // Command-line pre-fill and scripted run, the same path a click takes.
        var options = LaunchOptions.FromCommandLine();
        if (options.SourceFolder is not null) _sourceFolder.Text = options.SourceFolder;
        if (options.TargetFolder is not null) _targetFolder.Text = options.TargetFolder;
        if (options.SourceAlias is not null) _sourceAlias.Text = options.SourceAlias;
        if (options.TargetAlias is not null) _targetAlias.Text = options.TargetAlias;
        if (options.AddGamma) _criteria.Add(CriterionRow.From(DtaSpec.DefaultGamma));
        if (options.CanAutoRun)
        {
            Shown += async (_, _) =>
            {
                await RunAsync();
                if (options.ExportFolder is not null && _session is not null)
                {
                    var written = CsvReport.Write(_session, options.ExportFolder);
                    _status.Text = _session.Summary + " - wrote " + string.Join(" and ", written.Select(Path.GetFileName));
                }
                if (options.ShowGraphs) _tabs.SelectedIndex = 3;
                _graphs.Apply(options);
                await ApplyScansAsync(options);
            };
        }
        else if (options.ShowGraphs || options.ScanFolder is not null)
        {
            Shown += async (_, _) =>
            {
                if (options.ShowGraphs) _tabs.SelectedIndex = 3;
                _graphs.Apply(options);
                await ApplyScansAsync(options);
            };
        }
    }

    /// <summary>With a scans folder on the command line, opens the Doses vs scans tab, which runs when it has a doses folder.</summary>
    private async Task ApplyScansAsync(LaunchOptions options)
    {
        if (options.ScanFolder is null) return;
        _workflows.SelectedIndex = 1;
        await _scans.ApplyAsync(options);
    }

    private static TabPage Placeholder(string title, string text) =>
        new(new Label { Text = text, Wrap = WrapMode.Word }) { Text = title, Padding = new Padding(12) };

    private string? _lastPicked;
    private string? _lastExportFolder;

    /// <summary>
    /// Where a Browse dialog opens: the parent of this field's previous choice (crcrewso, 2026-11-08),
    /// else the parent of the other field's folder, else the last folder picked anywhere.
    /// </summary>
    private string? StartFor(string? own, string? other) =>
        DSCprototypes.Shared.ViewModels.MainViewModel.ParentOf(own)
        ?? DSCprototypes.Shared.ViewModels.MainViewModel.ParentOf(other)
        ?? _lastPicked;

    private string? PickFolder(string title, string? start)
    {
        // Eto's dialog is the platform's own folder dialog on every OS; Directory is its start folder.
        var dialog = new SelectFolderDialog { Title = title };
        if (start is not null) dialog.Directory = start;
        if (dialog.ShowDialog(this) != DialogResult.Ok) return null;
        _lastPicked = dialog.Directory;
        return dialog.Directory;
    }

    private Button BrowseButton(TextBox target, string title, TextBox other)
    {
        var button = new Button { Text = "Browse" };
        button.Click += (_, _) =>
        {
            var picked = PickFolder(title, StartFor(target.Text, other.Text));
            if (picked is not null) target.Text = picked;
        };
        return button;
    }

    private void BuildCriteriaGrid()
    {
        _criteriaGrid.DataStore = _criteria;
        _criteriaGrid.Columns.Add(new GridColumn
        {
            HeaderText = "Tol %", Editable = true, Width = 70,
            DataCell = new TextBoxCell { Binding = Binding.Property<CriterionRow, double>(r => r.TolerancePercent).Convert(d => d.ToString("0.##"), s => double.TryParse(s, out var v) ? v : 0) },
        });
        _criteriaGrid.Columns.Add(new GridColumn
        {
            HeaderText = "DTA mm", Editable = true, Width = 70,
            DataCell = new TextBoxCell { Binding = Binding.Property<CriterionRow, double>(r => r.DistanceMm).Convert(d => d.ToString("0.##"), s => double.TryParse(s, out var v) ? v : 0) },
        });
        _criteriaGrid.Columns.Add(new GridColumn
        {
            HeaderText = "Thr %", Editable = true, Width = 70,
            DataCell = new TextBoxCell { Binding = Binding.Property<CriterionRow, double>(r => r.ThresholdPercent).Convert(d => d.ToString("0.##"), s => double.TryParse(s, out var v) ? v : 0) },
        });
        _criteriaGrid.Columns.Add(new GridColumn
        {
            HeaderText = "Global", Editable = true, Width = 60,
            DataCell = new CheckBoxCell { Binding = Binding.Property<CriterionRow, bool?>(r => r.Global) },
        });
        _criteriaGrid.Columns.Add(new GridColumn
        {
            HeaderText = "Gamma", Editable = true, Width = 60,
            DataCell = new CheckBoxCell { Binding = Binding.Property<CriterionRow, bool?>(r => r.Gamma) },
        });
        _criteriaGrid.Columns.Add(new GridColumn
        {
            HeaderText = "Applies to", Editable = true, Width = 90,
            DataCell = new ComboBoxCell
            {
                DataStore = Enum.GetNames<BeamsCompared>(),
                Binding = Binding.Property<CriterionRow, object>(r => r.AppliesToText).Convert(o => o, o => o?.ToString() ?? nameof(BeamsCompared.Both)),
            },
        });
    }

    private void BuildResultsGrid()
    {
        _resultsGrid.DataStore = _rows;
        GridColumn Col(string header, Func<ResultRow, string> get, int width = 90) => new()
        {
            HeaderText = header, Width = width,
            DataCell = new TextBoxCell { Binding = Binding.Delegate(get) },
        };
        _resultsGrid.Columns.Add(Col("Key", r => r.Key, 320));
        _resultsGrid.Columns.Add(Col("Criterion", r => r.Criterion.Label, 180));
        _resultsGrid.Columns.Add(Col("Ambiguous", r => r.WorstOf, 90));
        _resultsGrid.Columns.Add(Col("Compared", r => r.Compared.ToString("N0")));
        _resultsGrid.Columns.Add(Col("Failed", r => r.Failed.ToString("N0")));
        _resultsGrid.Columns.Add(Col("% failed", r => r.PercentFailed is { } p ? p.ToString("0.000") : ""));
        _resultsGrid.Columns.Add(Col("Max γ", r => r.MaxGamma is { } g ? g.ToString("0.00") : "", 60));
        _resultsGrid.Columns.Add(Col("Src MU", r => r.SourceMonitorUnits?.ToString("0.#") ?? "", 70));
        _resultsGrid.Columns.Add(Col("Tgt MU", r => r.TargetMonitorUnits?.ToString("0.#") ?? "", 70));
        _resultsGrid.Columns.Add(Col("s", r => r.ElapsedSeconds.ToString("0.0"), 50));
        _resultsGrid.Columns.Add(Col("Failure", r => r.Failure ?? "", 240));
    }

    private void WireEvents()
    {
        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _cancelSource?.Cancel();
        _export.Click += (_, _) => Export();
    }

    // ---- Settings ----

    private void BuildSettings()
    {
        foreach (var m in Enum.GetValues<MaximumKind>()) _maximum.Items.Add(m.ToString());
        _maximum.SelectedIndex = 0;
        foreach (var k in Enum.GetValues<ComparerKind>()) _engine.Items.Add(k.ToString());
        foreach (int n in _threadCounts) _threads.Items.Add(n == CpuTopology.PhysicalCores ? $"{n} (recommended)" : n.ToString());

        var settings = MachineSettings.Load(MachineSettings.DefaultPath, out var problem);
        _loadingSettings = true;
        _engine.SelectedIndex = Array.IndexOf(Enum.GetValues<ComparerKind>(), settings.Engine);
        _threads.SelectedIndex = Array.IndexOf(_threadCounts, MachineSettings.ListedThreads(_threadCounts, settings.Threads));
        _pairsAtOnce.Value = settings.PairsAtOnce ?? 0;
        _partialTargetReads.Checked = settings.PartialTargetReads;
        _readers.Value = settings.Readers;
        _pairsHeld.Value = settings.PairsHeld;
        _algorithmNames.Text = settings.AlgorithmTokens ?? AlgorithmTokens.Default.ToString();
        _loadingSettings = false;
        if (problem is not null) _status.Text = problem;

        _engine.SelectedIndexChanged += (_, _) => SaveSettings();
        _threads.SelectedIndexChanged += (_, _) => SaveSettings();
        _pairsAtOnce.ValueChanged += (_, _) => SaveSettings();
        _partialTargetReads.CheckedChanged += (_, _) => SaveSettings();
        _readers.ValueChanged += (_, _) => SaveSettings();
        _pairsHeld.ValueChanged += (_, _) => SaveSettings();
        _algorithmNames.TextChanged += (_, _) => SaveSettings();
    }

    /// <summary>The Algorithm names setting, or the default list when it doesn't parse (the status line says why).</summary>
    private AlgorithmTokens ParsedAlgorithmNames()
    {
        try { return AlgorithmTokens.Parse(_algorithmNames.Text); }
        catch (FormatException ex) { _status.Text = $"Algorithm names not understood, using the default list: {ex.Message}"; return AlgorithmTokens.Default; }
    }

    private MachineSettings CurrentSettings()
    {
        int threads = _threadCounts[Math.Max(0, _threads.SelectedIndex)];
        return new MachineSettings
        {
            Engine = Enum.GetValues<ComparerKind>()[Math.Max(0, _engine.SelectedIndex)],
            Threads = threads == CpuTopology.PhysicalCores ? null : threads,
            PairsAtOnce = _pairsAtOnce.Value > 0 ? (int)_pairsAtOnce.Value : null,
            PartialTargetReads = _partialTargetReads.Checked == true,
            Readers = (int)_readers.Value,
            PairsHeld = Math.Max(1, (int)_pairsHeld.Value),
            AlgorithmTokens = _algorithmNames.Text == AlgorithmTokens.Default.ToString() ? null : _algorithmNames.Text,
        };
    }

    private void SaveSettings()
    {
        if (_loadingSettings) return;
        try { CurrentSettings().Save(MachineSettings.DefaultPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _status.Text = $"Settings not saved: {ex.Message}"; }
    }

    private TabPage SettingsPage()
    {
        Label L(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
        var table = new TableLayout { Spacing = new Size(12, 8) };
        table.Rows.Add(new TableRow(L("Engine"), _engine, null));
        table.Rows.Add(new TableRow(L("Threads"), _threads, null));
        table.Rows.Add(new TableRow(L("Pairs at once"), _pairsAtOnce, null));
        table.Rows.Add(new TableRow(L("Target box only"), _partialTargetReads, null));
        table.Rows.Add(new TableRow(L("Readers"), _readers, null));
        table.Rows.Add(new TableRow(L("Held"), _pairsHeld, null));
        var layout = new DynamicLayout { Padding = new Padding(12), DefaultSpacing = new Size(8, 10) };
        layout.AddRow(new Label { Text = "Settings for this machine. They change how fast a comparison runs, never its counts, and are kept for this user on this machine, not in a study.", Wrap = WrapMode.Word });
        layout.AddRow(table);
        layout.AddRow(new Label { Text = "Pairing", Font = SystemFonts.Bold() });
        layout.AddRow(new Label { Text = "Algorithm names change which doses pair, so they can change counts. A dose's algorithm is read from its plan label and beam name, else from its folder's Algorithm box; AAA pairs only with AAA, and a dose with none only with others that have none.", Wrap = WrapMode.Word });
        layout.AddRow(new TableLayout(new TableRow(L("Algorithm names"), _algorithmNames, null)) { Spacing = new Size(12, 8) });
        layout.AddRow(new Label { Text = "Kept in " + MachineSettings.DefaultPath, Wrap = WrapMode.Word });
        layout.Add(null, yscale: true);
        return new TabPage(layout) { Text = "Settings" };
    }

    private async Task RunAsync()
    {
        if (!Directory.Exists(_sourceFolder.Text) || !Directory.Exists(_targetFolder.Text))
        {
            _status.Text = "Both folders must exist.";
            return;
        }
        if (_criteria.Count == 0)
        {
            _status.Text = "Add at least one criterion.";
            return;
        }

        SetRunning(true);
        _rows.Clear();
        _unmatched.Items.Clear();
        _log.Text = "";
        _session = null;
        _graphs.SetSession(null);
        _cancelSource = new CancellationTokenSource();

        var strategy = MatchModes.Window[Math.Max(0, _strategy.SelectedIndex)].Strategy;
        var run = CurrentSettings().ToRun(Enum.GetValues<MaximumKind>()[Math.Max(0, _maximum.SelectedIndex)],
            _gpuFloat.Checked == true ? GpuPrecision.Float : GpuPrecision.Double);
        var request = new ComparisonRequest(
            _sourceFolder.Text, _targetFolder.Text, _sourceAlias.Text, _targetAlias.Text,
            _criteria.Select(c => c.ToSpec()).ToList(), strategy,
            DegreeOfParallelism: run.PairsAtOnce, Maximum: run.Maximum, Comparer: run.ComparerOrDefault,
            PartialTargetReads: run.PartialTargetReads, Queue: run.Queue,
            SourceAlgorithm: string.IsNullOrWhiteSpace(_sourceAlgorithm.Text) ? null : _sourceAlgorithm.Text,
            TargetAlgorithm: string.IsNullOrWhiteSpace(_targetAlgorithm.Text) ? null : _targetAlgorithm.Text,
            Algorithms: ParsedAlgorithmNames());

        // Application.Instance.Invoke is Eto's way back to the UI thread from the runner's worker.
        var progress = new Progress<string>(line => Application.Instance.Invoke(() =>
        {
            _log.Append(line + Environment.NewLine, scrollToCursor: true);
            _status.Text = line;
        }));

        // Rows as pairs finish (D30), replaced by the final ordered set when the run ends.
        var live = new RowBatcher<ResultRow>(_rows, post: a => Application.Instance.AsyncInvoke(a))
            { Flushed = n => _status.Text = $"Comparing: {n} result row(s) so far" };

        try
        {
            var session = await ComparisonRunner.RunAsync(request, progress, _cancelSource.Token, live.Progress);
            live.Stop();
            _rows.Clear();
            foreach (var row in session.Rows) _rows.Add(row);
            foreach (var u in session.Unmatched) _unmatched.Items.Add(u.ToString());
            foreach (var a in session.Match.AmbiguousKeys) _unmatched.Items.Add(a);
            _session = session;
            _graphs.SetSession(session);
            _status.Text = session.Summary;
        }
        catch (OperationCanceledException)
        {
            live.Stop();
            _status.Text = $"Cancelled. The {_rows.Count} row(s) shown are the pairs finished before it stopped; nothing was exported.";
        }
        catch (Exception ex)
        {
            live.Stop();
            _status.Text = $"Failed: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            SetRunning(false);
            _cancelSource.Dispose();
            _cancelSource = null;
        }
    }

    private void Export()
    {
        if (_session is null) return;
        var folder = PickFolder("Folder for the spreadsheet", StartFor(_lastExportFolder, _sourceFolder.Text));
        if (folder is null) return;
        _lastExportFolder = folder;
        var written = CsvReport.Write(_session, folder);
        _status.Text = "Wrote " + string.Join(" and ", written.Select(Path.GetFileName));
    }

    private void SetRunning(bool running)
    {
        _run.Enabled = !running;
        _cancel.Enabled = running;
        _export.Enabled = !running && _session is not null;
        _progress.Visible = running;
    }
}
