using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>The read queue (<see cref="BatchComparer.Queue"/>): K readers, M pairs held, the same rows as reading in each worker.</summary>
    [TestClass]
    public class ReadQueueTests
    {
        private static readonly string[] Files =
        {
            "RD.UnitTest.P1Ref.X-100A-10.0-1.dcm",
            "RD.UnitTest.P1 5 mm.X-100A-10.0-5.dcm",
            "RD.UnitTest.P1 2 mm.X-100A-10.0-0.2.dcm",
            "RD.UnitTest.P1Ref.e-100A-10.0-1-13.dcm",
            "RD.UnitTest.P4 E 2D.e-100A-10.0-1-12.dcm",
        };

        private static MatchedDosePair[] Pairs()
        {
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            return DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.Identifier).Pairs
                .Where(p => Files.Contains(Path.GetFileName(p.Source.Dose.SourcePath))).ToArray();
        }

        private static readonly Criterion[] Criteria =
        {
            new(new Dta(true, 0.10, 0.02, 2, relative: true), true),
            new(new Dta(true, 0.10, 0.001, 0.5, relative: false), false),
            new(new Dta(true, 0.95, 0.03, 2, relative: true), true, AppliesTo: BeamsCompared.Photon),
        };

        private static string Rows(BatchComparisonResult r) => string.Join("\n", r.Outcomes.Select(o =>
            $"{o.Key} {o.CriterionIndex} {o.Comparison?.TotalCount}/{o.Comparison?.TotalCompared}/{o.Comparison?.TotalFailed} {o.Maximum?.Dose:R} {o.EdgeCheck} {o.Failure}"));

        [TestMethod]
        public void TheQueueGivesTheSameRows()
        {
            var pairs = Pairs();
            Assert.AreEqual(Files.Length, pairs.Length);
            string expected = Rows(new BatchComparer(DoseGridReader.Read, new SingleThreadComparer()).CompareMany(pairs, Criteria, 1));
            foreach (var (readers, held, workers, partial) in new[] { (1, 1, 1, false), (2, 2, 3, false), (4, 1, 2, true), (1, 3, 1, true), (3, 3, 4, true) })
            {
                var batch = new BatchComparer(DoseGridReader.Read, new SingleThreadComparer())
                    { Queue = new BatchComparer.ReadQueue(readers, held), PartialTargetReads = partial };
                Assert.AreEqual(expected, Rows(batch.CompareMany(pairs, Criteria, workers)), $"{readers} readers, {held} held, {workers} workers, partial {partial}");
            }
            // And with a GPU comparer or the multi-thread one, which the queue feeds two pairs at a time.
            foreach (var engine in DoseComparerEquivalenceTests.GpuDoubles().Prepend(new MultiThreadComparer()))
                Assert.AreEqual(expected, Rows(new BatchComparer(DoseGridReader.Read, engine) { Queue = new(2, 2) }.CompareMany(pairs, Criteria)), engine.Description);
        }

        [TestMethod]
        public void AFileThatWontLoadIsAFailedRowNotALostBatch()
        {
            var pairs = Pairs();
            DoseMatrixOptimal Load(string path) => path.Contains("5 mm") ? throw new IOException("unreadable") : DoseGridReader.Read(path);
            var result = new BatchComparer(Load, new SingleThreadComparer()) { Queue = new(2, 1) }.CompareMany(pairs, Criteria, 2);
            var bad = result.Outcomes.Where(o => o.SourcePath.Contains("5 mm")).ToArray();
            Assert.IsTrue(bad.Length > 0 && bad.All(o => o.Failure == "IOException: unreadable"));
            Assert.IsTrue(result.Outcomes.Where(o => !o.SourcePath.Contains("5 mm")).All(o => o.Succeeded));
        }

        [TestMethod]
        public void CancellingAQueuedBatchStopsItWithinASecond()
        {
            var pairs = Pairs();
            var started = new SemaphoreSlim(0);
            var batch = new BatchComparer(DoseGridReader.Read, new Signalling(started)) { Queue = new(2, 2) };
            using var cts = new CancellationTokenSource();
            var run = Task.Run(() => batch.CompareMany(pairs, new[] { new Criterion(new Dta(true, 0.10, 0.02, 2, relative: true), true) }, 2, cts.Token));
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(60)), "no comparison started");
            var fromCancel = Stopwatch.StartNew();
            cts.Cancel();
            var ex = Assert.ThrowsExactly<AggregateException>(() => run.Wait(TimeSpan.FromSeconds(30)));
            Assert.IsInstanceOfType<OperationCanceledException>(ex.InnerExceptions.Single());
            Assert.IsTrue(fromCancel.ElapsedMilliseconds < 1000, $"took {fromCancel.ElapsedMilliseconds} ms after the cancel");
        }

        /// <summary>The hand-written engine, saying when a comparison begins.</summary>
        private sealed class Signalling(SemaphoreSlim started) : DoseComparer
        {
            private readonly HandWrittenComparer _inner = new();
            public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta) => _inner.CompareAbsolute(source, target, dta);
            public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta) => _inner.CompareRelative(source, target, dta);
            public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, CancellationToken cancellation)
            { started.Release(); return _inner.CompareAbsolute(source, target, dta, referenceMaximum, cancellation); }
            public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, CancellationToken cancellation)
            { started.Release(); return _inner.CompareRelative(source, target, dta, referenceMaximum, cancellation); }
        }
    }
}
