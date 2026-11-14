using DSClibrary.Matching;
using DSClibrary.Studies;
using DSCprototypes.Shared;
using DSCprototypes.Shared.Graphs;
using DSCprototypes.Shared.Studies;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>
/// A three-set study made from the committed files (F1, F2, F5, F7): "AAA" is P1Ref, "Acuros" is
/// P1 Acuros, and "AAA again" is P1Ref a second time. The sets live in temporary folders of links (or
/// copies where links are refused), because a set is a folder.
/// </summary>
[TestClass]
public class StudyRunnerTests
{
    private static string? _root;
    private static StudySession? _session;

    // A threshold near the maximum keeps the comparison quick, as in ComparisonRunnerTests.
    private static readonly DtaSpec Criterion = new(TolerancePercent: 2, DistanceMm: 2, ThresholdPercent: 95, Global: true);

    private static string Resources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "resources");
            if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "RP.*.dcm").Any()) return candidate;
            dir = dir.Parent;
        }
        Assert.Inconclusive("resources/ with RP.*.dcm not found above " + AppContext.BaseDirectory);
        return "";
    }

    private static string SetFolder(string name, string resources, string planLabel)
    {
        string folder = Path.Combine(_root!, name);
        if (Directory.Exists(folder)) return folder;
        Directory.CreateDirectory(folder);
        var files = Directory.GetFiles(resources, $"RP.UnitTest.{planLabel}.dcm")
            .Concat(Directory.GetFiles(resources, $"RD.UnitTest.{planLabel}.*.dcm"));
        foreach (var file in files)
        {
            string link = Path.Combine(folder, Path.GetFileName(file));
            try { File.CreateSymbolicLink(link, file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { File.Copy(file, link); }
        }
        return folder;
    }

    private static Study MakeStudy()
    {
        string resources = Resources();
        _root ??= Path.Combine(Path.GetTempPath(), "dsc-study-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
        var aaa = new DoseSet("AAA", SetFolder("aaa", resources, "P1Ref")) { Reporting = DoseReporting.DoseToWater };
        var axb = new DoseSet("Acuros", SetFolder("axb", resources, "P1 Acuros"));
        var again = new DoseSet("AAA again", SetFolder("again", resources, "P1Ref"));
        var acrossAlgorithms = new PairingRules(DoseMatchStrategy.Identifier,
            labelOverrides: new Dictionary<string, string> { ["P1Ref"] = "P1 Acuros" },
            fieldRules: new[] { new LabelRule("-100[AX]-", "-100{algorithm}-") });
        return new Study(
            new[] { aaa, axb, again },
            new[] { new SetComparison("AAA", "Acuros", acrossAlgorithms), new SetComparison("AAA", "AAA again") },
            DepthSettings.Parse("dmax, 50, 100, 2000"));
    }

    private static StudySession Session() => _session ??= StudyRunner.Run(MakeStudy(), new[] { Criterion });

    [ClassCleanup]
    public static void Cleanup()
    {
        if (_root is not null && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void ThePreviewPairsEachComparisonUnderItsOwnRules()
    {
        var preview = StudyRunner.Preview(MakeStudy());
        Assert.IsTrue(preview.IsPreview);
        var across = preview.Previews.Single(p => p.Comparison.Name == "AAA vs Acuros");
        var self = preview.Previews.Single(p => p.Comparison.Name == "AAA vs AAA again");
        Assert.AreEqual(2, across.Match.Pairs.Count, across.Summary);      // X and F; Acuros has no electron
        Assert.AreEqual(1, across.Match.UnmatchedSource.Count);
        Assert.AreEqual(3, self.Match.Pairs.Count, self.Summary);
        // One set declares dose to water and the Acuros one declares nothing: a set warning.
        StringAssert.Contains(across.SetWarnings.Single(), "Acuros");
    }

    [TestMethod]
    public void TwoSetsInOneFolderAreSeparatedByTheirPlanFilters()
    {
        // Both algorithms in one export folder, as QAtpsPTW holds them. Without filters every plan pairs with itself.
        string resources = Resources();
        var aaa = new DoseSet("AAA", resources) { PlanFilter = "^P1Ref$" };
        var axb = new DoseSet("Acuros", resources) { PlanFilter = "^P1 Acuros$" };
        var rules = new PairingRules(DoseMatchStrategy.Identifier,
            labelOverrides: new Dictionary<string, string> { ["P1Ref"] = "P1 Acuros" },
            fieldRules: new[] { new LabelRule("-100[AX]-", "-100{algorithm}-") });
        var log = new List<string>();
        var session = StudyRunner.Preview(new Study(new[] { aaa, axb }, new[] { new SetComparison("AAA", "Acuros", rules) }),
            new SyncProgress(log));

        var p = session.Previews.Single();
        Assert.AreEqual(2, p.Match.Pairs.Count, p.Summary);
        Assert.AreEqual(0, p.Match.AmbiguousKeys.Count, p.Summary);
        Assert.IsTrue(p.Match.Pairs.All(x => x.Source.Plan!.RTPlanLabel == "P1Ref" && x.Target.Plan!.RTPlanLabel == "P1 Acuros"));
        Assert.AreEqual(1, log.Count(l => l.StartsWith("Reading ", StringComparison.Ordinal)), "the shared folder is read once: " + string.Join(" | ", log));
    }

    private sealed class SyncProgress(List<string> log) : IProgress<string>
    {
        public void Report(string value) => log.Add(value);
    }

    [TestMethod]
    public void EveryRowCarriesItsFieldAndChecks()
    {
        var session = Session();
        Assert.AreEqual(5, session.Rows.Count, session.Summary);
        Assert.IsTrue(session.Rows.All(r => r.Failure is null), string.Join("; ", session.Rows.Select(r => r.Failure)));

        var self = session.Rows.Where(r => r.Comparison == "AAA vs AAA again").ToArray();
        Assert.IsTrue(self.All(r => r.PassRate == 100.0), string.Join("; ", self.Select(r => r.PassRate)));
        Assert.IsTrue(self.All(r => r.PlanCheck == "agrees"), string.Join("; ", self.Select(r => r.PlanCheck)));

        var x = session.Rows.Single(r => r.Comparison == "AAA vs Acuros" && r.Facts.Field.StartsWith("X-", StringComparison.Ordinal));
        Assert.AreEqual("P1Ref", x.Facts.SourcePlan);
        Assert.AreEqual("P1 Acuros", x.Facts.TargetPlan);
        Assert.AreEqual("10 MV", x.Facts.Energy);   // measured: the 10.0 in the field name is the energy
        Assert.AreNotEqual(FieldFacts.NotStated, x.Facts.Machine);
    }

    [TestMethod]
    public void AnIndexTablePoolsByFieldSizeAndEnergy()
    {
        var session = Session();
        var table = IndexTable.Build(session.Rows, "AAA vs AAA again", Criterion);
        Assert.IsTrue(table.ColumnLabels.Count >= 2, string.Join(", ", table.ColumnLabels));
        // Photons before electrons, flattened before FFF: measured, P1Ref holds 10 MV, 10 MV FFF and 20 MeV.
        CollectionAssert.AreEqual(new[] { "10 MV", "10 MV FFF", "20 MeV" }, table.ColumnLabels.ToArray());
        int cells = 0;
        for (int r = 0; r < table.RowLabels.Count; r++)
            for (int c = 0; c < table.ColumnLabels.Count; c++)
                if (table[r, c] is { } cell)
                {
                    cells += cell.Fields;
                    Assert.AreEqual(100.0, cell.PooledPassRate);
                }
        Assert.AreEqual(3, cells);
    }

    [TestMethod]
    public void PoolingWeightsAFieldByItsVoxels()
    {
        var cell = new IndexCell(Fields: 2, Compared: 1000, Passed: 900, WorstPassRate: 50, WorstField: "x", NotRun: 0);
        Assert.AreEqual(90.0, cell.PooledPassRate);
        StringAssert.Contains(cell.Text, "worst 50.00 %");
    }

    [TestMethod]
    public void TheSpreadsheetsAreWrittenAndNotOverwritten()
    {
        var session = Session();
        string dir = Path.Combine(_root!, "out");
        var written = StudyCsv.Write(session, dir, "t");
        Assert.IsTrue(written.Any(p => p.EndsWith("t-results.csv", StringComparison.Ordinal)));
        Assert.IsTrue(written.Any(p => p.EndsWith("t-pairing.csv", StringComparison.Ordinal)));
        Assert.AreEqual(2, written.Count(p => p.Contains("-index-", StringComparison.Ordinal)));
        string results = File.ReadAllText(written.First(p => p.EndsWith("results.csv", StringComparison.Ordinal)));
        StringAssert.StartsWith(results, string.Join(",", StudyCsv.ResultColumns));
        Assert.ThrowsExactly<IOException>(() => StudyCsv.Write(session, dir, "t"));
    }

    [TestMethod]
    public void AFieldIsFoundInEverySetThatHasIt()
    {
        var fields = StudyFieldCatalog.Build(Session());
        var x = fields.Single(f => f.Facts.Field.StartsWith("X-", StringComparison.Ordinal));
        CollectionAssert.AreEqual(new[] { "AAA", "Acuros", "AAA again" }, x.Doses.Select(d => d.SetName).ToArray());
        var e = fields.Single(f => f.Facts.Field.StartsWith("e-", StringComparison.Ordinal));
        Assert.AreEqual(2, e.Doses.Count);
    }

    [TestMethod]
    public void CurvesForEverySetAtTheChosenDepths()
    {
        var x = StudyFieldCatalog.Build(Session()).Single(f => f.Facts.Field.StartsWith("X-", StringComparison.Ordinal));
        var loaded = MultiCurves.Load(x.Doses, ComparisonRunner.LoadWithEvilDicom);

        var set = MultiCurves.For(x.Doses, loaded, CurveKind.Crossplane, Session().Study.Depths);
        // dmax, 50 and 100 mm are drawn; 2000 mm is past every grid and is named as skipped.
        Assert.AreEqual(3, set.Curves.Count, string.Join("; ", set.Skipped));
        StringAssert.StartsWith(set.Skipped.Single(), "2000 mm");
        foreach (var curve in set.Curves)
        {
            Assert.AreEqual(3, curve.Series.Count);
            Assert.AreEqual(2, curve.Differences.Count);
            var (positions, _) = curve.Overlap();
            Assert.IsTrue(positions.Length > 10, curve.Title);
            // "AAA again" is the same file as the reference, so its difference is zero wherever defined.
            var again = curve.Differences.Single(d => d.Name.StartsWith("AAA again", StringComparison.Ordinal));
            Assert.IsTrue(again.Values.Where(v => !double.IsNaN(v)).All(v => Math.Abs(v) < 1e-9), curve.Title);
        }

        var pdd = MultiCurves.For(x.Doses, loaded, CurveKind.DepthDose, Session().Study.Depths);
        Assert.AreEqual(1, pdd.Curves.Count);
        Assert.IsTrue(pdd.DmaxMm > 0, $"dmax {pdd.DmaxMm}");
    }
}
