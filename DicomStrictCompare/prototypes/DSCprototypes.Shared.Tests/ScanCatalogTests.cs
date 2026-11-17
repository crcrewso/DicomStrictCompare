using DSClibrary.Matching;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using DSClibrary.Units;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>
/// Scans against doses, with the repository's own files: the committed PTW scan (6 MV, 30 x 30 cm,
/// eleven scans) is catalogued, finds no 10 MV dose to match and says so, and a scan fabricated
/// from a dose's own line overlays that dose exactly once both are normalised.
/// </summary>
[TestClass]
public class ScanCatalogTests
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

    [TestMethod]
    public void TheCommittedScanIsCataloguedByEnergyFieldSizeCurveAndDepth()
    {
        var result = ScanCatalog.Load(Path.Combine(Resources(), "ptw scans"));
        Assert.AreEqual(1, result.Files);
        Assert.AreEqual(0, result.Errors.Count, string.Join("; ", result.Errors));
        Assert.AreEqual(11, result.Scans.Count, "eleven scans in the committed file");
        Assert.IsTrue(result.Scans.All(s => s.Energy == "6 MV"), string.Join(", ", result.Scans.Select(s => s.Energy).Distinct()));
        Assert.IsTrue(result.Scans.All(s => s.FieldSize == "30 x 30 cm"), string.Join(", ", result.Scans.Select(s => s.FieldSize).Distinct()));
        Assert.AreEqual(1, result.Scans.Count(s => s.Kind == CurveKind.DepthDose));
        Assert.IsTrue(result.Scans.Where(s => s.Kind != CurveKind.DepthDose).All(s => s.DepthMm is > 0));
        // Ordered: depth dose first, then profiles by depth.
        Assert.AreEqual(CurveKind.DepthDose, result.Scans[0].Kind);
        var depths = result.Scans.Where(s => s.Kind == CurveKind.Crossplane).Select(s => s.DepthMm!.Value).ToArray();
        CollectionAssert.AreEqual(depths.OrderBy(d => d).ToArray(), depths);
        Assert.AreEqual("Depth dose", result.Scans[0].CurveLabel);
        Assert.IsTrue(result.Scans[1].CurveLabel.EndsWith(" mm"), result.Scans[1].CurveLabel);
    }

    [TestMethod]
    public void ASixMvScanFindsNoTenMvDoseAndTheViewModelSaysSo()
    {
        string folder = Resources();
        var doses = RTFolderReader.Read(folder).Doses;
        var scan = ScanCatalog.Load(Path.Combine(folder, "ptw scans")).Scans[0];

        Assert.AreEqual(0, ScanCatalog.MatchingDoses(scan, doses).Count, "the reference doses are 10 MV, 10 x 10");

        var vm = new GraphViewModel(ComparisonRunner.LoadWithEvilDicom);
        vm.SetSourceDoses(folder, doses);
        vm.Mode = GraphMode.DoseVersusScans;
        vm.ScanFolder = Path.Combine(folder, "ptw scans");
        vm.LoadScansCommand.Execute(null);
        for (int i = 0; i < 200 && vm.IsLoading; i++) Thread.Sleep(50);

        Assert.IsTrue(vm.HasScans);
        Assert.AreEqual(11, vm.Scans.Count);
        Assert.IsNotNull(vm.SelectedScan);
        Assert.AreEqual(0, vm.DoseCandidates.Count);
        Assert.IsNull(vm.Curve);
        Assert.IsNull(vm.ScanResult);
        Assert.IsFalse(vm.CanSavePlot);
        Assert.IsTrue(vm.Status.Contains("No source dose matches 6 MV 30 x 30 cm: "), vm.Status);
        Assert.IsTrue(vm.Status.Contains("energy"), "the reason names the first difference: " + vm.Status);
        Assert.IsTrue(vm.ScanSummary.StartsWith("11 photon scan(s) in 1 file(s)"), vm.ScanSummary);
    }

    /// <summary>
    /// A pretend crossplane scan at 100 mm depth whose points are the dose's own values along that line, in arbitrary
    /// units: the library pairs it with that dose, the comparison passes every point with the curves equal, and the
    /// figure saves as PNG and SVG.
    /// </summary>
    [TestMethod]
    public void AScanTakenFromTheDoseItselfPassesEverywhereAndItsPlotSaves()
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
        var key = new ScanKey(CurveType.Crossplane, Distance.FromMillimetres(100));
        var positions = Enumerable.Range(-60, 121).Select(i => new ProfilePoint(Distance.FromMillimetres(i), 1, null));
        var line = DoseLineSampler.Sample(grid, frame!, new Profile(beam, key, positions), dose.Dose.SourcePath).Calculated;
        var profile = new Profile(beam, key, line.Points.Select(p => new ProfilePoint(p.Position, p.Value * 12.345, null)));   // arbitrary units
        var scan = new ScanDescriptor(profile, "pretend.mcc", 1, "10 MV", "10 x 10 cm", false, 10, false, plan.CrossplaneMm, plan.InplaneMm, CurveKind.Crossplane, 100);

        var candidates = ScanCatalog.MatchingDoses(scan, contents.Doses);
        Assert.IsTrue(candidates.Any(c => ReferenceEquals(c.Dose, dose)), "the scan pairs with the dose it came from");
        Assert.IsTrue(candidates.All(c => c.Dose.Beam!.NominalBeamEnergy == 10 && c.Dose.Beam.IsFluenceModeFff != true));

        var outcome = ScanComparison.Run(scan, candidates.First(c => ReferenceEquals(c.Dose, dose)), grid, new(1, 1));
        Assert.IsNotNull(outcome.Result, outcome.Status);
        var r = outcome.Result!;
        Assert.AreEqual(100.0, r.PassRate, 1e-9, outcome.Status);
        Assert.AreEqual(NormalizationType.CentralAxis, r.Normalization);
        for (int i = 0; i < r.Measured.Points.Count; i++)
            Assert.AreEqual(r.Calculated.Points[i].Value, r.Measured.Points[i].Value, 1e-9);
        Assert.IsTrue(outcome.Status.StartsWith("Gamma 1%/1 mm, measured reference: 100.00% of "), outcome.Status);
        Assert.IsTrue(outcome.Labels!.Title.EndsWith(": crossplane at 100 mm"), outcome.Labels.Title);

        string dir = Path.Combine(Path.GetTempPath(), "dsc-plot-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string format in ScanComparison.PlotFormats)
            {
                string path = Path.Combine(dir, ScanComparison.FileName(scan, candidates[0], format));
                Directory.CreateDirectory(dir);
                DSCplots.ProfilePlot.Save(r, outcome.Labels, path, 800, 600);
                Assert.IsTrue(new FileInfo(path).Length > 1000, $"{format}: {path}");
            }
            Assert.IsTrue(File.ReadAllText(Directory.GetFiles(dir, "*.svg").Single()).Contains("<svg"));
            Assert.ThrowsExactly<ArgumentException>(() => DSCplots.ProfilePlot.Save(r, outcome.Labels, Path.Combine(dir, "x.jpg")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>The strict test's figure draws the dose difference, and a criterion the library refuses comes back as a status.</summary>
    [TestMethod]
    public void TheStrictTestAndAnInvalidCriterionAreReported()
    {
        string folder = Resources();
        var contents = RTFolderReader.Read(folder);
        var dose = contents.Doses.First(d => d.Beam?.BeamName == "X-100A-10.0-1");
        var grid = ComparisonRunner.LoadWithEvilDicom(dose.Dose.SourcePath);
        var plan = ScanDoseMatching.FromPlan(dose.Beam!).Setup!;
        var beam = new BeamKey(Modality.Photon, 10, false, Distance.FromMillimetres(plan.CrossplaneMm), Distance.FromMillimetres(plan.InplaneMm),
            plan.SsdMm is { } ssd ? Distance.FromMillimetres(ssd) : null);
        var (frame, _) = PhantomFrame.For(dose.Beam, dose.Plan?.SetupFor(dose.Beam!), grid, null);
        var key = new ScanKey(CurveType.Pdd, null);
        var depths = Enumerable.Range(0, 150).Select(i => new ProfilePoint(Distance.FromMillimetres(i), 1, null));
        var line = DoseLineSampler.Sample(grid, frame!, new Profile(beam, key, depths), dose.Dose.SourcePath).Calculated;
        // 2 % hotter beyond 50 mm: the strict 1 % test fails there and passes above.
        var profile = new Profile(beam, key, line.Points.Select(p => new ProfilePoint(p.Position, p.Value * (p.Position.Millimetres > 50 ? 1.02 : 1), null)));
        var scan = new ScanDescriptor(profile, "pretend.mcc", 2, "10 MV", "10 x 10 cm", false, 10, false, plan.CrossplaneMm, plan.InplaneMm, CurveKind.DepthDose, null);
        var candidate = new DoseCandidate(dose, "");

        var strict = ScanComparison.Run(scan, candidate, grid, new(1, 0, ProfileTest.Strict));
        Assert.IsNotNull(strict.Result, strict.Status);
        Assert.IsTrue(strict.Result!.PassRate is > 0 and < 100, strict.Status);
        Assert.IsTrue(strict.Status.StartsWith("Dose difference 1%, measured reference: "), strict.Status);
        var multiplot = new ScottPlot.Multiplot();
        DSCplots.ProfilePlot.Fill(multiplot, strict.Result, strict.Labels!);
        Assert.AreEqual(2, ScottPlot.IMultiplotExtensions.Count(multiplot), "curves above, points below");

        var invalid = ScanComparison.Run(scan, candidate, grid, new(1, 0, ProfileTest.Gamma));
        Assert.IsNull(invalid.Result);
        Assert.IsTrue(invalid.Status.StartsWith("Invalid criterion: "), invalid.Status);
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
