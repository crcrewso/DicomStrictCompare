using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Gamma;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Every criterion on a pair loaded once (<see cref="BatchComparer.CompareMany"/>) and, on a GPU, uploaded once
    /// (<see cref="DoseComparer.CompareAll"/>): the counts must be the ones each criterion gives alone (2026-11-15).
    /// </summary>
    [TestClass]
    public class CompareManyTests
    {
        private static readonly HandWrittenComparer Reference = new();

        // Differing thresholds and distances, so the GPU's union upload is wider than any one criterion reads.
        private static readonly (Dta Dta, bool Global)[] Criteria =
        {
            (new Dta(true, 0.00, 0.0001, 0, relative: true), true),
            (new Dta(true, 0.10, 0.001, 0.5, relative: false), false),
            (new Dta(true, 0.10, 0.02, 2, relative: true), true),
            (new Dta(true, 0.50, 0.03, 3, relative: true), true),
            (new Dta(true, 0.01, 0.03, 2, relative: false, trim: 2), false),
        };

        private static readonly string[] Files =
        {
            "RD.UnitTest.P1Ref.X-100A-10.0-1.dcm",
            "RD.UnitTest.P1 5 mm.X-100A-10.0-5.dcm",
            "RD.UnitTest.P1Ref.e-100A-10.0-1-13.dcm",
            "RD.UnitTest.P4 E 2D.e-100A-10.0-1-12.dcm",
        };

        private static MatchedDosePair[] Pairs()
        {
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            return DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.Identifier).Pairs
                .Where(p => Files.Contains(Path.GetFileName(p.Source.Dose.SourcePath))).ToArray();
        }

        [TestMethod]
        public void CompareAllMatchesEachCriterionAloneForEveryComparer()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            // The GPU comparers override CompareAll; a boxed CPU one covers the default loop. The unboxed engines would
            // add minutes here and are held to the reference criterion by criterion in DoseComparerEquivalenceTests.
            var comparers = DoseComparerEquivalenceTests.GpuDoubles().Prepend(new MultiThreadComparer()).ToArray();
            Console.WriteLine("Checked: " + string.Join("; ", comparers.Select(c => c.Description)));
            var failures = new List<string>();
            foreach (var (s, t) in new[] { ("P1Ref.X-100A-10.0-1", "P1 Acuros.X-100X-10.0-1"), ("P1Ref.X-100A-10.0-1", "P1 5 mm.X-100A-10.0-5"), ("P1Ref.e-100A-10.0-1-13", "P4 E 2D.e-100A-10.0-1-12") })
            {
                var source = DoseGridReader.Read(TestData.DoseFiles.Single(f => Path.GetFileName(f) == $"RD.UnitTest.{s}.dcm"));
                var target = DoseGridReader.Read(TestData.DoseFiles.Single(f => Path.GetFileName(f) == $"RD.UnitTest.{t}.dcm"));
                // The source maximum, and a lower chosen one (as CAX Max gives), in the same call.
                var runs = Criteria.SelectMany(c => new[] { 1.0, 0.8 }.Select(f => new ComparisonRun(c.Dta, c.Global, f * source.MaxPointDose.Dose))).ToArray();
                var expected = runs.Select(r => Alone(Reference, source, target, r)).ToArray();
                foreach (var c in comparers)
                {
                    var got = c.CompareAll(source, target, runs);
                    for (int n = 0; n < runs.Length; n++)
                        if (!Same(expected[n], got[n]))
                            failures.Add($"{c.Description}, {s} vs {t}, run {n} ({runs[n].Dta.ShortToString()}): {Text(got[n])}, expected {Text(expected[n])}");
                }
            }
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(20)));
        }

        [TestMethod]
        public void CompareManyLoadsEachPairOnceForEveryCriterion()
        {
            var pairs = Pairs();
            Assert.AreEqual(Files.Length, pairs.Length);
            int loads = 0;
            var batch = new BatchComparer(path => { Interlocked.Increment(ref loads); return DoseGridReader.Read(path); }, new MultiThreadComparer());
            var criteria = Criteria.Select(c => new Criterion(c.Dta, c.Global))
                .Append(Criterion.ForGamma(new GammaOptions(3, 3, LowerPercentDoseCutoff: 10, MaxGamma: 1.1, SkipOncePassed: true, RandomSubset: 500, RandomSeed: 1)))
                .ToArray();
            var result = batch.CompareMany(pairs, criteria);
            Assert.AreEqual(2 * pairs.Length, loads);
            Assert.AreEqual(pairs.Length * criteria.Length, result.Outcomes.Count);
            Assert.IsTrue(result.Outcomes.All(o => o.Succeeded), string.Join("; ", result.Failed));
        }

        [TestMethod]
        public void CompareManyGivesTheRowsOfOneCriterionAtATime()
        {
            var pairs = Pairs();
            var batch = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer());
            var gamma = new GammaOptions(3, 3, LowerPercentDoseCutoff: 10, MaxGamma: 1.1, SkipOncePassed: true, RandomSubset: 500, RandomSeed: 1);
            var criteria = Criteria.Select(c => new Criterion(c.Dta, c.Global)).Append(Criterion.ForGamma(gamma)).ToArray();
            var many = batch.CompareMany(pairs, criteria, maximum: MaximumKind.CaxMax).Outcomes;
            for (int n = 0; n < criteria.Length; n++)
            {
                var alone = criteria[n].Gamma is { } g
                    ? batch.CompareGamma(pairs, g, maximum: MaximumKind.CaxMax).Outcomes
                    : batch.Compare(pairs, criteria[n].Dta, criteria[n].Global, maximum: MaximumKind.CaxMax).Outcomes;
                var mine = many.Where(o => o.CriterionIndex == n).ToArray();
                Assert.AreEqual(alone.Count, mine.Length);
                for (int i = 0; i < mine.Length; i++)
                {
                    Assert.AreEqual(alone[i].Key, mine[i].Key);
                    Assert.IsTrue(Same(alone[i].Comparison!, mine[i].Comparison!), $"criterion {n}, {mine[i].Key}: {Text(mine[i].Comparison!)}, alone {Text(alone[i].Comparison!)}");
                    Assert.AreEqual(alone[i].Maximum, mine[i].Maximum);
                    Assert.AreEqual(alone[i].EdgeCheck, mine[i].EdgeCheck);
                }
            }
        }

        [TestMethod]
        public void CriteriaApplyToTheirBeamTypeOnly()
        {
            var pairs = Pairs();
            var batch = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer());
            var dta = new Dta(true, 0.10, 0.02, 2, relative: true);
            var criteria = new[]
            {
                new Criterion(dta, true, AppliesTo: BeamsCompared.Photon),
                new Criterion(dta, true, AppliesTo: BeamsCompared.Electron),
                new Criterion(dta, true),
            };
            var outcomes = batch.CompareMany(pairs, criteria).Outcomes;
            string TypeOf(ComparisonOutcome o) => pairs.Single(p => p.Key == o.Key).Source.Beam!.RadiationType!.ToUpperInvariant();
            Assert.AreEqual(2, outcomes.Count(o => o.CriterionIndex == 0));
            Assert.IsTrue(outcomes.Where(o => o.CriterionIndex == 0).All(o => TypeOf(o) == "PHOTON"));
            Assert.AreEqual(2, outcomes.Count(o => o.CriterionIndex == 1));
            Assert.IsTrue(outcomes.Where(o => o.CriterionIndex == 1).All(o => TypeOf(o) == "ELECTRON"));
            Assert.AreEqual(pairs.Length, outcomes.Count(o => o.CriterionIndex == 2));

            // No plan, no RadiationType: only Both applies.
            Assert.IsTrue(criteria[2].AppliesToBeam(null));
            Assert.IsFalse(criteria[0].AppliesToBeam(null));
            Assert.IsFalse(criteria[1].AppliesToBeam(null));
        }

        [TestMethod]
        public void PairDoneReportsEachPairOnceWithAllItsRows()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            var pairs = Pairs();
            var criteria = Criteria.Take(3).Select(c => new Criterion(c.Dta, c.Global)).ToArray();
            // Both ways of reading: each worker reads its own pair, and the read queue.
            foreach (var queue in new BatchComparer.ReadQueue?[] { null, new(2, 1) })
            {
                var reports = new List<IReadOnlyList<ComparisonOutcome>>();
                var comparer = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer())
                    { Queue = queue, PairDone = done => { lock (reports) reports.Add(done); } };
                var result = comparer.CompareMany(pairs, criteria, degreeOfParallelism: 2);

                Assert.AreEqual(pairs.Length, reports.Count, $"one report per pair, queue {queue}");
                foreach (var r in reports)
                {
                    Assert.AreEqual(criteria.Length, r.Count, "a report holds every criterion's row for its pair");
                    Assert.AreEqual(1, r.Select(o => (o.SourcePath, o.TargetPath)).Distinct().Count(), "and only that pair's");
                }
                // The same outcome objects the result returns, each reported once; the result keeps its own order.
                CollectionAssert.AreEquivalent(result.Outcomes.ToArray(), reports.SelectMany(r => r).ToArray());
            }
        }

        [TestMethod]
        public void AnAmbiguousKeyKeepsTheWorstPairingAtEachCriterion()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            RTDoseWithPlan Dose(string name) => folder.Doses.Single(d => Path.GetFileName(d.Dose.SourcePath) == $"RD.UnitTest.{name}.dcm");
            var source = Dose("P1Ref.X-100A-10.0-1");
            // One key, two targets: the source itself (fails nothing) and its 5 mm recalculation (fails some).
            var key = DoseMatchKey.For(source, DoseMatchStrategy.PlanAndBeamUid)!;
            var pairings = new[] { Dose("P1Ref.X-100A-10.0-1"), Dose("P1 5 mm.X-100A-10.0-5") }
                .Select(t => new MatchedDosePair(key, source, t) { Candidates = 2 }).ToArray();
            var criteria = Criteria.Take(3).Select(c => new Criterion(c.Dta, c.Global)).ToArray();

            var reported = new List<IReadOnlyList<ComparisonOutcome>>();
            var comparer = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer()) { PairDone = d => { lock (reported) reported.Add(d); } };
            var result = comparer.CompareMany(pairings, criteria, degreeOfParallelism: 2);
            var plain = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer());
            var alone = pairings.Select(p => plain.CompareMany(new[] { p with { Candidates = 1 } }, criteria).Outcomes).ToArray();

            Assert.AreEqual(criteria.Length, result.Outcomes.Count, "one row per criterion for the key, not one per pairing");
            Assert.AreEqual(1, reported.Count, "reported once, when both pairings were done");
            for (int c = 0; c < criteria.Length; c++)
            {
                var kept = result.Outcomes.Single(o => o.CriterionIndex == c);
                var worst = alone.Select(a => a.Single(o => o.CriterionIndex == c)).MaxBy(o => o.PercentFailed)!;
                Assert.AreEqual(2, kept.Candidates);
                Assert.AreEqual(worst.TargetPath, kept.TargetPath, $"criterion {c}");
                Assert.AreEqual(worst.Comparison!.TotalFailed, kept.Comparison!.TotalFailed, $"criterion {c}");
            }
            Assert.IsTrue(result.Outcomes.Any(o => o.Comparison!.TotalFailed > 0), "the 5 mm pairing fails somewhere, so the test can tell");
        }

        [TestMethod]
        public void AFailedPairingIsTheWorst()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            var source = folder.Doses.First(d => Path.GetFileName(d.Dose.SourcePath).Contains("P1Ref.X-100A"));
            var key = DoseMatchKey.For(source, DoseMatchStrategy.PlanAndBeamUid)!;
            var pairings = new[] { new MatchedDosePair(key, source, source) { Candidates = 2 }, new MatchedDosePair(key, source, source) { Candidates = 2 } };
            int calls = 0;
            // The second load of the target throws, so one pairing fails outright.
            var comparer = new BatchComparer(p => Interlocked.Increment(ref calls) == 4 ? throw new IOException("unreadable") : DoseGridReader.Read(p));
            var result = comparer.CompareMany(pairings, new[] { new Criterion(Criteria[2].Dta, true) }, degreeOfParallelism: 1);
            Assert.AreEqual(1, result.Outcomes.Count);
            Assert.IsNotNull(result.Outcomes[0].Failure, "the failed pairing is kept, not hidden behind the one that worked");
        }

        private static SingleComparison Alone(DoseComparer c, DoseMatrixOptimal s, DoseMatrixOptimal t, ComparisonRun r) =>
            r.Global ? c.CompareRelative(s, t, r.Dta, r.ReferenceMaximum, CancellationToken.None)
                     : c.CompareAbsolute(s, t, r.Dta, r.ReferenceMaximum, CancellationToken.None);

        private static bool Same(SingleComparison a, SingleComparison b) =>
            a.TotalCount == b.TotalCount && a.TotalCompared == b.TotalCompared && a.TotalFailed == b.TotalFailed;

        private static string Text(SingleComparison c) => $"{c.TotalCount}/{c.TotalCompared}/{c.TotalFailed}";
    }
}
