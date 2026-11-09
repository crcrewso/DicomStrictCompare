using System;
using System.Linq;
using DSClibrary;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// The resampler against grids whose right answer is known: a linear field, which trilinear
    /// interpolation reproduces exactly at every point, and the reference files, where a coarse node
    /// must read what the engine's own interpolation reads at that point.
    /// </summary>
    [TestClass]
    public class GridResamplerTests
    {
        /// <summary>A grid whose value is 1 + 2x + 3y + 5z, so any trilinear sample is exact.</summary>
        private static DoseMatrixOptimal Ramp(int nx, int ny, int nz, double res, double x0 = -10, double y0 = -20, double z0 = -30)
        {
            var values = new double[nx * ny * nz];
            for (int k = 0; k < nz; k++)
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                        values[i + j * nx + k * nx * ny] = Field(x0 + i * res, y0 + j * res, z0 + k * res);
            return new DoseMatrixOptimal(nx, ny, nz, x0, y0, z0, res, res, res, values);
        }

        private static double Field(double x, double y, double z) => 1 + 2 * x + 3 * y + 5 * z;

        [TestMethod]
        public void OneMillimetreToFiveKeepsTheOriginAndCoversTheExtentInWholeSteps()
        {
            var fine = Ramp(41, 36, 31, 1.0);            // 40, 35, 30 mm extents
            var coarse = GridResampler.Resample(fine, 5.0);

            Assert.AreEqual(9, coarse.DimensionX, "0..40 in steps of 5");
            Assert.AreEqual(8, coarse.DimensionY, "0..35 in steps of 5");
            Assert.AreEqual(7, coarse.DimensionZ, "0..30 in steps of 5");
            Assert.AreEqual(fine.X0, coarse.X0);
            Assert.AreEqual(fine.Y0, coarse.Y0);
            Assert.AreEqual(fine.Z0, coarse.Z0);
            Assert.AreEqual(5.0, coarse.XRes);
            Assert.AreEqual(fine.XMax, coarse.XMax, 1e-9, "40 is a whole number of steps");
            Assert.AreEqual(fine.ZMax, coarse.ZMax, 1e-9);
        }

        [TestMethod]
        public void AnExtentThatIsNotAWholeNumberOfStepsStopsInsideTheSource()
        {
            var fine = Ramp(42, 42, 42, 1.0);            // 41 mm: 5 mm steps reach 40
            var coarse = GridResampler.Resample(fine, 5.0);
            Assert.AreEqual(9, coarse.DimensionX);
            Assert.AreEqual(fine.X0 + 40, coarse.XMax, 1e-9);
            Assert.IsTrue(coarse.XMax <= fine.XMax);
        }

        [TestMethod]
        public void CoincidentNodesAreCopiedExactly()
        {
            var fine = Ramp(41, 41, 41, 1.0);
            var coarse = GridResampler.Resample(fine, 5.0);
            for (int k = 0; k < coarse.DimensionZ; k++)
                for (int j = 0; j < coarse.DimensionY; j++)
                    for (int i = 0; i < coarse.DimensionX; i++)
                    {
                        double expected = fine.DoseValues[(i * 5) + (j * 5) * 41 + (k * 5) * 41 * 41];
                        double actual = coarse.DoseValues[i + j * coarse.DimensionX + k * coarse.DimensionX * coarse.DimensionY];
                        Assert.AreEqual(expected, actual, $"node ({i},{j},{k}) should be a copy, not an interpolation");
                    }
        }

        [TestMethod]
        public void TwoMillimetreToFiveInterpolatesALinearFieldExactly()
        {
            // 5 is not a multiple of 2, so every other coarse node lies between source nodes.
            var fine = Ramp(31, 31, 31, 2.0);            // 60 mm extents
            var coarse = GridResampler.Resample(fine, 5.0);
            Assert.AreEqual(13, coarse.DimensionX, "0..60 in steps of 5");

            for (int k = 0; k < coarse.DimensionZ; k++)
                for (int j = 0; j < coarse.DimensionY; j++)
                    for (int i = 0; i < coarse.DimensionX; i++)
                    {
                        double expected = Field(coarse.X0 + i * 5, coarse.Y0 + j * 5, coarse.Z0 + k * 5);
                        double actual = coarse.DoseValues[i + j * coarse.DimensionX + k * coarse.DimensionX * coarse.DimensionY];
                        Assert.AreEqual(expected, actual, 1e-9, $"node ({i},{j},{k})");
                    }
        }

        [TestMethod]
        public void TwoPointFiveMillimetreToFiveIsADecimation()
        {
            var fine = Ramp(21, 21, 21, 2.5);
            var coarse = GridResampler.Resample(fine, 5.0);
            Assert.AreEqual(11, coarse.DimensionX);
            Assert.AreEqual(fine.DoseValues[2 + 4 * 21 + 6 * 21 * 21], coarse.DoseValues[1 + 2 * 11 + 3 * 11 * 11]);
        }

        [TestMethod]
        public void ResamplingToTheOwnSpacingIsTheIdentity()
        {
            var fine = Ramp(11, 12, 13, 2.0);
            var same = GridResampler.Resample(fine, 2.0);
            CollectionAssert.AreEqual(fine.DoseValues, same.DoseValues);
        }

        [TestMethod]
        public void AnisotropicSpacingIsHonouredPerAxis()
        {
            var fine = Ramp(21, 21, 21, 1.0);
            var coarse = GridResampler.Resample(fine, 5.0, 2.0, 4.0);
            Assert.AreEqual(5, coarse.DimensionX);
            Assert.AreEqual(11, coarse.DimensionY);
            Assert.AreEqual(6, coarse.DimensionZ);
            Assert.AreEqual(Field(fine.X0 + 10, fine.Y0 + 4, fine.Z0 + 8), coarse.DoseValues[2 + 2 * 5 + 2 * 5 * 11], 1e-9);
        }

        [TestMethod]
        public void TheMaximumOfAResampleNeverExceedsTheSourceMaximum()
        {
            var rng = new Random(7);
            var values = Enumerable.Range(0, 21 * 21 * 21).Select(_ => rng.NextDouble() * 1000).ToArray();
            var fine = new DoseMatrixOptimal(21, 21, 21, 0, 0, 0, 1, 1, 1, values);
            var coarse = GridResampler.Resample(fine, 3.0);
            Assert.IsTrue(coarse.MaxPointDose.Dose <= fine.MaxPointDose.Dose);
            Assert.IsTrue(coarse.DoseValues.Min() >= fine.DoseValues.Min());
        }

        [TestMethod]
        public void AReferenceFileResampledReadsWhatTheEngineReadsAtEachNode()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            var dose = RTDoseResult.ReadFile(TestData.DoseFiles[0]);
            var fine = RTDoseDerivation.RawGrid(dose);
            var coarse = GridResampler.Resample(fine, 5.0);

            // Measured 2026-11-08 with the survey tool: the reference grids are 2 mm, 100 columns, so
            // the extent is 198 mm and 5 mm steps reach 195 - forty nodes, every other one between
            // source voxels. That makes this a test of real interpolation, not of copying.
            Assert.AreEqual(2.0, fine.XRes, "the reference grids are 2 mm");
            Assert.AreEqual(100, fine.DimensionX);
            Assert.AreEqual(40, coarse.DimensionX, "100 voxels at 2 mm -> 198 mm -> 40 at 5 mm");

            // GetPointDose is the comparison engine's own interpolation; at a coarse node both must agree.
            int checks = 0;
            for (int k = 0; k < coarse.DimensionZ; k += 5)
                for (int j = 0; j < coarse.DimensionY; j += 5)
                    for (int i = 0; i < coarse.DimensionX; i += 5)
                    {
                        double x = coarse.X0 + i * 5, y = coarse.Y0 + j * 5, z = coarse.Z0 + k * 5;
                        double engine = fine.GetPointDose(x, y, z).Dose;
                        double node = coarse.DoseValues[i + j * coarse.DimensionX + k * coarse.DimensionX * coarse.DimensionY];
                        Assert.AreEqual(engine, node, 1e-6, $"at ({x},{y},{z})");
                        checks++;
                    }
            Assert.IsTrue(checks > 100, $"{checks} nodes checked");
        }
    }
}
