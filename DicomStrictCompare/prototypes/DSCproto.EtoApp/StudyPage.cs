using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows.Input;
using DSClibrary.Studies;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.Studies;
using DSCprototypes.Shared.ViewModels;
using Eto.Drawing;
using Eto.Forms;
using Label = Eto.Forms.Label;   // ScottPlot has a Label type of its own

namespace DSCproto.EtoApp;

/// <summary>
/// The Study tab beside the criteria list: setup, pairing, results, index tables, curves and log. Bound to the shared
/// <see cref="StudyViewModel"/>, so it behaves exactly as the Avalonia tab does; this panel only shows it and draws.
/// </summary>
public sealed class StudyPage : Panel
{
    private readonly StudyViewModel _vm;

    private readonly GridView _sets = new() { AllowMultipleSelection = false };
    private readonly GridView _comparisons = new() { AllowMultipleSelection = false };
    private readonly Button _acrossAlgorithms = new() { Text = "Use the AAA / AXB label rule", Enabled = false,
        ToolTip = "Sets the selected comparison's plan label rule to (AAA|AXB) -> {algorithm}" };
    private readonly ProgressBar _running = new() { Indeterminate = true, Visible = false, Width = 140 };

    private readonly ListBox _pairing = new() { Font = Fonts.Monospace(9) };
    private readonly GridView _rows = new();

    private readonly DropDown _indexComparison = new() { Width = 220 };
    private readonly DropDown _indexCriterion = new() { Width = 220 };
    private readonly DropDown _rowAxis = new() { Width = 120 };
    private readonly DropDown _columnAxis = new() { Width = 120 };
    private readonly Label _indexTitle = new() { Wrap = WrapMode.Word };
    private readonly GridView _index = new() { GridLines = GridLines.Both };

    private readonly DropDown _field = new() { Width = 420 };
    private readonly DropDown _kind = new() { Width = 130 };
    private readonly DropDown _reference = new() { Width = 140 };
    private readonly ProgressBar _loadingCurves = new() { Indeterminate = true, Visible = false, Width = 100 };
    private readonly ScottPlot.Eto.EtoPlot _curvePlot = new();
    private readonly ScottPlot.Eto.EtoPlot _differencePlot = new();
    private readonly Label _curveStatus = new() { Wrap = WrapMode.Word };

    private readonly TextArea _log = new() { ReadOnly = true, Wrap = true, Font = Fonts.Monospace(9) };
    private readonly Label _status = new() { Wrap = WrapMode.Word };

    private const string DepthsTip = "For example: dmax, 15, 50, 100, 200, 300 mm (cm also accepted)";

    public StudyPage(StudyViewModel vm)
    {
        _vm = vm;
        BuildSetsGrid();
        BuildComparisonsGrid();
        BuildResultsGrid();
        foreach (var g in new[] { _sets, _comparisons, _rows, _index }) GridCopy.Attach(g);

        _pairing.DataStore = vm.PairingLines;
        _rows.DataStore = vm.Rows;
        Choice(_indexComparison, vm.IndexComparisons, () => vm.SelectedIndexComparison, v => vm.SelectedIndexComparison = v, nameof(vm.SelectedIndexComparison));
        Choice(_indexCriterion, vm.IndexCriteria, () => vm.SelectedIndexCriterion, v => vm.SelectedIndexCriterion = v, nameof(vm.SelectedIndexCriterion));
        Choice(_rowAxis, vm.Axes.Cast<object>().ToArray(), () => vm.RowAxis, v => { if (v is BeamAxis a) vm.RowAxis = a; }, nameof(vm.RowAxis));
        Choice(_columnAxis, vm.Axes.Cast<object>().ToArray(), () => vm.ColumnAxis, v => { if (v is BeamAxis a) vm.ColumnAxis = a; }, nameof(vm.ColumnAxis));
        Choice(_field, vm.Fields, () => vm.SelectedField, v => vm.SelectedField = v, nameof(vm.SelectedField));
        Choice(_kind, vm.Kinds.Cast<object>().ToArray(), () => vm.Kind, v => { if (v is CurveKind k) vm.Kind = k; }, nameof(vm.Kind));
        Choice(_reference, vm.ReferenceSets, () => vm.ReferenceSet, v => vm.ReferenceSet = v, nameof(vm.ReferenceSet));

        _log.TextBinding.Bind(Binding.Property(vm, v => v.Log), DualBindingMode.OneWay);
        _status.TextBinding.Bind(Binding.Property(vm, v => v.Status), DualBindingMode.OneWay);
        _curveStatus.TextBinding.Bind(Binding.Property(vm, v => v.CurveStatus), DualBindingMode.OneWay);
        vm.PropertyChanged += OnChanged;

        var tabs = new TabControl();
        tabs.Pages.Add(new TabPage(SetupPage()) { Text = "Setup", Padding = new Padding(6) });
        tabs.Pages.Add(new TabPage(_pairing) { Text = "Pairing" });
        tabs.Pages.Add(new TabPage(_rows) { Text = "Results" });
        tabs.Pages.Add(new TabPage(IndexPage()) { Text = "Index tables", Padding = new Padding(6) });
        tabs.Pages.Add(new TabPage(CurvesPage()) { Text = "Curves", Padding = new Padding(6) });
        tabs.Pages.Add(new TabPage(_log) { Text = "Log" });

        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6), Padding = new Padding(0, 6, 0, 0) };
        layout.Add(tabs, xscale: true, yscale: true);
        layout.Add(_status);
        Content = layout;

        DrawStudy(null);
        BuildIndexColumns(null);
    }

    private static Label Caption(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Wrap = WrapMode.None };

    /// <summary>A text box bound both ways to a view-model property; no width lets it take the row's spare room.</summary>
    private TextBox Text(Expression<Func<StudyViewModel, string>> property, int? width, string? tip = null, string? placeholder = null)
    {
        var box = new TextBox { ToolTip = tip, PlaceholderText = placeholder };
        if (width is { } w) box.Width = w;
        box.TextBinding.Bind(Binding.Property(_vm, property));
        return box;
    }

    private static Button Do(string text, ICommand command) => new() { Text = text, Command = command };

    private Control SetupPage()
    {
        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6) };
        layout.Add(new Label { Text = "Dose sets: a folder of RT Dose and RT Plan files each, under a name you choose", Font = SystemFonts.Bold() });
        layout.Add(_sets, yscale: true);
        layout.AddSeparateRow(Do("Add set", _vm.AddSetCommand), Do("Remove set", _vm.RemoveSetCommand),
            Do("Browse folder for the selected set", _vm.BrowseSetFolderCommand), null);

        layout.Add(new Label { Font = SystemFonts.Bold(), Wrap = WrapMode.Word,
            Text = "Comparisons. Rules: 'pattern -> replacement; ...' (regular expressions). Overrides: 'source label = target label; ...'" });
        layout.Add(_comparisons, yscale: true);
        _acrossAlgorithms.Click += (_, _) => _vm.SelectedComparison?.UseAcrossAlgorithmsCommand.Execute(null);
        layout.AddSeparateRow(Do("Add comparison", _vm.AddComparisonCommand), Do("Remove comparison", _vm.RemoveComparisonCommand), _acrossAlgorithms, null);

        // The study file box takes the spare width, so the row fits beside the criteria list.
        layout.Add(new TableLayout(new TableRow(Caption("Profile depths"), Text(v => v.DepthsText, 220, DepthsTip),
            Caption("Study file"), new TableCell(Text(v => v.StudyFile, null, placeholder: "Path to a .json study file"), true),
            Do("Open", _vm.OpenStudyCommand), Do("Save", _vm.SaveStudyCommand))) { Spacing = new Size(8, 6) });
        layout.AddSeparateRow(Do("Preview pairing", _vm.PreviewCommand), Do("Run study", _vm.RunCommand), Do("Cancel", _vm.CancelCommand),
            Do("Export study spreadsheets (CSV)", _vm.ExportCommand), _running, null);
        return layout;
    }

    private Control IndexPage()
    {
        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6) };
        // Two rows, so the page fits beside the criteria list.
        layout.AddSeparateRow(Caption("Comparison"), _indexComparison, Caption("Criterion"), _indexCriterion, null);
        layout.AddSeparateRow(Caption("Rows"), _rowAxis, Caption("Columns"), _columnAxis, null);
        layout.Add(_indexTitle);
        layout.Add(_index, yscale: true);
        return layout;
    }

    private Control CurvesPage()
    {
        var layout = new DynamicLayout { DefaultSpacing = new Size(8, 6) };
        layout.AddSeparateRow(Caption("Field"), _field, _loadingCurves, null);
        layout.AddSeparateRow(Caption("Curve"), _kind, Caption("Reference"), _reference, Caption("Depths"), Text(v => v.DepthsText, 200, DepthsTip), null);
        // Curves above, differences below, two to one as Avalonia's rows are; the split can be dragged.
        layout.Add(new Splitter
        {
            Orientation = Orientation.Vertical,
            FixedPanel = SplitterFixedPanel.None,
            RelativePosition = 0.67,
            Panel1 = _curvePlot,
            Panel2 = _differencePlot,
        }, yscale: true);
        layout.Add(_curveStatus);
        return layout;
    }

    private void BuildSetsGrid()
    {
        _sets.DataStore = _vm.Sets;
        _sets.Columns.Add(Edit<DoseSetViewModel>("Name", s => s.Name, 140));
        _sets.Columns.Add(Edit<DoseSetViewModel>("Folder", s => s.Folder, 300));
        _sets.Columns.Add(Edit<DoseSetViewModel>("Algorithm", s => s.Algorithm, 100));
        _sets.Columns.Add(Edit<DoseSetViewModel>("TPS", s => s.TpsVersion, 100));
        _sets.Columns.Add(new GridColumn
        {
            HeaderText = "Dose reported as", Editable = true, Width = 170,
            DataCell = new ComboBoxCell
            {
                DataStore = DoseSetViewModel.ReportingChoices.Select(r => r.ToString()).ToArray(),
                Binding = Binding.Delegate<DoseSetViewModel, object>(s => s.Reporting.ToString(),
                    (s, v) => { if (Enum.TryParse<DoseReporting>(v?.ToString(), out var r)) s.Reporting = r; }),
            },
        });
        _sets.Columns.Add(Edit<DoseSetViewModel>("Plan filter (regex)", s => s.PlanFilter, 140));
        _sets.Columns.Add(Edit<DoseSetViewModel>("Notes", s => s.Notes, 160));
        _sets.SelectionChanged += (_, _) => _vm.SelectedSet = _sets.SelectedItem as DoseSetViewModel;
        Follow(_sets, _vm.Sets);
    }

    private void BuildComparisonsGrid()
    {
        _comparisons.DataStore = _vm.Comparisons;
        _comparisons.Columns.Add(Edit<SetComparisonViewModel>("Source set", c => c.SourceSet, 140));
        _comparisons.Columns.Add(Edit<SetComparisonViewModel>("Target set", c => c.TargetSet, 140));
        _comparisons.Columns.Add(new GridColumn
        {
            HeaderText = "Match by", Editable = true, Width = 250,
            DataCell = new ComboBoxCell
            {
                DataStore = MatchModes.Study.Select(m => m.Label).ToArray(),
                Binding = Binding.Delegate<SetComparisonViewModel, object>(c => c.Match.Label,
                    (c, v) => { if (MatchModes.Study.FirstOrDefault(x => x.Label == v as string) is { } mode) c.Match = mode; }),
            },
        });
        _comparisons.Columns.Add(Edit<SetComparisonViewModel>("Plan label rules", c => c.LabelRules, 200));
        _comparisons.Columns.Add(Edit<SetComparisonViewModel>("Field name rules", c => c.FieldRules, 200));
        _comparisons.Columns.Add(Edit<SetComparisonViewModel>("Label overrides", c => c.Overrides, 200));
        _comparisons.SelectionChanged += (_, _) => _vm.SelectedComparison = _comparisons.SelectedItem as SetComparisonViewModel;
        Follow(_comparisons, _vm.Comparisons);
    }

    private static GridColumn Edit<T>(string header, Expression<Func<T, string>> property, int width) => new()
    {
        HeaderText = header, Editable = true, Width = width,
        DataCell = new TextBoxCell { Binding = Binding.Property(property) },
    };

    private void BuildResultsGrid()
    {
        GridColumn Col(string header, Func<StudyRow, string> get, int width = 90, string? tip = null) => new()
        {
            HeaderText = header, HeaderToolTip = tip, Width = width,
            DataCell = new TextBoxCell { Binding = Binding.Delegate(get) },
        };
        _rows.Columns.Add(Col("Comparison", r => r.Comparison, 160));
        _rows.Columns.Add(Col("Patient", r => r.Facts.Patient, 100));
        _rows.Columns.Add(Col("Source plan", r => r.Facts.SourcePlan, 120));
        _rows.Columns.Add(Col("Target plan", r => r.Facts.TargetPlan, 120));
        _rows.Columns.Add(Col("Field", r => r.Facts.Field, 100));
        _rows.Columns.Add(Col("Machine", r => r.Facts.Machine, 90));
        _rows.Columns.Add(Col("Energy", r => r.Facts.Energy, 70));
        _rows.Columns.Add(Col("Field size", r => r.Facts.FieldSize, 90));
        _rows.Columns.Add(Col("Criterion", r => r.Criterion.Label, 180, HelpText.Criterion));
        _rows.Columns.Add(Col("Ambiguous", r => r.WorstOf, 90, HelpText.Ambiguous));
        _rows.Columns.Add(Col("Max used", r => r.MaximumUsed, 140, HelpText.MaximumUsed));
        _rows.Columns.Add(Col("Edge check", r => r.EdgeCheck, 120, HelpText.EdgeCheck));
        _rows.Columns.Add(Col("Pass %", r => r.PassRate is { } p ? p.ToString("0.000") : "", 70, HelpText.PassRate));
        _rows.Columns.Add(Col("Compared", r => r.Compared.ToString("N0"), 90, HelpText.Compared));
        _rows.Columns.Add(Col("Plan check", r => r.PlanCheck, 200, HelpText.PlanCheck));
        _rows.Columns.Add(Col("Warnings", r => r.Warnings, 200, HelpText.Warnings));
        _rows.Columns.Add(Col("Failure", r => r.Failure ?? "", 200, HelpText.Failure));
    }

    /// <summary>A grid doesn't watch its items, so a row is redrawn when its view model changes (Browse, the AAA / AXB rule, opening a study).</summary>
    private static void Follow<T>(GridView grid, ObservableCollection<T> items) where T : INotifyPropertyChanged
    {
        void Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not T item) return;
            Application.Instance.AsyncInvoke(() => { int i = items.IndexOf(item); if (i >= 0) grid.ReloadData(i); });
        }
        foreach (var i in items) i.PropertyChanged += Changed;
        items.CollectionChanged += (_, e) =>
        {
            foreach (var old in e.OldItems?.OfType<T>() ?? Enumerable.Empty<T>()) old.PropertyChanged -= Changed;
            foreach (var added in e.NewItems?.OfType<T>() ?? Enumerable.Empty<T>()) added.PropertyChanged += Changed;
        };
    }

    /// <summary>A dropdown kept in step with one view-model property, both ways; a list that changes is followed.</summary>
    private void Choice<T>(DropDown list, IEnumerable<T> items, Func<T?> get, Action<T?> set, string property)
    {
        bool syncing = false;
        list.DataStore = items as IEnumerable<object> ?? items.Cast<object>().ToArray();
        list.SelectedValue = get();
        list.SelectedValueChanged += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            try { set(list.SelectedValue is T v ? v : default); }
            finally { syncing = false; }
        };
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != property || syncing) return;
            syncing = true;
            try { list.SelectedValue = get(); }
            finally { syncing = false; }
        };
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(StudyViewModel.IsRunning): _running.Visible = _vm.IsRunning; break;
            case nameof(StudyViewModel.IsLoadingCurves): _loadingCurves.Visible = _vm.IsLoadingCurves; break;
            case nameof(StudyViewModel.SelectedComparison): _acrossAlgorithms.Enabled = _vm.SelectedComparison is not null; break;
            case nameof(StudyViewModel.Curves): DrawStudy(_vm.Curves); break;
            case nameof(StudyViewModel.Index): BuildIndexColumns(_vm.Index); break;
            case nameof(StudyViewModel.IndexRows): _index.DataStore = _vm.IndexRows; break;
            case nameof(StudyViewModel.Log): _log.ScrollToEnd(); break;
        }
    }

    /// <summary>An index table's columns depend on the data (one per energy, say), so they are rebuilt when the table changes.</summary>
    private void BuildIndexColumns(IndexTable? table)
    {
        _indexTitle.Text = table?.Title ?? "Run the study to see index tables.";
        _index.DataStore = null;
        _index.Columns.Clear();
        if (table is null) return;
        _index.Columns.Add(new GridColumn
        {
            HeaderText = IndexTable.Say(table.RowAxis) + " \\ " + IndexTable.Say(table.ColumnAxis), Width = 160,
            DataCell = new TextBoxCell { Binding = Binding.Delegate((IndexRowView r) => r.Label) },
        });
        for (int i = 0; i < table.ColumnLabels.Count; i++)
        {
            int c = i;
            _index.Columns.Add(new GridColumn
            {
                HeaderText = table.ColumnLabels[c], Width = 110,
                DataCell = new TextBoxCell { Binding = Binding.Delegate((IndexRowView r) => c < r.Cells.Count ? r.Cells[c] : "") },
            });
        }
        _index.DataStore = _vm.IndexRows;
    }

    /// <summary>
    /// Every set's curve for one field: one colour per set; with several depths, one line style per depth. The lower plot
    /// is each set minus the reference, in percent of the reference maximum. The Avalonia window's drawing, unchanged.
    /// </summary>
    private void DrawStudy(MultiCurveSet? set)
    {
        var plot = _curvePlot.Plot;
        var diff = _differencePlot.Plot;
        plot.Clear();
        diff.Clear();
        if (set is null || set.Curves.Count == 0)
        {
            plot.Title("No curve: run the study and choose a field");
            plot.Legend.IsVisible = false;
            diff.Legend.IsVisible = false;
        }
        else
        {
            var palette = new ScottPlot.Palettes.Category10();
            var patterns = new[] { ScottPlot.LinePattern.Solid, ScottPlot.LinePattern.Dashed, ScottPlot.LinePattern.Dotted, ScottPlot.LinePattern.DenselyDashed };
            for (int c = 0; c < set.Curves.Count; c++)
            {
                var curve = set.Curves[c];
                string tag = set.Curves.Count > 1 ? curve.Tag + ": " : "";
                for (int s = 0; s < curve.Series.Count; s++)
                {
                    var (xs, ys) = Defined(curve.Positions, curve.Series[s].Values);
                    if (xs.Length == 0) continue;
                    var line = plot.Add.Scatter(xs, ys);
                    line.LegendText = tag + curve.Series[s].Name;
                    line.LineWidth = 2;
                    line.MarkerSize = 0;
                    line.Color = palette.GetColor(s);
                    line.LinePattern = patterns[c % patterns.Length];
                }
                for (int d = 0; d < curve.Differences.Count; d++)
                {
                    var (xs, ys) = Defined(curve.Positions, curve.Differences[d].Values);
                    if (xs.Length == 0) continue;
                    var line = diff.Add.Scatter(xs, ys);
                    line.LegendText = tag + curve.Differences[d].Name;
                    line.LineWidth = 1.5f;
                    line.MarkerSize = 0;
                    line.Color = palette.GetColor(d + 1);
                    line.LinePattern = patterns[c % patterns.Length];
                }
            }
            var first = set.Curves[0];
            plot.Title(set.Curves.Count > 1 ? first.Title.Split(" at ")[0] + " at each depth" : first.Title);
            plot.XLabel(first.AxisLabel);
            plot.YLabel(first.ValueLabel);
            plot.Legend.IsVisible = true;
            plot.Legend.Alignment = ScottPlot.Alignment.UpperRight;
            plot.Axes.AutoScale();
            diff.Title("Difference from the reference (% of the reference maximum)");
            diff.XLabel(first.AxisLabel);
            diff.YLabel("%");
            diff.Legend.IsVisible = first.Differences.Count > 0;
            diff.Legend.Alignment = ScottPlot.Alignment.UpperRight;
            diff.Axes.AutoScale();
        }
        _curvePlot.Refresh();
        _differencePlot.Refresh();

        static (double[] Xs, double[] Ys) Defined(double[] xs, double[] ys)
        {
            var keep = Enumerable.Range(0, xs.Length).Where(i => !double.IsNaN(ys[i])).ToArray();
            return (keep.Select(i => xs[i]).ToArray(), keep.Select(i => ys[i]).ToArray());
        }
    }
}
