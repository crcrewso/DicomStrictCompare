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
        Assert.IsTrue(vm.Status.Contains("No source dose matches 6 MV 30 x 30 cm"), vm.Status);
        Assert.IsTrue(vm.ScanSummary.StartsWith("11 photon open-field scan(s) in 1 file(s)"), vm.ScanSummary);
    }

    [TestMethod]
    public void AScanTakenFromTheDoseItselfOverlaysItExactly()
    {
        string folder = Resources();
        var contents = RTFolderReader.Read(folder);
        var dose = contents.Doses.First(d => d.Beam?.BeamName == "X-100A-10.0-1");
        var loaded = BeamLines.LoadDose(dose, ComparisonRunner.LoadWithEvilDicom);
        Assert.IsTrue(loaded.IsocentreFromPlan);

        // A pretend crossplane scan at 100 mm depth, whose points are the dose's own values there.
        var xs = Enumerable.Range(-60, 121).Select(i => (double)i).ToArray();
        var line = DSClibrary.Resampling.GridLines.Along(loaded.Grid, DSClibrary.Resampling.GridAxis.X, loaded.IsoY + 100, loaded.IsoZ, xs.Select(x => loaded.IsoX + x).ToArray());
        var beam = new BeamKey(Modality.Photon, 10, false, Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000));
        var profile = new Profile(beam, new ScanKey(CurveType.Crossplane, Distance.FromMillimetres(100)),
            xs.Select((x, i) => new ProfilePoint(Distance.FromMillimetres(x), line[i] * 1234.5, null)));   // arbitrary units
        var scan = new ScanDescriptor(profile, "pretend.mcc", 1, "10 MV", "10 x 10 cm", false, 10, false, 100, 100, CurveKind.Crossplane, 100);

        // The matcher finds the 10 MV 10 x 10 beams, open fields first.
        var candidates = ScanCatalog.MatchingDoses(scan, contents.Doses);
        Assert.IsTrue(candidates.Count >= 1, "a 10 MV 10 x 10 dose exists in resources/");
        Assert.IsTrue(candidates.All(c => c.Dose.Beam!.NominalBeamEnergy == 10));
        Assert.IsTrue(candidates.Any(c => ReferenceEquals(c.Dose, dose)));
        Assert.IsFalse(candidates.Any(c => c.Dose.Beam!.IsFluenceModeFff == true), "FFF beams are a different filter");

        var curve = BeamLines.ScanOverDose(scan, loaded);
        var (positions, calculated, measured) = curve.Overlap();
        Assert.AreEqual(121, positions.Length);
        for (int i = 0; i < positions.Length; i++)
            Assert.AreEqual(calculated[i], measured[i], 1e-9, $"at {positions[i]} mm");
        Assert.AreEqual(100.0, measured[Array.IndexOf(positions, 0.0)], 1e-9, "normalised to the central axis");
        Assert.IsTrue(curve.ValueLabel.Contains("central axis"));
        Assert.IsTrue(measured.Max() < 110 && measured.Min() >= 0);
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
