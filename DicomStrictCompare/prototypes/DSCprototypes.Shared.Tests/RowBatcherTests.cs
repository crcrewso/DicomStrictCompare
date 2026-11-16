using DSClibrary.Matching;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>Rows shown while a run is going (D30): the batcher, and the runners' rows as pairs finish.</summary>
[TestClass]
public class RowBatcherTests
{
    [TestMethod]
    public void WithNoIntervalEachReportIsShownAtOnce()
    {
        var grid = new List<int>();
        var shown = new List<int>();
        var b = new RowBatcher<int>(grid, TimeSpan.Zero, a => a()) { Flushed = shown.Add };
        b.Add(new[] { 1, 2 });
        b.Add(new[] { 3 });
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, grid);
        CollectionAssert.AreEqual(new[] { 2, 3 }, shown);
    }

    [TestMethod]
    public void WithinTheIntervalRowsWaitAndStopDropsThem()
    {
        var grid = new List<int>();
        var b = new RowBatcher<int>(grid, TimeSpan.FromHours(1), a => a());
        b.Add(new[] { 1, 2, 3 });
        Assert.AreEqual(0, grid.Count, "held for the next batch");
        Assert.AreEqual(3, b.Received);
        b.Stop();
        b.Add(new[] { 4 });
        Assert.AreEqual(0, grid.Count, "nothing is added after Stop");
        Assert.AreEqual(3, b.Received);
    }

    [TestMethod]
    public void WaitingRowsAreShownWhenTheIntervalPassesWithNoMoreReports()
    {
        var grid = new List<int>();
        var gate = new object();
        var b = new RowBatcher<int>(grid, TimeSpan.FromMilliseconds(100), a => { lock (gate) a(); });
        b.Progress.Report(new[] { 7 });
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) { lock (gate) if (grid.Count == 1) break; Thread.Sleep(10); }
        lock (gate) CollectionAssert.AreEqual(new[] { 7 }, grid);
    }

    [TestMethod]
    public void TheRunnerReportsEveryRowItReturnsAsPairsFinish()
    {
        string resources = ComparisonRunnerTests.Resources();
        var request = new ComparisonRequest(resources, resources, "Ref", "Tgt",
            new[] { new DtaSpec(2, 2, 95, true), new DtaSpec(3, 3, 95, true) }, DoseMatchStrategy.PlanAndBeamUid);
        var live = new List<ResultRow>();
        var session = ComparisonRunner.Run(request, rowsSoFar: new Collect<ResultRow>(live));
        Assert.AreEqual(52, session.Rows.Count, "26 doses by UID, 2 criteria");
        CollectionAssert.AreEquivalent(session.Rows.ToArray(), live.ToArray());
    }

    private sealed class Collect<T>(List<T> sink) : IProgress<IReadOnlyList<T>>
    {
        public void Report(IReadOnlyList<T> value) { lock (sink) sink.AddRange(value); }
    }
}
