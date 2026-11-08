using DSClibrary.Matching;
using DSCprototypes.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>
/// The workflow the three front ends share, run headless against the repository's resources folder.
/// This is what each GUI's Run button does, so a green run here means the UIs differ only in how
/// they present it.
/// </summary>
[TestClass]
public class ComparisonRunnerTests
{
    private static string Resources()
    {
        // Walk up from the test assembly to the repository's resources/ folder.
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
    public void SelfComparisonByUidMatchesEveryDoseAndFailsNothing()
    {
        string resources = Resources();
        var request = new ComparisonRequest(
            resources, resources, "Ref", "Tgt",
            // A threshold near the maximum so few voxels are evaluated and the run stays quick.
            new[] { new DtaSpec(TolerancePercent: 2, DistanceMm: 2, ThresholdPercent: 95, Global: true) },
            DoseMatchStrategy.PlanAndBeamUid);

        var log = new List<string>();
        var session = ComparisonRunner.Run(request, new SynchronousProgress(log));

        Assert.AreEqual(26, session.Source.Doses.Count, "the resources folder holds 26 doses");
        Assert.AreEqual(9, session.Source.PlansByUid.Count, "and 9 plans");
        Assert.AreEqual(26, session.Match.Pairs.Count, "every dose pairs with itself by UID");
        Assert.AreEqual(0, session.Unmatched.Count());
        Assert.AreEqual(26, session.Rows.Count);
        Assert.IsTrue(session.Rows.All(r => r.Succeeded), string.Join("; ", session.Rows.Where(r => !r.Succeeded).Select(r => r.Failure)));
        Assert.IsTrue(session.Rows.All(r => r.Failed == 0), "a file compared with itself fails nowhere");
        Assert.IsTrue(session.Rows.All(r => r.Compared > 0));
        Assert.IsTrue(log.Count >= 5, "the runner reports progress");
    }

    [TestMethod]
    public void TheSpreadsheetHasOneRowPerComparisonAndOpensAsCsv()
    {
        string resources = Resources();
        var request = new ComparisonRequest(resources, resources, "Ref", "Tgt",
            new[] { new DtaSpec(3, 3, 95, true) });
        var session = ComparisonRunner.Run(request);

        string outDir = Path.Combine(Path.GetTempPath(), "dsc-proto-" + Guid.NewGuid().ToString("N"));
        try
        {
            var written = CsvReport.Write(session, outDir);
            Assert.AreEqual(1, written.Count, "nothing unmatched, so one file");
            var lines = File.ReadAllLines(written[0]);
            Assert.AreEqual(1 + 26, lines.Length, "header plus one row per comparison");
            Assert.AreEqual(string.Join(",", CsvReport.ResultColumns), lines[0]);
            Assert.IsTrue(lines.Skip(1).All(l => l.Split(',').Length >= CsvReport.ResultColumns.Length));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }

    private sealed class SynchronousProgress : IProgress<string>
    {
        private readonly List<string> _sink;
        public SynchronousProgress(List<string> sink) => _sink = sink;
        public void Report(string value) { lock (_sink) _sink.Add(value); }
    }
}
