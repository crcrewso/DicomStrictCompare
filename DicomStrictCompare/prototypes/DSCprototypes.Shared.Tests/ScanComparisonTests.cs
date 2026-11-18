using DSClibrary;
using DSClibrary.Matching;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using DSClibrary.Units;
using DSCplots;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>
/// The Doses vs scans tab (2026-11-17), with the repository's own files: the committed PTW scan (6 MV, 30 x 30 cm,
/// eleven scans) pairs with none of the 10 MV doses and the tab says why; and a batch built from scans taken from a
/// dose's own lines is listed, filtered, drawn and exported the way DSCprofiles writes it.
/// </summary>
[TestClass]
public class ScanComparisonTests
{
    private static string Resources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "resources");
            if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "RP.*.dcm").Any())
                return candidate;
            dir = dir.Parent;
        }
        Assert.Inconclusive("resources/ not found above " + AppContext.BaseDirectory);
        return "";
    }

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "dsc-scans-tab-test-" + Guid.NewGuid().ToString("N"));

    [TestMethod]
    public async Task ASixMvScanFolderPairsWithNoTenMvDoseAndTheTabSaysWhy()
    {
        string folder = Resources();
        var vm = new ScanComparisonViewModel((_, _) => Task.FromResult<string?>(null))
        {
            DosesFolder = folder,
            ScansFolder = Path.Combine(folder, "ptw scans"),
        };
        await vm.RunCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.HasResult, vm.Status);
        Assert.AreEqual(0, vm.Rows.Count, "the reference doses are 10 MV");
        Assert.AreEqual(11, vm.Unpaired.Count, string.Join(Environment.NewLine, vm.Unpaired));
        Assert.IsTrue(vm.Unpaired.All(u => u.StartsWith("PTW_6MV_30x30.mcc scan ", StringComparison.Ordinal) || u.Contains(".mcc scan ")), vm.Unpaired[0]);
        Assert.IsTrue(vm.Unpaired.All(u => u.Contains("energy")), "each reason names the first difference: " + vm.Unpaired[0]);
        Assert.IsTrue(vm.Status.StartsWith("Gamma 1%/1 mm, measured reference: 0 pair(s) of 11 scan(s) in 1 file(s)"), vm.Status);
        Assert.IsNull(vm.SelectedResult);
        Assert.IsFalse(vm.CanSavePlot);
    }

    [TestMethod]
    public async Task TheTabRefusesWhatCantRun()
    {
        string folder = Resources();
        var vm = new ScanComparisonViewModel((_, _) => Task.FromResult<string?>(null));
        await vm.RunCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.Status.StartsWith("Choose a doses folder"), vm.Status);

        vm.DosesFolder = folder;
        vm.ScansFolder = folder;
        vm.PlanFilter = "(";
        await vm.RunCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.Status.StartsWith("Plan label filter isn't a regular expression"), vm.Status);

        vm.PlanFilter = "";
        vm.DistanceMm = 0;   // gamma needs a distance
        await vm.RunCommand.ExecuteAsync(null);
        Assert.IsTrue(vm.Status.StartsWith("Invalid criterion: "), vm.Status);
        Assert.IsFalse(vm.HasResult);
    }

    /// <summary>The dose-versus-dose source folder fills the doses box until one is chosen there.</summary>
    [TestMethod]
    public void TheSourceFolderIsOfferedUntilADosesFolderIsChosen()
    {
        var main = new MainViewModel((_, _) => Task.FromResult<string?>(null));
        main.SourceFolder = @"C:\a";
        Assert.AreEqual(@"C:\a", main.Scans.DosesFolder);
        main.SourceFolder = @"C:\ab";
        Assert.AreEqual(@"C:\ab", main.Scans.DosesFolder, "still following: typing into Source goes letter by letter");
        main.Scans.DosesFolder = @"D:\doses";
        main.SourceFolder = @"C:\abc";
        Assert.AreEqual(@"D:\doses", main.Scans.DosesFolder, "chosen here, so kept");
        main.Scans.DosesFolder = "";
        main.SourceFolder = @"C:\x";
        Assert.AreEqual(@"C:\x", main.Scans.DosesFolder, "emptied, so offered again");

        var options = LaunchOptions.Parse(new[] { "--scans", @"S:\scans", "--doses", @"D:\doses" });
        Assert.AreEqual(@"S:\scans", options.ScanFolder);
        Assert.AreEqual(@"D:\doses", options.DosesFolder);
    }

    /// <summary>
    /// Three rows: a crossplane scan taken from the dose itself (passes everywhere), a depth dose 2 % hotter beyond 50 mm
    /// under the strict 1 % test (fails beyond), and a pair the batch couldn't compare. The tab lists, filters and draws
    /// them, and the export writes DSCprofiles' files with the failing pair's plot named in its row.
    /// </summary>
    [TestMethod]
    public async Task ABatchIsListedFilteredDrawnAndExportedAsDSCprofilesWritesIt()
    {
        string folder = Resources();
        var contents = RTFolderReader.Read(folder);
        var dose = contents.Doses.First(d => d.Beam?.BeamName == "X-100A-10.0-1");
        var grid = ComparisonRunner.LoadWithEvilDicom(dose.Dose.SourcePath);
        var plan = ScanDoseMatching.FromPlan(dose.Beam!).Setup!;
        var (frame, refusal) = PhantomFrame.For(dose.Beam, dose.Plan?.SetupFor(dose.Beam!), grid, null);
        Assert.IsNotNull(frame, refusal);
        var beam = new BeamKey(Modality.Photon, 10, false, Distance.FromMillimetres(plan.CrossplaneMm), Distance.FromMillimetres(plan.InplaneMm),
            plan.SsdMm is { } ssd ? Distance.FromMillimetres(ssd) : null);

        Profile FromDose(ScanKey key, IEnumerable<int> mm, Func<double, double> scale)
        {
            var positions = mm.Select(i => new ProfilePoint(Distance.FromMillimetres(i), 1, null));
            var line = DoseLineSampler.Sample(grid, frame!, new Profile(beam, key, positions), dose.Dose.SourcePath).Calculated;
            return new Profile(beam, key, line.Points.Select(p => new ProfilePoint(p.Position, p.Value * 12.345 * scale(p.Position.Millimetres), null)));
        }
        var cross = FromDose(new ScanKey(CurveType.Crossplane, Distance.FromMillimetres(100)), Enumerable.Range(-60, 121), _ => 1);
        var pdd = FromDose(new ScanKey(CurveType.Pdd, null), Enumerable.Range(0, 150), d => d > 50 ? 1.02 : 1);

        string scansDir = TempDir(), outDir = TempDir();
        string file = Path.Combine(scansDir, "sub", "pretend.mcc");
        var crossRef = new ScanRef(file, "sub", 1, cross);
        var pddRef = new ScanRef(file, "sub", 2, pdd);
        var gamma = new ProfileComparisonOptions(1, 1);
        var strict = new ProfileComparisonOptions(1, 0, ProfileTest.Strict);
        var crossResult = ProfileComparison.Compare(cross, grid, frame!, gamma, dose.Dose.SourcePath);
        var pddResult = ProfileComparison.Compare(pdd, grid, frame!, strict, dose.Dose.SourcePath);
        Assert.AreEqual(100.0, crossResult.PassRate, 1e-9);
        Assert.IsTrue(pddResult.PassRate is > 0 and < 100, ProfilePlot.Verdict(pddResult));
        var notes = Array.Empty<string>();
        var batch = new ProfileBatchResult
        {
            Rows = new[]
            {
                new ProfilePairRow(crossRef, dose, null, plan, notes, frame, crossResult, null)
                {
                    HeaderCheck = new ScanHeaderCheckResult(100, 200, 0, new[] { "the header says a 100 mm field crossplane, but the scan's field is 200 mm at the SSD" })
                },
                new ProfilePairRow(pddRef, dose, null, plan, notes, frame, pddResult, null),
                new ProfilePairRow(pddRef with { ScanNumber = 3 }, dose, null, plan, notes, null, null, "the dose couldn't be read: test"),
            },
            Unmatched = new[] { new UnmatchedScan(crossRef with { ScanNumber = 4 }, "energy: scan 6 MV, dose 10 MV") },
            UnreadableScans = Array.Empty<(string, string)>(),
            UnreadableDoses = Array.Empty<(string, string)>(),
            ScanFiles = 1, Scans = 4, Doses = 1, Threads = 1, Elapsed = TimeSpan.FromSeconds(1),
        };

        string? pick = outDir;
        var vm = new ScanComparisonViewModel((_, _) => Task.FromResult(pick));
        vm.SetResult(batch, folder, scansDir, null, null, gamma);
        try
        {
            Assert.AreEqual(3, vm.Rows.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, vm.Rows.Select(r => r.Row).ToArray());
            Assert.AreEqual(Path.Combine("sub", "pretend.mcc") + " scan 1", vm.Rows[0].Scan);
            CollectionAssert.AreEqual(new[] { ScanOutcome.Passed, ScanOutcome.Failed, ScanOutcome.NotCompared }, vm.Rows.Select(r => r.Outcome).ToArray());
            CollectionAssert.AreEqual(new[] { "Crossplane", "Depth dose", "Depth dose" }, vm.Rows.Select(r => r.Curve).ToArray());
            Assert.AreEqual("100", vm.Rows[0].Depth);
            Assert.AreEqual("100.00", vm.Rows[0].PassRate);
            Assert.IsTrue(vm.Rows[2].OutcomeText.StartsWith("not compared: the dose couldn't be read"), vm.Rows[2].OutcomeText);
            CollectionAssert.AreEqual(new[] { "10 MV" }, vm.EnergyFilter.Items.Select(i => i.Label).ToArray());
            Assert.AreEqual("All energies", vm.EnergyFilter.Summary);
            Assert.AreEqual(1, vm.Unpaired.Count);
            Assert.IsTrue(vm.Status.Contains("3 pair(s) of 4 scan(s) in 1 file(s) with 1 dose(s); 1 pass every point, 1 fail somewhere, 1 not compared; 1 scan(s) unpaired"), vm.Status);

            // The first row is chosen and drawn.
            Assert.AreSame(vm.Rows[0], vm.SelectedRow);
            Assert.IsNotNull(vm.SelectedResult);
            Assert.IsTrue(vm.SelectedLabels!.Title.EndsWith(": crossplane at 100 mm"), vm.SelectedLabels.Title);
            Assert.IsTrue(vm.CanSavePlot);
            Assert.IsTrue(vm.Details.StartsWith("Row 1: "), vm.Details);
            var multiplot = new ScottPlot.Multiplot();
            ProfilePlot.Fill(multiplot, vm.Rows[1].Pair.Result!, ScanRows.Labels(vm.Rows[1], scansDir));
            Assert.AreEqual(2, ScottPlot.IMultiplotExtensions.Count(multiplot), "curves above, points below");

            vm.SelectedRow = vm.Rows[2];
            Assert.IsNull(vm.SelectedResult);
            Assert.IsFalse(vm.CanSavePlot);
            Assert.IsTrue(vm.Details.Contains("not compared: the dose couldn't be read"), vm.Details);

            vm.Show = ScanRows.FailingPairs;
            Assert.AreEqual(2, vm.Rows.Single().Row);
            Assert.AreEqual("1 of 3 pair(s) listed", vm.Listed);
            vm.Show = ScanRows.NotComparedPairs;
            Assert.AreEqual(3, vm.Rows.Single().Row);
            vm.Show = ScanRows.AllPairs;
            vm.SelectedCurve = "Crossplane";
            Assert.AreEqual(1, vm.Rows.Single().Row);
            vm.SelectedCurve = ScanRows.AnyCurve;

            // The header check: a column, a Show choice, the details line and the run's summary.
            Assert.AreEqual("check", vm.Rows[0].HeaderFlag);
            Assert.AreEqual("", vm.Rows[1].HeaderFlag);
            vm.Show = ScanRows.HeaderDisagrees;
            Assert.AreEqual(1, vm.Rows.Single().Row);
            Assert.IsTrue(vm.Details.Contains("CHECK THE SCAN'S HEADER: the header says a 100 mm field"), vm.Details);
            vm.Show = ScanRows.AllPairs;

            // Find: every word, anywhere in the row, case and spaces ignored, numbers whole.
            vm.Find = "PRETEND crossplane";
            Assert.AreEqual(1, vm.Rows.Single().Row);
            vm.Find = "10mv \"depth dose\"";
            CollectionAssert.AreEqual(new[] { 2, 3 }, vm.Rows.Select(r => r.Row).ToArray());
            vm.Find = "10 MV";
            Assert.AreEqual(3, vm.Rows.Count, "two words, 10 and mv, each found whole");
            vm.Find = "nothing-like-this";
            Assert.AreEqual(0, vm.Rows.Count);
            Assert.AreEqual("0 of 3 pair(s) listed", vm.Listed);
            vm.Find = "";

            // Energy and field size: dropdowns of check boxes with All and None; fields follow the ticked energies.
            vm.EnergyFilter.NoneCommand.Execute(null);
            Assert.AreEqual(0, vm.Rows.Count);
            Assert.AreEqual("None", vm.EnergyFilter.Summary);
            Assert.AreEqual(0, vm.FieldFilter.Items.Count, "no energy ticked, no field sizes to offer");
            vm.EnergyFilter.Items[0].IsChecked = true;
            Assert.AreEqual(3, vm.Rows.Count);
            Assert.IsTrue(vm.FieldFilter.Items.Count > 0 && vm.FieldFilter.Items.All(i => i.IsChecked), "fields offered again, all ticked");
            vm.FieldFilter.NoneCommand.Execute(null);
            Assert.AreEqual(0, vm.Rows.Count);
            vm.FieldFilter.AllCommand.Execute(null);
            Assert.AreEqual(3, vm.Rows.Count);

            var r0 = vm.Rows[0];
            var big = new ScanPairRow
            {
                Row = 9, Pair = r0.Pair, Scan = @"10FFF\X10 FFF OPEN 16X16 XY WAT.mcc scan 1", Energy = "10 MV FFF", Field = "16 x 16 cm",
                Curve = r0.Curve, Depth = "150", DoseFile = "", Plan = "", Beam = "", PassRate = "", Points = "", MaxGamma = "",
                Outcome = r0.Outcome, OutcomeText = r0.OutcomeText,
            };
            Assert.IsFalse(ScanRows.Matches(big, new[] { "6x6" }), "6x6 isn't found inside 16x16");
            Assert.IsFalse(ScanRows.Matches(big, new[] { "50mm" }), "nor 50 mm inside 150 mm");
            Assert.IsTrue(ScanRows.Matches(big, new[] { "10fff", "16x16" }));
            Assert.AreEqual(3, vm.Rows.Count);

            vm.DosePercent = 2;
            Assert.IsTrue(vm.Status.StartsWith("The rows are at Gamma 1%/1 mm, measured reference; run again to compare at Gamma 2%/1 mm"), vm.Status);

            // Export: DSCprofiles' files, with the failing pair's plot named in its row.
            vm.ExportPlots = PlotSelection.Failing;
            await vm.ExportCommand.ExecuteAsync(null);
            Assert.IsTrue(vm.Status.StartsWith("Wrote profiles-results.csv, profiles-unpaired.csv, profiles-settings.txt and 1 plot(s) in profiles-plots"), vm.Status);
            var results = File.ReadAllLines(Path.Combine(outDir, "profiles-results.csv"));
            Assert.AreEqual(4, results.Length, "a header and three rows");
            string plotFile = Directory.GetFiles(Path.Combine(outDir, "profiles-plots")).Single();
            Assert.IsTrue(Path.GetFileName(plotFile).StartsWith("0002 pretend s2 "), plotFile);
            Assert.IsTrue(results[2].EndsWith("," + Path.Combine("profiles-plots", Path.GetFileName(plotFile))), results[2]);
            Assert.IsTrue(results[1].EndsWith(","), "a passing row has no plot: " + results[1]);
            Assert.AreEqual(2, File.ReadAllLines(Path.Combine(outDir, "profiles-unpaired.csv")).Length);
            Assert.IsTrue(File.ReadAllText(Path.Combine(outDir, "profiles-settings.txt")).StartsWith("DSC prototype window "));

            // Every listed pair's plot, as SVG; the row that wasn't compared has none.
            string listDir = Path.Combine(outDir, "listed");
            pick = listDir;
            vm.PlotFormat = "svg";
            await vm.SaveListedPlotsCommand.ExecuteAsync(null);
            Assert.AreEqual(2, Directory.GetFiles(listDir, "*.svg").Length, vm.Status);
            Assert.IsTrue(vm.Status.EndsWith("; 1 listed row(s) have no comparison to draw"), vm.Status);
            Assert.ThrowsExactly<ArgumentException>(() => ProfilePlot.Save(crossResult, ScanRows.Labels(vm.Rows[0], scansDir), Path.Combine(listDir, "x.jpg")));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }

    [TestMethod]
    public void TheFixedDepthsAreDmaxFiveTenAndTwentyCentimetres()
    {
        CollectionAssert.AreEqual(new[] { "Dmax", "5 cm", "10 cm", "20 cm" }, BeamLines.FixedDepths.Select(d => d.Label).ToArray());
        CollectionAssert.AreEqual(new double?[] { null, 50, 100, 200 }, BeamLines.FixedDepths.Select(d => d.Mm).ToArray());

        string folder = Resources();
        var match = DoseMatcher.Match(RTFolderReader.Read(folder).Doses, RTFolderReader.Read(folder).Doses, DoseMatchStrategy.PlanAndBeamUid);
        var pair = match.Pairs.First(p => p.Source.Beam?.BeamName == "X-100A-10.0-1");
        var loaded = BeamLines.Load(pair, ComparisonRunner.LoadWithEvilDicom);
        double dmax = BeamLines.DepthOfMaximum(loaded);
        Assert.IsTrue(dmax > 10 && dmax < 40, $"10 MV dmax on the grid is {dmax} mm");

        // "All depths": one crossplane profile per fixed depth, tagged, dmax first.
        var all = BeamLines.ProfilesAtAllDepths(loaded, CurveKind.Crossplane);
        Assert.AreEqual(4, all.Count);
        Assert.IsTrue(all[0].Tag.StartsWith("dmax "), all[0].Tag);
        CollectionAssert.AreEqual(new[] { "5 cm", "10 cm", "20 cm" }, all.Skip(1).Select(c => c.Tag).ToArray());
        Assert.IsTrue(all[2].Title.Contains("at 100 mm depth"), all[2].Title);
        Assert.AreEqual(1, BeamLines.ProfilesAtAllDepths(loaded, CurveKind.DepthDose).Count, "a depth dose has no depths to run through");

        var vm = new GraphViewModel(ComparisonRunner.LoadWithEvilDicom);
        Assert.AreEqual("Dmax", vm.Depth!.Label, "dmax is the default");
        Assert.AreEqual("All depths", vm.DepthChoices[^1].Label);
    }
}
