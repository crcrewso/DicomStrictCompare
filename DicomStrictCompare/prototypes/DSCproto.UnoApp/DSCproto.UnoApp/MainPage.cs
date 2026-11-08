using DSCprototypes.Shared;
using DSCprototypes.Shared.ViewModels;
using Microsoft.UI.Text;
using Windows.Storage.Pickers;

namespace DSCproto.UnoApp;

/// <summary>
/// The window's one page, in Uno's C# Markup: the WinUI object model written as a fluent C# tree,
/// with bindings as lambdas that the markup source generator turns into real XAML bindings. No XAML
/// file, no code-behind wiring; the page is a constructor.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        var options = LaunchOptions.FromCommandLine();
        var viewModel = new MainViewModel(PickFolderAsync);
        viewModel.Apply(options);
        Loaded += async (_, _) => await viewModel.AutoRunIfRequestedAsync(options);

        this.DataContext(viewModel, (page, vm) => page
            .Background(ThemeResource.Get<Brush>("ApplicationPageBackgroundThemeBrush"))
            .Content(new Grid()
                .Padding(12)
                .RowSpacing(12)
                .RowDefinitions("Auto,*,Auto")
                .Children(
                    Inputs(vm).Grid(row: 0),
                    WorkArea(vm).Grid(row: 1),
                    ActionBar(vm).Grid(row: 2))));
    }

    /// <summary>Two folders, two aliases, the matching strategy.</summary>
    private static Grid Inputs(MainViewModel vm) => new Grid()
        .ColumnSpacing(8).RowSpacing(6)
        .ColumnDefinitions("Auto,*,Auto,Auto,180")
        .RowDefinitions("Auto,Auto,Auto")
        .Children(
            new TextBlock().Text("Source (reference)").VerticalAlignment(VerticalAlignment.Center).Grid(row: 0, column: 0),
            new TextBox().PlaceholderText("Folder of RT Dose and RT Plan files")
                .Text(x => x.Binding(() => vm.SourceFolder).TwoWay()).Grid(row: 0, column: 1),
            new Button().Content("Browse").Command(() => vm.BrowseSourceCommand).Grid(row: 0, column: 2),
            new TextBlock().Text("Alias").VerticalAlignment(VerticalAlignment.Center).Grid(row: 0, column: 3),
            new TextBox().Text(x => x.Binding(() => vm.SourceAlias).TwoWay()).Grid(row: 0, column: 4),

            new TextBlock().Text("Target").VerticalAlignment(VerticalAlignment.Center).Grid(row: 1, column: 0),
            new TextBox().PlaceholderText("Folder of RT Dose and RT Plan files")
                .Text(x => x.Binding(() => vm.TargetFolder).TwoWay()).Grid(row: 1, column: 1),
            new Button().Content("Browse").Command(() => vm.BrowseTargetCommand).Grid(row: 1, column: 2),
            new TextBlock().Text("Alias").VerticalAlignment(VerticalAlignment.Center).Grid(row: 1, column: 3),
            new TextBox().Text(x => x.Binding(() => vm.TargetAlias).TwoWay()).Grid(row: 1, column: 4),

            new StackPanel().Orientation(Orientation.Horizontal).Spacing(16)
                .Grid(row: 2, column: 1, columnSpan: 4)
                .Children(
                    new TextBlock().Text("Match doses by").VerticalAlignment(VerticalAlignment.Center),
                    new RadioButton().Content("plan UID and beam number").GroupName("match")
                        .IsChecked(x => x.Binding(() => vm.MatchByUid).TwoWay()),
                    new RadioButton().Content("patient, plan label and beam name").GroupName("match")
                        .IsChecked(x => x.Binding(() => vm.MatchByUid).Convert(b => !b))));

    /// <summary>Criteria on the left, results in tabs on the right.</summary>
    private static Grid WorkArea(MainViewModel vm) => new Grid()
        .ColumnSpacing(12)
        .ColumnDefinitions("340,*")
        .Children(
            Criteria(vm).Grid(column: 0),
            new TabView()
                .IsAddTabButtonVisible(false)
                .CanReorderTabs(false)
                .Grid(column: 1)
                .TabItems(
                    Tab("Results", Results(vm)),
                    Tab("Unmatched", new ListView().ItemsSource(() => vm.Unmatched)),
                    Tab("Log", new TextBox().IsReadOnly(true).AcceptsReturn(true).TextWrapping(TextWrapping.Wrap)
                        .FontFamily(new FontFamily("Cascadia Mono,Consolas,monospace"))
                        .Text(() => vm.Log)),
                    Tab("Profiles", Placeholder(
                        "Placeholder: PTW mcc profiles against extracted dose lines, filtered by energy, field size and depth, rendered live from retained Profile objects (backlog D3, D15, D17).")),
                    Tab("Detector geometry", Placeholder(
                        "Placeholder: detector geometry correction for PTW scans. IDetectorCorrection exists in DSClibrary; no physical kernel is implemented yet (backlog D4), so there is nothing honest to switch on here.")),
                    Tab("Archive", Placeholder(
                        "Placeholder: PNG export of each comparison for the record (an archiving output, not a display mechanism - Cody, 2026-11-08). Rendered from the retained ComparisonOutcome, independent of this page."))));

    private static TabViewItem Tab(string header, UIElement content) =>
        new TabViewItem().Header(header).IsClosable(false).Content(content);

    private static TextBlock Placeholder(string text) =>
        new TextBlock().Text(text).TextWrapping(TextWrapping.Wrap).Margin(12);

    /// <summary>The editable criteria list: one row per Dta, edited in place.</summary>
    private static Grid Criteria(MainViewModel vm) => new Grid()
        .RowSpacing(6)
        .RowDefinitions("Auto,Auto,*,Auto")
        .Children(
            new TextBlock().Text("Criteria").FontWeight(FontWeights.SemiBold).Grid(row: 0),
            new Grid().ColumnDefinitions("*,*,*,Auto").ColumnSpacing(6).Grid(row: 1).Children(
                new TextBlock().Text("Tol %").Grid(column: 0),
                new TextBlock().Text("DTA mm").Grid(column: 1),
                new TextBlock().Text("Thr %").Grid(column: 2),
                new TextBlock().Text("Global").Grid(column: 3)),
            new ListView()
                .Grid(row: 2)
                .SelectionMode(ListViewSelectionMode.Single)
                .ItemsSource(() => vm.Criteria)
                .SelectedItem(x => x.Binding(() => vm.SelectedCriterion).TwoWay())
                .ItemTemplate<CriterionViewModel>(c => new Grid()
                    .ColumnDefinitions("*,*,*,Auto").ColumnSpacing(6)
                    .Children(
                        new NumberBox().SmallChange(0.5).Value(x => x.Binding(() => c.TolerancePercent).TwoWay()).Grid(column: 0),
                        new NumberBox().SmallChange(0.5).Value(x => x.Binding(() => c.DistanceMm).TwoWay()).Grid(column: 1),
                        new NumberBox().SmallChange(1).Value(x => x.Binding(() => c.ThresholdPercent).TwoWay()).Grid(column: 2),
                        new CheckBox().MinWidth(0).IsChecked(x => x.Binding(() => c.Global).TwoWay()).Grid(column: 3))),
            new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Grid(row: 3).Children(
                new Button().Content("Add").Command(() => vm.AddCriterionCommand),
                new Button().Content("Remove").Command(() => vm.RemoveCriterionCommand)));

    /// <summary>
    /// Results as a ListView with a column grid per row. WinUI has no DataGrid in the box; the
    /// Community Toolkit one is a later choice, not a prototype's.
    /// </summary>
    private static Grid Results(MainViewModel vm) => new Grid()
        .RowDefinitions("Auto,*")
        .Children(
            new Grid().ColumnDefinitions("2*,*,90,90,80,70,70,50,*").ColumnSpacing(6).Padding(12, 4, 12, 4).Grid(row: 0).Children(
                new TextBlock().Text("Key").FontWeight(FontWeights.SemiBold).Grid(column: 0),
                new TextBlock().Text("Criterion").FontWeight(FontWeights.SemiBold).Grid(column: 1),
                new TextBlock().Text("Compared").FontWeight(FontWeights.SemiBold).Grid(column: 2),
                new TextBlock().Text("Failed").FontWeight(FontWeights.SemiBold).Grid(column: 3),
                new TextBlock().Text("% failed").FontWeight(FontWeights.SemiBold).Grid(column: 4),
                new TextBlock().Text("Src MU").FontWeight(FontWeights.SemiBold).Grid(column: 5),
                new TextBlock().Text("Tgt MU").FontWeight(FontWeights.SemiBold).Grid(column: 6),
                new TextBlock().Text("s").FontWeight(FontWeights.SemiBold).Grid(column: 7),
                new TextBlock().Text("Failure").FontWeight(FontWeights.SemiBold).Grid(column: 8)),
            new ListView()
                .Grid(row: 1)
                .SelectionMode(ListViewSelectionMode.None)
                .ItemsSource(() => vm.Rows)
                .ItemTemplate<ResultRow>(r => new Grid()
                    .ColumnDefinitions("2*,*,90,90,80,70,70,50,*").ColumnSpacing(6)
                    .Children(
                        new TextBlock().Text(() => r.Key).TextTrimming(TextTrimming.CharacterEllipsis).Grid(column: 0),
                        new TextBlock().Text(() => r.Criterion.Label).Grid(column: 1),
                        new TextBlock().Text(x => x.Binding(() => r.Compared).Convert(v => v.ToString("N0"))).Grid(column: 2),
                        new TextBlock().Text(x => x.Binding(() => r.Failed).Convert(v => v.ToString("N0"))).Grid(column: 3),
                        new TextBlock().Text(x => x.Binding(() => r.PercentFailed).Convert(v => v is { } p ? p.ToString("0.000") : "")).Grid(column: 4),
                        new TextBlock().Text(x => x.Binding(() => r.SourceMonitorUnits).Convert(v => v?.ToString("0.#") ?? "")).Grid(column: 5),
                        new TextBlock().Text(x => x.Binding(() => r.TargetMonitorUnits).Convert(v => v?.ToString("0.#") ?? "")).Grid(column: 6),
                        new TextBlock().Text(x => x.Binding(() => r.ElapsedSeconds).Convert(v => v.ToString("0.0"))).Grid(column: 7),
                        new TextBlock().Text(() => r.Failure).TextTrimming(TextTrimming.CharacterEllipsis).Grid(column: 8))));

    /// <summary>Status on the left, progress and the three actions on the right.</summary>
    private static Grid ActionBar(MainViewModel vm) => new Grid()
        .ColumnSpacing(8)
        .ColumnDefinitions("*,Auto,Auto,Auto,Auto")
        .Children(
            new TextBlock().Text(() => vm.Status).VerticalAlignment(VerticalAlignment.Center)
                .TextTrimming(TextTrimming.CharacterEllipsis).Grid(column: 0),
            new ProgressBar().IsIndeterminate(true).Width(160)
                .Visibility(x => x.Binding(() => vm.IsRunning).Convert(b => b ? Visibility.Visible : Visibility.Collapsed))
                .Grid(column: 1),
            new Button().Content("Run comparison").Style(StaticResource.Get<Style>("AccentButtonStyle"))
                .Command(() => vm.RunCommand).Grid(column: 2),
            new Button().Content("Cancel").Command(() => vm.CancelCommand).Grid(column: 3),
            new Button().Content("Export spreadsheet (CSV)").Command(() => vm.ExportCommand).Grid(column: 4));

    /// <summary>The platform folder picker, through WinRT's picker API as Uno implements it on desktop.</summary>
    private static async Task<string?> PickFolderAsync(string title)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, CommitButtonText = title };
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
