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
    }
}
