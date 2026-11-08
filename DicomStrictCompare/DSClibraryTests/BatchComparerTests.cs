using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// The batch runner: the parallelism decision 7 already sanctions - one pair per task - which
    /// nothing was using. Also the first place a comparison result is kept as an object rather than
    /// rendered and discarded, which backlog D15 needs.
    /// </summary>
    [TestClass]
    public class BatchComparerTests
    {
        private static DoseMatrixOptimal LoadWithEvilDicom(string path) =>
            new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path)));

        private static MatchedDosePair[] SelfPairs(int take)
        {
            var doses = RTFolderReader.Read(TestData.ResourcesDirectory!).Doses.ToArray();
            return DoseMatcher.Match(doses, doses, DoseMatchStrategy.PlanAndBeamUid)
                .Pairs
                .OrderBy(p => p.Source.Dose.SourcePath, StringComparer.Ordinal)
                .Take(take)
                .ToArray();
        }

        /// <summary>A threshold near the maximum, so few points are evaluated and the test is quick.</summary>
        private static Dta Quick() => new(true, 0.95, 0.02, 2.0);

        [TestMethod]
        public void ComparesEveryPairAndKeepsTheResults()
        {
            var pairs = SelfPairs(4);
            var result = new BatchComparer(LoadWithEvilDicom).Compare(pairs, Quick());

            Assert.AreEqual(4, result.Outcomes.Count);
            Assert.AreEqual(4, result.Succeeded.Count());
            Assert.AreEqual(0, result.Failed.Count(), string.Join("; ", result.Failed.Select(f => f.Failure)));

            foreach (var outcome in result.Outcomes)
            {
                // The result carries what it was given as well as what it found, so it can be
                // re-rendered without being recomputed and a number can be traced to its files.
                Assert.IsNotNull(outcome.Comparison);
                Assert.IsNotNull(outcome.Key);
                Assert.IsTrue(File.Exists(outcome.SourcePath));
                Assert.IsTrue(File.Exists(outcome.TargetPath));
                Assert.AreEqual(0.95, outcome.Dta.Threshhold, 1e-9, "the parameters travel with the result");
                Assert.IsTrue(outcome.Global);
                Assert.IsTrue(outcome.Elapsed > TimeSpan.Zero);
            }
        }

        [TestMethod]
        public void ComparingAFileWithItselfFindsNoFailures()
        {
            // The strongest available correctness check on the batch path: identical inputs must
            // agree everywhere, whatever the tolerance.
            var result = new BatchComparer(LoadWithEvilDicom).Compare(SelfPairs(3), Quick());

            foreach (var outcome in result.Succeeded)
            {
                Assert.AreEqual(0, outcome.Comparison!.TotalFailed, outcome.ToString());
                Assert.AreEqual(0.0, outcome.PercentFailed!.Value, 1e-9);
                Assert.IsTrue(outcome.Comparison.TotalCompared > 0, "something must have been evaluated");
            }
        }

        [TestMethod]
        public void MonitorUnitsTravelWithTheOutcomeAndChangeNothing()
        {
            var varied = RTFolderReader.Read(TestData.ResourcesDirectory!).Doses
                .Where(d => d.Plan?.RTPlanLabel == "P5 Varied MU" && d.Beam!.BeamName!.StartsWith("X-"))
                .ToArray();
            var pairs = DoseMatcher.Match(varied, varied, DoseMatchStrategy.PlanAndBeamUid).Pairs;

            var result = new BatchComparer(LoadWithEvilDicom).Compare(pairs, Quick());

            CollectionAssert.AreEquivalent(
                new[] { 95.0, 99.0, 101.0 },
                result.Succeeded.Select(o => o.SourceMonitorUnits!.Value).ToArray());

            // Each is compared against itself, so despite three different metersets nothing fails.
            Assert.IsTrue(result.Succeeded.All(o => o.Comparison!.TotalFailed == 0));
        }

        [TestMethod]
        public void ABadPairDoesNotLoseTheBatch()
        {
            var pairs = SelfPairs(3);
            int calls = 0;
            var comparer = new BatchComparer(path =>
            {
                // Fail the second load attempt, whichever pair gets there first.
                if (Interlocked.Increment(ref calls) == 2)
                    throw new InvalidOperationException("simulated read failure");
                return LoadWithEvilDicom(path);
            });

            var result = comparer.Compare(pairs, Quick(), degreeOfParallelism: 1);

            Assert.AreEqual(3, result.Outcomes.Count, "every pair is accounted for");
            Assert.AreEqual(1, result.Failed.Count());
            StringAssert.Contains(result.Failed.Single().Failure!, "simulated read failure");
            Assert.AreEqual(2, result.Succeeded.Count());
        }

        [TestMethod]
        public void TheOrderIsStableSoTwoRunsReportAlike()
        {
            var pairs = SelfPairs(5);
            var comparer = new BatchComparer(LoadWithEvilDicom);

            var first = comparer.Compare(pairs, Quick(), degreeOfParallelism: 4);
            var second = comparer.Compare(pairs.Reverse().ToArray(), Quick(), degreeOfParallelism: 4);

            CollectionAssert.AreEqual(
                first.Outcomes.Select(o => o.Key.ToString()).ToArray(),
                second.Outcomes.Select(o => o.Key.ToString()).ToArray(),
                "parallel completion order must not leak into the report");
        }

        [TestMethod]
        public void ReportBatchSpeedupFromParallelism()
        {
            // Decision 7's sanctioned win, measured. A report rather than a gate: a timing assertion
            // would fail on a busy machine and tell nobody anything.
            var pairs = SelfPairs(4);
            var comparer = new BatchComparer(LoadWithEvilDicom);
            var dta = new Dta(true, 0.5, 0.02, 2.0);

            var serial = comparer.Compare(pairs, dta, degreeOfParallelism: 1);
            var parallel = comparer.Compare(pairs, dta, degreeOfParallelism: 4);

            Console.WriteLine($"  {pairs.Length} pairs serial   : {serial.Elapsed.TotalSeconds:0.00}s");
            Console.WriteLine($"  {pairs.Length} pairs at 4-way : {parallel.Elapsed.TotalSeconds:0.00}s");
            Console.WriteLine($"  speedup: {serial.Elapsed.TotalSeconds / Math.Max(0.001, parallel.Elapsed.TotalSeconds):0.0}x "
                              + $"on {Environment.ProcessorCount} logical cores");

            Assert.AreEqual(serial.Succeeded.Count(), parallel.Succeeded.Count(),
                "parallelism must not change what is computed");
            CollectionAssert.AreEqual(
                serial.Outcomes.Select(o => o.Comparison!.TotalFailed).ToArray(),
                parallel.Outcomes.Select(o => o.Comparison!.TotalFailed).ToArray(),
                "nor the answers");
        }
    }
}
