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
using DSClibrary.Studies;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// A target read only where the criteria and the edge check look (<see cref="BatchComparer.OpenTarget"/>) must give
    /// exactly a full read's counts and edges. Unread voxels hold <see cref="DoseGridReader.PartialDoseGrid.Unread"/>,
    /// so a read outside the region would make points fail and show here as a difference.
    /// </summary>
    [TestClass]
    public class PartialTargetTests
    {
        private static readonly (string Source, string Target)[] Pairs =
        {
            ("P1Ref.X-100A-10.0-1", "P1 Acuros.X-100X-10.0-1"),
            ("P1Ref.F-100A-10.0-1", "P1 Acuros.F-100X-10.0-1"),
            ("P1Ref.X-100A-10.0-1", "P1 2 mm.X-100A-10.0-0.2"),
            ("P1Ref.X-100A-10.0-1", "P1 5 mm.X-100A-10.0-5"),
            ("P1 5 mm.X-100A-10.0-5", "P1Ref.X-100A-10.0-1"),
            ("P2 10.1.X-100X-10.1-1", "P3 11.0.X-100X-10.1-1"),
            ("P5 Varied MU.X-095A-10.0-1", "P5 Varied MU.X-101A-10.0-1"),
            ("P1Ref.e-100A-10.0-1-13", "P4 E 2D.e-100A-10.0-1-12"),
        };

        private static readonly (Dta Dta, bool Global)[] Criteria =
        {
            (new Dta(true, 0.10, 0.02, 2, relative: true), true),
            (new Dta(true, 0.10, 0.001, 0.5, relative: false), false),
            (new Dta(true, 0.50, 0.03, 3, relative: true), true),
            (new Dta(true, 0.95, 0.03, 2, relative: true), true),               // a small box: the edge rows matter
            (new Dta(true, 0.01, 0.03, 2, relative: false, trim: 2), false),
        };

        private static Dictionary<string, RTDoseWithPlan> Doses() =>
            RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true).Doses
                .ToDictionary(d => Path.GetFileName(d.Dose.SourcePath));

        private static string File(string stem) => TestData.DoseFiles.Single(f => Path.GetFileName(f) == $"RD.UnitTest.{stem}.dcm");

        [TestMethod]
        public void APartialTargetGivesAFullReadsCountsAndEdges()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            var doses = Doses();
            var comparers = DoseComparerEquivalenceTests.GpuDoubles().Prepend(new MultiThreadComparer()).ToArray();
            var failures = new List<string>();
            foreach (var (s, t) in Pairs)
            {
                var source = DoseGridReader.Read(File(s));
                var full = DoseGridReader.Read(File(t));
                var beam = doses[$"RD.UnitTest.{t}.dcm"].Beam;
                foreach (var subset in new[] { Criteria, Criteria.Skip(3).Take(1).ToArray() })   // all, and the 95 % one alone
                {
                    var runs = subset.Select(c => new ComparisonRun(c.Dta, c.Global, source.MaxPointDose.Dose)).ToArray();
                    using var partial = BatchComparer.OpenTarget(File(t), source, runs, beam);
                    Console.WriteLine($"{s} vs {t}, {runs.Length} criteria: {partial.FractionRead:P1} of the target read");
                    foreach (var c in comparers)
                    {
                        var want = c.CompareAll(source, full, runs);
                        var got = c.CompareAll(source, partial.Grid, runs);
                        for (int n = 0; n < runs.Length; n++)
                            if (want[n].TotalCompared != got[n].TotalCompared || want[n].TotalFailed != got[n].TotalFailed || want[n].TotalCount != got[n].TotalCount)
                                failures.Add($"{c.Description}, {s} vs {t}, {runs[n].Dta.ShortToString()}: {got[n].TotalCompared}/{got[n].TotalFailed}, full {want[n].TotalCompared}/{want[n].TotalFailed}");
                    }
                    var edgesFull = JawEdgeCheck.For(full, beam);
                    var edgesPartial = JawEdgeCheck.For(partial.Grid, beam);
                    if (edgesFull.Checked != edgesPartial.Checked || edgesFull.NotCheckedReason != edgesPartial.NotCheckedReason
                        || !edgesFull.Edges.SequenceEqual(edgesPartial.Edges))
                        failures.Add($"edge check, {t}: {edgesPartial.Summary}, full {edgesFull.Summary}");
                }
            }
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(20)));
        }

        [TestMethod]
        public void TheHandWrittenEngineReadsNothingOutsideTheRegion()
        {
            // The reference engine evaluates every sample point, boxed or not, so it is the strictest check of the region.
            var source = DoseGridReader.Read(File("P1Ref.X-100A-10.0-1"));
            var full = DoseGridReader.Read(File("P1 2 mm.X-100A-10.0-0.2"));
            var runs = Criteria.Select(c => new ComparisonRun(c.Dta, c.Global, source.MaxPointDose.Dose)).ToArray();
            using var partial = BatchComparer.OpenTarget(File("P1 2 mm.X-100A-10.0-0.2"), source, runs, null);
            var engine = new HandWrittenComparer();
            var want = engine.CompareAll(source, full, runs);
            var got = engine.CompareAll(source, partial.Grid, runs);
            for (int n = 0; n < runs.Length; n++)
            {
                Assert.AreEqual(want[n].TotalCompared, got[n].TotalCompared, runs[n].Dta.ShortToString());
                Assert.AreEqual(want[n].TotalFailed, got[n].TotalFailed, runs[n].Dta.ShortToString());
            }
        }

        [TestMethod]
        public void ABatchWithPartialTargetsGivesTheSameRows()
        {
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            var files = new[] { "RD.UnitTest.P1Ref.X-100A-10.0-1.dcm", "RD.UnitTest.P1 5 mm.X-100A-10.0-5.dcm", "RD.UnitTest.P1Ref.e-100A-10.0-1-13.dcm" };
            var pairs = DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.Identifier).Pairs
                .Where(p => files.Contains(Path.GetFileName(p.Source.Dose.SourcePath))).ToArray();
            var criteria = Criteria.Select(c => new Criterion(c.Dta, c.Global)).ToArray();
            var full = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer()).CompareMany(pairs, criteria, maximum: MaximumKind.CaxMax).Outcomes;
            var part = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer()) { PartialTargetReads = true }.CompareMany(pairs, criteria, maximum: MaximumKind.CaxMax).Outcomes;
            Assert.AreEqual(full.Count, part.Count);
            for (int i = 0; i < full.Count; i++)
            {
                Assert.AreEqual(full[i].Comparison!.TotalCompared, part[i].Comparison!.TotalCompared, full[i].ToString());
                Assert.AreEqual(full[i].Comparison!.TotalFailed, part[i].Comparison!.TotalFailed, full[i].ToString());
                Assert.AreEqual(full[i].EdgeCheck, part[i].EdgeCheck);
                Assert.AreEqual(full[i].Maximum, part[i].Maximum);
                Assert.IsNull(full[i].TargetFractionRead);
                Assert.IsNotNull(part[i].TargetFractionRead);
                Console.WriteLine($"{part[i].Key}: {part[i].TargetFractionRead:P1} of the target read");
            }

            // A gamma criterion checks and flips the whole evaluation grid, so its pairs read the target whole.
            var withGamma = criteria.Append(Criterion.ForGamma(new GammaOptions(3, 3, LowerPercentDoseCutoff: 10, MaxGamma: 1.1, SkipOncePassed: true, RandomSubset: 200, RandomSeed: 1))).ToArray();
            var gammaRun = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer()) { PartialTargetReads = true }.CompareMany(pairs.Take(1), withGamma).Outcomes;
            Assert.IsTrue(gammaRun.All(o => o.Succeeded && o.TargetFractionRead is null));
        }
    }
}
