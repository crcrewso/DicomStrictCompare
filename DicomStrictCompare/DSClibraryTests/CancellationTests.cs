using System;
using System.Diagnostics;
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
    /// <summary>
    /// A cancel must stop a comparison that is under way, not wait for it. Cody, 2026-11-08: "Cancel
    /// should also work faster than it is currently" - before this the token was checked only between
    /// pairs, so up to four comparisons of two to four seconds each ran to completion first.
    /// </summary>
    [TestClass]
    public class CancellationTests
    {
        private static DoseMatrixOptimal LoadWithEvilDicom(string path) =>
            new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path)));

        /// <summary>
        /// The largest files first: a cancel test needs comparisons that are still running when the
        /// cancel arrives, and the smallest reference grids finish in well under a second.
        /// </summary>
        private static MatchedDosePair[] SelfPairs(int take)
        {
            var doses = RTFolderReader.Read(TestData.ResourcesDirectory!).Doses.ToArray();
            return DoseMatcher.Match(doses, doses, DoseMatchStrategy.PlanAndBeamUid)
                .Pairs
                .OrderByDescending(p => new System.IO.FileInfo(p.Source.Dose.SourcePath).Length)
                .ThenBy(p => p.Source.Dose.SourcePath, StringComparer.Ordinal)
                .Take(take)
                .ToArray();
        }

        [TestMethod]
        public void APreCancelledTokenStopsTheEngineBeforeItStarts()
        {
            var pairs = SelfPairs(1);
            var source = LoadWithEvilDicom(pairs[0].Source.Dose.SourcePath);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var timer = Stopwatch.StartNew();
            Assert.ThrowsExactly<OperationCanceledException>(() =>
                new HandWrittenComparer().CompareRelative(source, source, new Dta(true, 0.10, 0.02, 2.0), cts.Token));
            timer.Stop();
            Assert.IsTrue(timer.ElapsedMilliseconds < 500, $"took {timer.ElapsedMilliseconds} ms");
        }

        [TestMethod]
        public void TheThreeArgumentOverloadsStillRunWithoutAToken()
        {
            // The old entry points are unchanged for callers that have no token (DSCcore among them).
            var pairs = SelfPairs(1);
            var source = LoadWithEvilDicom(pairs[0].Source.Dose.SourcePath);
            var result = new HandWrittenComparer().CompareRelative(source, source, new Dta(true, 0.95, 0.02, 2.0));
            Assert.AreEqual(0, result.TotalFailed);
        }

        [TestMethod]
        public void CancellingABatchUnderWayStopsItWithinASecond()
        {
            // Cancel once a comparison has started, not after a fixed sleep: on a fast, warm machine a fixed second
            // could outlast the batch (crane, 2026-11-15). Then measure from the cancel to the batch ending.
            var pairs = SelfPairs(4);
            var started = new SemaphoreSlim(0);
            var comparer = new BatchComparer(LoadWithEvilDicom, new SignallingComparer(started));
            using var cts = new CancellationTokenSource();

            var batch = Task.Run(() => comparer.Compare(pairs, new Dta(true, 0.10, 0.02, 2.0), true, 4, cts.Token));
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(60)), "no comparison started");
            var fromCancel = Stopwatch.StartNew();
            cts.Cancel();

            var ex = Assert.ThrowsExactly<AggregateException>(() => batch.Wait(TimeSpan.FromSeconds(30)));
            fromCancel.Stop();

            Assert.IsInstanceOfType<OperationCanceledException>(ex.InnerExceptions.Single(),
                "the batch ends with the cancel, not with a report of failed pairs");
            Assert.IsTrue(fromCancel.ElapsedMilliseconds < 1000,
                $"the batch took {fromCancel.ElapsedMilliseconds} ms to stop after the cancel");
        }

        /// <summary>The hand-written engine, saying when a comparison begins.</summary>
        private sealed class SignallingComparer(SemaphoreSlim started) : DoseComparer
        {
            private readonly HandWrittenComparer _inner = new();
            public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta) => _inner.CompareAbsolute(source, target, dta);
            public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta) => _inner.CompareRelative(source, target, dta);
            public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, CancellationToken cancellation)
            {
                started.Release();
                return _inner.CompareAbsolute(source, target, dta, referenceMaximum, cancellation);
            }
            public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, CancellationToken cancellation)
            {
                started.Release();
                return _inner.CompareRelative(source, target, dta, referenceMaximum, cancellation);
            }
        }
    }
}
