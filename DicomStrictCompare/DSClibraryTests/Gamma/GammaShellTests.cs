using System;
using System.Linq;
using DSClibrary;
using DSClibrary.Gamma;
using DSClibrary.Resampling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests.Gamma
{
    /// <summary>
    /// The port of pymedphys' gamma, checked against pymedphys' own test cases
    /// (lib/pymedphys/tests/gamma/test_gamma_shell.py, Apache-2.0) and against this library's grids.
    /// </summary>
    [TestClass]
    public class GammaShellTests
    {
        // ---- pymedphys' dummy set: get_dummy_gamma_set() ----

        /// <summary>numpy.arange(0, stop, 0.1) as numpy computes it: start + i * step.</summary>
        private static double[] Arange(int count) => Enumerable.Range(0, count).Select(i => i * 0.1).ToArray();

        private sealed class DummySet
        {
            public readonly double[][] Coords = { Arange(10), Arange(12), Arange(14) };
            public readonly int[] Shape = { 10, 12, 14 };
            public readonly double[] Reference;
            public readonly double[] Evaluation;
            public readonly double[] Expected;

            public DummySet()
            {
                int n = 10 * 12 * 14;
                Reference = new double[n];
                Evaluation = new double[n];
                Expected = new double[n];
                for (int x = 0; x < 10; x++)
                    for (int y = 0; y < 12; y++)
                        for (int z = 0; z < 14; z++)
                        {
                            int i = Index(x, y, z);
                            // reference[3:-2, 4:-2, 5:-2] = 1.015
                            if (x >= 3 && x < 8 && y >= 4 && y < 10 && z >= 5 && z < 12) Reference[i] = 1.015;
                            // evaluation[2:-2, 2:-2, 2:-2] = 1
                            if (x >= 2 && x < 8 && y >= 2 && y < 10 && z >= 2 && z < 12) Evaluation[i] = 1;
                            // expected, in the order pymedphys assigns it
                            if (x >= 2 && x < 8 && y >= 2 && y < 10 && z >= 2 && z < 12) Expected[i] = 0.4;
                            if (x >= 3 && x < 7 && y >= 3 && y < 9 && z >= 3 && z < 11) Expected[i] = 0.7;
                            if (x >= 4 && x < 6 && y >= 4 && y < 8 && z >= 4 && z < 10) Expected[i] = 1;
                            if (x >= 3 && x < 8 && y >= 4 && y < 10 && z >= 5 && z < 12) Expected[i] = 0.5;
                        }
            }

            public int Index(int x, int y, int z) => (x * 12 + y) * 14 + z;

            public GammaGrid ReferenceGrid => new(Coords, Reference);
            public GammaGrid EvaluationGrid => new(Coords, Evaluation);
        }

        private static double[] RoundedToOneDecimal(double[] values) =>
            values.Select(v => Math.Round(v, 1, MidpointRounding.ToEven)).ToArray();

        [TestMethod]
        public void RegressionOfGamma3D()
        {
            var set = new DummySet();
            var result = GammaShell.Compute(set.ReferenceGrid, set.EvaluationGrid,
                new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0));

            CollectionAssert.AreEqual(set.Shape, result.Shape);
            Assert.AreEqual(set.Reference.Length, result.Analysed, "cutoff 0 analyses every point");
            double[] gamma = RoundedToOneDecimal(result.Gamma);
            int mismatches = Enumerable.Range(0, gamma.Length).Count(i => gamma[i] != set.Expected[i]);
            Assert.AreEqual(0, mismatches, "points whose rounded gamma differs from pymedphys' expected set: "
                + string.Join(", ", Enumerable.Range(0, gamma.Length).Where(i => gamma[i] != set.Expected[i]).Take(10)
                    .Select(i => $"[{i}] {result.Gamma[i]:0.000} vs {set.Expected[i]}")));
        }

        [TestMethod]
        public void RegressionOfGamma2D()
        {
            // coords[1:], reference[5, :, :]
            var set = new DummySet();
            double[] Plane(double[] volume) => Enumerable.Range(0, 12 * 14).Select(j => volume[set.Index(5, j / 14, j % 14)]).ToArray();
            var coords = new[] { set.Coords[1], set.Coords[2] };
            var result = GammaShell.Compute(new GammaGrid(coords, Plane(set.Reference)), new GammaGrid(coords, Plane(set.Evaluation)),
                new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0));
            CollectionAssert.AreEqual(Plane(set.Expected), RoundedToOneDecimal(result.Gamma));
        }

        [TestMethod]
        public void RegressionOfGamma1D()
        {
            // coords[2], reference[5, 5, :]
            var set = new DummySet();
            double[] Line(double[] volume) => Enumerable.Range(0, 14).Select(z => volume[set.Index(5, 5, z)]).ToArray();
            var coords = new[] { set.Coords[2] };
            var result = GammaShell.Compute(new GammaGrid(coords, Line(set.Reference)), new GammaGrid(coords, Line(set.Evaluation)),
                new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0));
            CollectionAssert.AreEqual(Line(set.Expected), RoundedToOneDecimal(result.Gamma));
        }

        [TestMethod]
        public void GammaScalesWithTheCriteria()
        {
            // does_gamma_scale_as_expected(0.6, ratio, [2, 3, 4]) for ratios 10, 15, 20: scaling both
            // criteria by s divides gamma by s, within 0.1.
            var set = new DummySet();
            foreach (double ratio in new[] { 10.0, 15.0, 20.0 })
            {
                double[] Gamma(double scale) => GammaShell.Compute(set.ReferenceGrid, set.EvaluationGrid,
                    new GammaOptions(0.6 * scale * ratio, 0.6 * scale, LowerPercentDoseCutoff: 0)).Gamma;
                double[] unscaled = Gamma(1);
                foreach (double scale in new[] { 2.0, 3.0, 4.0 })
                {
                    double[] scaled = Gamma(scale);
                    double worst = Enumerable.Range(0, scaled.Length).Max(i => Math.Abs(scaled[i] - unscaled[i] / scale));
                    Assert.IsTrue(worst <= 0.1, $"ratio {ratio}, scale {scale}: worst deviation {worst:0.000}");
                }
            }
        }

        [TestMethod]
        public void TheLowerDoseCutoffLeavesPointsUnanalysed()
        {
            // test_lower_dose_threshold: NaN exactly where the reference is below 20 % of its maximum.
            double[] reference = { 0, 1, 1.9, 2, 2.1, 3, 4, 5, 10, 10 };
            var coordsRef = new[] { Enumerable.Range(0, 10).Select(i => (double)i).ToArray() };
            double[] evaluation = Enumerable.Repeat(10.0, 12).ToArray();
            var coordsEval = new[] { Enumerable.Range(0, 12).Select(i => (double)(i - 4)).ToArray() };

            var result = GammaShell.Compute(new GammaGrid(coordsRef, reference), new GammaGrid(coordsEval, evaluation),
                new GammaOptions(10, 1));

            bool[] expectedNan = reference.Select(r => r < 0.2 * 10).ToArray();
            CollectionAssert.AreEqual(expectedNan, result.Gamma.Select(double.IsNaN).ToArray());
            Assert.AreEqual(7, result.Analysed);
            Assert.AreEqual(2.0, result.LowerDoseCutoff);
        }

        [TestMethod]
        public void ShellPointsAreNoFurtherApartThanTheStep()
        {
            // test_coords_stepsize: the largest nearest-neighbour gap on a 3D shell of radius 1 at
            // step 0.1 is at most the step and more than 90 % of it.
            double[][] shell = ShellCoordinates.Calculate(1, 3, 0.1);
            int n = shell[0].Length;
            double largest = 0;
            for (int i = 0; i < n; i++)
            {
                double nearest = double.PositiveInfinity;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    double d = Math.Sqrt(Sq(shell[0][i] - shell[0][j]) + Sq(shell[1][i] - shell[1][j]) + Sq(shell[2][i] - shell[2][j]));
                    if (d > 0 && d < nearest) nearest = d;
                }
                largest = Math.Max(largest, nearest);
            }
            Assert.IsTrue(largest <= 0.1, $"largest gap {largest}");
            Assert.IsTrue(largest > 0.09, $"largest gap {largest}");
        }

        [TestMethod]
        public void ADistanceOfZeroIsOnePoint()
        {
            foreach (int dims in new[] { 1, 2, 3 })
            {
                double[][] shell = ShellCoordinates.Calculate(0, dims, 0.1);
                Assert.AreEqual(dims, shell.Length);
                foreach (double[] axis in shell) { Assert.AreEqual(1, axis.Length); Assert.AreEqual(0, axis[0]); }
            }
        }

        // ---- this library's grids ----

        [TestMethod]
        public void AShiftedRampGivesTheGammaTheFormulaPredicts()
        {
            // A 2D ramp on a 1 mm grid, the evaluation the same ramp shifted by 1 mm in x. Gamma trades
            // dose against distance: along the gradient g = (1, 0.5) Gy/mm the dose difference at
            // displacement u is 1 - |g| u, so gamma at 3 %/3 mm (normalised to the maximum, 105 Gy) is
            // the minimum over u of sqrt(((1 - |g| u) / 105 / 0.03)^2 + (u / 3)^2). The shells sample
            // displacements every 0.3 mm in all directions, so the found value sits at or a little
            // above that minimum, and is the same at every interior point. (A first draft of this
            // test expected 1/3, the distance term alone; the engine was right and the draft wrong.)
            double[] xs = Enumerable.Range(0, 41).Select(i => (double)i).ToArray();
            double[] ys = Enumerable.Range(0, 31).Select(i => (double)i).ToArray();
            double Ramp(double x, double y) => 50 + x + 0.5 * y;
            double[] reference = ys.SelectMany(y => xs.Select(x => Ramp(x, y))).ToArray();
            double[] evaluation = ys.SelectMany(y => xs.Select(x => Ramp(x - 1, y))).ToArray();

            var result = GammaShell.Compute(new GammaGrid(new[] { ys, xs }, reference), new GammaGrid(new[] { ys, xs }, evaluation),
                new GammaOptions(3, 3, LowerPercentDoseCutoff: 0));

            double normalisation = Ramp(40, 30);
            double gradient = Math.Sqrt(1 + 0.25);
            double predicted = Enumerable.Range(0, 2001).Select(k => k * 0.001)
                .Min(u => Math.Sqrt(Sq((1 - gradient * u) / normalisation / 0.03) + Sq(u / 3)));

            var interior = new System.Collections.Generic.List<double>();
            for (int j = 2; j < ys.Length - 2; j++)
                for (int i = 2; i < xs.Length - 2; i++)
                    interior.Add(result.Gamma[j * xs.Length + i]);
            Assert.IsTrue(interior.All(g => g >= predicted - 1e-9 && g <= predicted + 0.02),
                $"interior gamma from {interior.Min():0.0000} to {interior.Max():0.0000}, predicted minimum {predicted:0.0000}");
            Assert.IsTrue(interior.Max() - interior.Min() < 1e-9, "the same everywhere on a uniform ramp");
            Assert.AreEqual(100, result.PassRate, 1e-9);
        }

        [TestMethod]
        public void ADescendingEvaluationAxisGivesTheSameGamma()
        {
            var set = new DummySet();
            var ascending = GammaShell.Compute(set.ReferenceGrid, set.EvaluationGrid, new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0));

            // Reverse the z axis of the evaluation and its values along z.
            double[] zDesc = set.Coords[2].Reverse().ToArray();
            double[] flipped = GammaShell.FlipAlong(set.Evaluation, set.Shape, 2);
            var descending = GammaShell.Compute(set.ReferenceGrid,
                new GammaGrid(new[] { set.Coords[0], set.Coords[1], zDesc }, flipped), new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0));

            CollectionAssert.AreEqual(ascending.Gamma, descending.Gamma);
        }

        [TestMethod]
        public void ADoseAgainstItselfIsZeroEverywhereAnalysed()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            string path = TestData.DoseFiles.First(f => f.Contains("P1 5 mm.X", StringComparison.Ordinal));
            var dose = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path)));
            var result = GammaShell.Compare(dose, dose, new GammaOptions(3, 3, LowerPercentDoseCutoff: 10, MaxGamma: 1.1));

            Assert.AreEqual(dose.DoseValues.Length, result.Gamma.Length);
            CollectionAssert.AreEqual(new[] { dose.DimensionZ, dose.DimensionY, dose.DimensionX }, result.Shape);
            Assert.IsTrue(result.Analysed > 0);
            // Interpolating at a grid node weighs the node at 1 within an ulp of the spacing, so the
            // self-gamma is zero to about 1e-15 rather than exactly zero, as in pymedphys.
            Assert.IsTrue(result.Gamma.Where(g => !double.IsNaN(g)).All(g => g < 1e-9), $"max gamma {result.MaxGammaFound}");
            Assert.AreEqual(100, result.PassRate);
            Assert.AreEqual(0, result.OutsideEvaluationGrid);
            Assert.AreEqual(dose.DoseValues.Max(), result.GlobalNormalisation);
        }

        [TestMethod]
        public void MaxGammaCapsTheValuesWithoutChangingWhoPasses()
        {
            // The reference block against an evaluation of zeros: every point in the block fails by
            // a dose difference of 100 %, far beyond the cap; the points outside it pass at 0.
            var set = new DummySet();
            var zeros = new GammaGrid(set.Coords, new double[set.Reference.Length]);
            var full = GammaShell.Compute(set.ReferenceGrid, zeros, new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0));
            var capped = GammaShell.Compute(set.ReferenceGrid, zeros, new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0, MaxGamma: 1.1));
            Assert.AreEqual(full.Passed, capped.Passed);
            Assert.AreEqual(full.Analysed, capped.Analysed);
            Assert.IsTrue(capped.Gamma.All(g => g <= 1.1));
            Assert.AreEqual(1.1, capped.MaxGammaFound);
            Assert.IsTrue(full.MaxGammaFound > 30, $"uncapped maximum {full.MaxGammaFound:0.0}");
            int inBlock = set.Reference.Count(r => r > 0);
            Assert.AreEqual(set.Reference.Length - inBlock, full.Passed);
        }

        [TestMethod]
        public void TwoResolutionsOfTheSamePlanCompareInReasonableTime()
        {
            // The 5 mm grid as reference against the 2 mm grid of the same field; a size of job the
            // tool will meet. The numbers are printed for the record, only completion is asserted.
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            DoseMatrixOptimal Load(string fragment) => new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(
                TestData.DoseFiles.First(f => f.Contains(fragment, StringComparison.Ordinal)))));
            var coarse = Load("P1 5 mm.X");
            var fine = Load("P1 2 mm.X");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = GammaShell.Compare(coarse, fine, new GammaOptions(3, 3, LowerPercentDoseCutoff: 10, MaxGamma: 1.1));
            watch.Stop();
            Console.WriteLine($"5 mm vs 2 mm, {result}: {result.Analysed} points in {watch.Elapsed.TotalSeconds:0.0} s "
                + $"({coarse.DimensionX}x{coarse.DimensionY}x{coarse.DimensionZ} reference)");

            Assert.IsTrue(result.Analysed > 1000);
            Assert.IsTrue(result.PassRate >= 0 && result.PassRate <= 100);
            Assert.IsTrue(result.Gamma.Where(g => !double.IsNaN(g)).All(double.IsFinite));

            // And the other way round: the 2 mm grid as reference, a million points before the cutoff.
            watch.Restart();
            var reverse = GammaShell.Compare(fine, coarse, new GammaOptions(3, 3, LowerPercentDoseCutoff: 10, MaxGamma: 1.1));
            watch.Stop();
            Console.WriteLine($"2 mm vs 5 mm, {reverse}: {reverse.Analysed} points in {watch.Elapsed.TotalSeconds:0.0} s "
                + $"({fine.DimensionX}x{fine.DimensionY}x{fine.DimensionZ} reference)");
            Assert.IsTrue(reverse.Analysed > result.Analysed);
        }

        [TestMethod]
        public void TheOptionsAreChecked()
        {
            var set = new DummySet();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GammaOptions(3, 3, MaxGamma: 1).Validate());
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GammaOptions(0, 3).Validate());
            Assert.ThrowsExactly<ArgumentException>(() => GammaShell.Compute(set.ReferenceGrid,
                new GammaGrid(new[] { set.Coords[1], set.Coords[2] }, new double[12 * 14]), new GammaOptions(3, 0.3)));
            var withNan = (double[])set.Reference.Clone();
            withNan[0] = double.NaN;
            Assert.ThrowsExactly<ArgumentException>(() => GammaShell.Compute(new GammaGrid(set.Coords, withNan), set.EvaluationGrid, new GammaOptions(3, 0.3)));
            var excluded = GammaShell.Compute(new GammaGrid(set.Coords, withNan), set.EvaluationGrid, new GammaOptions(3, 0.3, LowerPercentDoseCutoff: 0, ExcludeNanReference: true));
            Assert.IsTrue(double.IsNaN(excluded.Gamma[0]));
            Assert.AreEqual(set.Reference.Length - 1, excluded.Analysed);
        }

        [TestMethod]
        public void APlaneOfADoseGridIsThatPlane()
        {
            var values = Enumerable.Range(0, 3 * 4 * 5).Select(i => (double)i).ToArray();
            var dose = new DoseMatrixOptimal(3, 4, 5, -1, -2, -3, 1, 1, 1, values); // x fastest
            var plane = GammaGrid.PlaneOf(dose, GridAxis.Z, 2);
            CollectionAssert.AreEqual(new[] { 4, 3 }, plane.Shape);
            Assert.AreEqual(dose.DoseValues[DoseMatrixOptimal.LatticeXYZToIndex(1, 3, 2, 3, 4)], plane.Values[3 * 3 + 1]);
            var planeY = GammaGrid.PlaneOf(dose, GridAxis.Y, 1);
            Assert.AreEqual(dose.DoseValues[DoseMatrixOptimal.LatticeXYZToIndex(2, 1, 4, 3, 4)], planeY.Values[4 * 3 + 2]);
            var planeX = GammaGrid.PlaneOf(dose, GridAxis.X, 0);
            Assert.AreEqual(dose.DoseValues[DoseMatrixOptimal.LatticeXYZToIndex(0, 2, 3, 3, 4)], planeX.Values[3 * 4 + 2]);
        }

        [TestMethod]
        public void TheSearchScheduleLandsOnTheCriterionAndTheEnd()
        {
            var schedule = GammaShell.SearchSchedule(0, 1.0, 0.3, new GammaOptions(3, 0.3));
            Assert.AreEqual(0, schedule[0].Distance);
            Assert.IsTrue(schedule.Any(s => s.Distance == 0.3), "the distance criterion itself is searched");
            Assert.AreEqual(1.0, schedule[^1].Distance, 1e-12, "the end point is searched");
            Assert.IsTrue(schedule.All(s => Math.Abs(s.Step - 0.03) < 1e-12), "without a max gamma the step is constant");

            var growing = GammaShell.SearchSchedule(0, 10, 1, new GammaOptions(3, 1, MaxGamma: 2));
            Assert.IsTrue(growing[^1].Step > growing[0].Step, "with a max gamma the step grows with the radius");
        }

        private static double Sq(double v) => v * v;
    }
}
