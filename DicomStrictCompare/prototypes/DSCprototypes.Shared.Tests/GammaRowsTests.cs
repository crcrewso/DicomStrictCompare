using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using DSCprototypes.Shared;

namespace DSCprototypes.Shared.Tests;

/// <summary>A gamma criterion runs through the same runner, rows and spreadsheet as a DTA one.</summary>
[TestClass]
public class GammaRowsTests
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

    [TestMethod]
    public void TheSpecCarriesTheAlgorithm()
    {
        var dta = new DtaSpec(3, 3, 10, true);
        var gamma = dta with { Gamma = true };
        Assert.AreEqual("dta", dta.Algorithm);
        Assert.AreEqual("gamma", gamma.Algorithm);
        Assert.IsFalse(dta.Label.Contains("gamma"));
        Assert.IsTrue(gamma.Label.EndsWith(" gamma"));
        Assert.AreEqual(DSClibrary.Dta.CalcAlgorithm.gamma, gamma.ToDta().Algorithm);
        Assert.AreEqual(DSClibrary.Dta.CalcAlgorithm.dta, dta.ToDta().Algorithm);

        var options = gamma.ToGammaOptions();
        Assert.AreEqual(3, options.DosePercentThreshold);
        Assert.AreEqual(3, options.DistanceThreshold);
        Assert.AreEqual(10, options.LowerPercentDoseCutoff);
        Assert.IsFalse(options.LocalGamma);
        Assert.IsTrue((dta with { Global = false, Gamma = true }).ToGammaOptions().LocalGamma);
        Assert.AreEqual(1.1, options.MaxGamma);
        Assert.IsTrue(options.SkipOncePassed);
        Assert.IsTrue(DtaSpec.DefaultGamma.Gamma);
    }

    [TestMethod]
    public void AGammaCriterionProducesRowsLikeADtaOneAndASpreadsheetColumnSayingSo()
    {
        string resources = Resources();
        var request = new ComparisonRequest(resources, resources, "Ref", "Tgt",
            new[] { new DtaSpec(3, 3, 95, true), new DtaSpec(3, 3, 95, true, Gamma: true) },
            DoseMatchStrategy.PlanAndBeamUid);

        var session = ComparisonRunner.Run(request);

        Assert.AreEqual(52, session.Rows.Count, "26 pairs under two criteria");
        var gammaRows = session.Rows.Where(r => r.Criterion.Gamma).ToList();
        var dtaRows = session.Rows.Where(r => !r.Criterion.Gamma).ToList();
        Assert.AreEqual(26, gammaRows.Count);
        Assert.IsTrue(gammaRows.All(r => r.Succeeded), string.Join("; ", gammaRows.Where(r => !r.Succeeded).Select(r => r.Failure)));
        Assert.IsTrue(gammaRows.All(r => r.Compared > 0 && r.Failed == 0), "a file against itself passes gamma everywhere analysed");
        Assert.IsTrue(gammaRows.All(r => r.MaxGamma is < 1e-9), "and its gamma is zero");
        Assert.IsTrue(gammaRows.All(r => r.GammaPassRate == 100));
        Assert.IsTrue(dtaRows.All(r => r.MaxGamma is null && r.GammaPassRate is null));
        Assert.IsTrue(gammaRows.All(r => r.TotalVoxels == dtaRows.Single(d => d.Key == r.Key).TotalVoxels),
            "both count the same reference voxels");

        // The outcome keeps the whole gamma result.
        var outcome = session.Outcomes.First(o => o.Gamma is not null);
        Assert.IsTrue(outcome.IsGamma);
        Assert.AreEqual(outcome.Gamma!.Analysed, outcome.Comparison!.TotalCompared);
        Assert.AreEqual(outcome.Gamma.Analysed - outcome.Gamma.Passed, outcome.Comparison.TotalFailed);

        string csv = CsvReport.ResultsText(session);
        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] header = lines[0].TrimEnd('\r').Split(',');
        int algorithm = Array.IndexOf(header, "Algorithm");
        int maxGamma = Array.IndexOf(header, "MaxGamma");
        Assert.IsTrue(algorithm > 0 && maxGamma > 0);
        var cells = lines.Skip(1).Select(l => l.TrimEnd('\r').Split(',')).ToList();
        Assert.AreEqual(26, cells.Count(c => c[algorithm] == "gamma"));
        Assert.AreEqual(26, cells.Count(c => c[algorithm] == "dta"));
        Assert.IsTrue(cells.Where(c => c[algorithm] == "dta").All(c => c[maxGamma] == ""));
        Assert.IsTrue(cells.Where(c => c[algorithm] == "gamma").All(c => c[maxGamma] != ""));
    }
}
