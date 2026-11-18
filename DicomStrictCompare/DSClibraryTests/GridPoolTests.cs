using System;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Grids reused between pairs (WC2, 2026-11-18): the pool hands back exact lengths within its cap, and a pooled
    /// batch gives the same rows as one that allocates every grid.
    /// </summary>
    [TestClass]
    public class GridPoolTests
    {
        [TestMethod]
        public void ARentedArrayComesBackAtTheSameLength()
        {
            var pool = new GridPool(1 << 20);
            var a = pool.Rent(1000);
            pool.Return(a);
            Assert.AreSame(a, pool.Rent(1000));
            Assert.AreNotSame(a, pool.Rent(999));
            Assert.AreEqual(1, pool.Hits);
            Assert.AreEqual(2, pool.Misses);
        }

        [TestMethod]
        public void TheOldestGoWhenTheCapIsPassed()
        {
            var pool = new GridPool(2 * 100 * sizeof(double));
            double[] first = new double[100], second = new double[100], third = new double[100];
            pool.Return(first);
            pool.Return(second);
            pool.Return(third);
            Assert.AreEqual(2 * 100 * sizeof(double), pool.Bytes);
            var kept = new[] { pool.Rent(100), pool.Rent(100) };
            CollectionAssert.DoesNotContain(kept, first);
            CollectionAssert.Contains(kept, second);
            CollectionAssert.Contains(kept, third);
        }

        [TestMethod]
        public void KeptArraysOfAnotherLengthMakeRoomForANewOne()
        {
            // WC2 rerun, 2026-11-18: with mixed grid sizes, idle arrays of the wrong length plus the ones in use reached
            // twice the cap and the heap limit threw OutOfMemoryException.
            var pool = new GridPool(2 * 100 * sizeof(double));
            pool.Return(pool.Rent(100));
            pool.Return(pool.Rent(100));
            Assert.AreEqual(100 * sizeof(double), pool.Bytes);
            var a = pool.Rent(90);
            var b = pool.Rent(90);
            Assert.IsTrue(pool.Bytes + pool.Lent <= pool.MaxBytes || pool.Bytes == 0, $"kept {pool.Bytes} + lent {pool.Lent} over {pool.MaxBytes}");
            Assert.AreEqual(0, pool.Bytes);
            pool.Return(a);
            pool.Return(b);
            Assert.AreEqual(0, pool.Lent);
        }

        [TestMethod]
        public void AnArrayLargerThanTheCapIsNotKept()
        {
            var pool = new GridPool(10 * sizeof(double));
            pool.Return(new double[11]);
            Assert.AreEqual(0, pool.Bytes);
        }

        [TestMethod]
        public void APooledBatchGivesTheSameRowsAndReusesGrids()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            var pairs = DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.Identifier).Pairs.ToArray();
            var criteria = new[] { new Criterion(new Dta(true, 0.10, 0.02, 2, relative: true), true), new Criterion(new Dta(true, 0.10, 0.001, 0.5, relative: false), false) };
            foreach (bool partial in new[] { false, true })
            {
                var plain = new BatchComparer(DoseGridReader.Read, new MultiThreadComparer()) { PartialTargetReads = partial }
                    .CompareMany(pairs, criteria, degreeOfParallelism: 1).Outcomes;
                var comparer = new BatchComparer(DoseGridReader.ReadRented, new MultiThreadComparer()) { PartialTargetReads = partial };
                var pooled = comparer.CompareMany(pairs, criteria, degreeOfParallelism: 1).Outcomes;
                Assert.AreEqual(plain.Count, pooled.Count);
                for (int i = 0; i < plain.Count; i++)
                {
                    Assert.AreEqual(plain[i].Key, pooled[i].Key);
                    Assert.AreEqual(plain[i].Failure, pooled[i].Failure, plain[i].Key.ToString());
                    if (plain[i].Comparison is { } a)
                    {
                        var b = pooled[i].Comparison!;
                        Assert.IsTrue(a.TotalCount == b.TotalCount && a.TotalCompared == b.TotalCompared && a.TotalFailed == b.TotalFailed,
                            $"partial {partial}, {plain[i].Key}: {b.TotalCompared}/{b.TotalFailed}, unpooled {a.TotalCompared}/{a.TotalFailed}");
                    }
                    Assert.AreEqual(plain[i].EdgeCheck, pooled[i].EdgeCheck);
                }
                Console.WriteLine($"partial {partial}: {pairs.Length} pairs, pool hits {comparer.Pool!.Hits}, misses {comparer.Pool.Misses}");
                Assert.IsTrue(comparer.Pool.Hits > 0, "no grid was reused");
            }
        }
    }
}
