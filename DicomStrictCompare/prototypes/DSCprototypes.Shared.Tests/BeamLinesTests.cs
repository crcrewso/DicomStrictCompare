using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>
/// The graph layer against the repository's resources: every reference beam gets an energy and a
/// field size from its plan, a self pair draws identical source and target curves, and the view
/// model's filters narrow the beam list the way the combo boxes will.
/// </summary>
[TestClass]
public class BeamLinesTests
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
        Assert.Inconclusive("resources/ with RP.*.dcm not found above " + AppContext.BaseDirectory);
        return "";
    }

    private static DoseMatchResult SelfMatch()
    {
        var folder = RTFolderReader.Read(Resources());
        return DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.PlanAndBeamUid);
    }

    [TestMethod]
    public void EveryReferenceBeamHasAnEnergyAndAFieldSizeFromItsPlan()
    {
        var beams = PairCatalog.Describe(SelfMatch().Pairs);
        Assert.AreEqual(26, beams.Count);
        Assert.IsTrue(beams.All(b => b.Energy != PairCatalog.Unknown), string.Join(", ", beams.Where(b => b.Energy == PairCatalog.Unknown).Select(b => b.Label)));
        Assert.IsTrue(beams.All(b => b.FieldSize != PairCatalog.Unknown), string.Join(", ", beams.Where(b => b.FieldSize == PairCatalog.Unknown).Select(b => b.Label)));

        var energies = PairCatalog.Energies(beams);
        var fields = PairCatalog.FieldSizes(beams);
        Assert.IsTrue(energies.Count >= 2, "the reference set has photons and electrons: " + string.Join(", ", energies));
        Assert.IsTrue(energies.Any(e => e.EndsWith("MV")) && energies.Any(e => e.EndsWith("MeV")), string.Join(", ", energies));
        Assert.IsTrue(fields.Contains("10 x 10 cm"), string.Join(", ", fields));
        // Photons sort before electrons, so the first label is an MV one.
        Assert.IsTrue(energies[0].EndsWith("MV"), energies[0]);

        // Electrons: the applicator, not the jaws. Measured 2026-11-09: every electron beam in
        // resources/ has applicator A10 with the jaws at +-70 mm, and the first version of this
        // label called them "14 x 14 cm" (Cody: "some confusion with the field size").
        var electrons = beams.Where(b => b.IsElectron).ToArray();
        Assert.IsTrue(electrons.Length > 0);
        Assert.IsTrue(electrons.All(b => b.FieldSize == "10 x 10 cm (A10)"), string.Join(", ", electrons.Select(b => b.FieldSize).Distinct()));
        Assert.IsTrue(electrons.All(b => b.Pair.Source.Beam!.FieldSizeAtIsocentre.CrossplaneMm == 140), "the jaws really are at 14 cm");
        Assert.IsFalse(fields.Contains("14 x 14 cm"), string.Join(", ", fields));
    }

    [TestMethod]
    public void ASelfPairDrawsIdenticalCurvesWithDepthsAscendingThroughTheGrid()
    {
        var pair = SelfMatch().Pairs.OrderBy(p => p.Source.Dose.SourcePath, StringComparer.Ordinal).First();
        var loaded = BeamLines.Load(pair, ComparisonRunner.LoadWithEvilDicom);

        Assert.IsTrue(loaded.IsocentreFromPlan, "the reference plans state an isocentre");
        Assert.AreEqual(0, loaded.IsoX, 0.5);
        Assert.AreEqual(-100.0, loaded.IsoY, 0.5, "measured: isocentre y is -100.009 in every reference plan");

        var depths = BeamLines.Depths(loaded);
        Assert.IsTrue(depths.Length > 50, $"{depths.Length} depths");
        Assert.IsTrue(depths.Zip(depths.Skip(1), (a, b) => b > a).All(x => x), "ascending");
        Assert.IsTrue(depths[0] >= -1 && depths[0] <= 2, $"the first depth is at the surface, not {depths[0]}");

        var pdd = BeamLines.DepthDose(loaded);
        CollectionAssert.AreEqual(pdd.Source, pdd.Target, "a file against itself");
        Assert.AreEqual(depths.Length, pdd.Positions.Length);
        Assert.IsFalse(pdd.Source.Any(double.IsNaN), "the axis runs inside the grid everywhere");
        int peak = Array.IndexOf(pdd.Source, pdd.Source.Max());
        Assert.IsTrue(pdd.Positions[peak] > 0 && pdd.Positions[peak] < 60, $"dmax at {pdd.Positions[peak]} mm");

        var cross = BeamLines.ProfileAt(loaded, CurveKind.Crossplane, 100);
        var (xs, src, tgt) = cross.Overlap();
        CollectionAssert.AreEqual(src, tgt);
        Assert.IsTrue(xs.Length > 50);
        Assert.IsTrue(xs.First() < -50 && xs.Last() > 50, "the profile spans the field and beyond");
        // The field is centred on the axis: the dose at the centre is near the maximum of the profile.
        int centre = Array.IndexOf(xs, xs.OrderBy(Math.Abs).First());
        Assert.IsTrue(src[centre] > 0.8 * src.Max(), $"centre {src[centre]} vs max {src.Max()}");

        var inplane = BeamLines.ProfileAt(loaded, CurveKind.Inplane, 100);
        Assert.IsTrue(inplane.Overlap().Positions.Length > 50);
    }

    [TestMethod]
    public async Task TheViewModelFiltersBeamsAndProducesACurve()
    {
        var folder = Resources();
        var session = ComparisonRunner.Run(new ComparisonRequest(folder, folder, "Ref", "Tgt",
            new[] { new DtaSpec(3, 3, 95, true) }));

        var vm = new GraphViewModel(ComparisonRunner.LoadWithEvilDicom);
        vm.SetSession(session);

        Assert.IsTrue(vm.HasBeams);
        // Photons only for now (Cody, 2026-11-09): 26 pairs less the 8 electron beams.
        Assert.AreEqual(18, vm.Beams.Count, "all photon energies and field sizes to begin with");
        Assert.IsFalse(vm.Energies.Any(e => e.EndsWith("MeV")), string.Join(", ", vm.Energies));
        Assert.IsTrue(vm.Status.Contains("8 electron beam(s) not shown"), vm.Status);
        Assert.AreEqual(PairCatalog.AnyEnergy, vm.SelectedEnergy);

        vm.SelectedFieldSize = "10 x 10 cm";
        Assert.IsTrue(vm.Beams.Count > 0 && vm.Beams.Count < 26, $"{vm.Beams.Count} beams at 10 x 10");
        Assert.IsTrue(vm.Beams.All(b => b.FieldSize == "10 x 10 cm"));

        vm.SelectedEnergy = vm.Energies[1];
        Assert.IsTrue(vm.Beams.All(b => b.Energy == vm.Energies[1]));

        // Selecting a beam loads asynchronously; wait for the curve.
        for (int i = 0; i < 200 && vm.Curve is null; i++) await Task.Delay(50);
        Assert.IsNotNull(vm.Curve, vm.Status);
        Assert.AreEqual(CurveKind.DepthDose, vm.Kind);
        Assert.IsFalse(vm.DepthEnabled, "the depth slider means nothing for a depth dose");

        vm.Kind = CurveKind.Crossplane;
        Assert.IsTrue(vm.DepthEnabled);
        Assert.IsTrue(vm.Curve!.Title.Contains("crossplane"), vm.Curve.Title);
        Assert.IsTrue(vm.DepthText.EndsWith("mm"), vm.DepthText);

        int before = vm.DepthIndex;
        vm.DepthIndex = Math.Min(before + 5, vm.Depths.Length - 1);
        Assert.AreNotEqual(before, vm.DepthIndex);
        Assert.IsTrue(vm.Curve.Title.Contains($"{vm.Depths[vm.DepthIndex]:0.#} mm"), vm.Curve.Title);

        vm.SetSession(null);
        Assert.IsFalse(vm.HasBeams);
        Assert.IsNull(vm.Curve);
    }
}
