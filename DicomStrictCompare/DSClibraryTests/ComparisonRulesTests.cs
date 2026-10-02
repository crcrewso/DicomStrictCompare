using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;

namespace DSClibraryTests
{
    /// <summary>
    /// Known-answer tests on small synthetic dose grids. Each test checks one rule of the comparison, so a
    /// failure points at the rule that broke. Grids are 1 mm spacing unless stated.
    /// </summary>
    [TestClass]
    public class ComparisonRulesTests
    {
        readonly IMathematics mathematics = new X86Mathematics();

        // Dta(useMM, threshhold, tolerance, distance, relative (true = global), gamma, trim)
        static Dta Global(double tolerance, double distance = 0, double threshold = 0, bool useMM = true) =>
            new Dta(useMM, threshold, tolerance, distance, true);
        static Dta Local(double tolerance, double distance = 0, double threshold = 0, bool useMM = true) =>
            new Dta(useMM, threshold, tolerance, distance, false);

        /// <summary>A row of doses along x: dims n x 1 x 1, 1 mm spacing, starting at 0.</summary>
        static DoseMatrixOptimal Row(params double[] doses) =>
            new DoseMatrixOptimal(doses.Length, 1, 1, 0, 0, 0, 1, 1, 1, doses);

        [TestMethod]
        public void GlobalToleranceIsFractionOfReferenceMaximum()
        {
            // Reference max 100 Gy, 3 % global allows 3 Gy anywhere: 12.1 vs 15.0 differs by 2.9 Gy and passes.
            var source = Row(100, 12.1);
            var target = Row(100, 15.0);
            var result = mathematics.CompareRelative(source, target, Global(0.03));
            Assert.AreEqual(2, result.TotalCompared);
            Assert.AreEqual(0, result.TotalFailed);
        }

        [TestMethod]
        public void LocalToleranceIsFractionOfReferenceDoseAtThePoint()
        {
            // 3 % local of 12.1 Gy is 0.363 Gy, so the same 2.9 Gy difference fails.
            var source = Row(100, 12.1);
            var target = Row(100, 15.0);
            var result = mathematics.CompareAbsolute(source, target, Local(0.03));
            Assert.AreEqual(2, result.TotalCompared);
            Assert.AreEqual(1, result.TotalFailed);
        }

        [TestMethod]
        public void ToleranceBoundaryIsInclusive()
        {
            var result = mathematics.CompareRelative(Row(100, 50), Row(100, 53), Global(0.03));
            Assert.AreEqual(0, result.TotalFailed);
        }

        [TestMethod]
        public void ThresholdDependsOnlyOnReferenceDose()
        {
            // Threshold 10 % of 100 Gy. The second point's reference (50 Gy) is above it; its target (5 Gy) is
            // below it. The point must still be evaluated, and fail, in both modes.
            var source = Row(100, 50);
            var target = Row(100, 5);
            foreach (var result in new[] {
                mathematics.CompareRelative(source, target, Global(0.03, threshold: 0.10)),
                mathematics.CompareAbsolute(source, target, Local(0.03, threshold: 0.10)) })
            {
                Assert.AreEqual(2, result.TotalCompared);
                Assert.AreEqual(1, result.TotalFailed);
            }
        }

        [TestMethod]
        public void PointsBelowReferenceThresholdAreNotEvaluated()
        {
            // Second point: reference 5 Gy is below 10 % of 100 Gy, even though the target is high.
            var result = mathematics.CompareRelative(Row(100, 5), Row(100, 60), Global(0.03, threshold: 0.10));
            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual(1, result.TotalCompared);
            Assert.AreEqual(0, result.TotalFailed);
        }

        [TestMethod]
        public void GlobalWithMmDtaCanFail()
        {
            // Regression test: global + mm DTA used to pass every point that failed the dose test.
            // Middle point: reference 50, neighbours 1 mm away are 40 and 60. Target 80 is outside [40, 60].
            var source = Row(40, 50, 60);
            var target = Row(40, 80, 60);
            var result = mathematics.CompareRelative(source, target, Global(0.03, distance: 1));
            Assert.AreEqual(1, result.TotalFailed);
        }

        [TestMethod]
        public void DtaRescuesPointWithinNeighbourRange()
        {
            // Middle point fails 3 % global (55 vs 50 is 5 Gy > 1.8 Gy), but 55 lies within [40, 60].
            var source = Row(40, 50, 60);
            var target = Row(40, 55, 60);
            Assert.AreEqual(0, mathematics.CompareRelative(source, target, Global(0.03, distance: 1)).TotalFailed);
            Assert.AreEqual(0, mathematics.CompareAbsolute(source, target, Local(0.03, distance: 1)).TotalFailed);
            Assert.AreEqual(1, mathematics.CompareRelative(source, target, Global(0.03, distance: 0)).TotalFailed);
        }

        [TestMethod]
        public void UseMmFalseMeansDoseDifferenceOnly()
        {
            // Same doses as above, but with useMM false the distance must be ignored, so the point fails.
            var source = Row(40, 50, 60);
            var target = Row(40, 55, 60);
            var result = mathematics.CompareRelative(source, target, Global(0.03, distance: 1, useMM: false));
            Assert.AreEqual(1, result.TotalFailed);
        }

        [TestMethod]
        public void DtaNeighboursOutsideTheGridAreIgnoredNotWrapped()
        {
            // 3 x 2 x 1 grid. The last point of row 0 (x = 2, y = 0) fails the dose test (target 90, source 30).
            // Its real neighbours are x - 1 (20) and y + 1 (25), so it should fail. Its +x neighbour is off the
            // grid; reading it by index would wrap to the first point of row 1 (95) and wrongly rescue it.
            double[] sourceDoses = { 10, 20, 30, 95, 25, 25 };
            double[] targetDoses = { 10, 20, 90, 95, 25, 25 };
            var source = new DoseMatrixOptimal(3, 2, 1, 0, 0, 0, 1, 1, 1, sourceDoses);
            var target = new DoseMatrixOptimal(3, 2, 1, 0, 0, 0, 1, 1, 1, targetDoses);
            var result = mathematics.CompareRelative(source, target, Global(0.03, distance: 1));
            Assert.AreEqual(1, result.TotalFailed);
        }

        [TestMethod]
        public void AxialGridWithIndependentZSpacing()
        {
            // x = y = 2.5 mm, z = 3 mm. The dose rises along z only. A target shifted by one z slice (3 mm)
            // fails the dose test but passes a 3 mm DTA, which needs the z neighbour at z +- 3 mm.
            int nx = 3, ny = 3, nz = 5;
            double[] Make(Func<int, double> doseAtSlice) =>
                Enumerable.Range(0, nx * ny * nz).Select(i => doseAtSlice(i / (nx * ny))).ToArray();
            var source = new DoseMatrixOptimal(nx, ny, nz, 0, 0, 0, 2.5, 2.5, 3, Make(k => 20 + 20 * k));
            var target = new DoseMatrixOptimal(nx, ny, nz, 0, 0, 0, 2.5, 2.5, 3, Make(k => 20 + 20 * Math.Min(k + 1, nz - 1)));
            var withDta = mathematics.CompareRelative(source, target, Global(0.03, distance: 3));
            var doseOnly = mathematics.CompareRelative(source, target, Global(0.03, distance: 0));
            Assert.AreEqual(nx * ny * nz, withDta.TotalCount);
            // Slices 0-3 differ by 20 Gy (> 3 Gy); the top slice matches.
            Assert.AreEqual(nx * ny * 4, doseOnly.TotalFailed);
            // With DTA, slices 1-3 are rescued by the slice above. Slice 0 is rescued too: its +z neighbour (40)
            // equals the target. So nothing fails.
            Assert.AreEqual(0, withDta.TotalFailed);
        }

        [TestMethod]
        public void SelfComparisonCountsEveryPointAndFailsNone()
        {
            var grid = new DoseMatrixOptimal(4, 3, 2, -1.5, 2, 7, 0.5, 0.5, 2, Enumerable.Range(1, 24).Select(v => (double)v).ToArray());
            var result = mathematics.CompareRelative(grid, grid, Global(0.0, distance: 1));
            Assert.AreEqual(24, result.TotalCount);
            Assert.AreEqual(24, result.TotalCompared);
            Assert.AreEqual(0, result.TotalFailed);
        }
    }
}
