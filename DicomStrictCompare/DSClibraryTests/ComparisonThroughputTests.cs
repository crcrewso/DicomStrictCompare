using System;
using System.Diagnostics;
using DSClibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Measures what the present comparison actually costs, so that any talk of accelerating it rests
    /// on numbers. Reports rather than asserts a threshold: a timing assertion would fail on a busy
    /// machine and tell nobody anything.
    /// </summary>
    [TestClass]
    public class ComparisonThroughputTests
    {
        private static DoseMatrixOptimal Load(byte[] bytes) =>
            new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(bytes)));

        [TestMethod]
        public void ReportComparisonThroughput()
        {
            var source = Load(Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1);
            var target = Load(Properties.Resources.RD_UnitTest_P1Ref_F_100A_10_0_1);
            var mathematics = new X86Mathematics();

            Console.WriteLine($"grid {source.DimensionX}x{source.DimensionY}x{source.DimensionZ} "
                              + $"= {source.Length:N0} voxels, {source.Length * 8 / 1024 / 1024} MB as double[]");

            // Three configurations, to separate the cost of the dose test from the cost of the DTA
            // rescue that runs only on points which fail it.
            foreach (var (label, dta) in new[]
                     {
                         ("dose test only, no DTA", new Dta(true, 0.1, 0.02, 0.0)),
                         ("with 2 mm DTA",          new Dta(true, 0.1, 0.02, 2.0)),
                         ("with 3 mm DTA",          new Dta(true, 0.1, 0.02, 3.0)),
                     })
            {
                var timer = Stopwatch.StartNew();
                SingleComparison result = mathematics.CompareRelative(source, target, dta);
                timer.Stop();

                double seconds = timer.Elapsed.TotalSeconds;
                Console.WriteLine(
                    $"  {label,-24} {seconds,6:0.00}s  "
                    + $"{result.TotalCompared:N0} compared, {result.TotalFailed:N0} failed  "
                    + $"=> {result.TotalCompared / seconds / 1e6:0.0}M points/s");
            }

            Assert.IsTrue(true, "a report, not a gate");
        }

        [TestMethod]
        public void ReportAllocationCostOfAComparison()
        {
            // GetPointDose returns a DoseValue per call, and the engine calls it up to eight times per
            // evaluated point. If DoseValue is a reference type that is tens of millions of heap
            // allocations per comparison, which would dominate everything else - and would make
            // allocation, not arithmetic, the thing worth removing first.
            var source = Load(Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1);
            var target = Load(Properties.Resources.RD_UnitTest_P1Ref_F_100A_10_0_1);
            var mathematics = new X86Mathematics();
            var dta = new Dta(true, 0.1, 0.02, 2.0);

            // Warm up, so the measurement is not dominated by first-call overhead.
            _ = mathematics.CompareRelative(source, target, new Dta(true, 0.9, 0.02, 0));

            long before = GC.GetTotalAllocatedBytes(precise: true);
            int gen0Before = GC.CollectionCount(0);
            var result = mathematics.CompareRelative(source, target, dta);
            long after = GC.GetTotalAllocatedBytes(precise: true);
            int gen0After = GC.CollectionCount(0);

            double megabytes = (after - before) / 1024.0 / 1024.0;
            Console.WriteLine($"  allocated {megabytes:N0} MB over {result.TotalCompared:N0} compared points");
            Console.WriteLine($"  = {(after - before) / (double)result.TotalCompared:N0} bytes per point");
            Console.WriteLine($"  gen0 collections: {gen0After - gen0Before}");
            Console.WriteLine($"  DoseValue is a {(typeof(EvilDICOM.RT.DoseValue).IsValueType ? "STRUCT" : "CLASS")}");

            Assert.IsTrue(true, "a report, not a gate");
        }

        [TestMethod]
        public void ReportCostOfTheInterpolationDecision()
        {
            // GetPointDose decides whether to interpolate with `(x - X0) / XRes % 1 != 0`, three times
            // per call - once per axis - and the engine calls it up to eight times per point. That is
            // a floating point remainder tens of millions of times per comparison, and fmod is far
            // dearer than a divide. This measures whether that is where the time goes.
            const int iterations = 20_000_000;
            double x0 = -99.5093197, res = 1.0;
            var samples = new double[4096];
            var rng = new Random(42);
            for (int i = 0; i < samples.Length; i++) samples[i] = x0 + rng.Next(0, 200) * res;

            int sink = 0;
            var asWritten = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                double x = samples[i & 4095];
                if ((x - x0) / res % 1 != 0.0) sink++;
            }
            asWritten.Stop();

            var viaTruncation = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                double x = samples[i & 4095];
                double steps = (x - x0) / res;
                if (steps != (int)steps) sink++;
            }
            viaTruncation.Stop();

            Console.WriteLine($"  {iterations:N0} interpolation decisions");
            Console.WriteLine($"    as written, fmod   : {asWritten.ElapsedMilliseconds,6} ms");
            Console.WriteLine($"    integer truncation : {viaTruncation.ElapsedMilliseconds,6} ms");
            double ratio = asWritten.Elapsed.TotalMilliseconds
                           / Math.Max(1.0, viaTruncation.Elapsed.TotalMilliseconds);
            Console.WriteLine($"    ratio              : {ratio:0.0}x   (sink {sink})");

            Assert.IsTrue(true, "a report, not a gate");
        }

        [TestMethod]
        public void ReportCostOfTheLoopOrder()
        {
            // The engine iterates x outer, y middle, z INNERMOST, while the voxel index is
            // x + y*dimX + z*dimX*dimY - so z is the slowest-varying axis in memory and the innermost
            // loop has the largest possible stride. For a 200x201x201 grid of doubles that is
            // 200*201*8 = 321 KB per inner step, against an L3 of a few tens of megabytes, so every
            // inner iteration is a cache miss.
            //
            // This walks the same array both ways to measure what that ordering costs, with no change
            // to the engine.
            const int dimX = 200, dimY = 201, dimZ = 201;
            var grid = new double[dimX * dimY * dimZ];
            for (int i = 0; i < grid.Length; i++) grid[i] = i * 1e-7;
            Console.WriteLine($"  {grid.Length:N0} voxels, {grid.Length * 8 / 1024 / 1024} MB");

            // As the engine orders it: z innermost.
            double sum1 = 0;
            var zInner = Stopwatch.StartNew();
            for (int x = 0; x < dimX; x++)
                for (int y = 0; y < dimY; y++)
                    for (int z = 0; z < dimZ; z++)
                        sum1 += grid[x + y * dimX + z * dimX * dimY];
            zInner.Stop();

            // Memory order: x innermost, so the inner loop walks consecutive doubles.
            double sum2 = 0;
            var xInner = Stopwatch.StartNew();
            for (int z = 0; z < dimZ; z++)
                for (int y = 0; y < dimY; y++)
                    for (int x = 0; x < dimX; x++)
                        sum2 += grid[x + y * dimX + z * dimX * dimY];
            xInner.Stop();

            Console.WriteLine($"    z innermost (as written) : {zInner.ElapsedMilliseconds,5} ms");
            Console.WriteLine($"    x innermost (memory order): {xInner.ElapsedMilliseconds,5} ms");
            double ratio = zInner.Elapsed.TotalMilliseconds / Math.Max(1.0, xInner.Elapsed.TotalMilliseconds);
            Console.WriteLine($"    ratio                     : {ratio:0.0}x");
            Assert.AreEqual(sum1, sum2, 1e-6, "both orders must visit the same voxels");
        }
    }
}
