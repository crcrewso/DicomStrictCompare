using System;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Gamma;
using DSClibrary.Maps;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>The beam frame, the NIfTI writer and the difference maps the viewer shows (2026-11-18).</summary>
    [TestClass]
    public class DifferenceMapTests
    {
        private static DoseMatrixOptimal Grid(int nx, int ny, int nz, Func<double, double, double, double> dose,
            double x0 = -10, double y0 = -20, double z0 = 5, double res = 2)
        {
            var v = new double[nx * ny * nz];
            for (int k = 0, i = 0; k < nz; k++)
                for (int j = 0; j < ny; j++)
                    for (int ii = 0; ii < nx; ii++, i++) v[i] = dose(x0 + ii * res, y0 + j * res, z0 + k * res);
            return new DoseMatrixOptimal(nx, ny, nz, x0, y0, z0, res, res, res, v);
        }

        [TestMethod]
        public void AtGantryZeroTheBeamRunsDownThePatientsYAxis()
        {
            var f = BeamFrame.AtGantry(0, new Position3(1, 2, 3), "");
            Assert.AreEqual(new Axis3(1, 0, 0), f.X);   // X jaws: patient left-right
            Assert.AreEqual(new Axis3(0, 0, 1), f.Y);   // Y jaws: toward the head and gantry
            Assert.AreEqual(new Axis3(0, -1, 0), f.Z);  // toward the source: anterior
            var deeper = f.ToFrame(1, 2 + 50, 3);       // 50 mm further along the beam
            Assert.AreEqual(-50, deeper.Z, 1e-12);
            Assert.AreEqual(0, deeper.X, 1e-12);
        }

        [TestMethod]
        public void AtGantryNinetyTheSourceIsOnThePatientsLeft()
        {
            var f = BeamFrame.AtGantry(90, default, "");
            Assert.AreEqual(new Axis3(1, 0, 0), f.Z);
            Assert.AreEqual(new Axis3(0, 1, 0), f.X);
            // Right-handed at any angle: X x Y = Z.
            foreach (double g in new[] { 0.0, 33, 90, 181, 270 })
            {
                var a = BeamFrame.AtGantry(g, default, "");
                var cross = (a.X.Y * a.Y.Z - a.X.Z * a.Y.Y, a.X.Z * a.Y.X - a.X.X * a.Y.Z, a.X.X * a.Y.Y - a.X.Y * a.Y.X);
                Assert.AreEqual(a.Z.X, cross.Item1, 1e-12); Assert.AreEqual(a.Z.Y, cross.Item2, 1e-12); Assert.AreEqual(a.Z.Z, cross.Item3, 1e-12);
            }
        }

        [TestMethod]
        public void ThePhantomCentreIsTheMiddleOfTheDosedRegion()
        {
            var g = Grid(10, 10, 10, (x, y, z) => x >= -6 && x <= 2 && y >= -20 && y <= -12 && z >= 9 && z <= 13 ? 1 : 0);
            var c = BeamFrame.PhantomCentre(g, out string how);
            Assert.AreEqual(new Position3(-2, -16, 11), c, how);
        }

        [TestMethod]
        public void TheNiftiHeaderPlacesEachVoxelInTheBeamFrame()
        {
            var g = Grid(4, 3, 2, (x, y, z) => x + 10 * y + 100 * z);
            var frame = BeamFrame.AtGantry(0, new Position3(0, 0, 0), "");
            var values = g.DoseValues.Select(v => (float)v).ToArray();
            using var ms = new MemoryStream();
            NiftiWriter.Write(ms, values, g, frame, "test");
            var bytes = ms.ToArray();
            Assert.AreEqual(348 + 4 + values.Length * 4, bytes.Length);
            Assert.AreEqual(348, BitConverter.ToInt32(bytes, 0));
            Assert.AreEqual("n+1", System.Text.Encoding.ASCII.GetString(bytes, 344, 3));
            Assert.AreEqual(4, BitConverter.ToInt16(bytes, 42));
            Assert.AreEqual(16, BitConverter.ToInt16(bytes, 70));
            Assert.AreEqual(352f, BitConverter.ToSingle(bytes, 108));
            var srow = Enumerable.Range(0, 12).Select(n => (double)BitConverter.ToSingle(bytes, 280 + 4 * n)).ToArray();
            // Voxel (3, 2, 1) is patient (-4, -16, 7): in the frame x = -4, y = 7 (toward the head), z = 16 (toward the source).
            double[] ijk = { 3, 2, 1, 1 };
            double W(int row) => Enumerable.Range(0, 4).Sum(c => srow[row * 4 + c] * ijk[c]);
            Assert.AreEqual(-4, W(0), 1e-5); Assert.AreEqual(7, W(1), 1e-5); Assert.AreEqual(16, W(2), 1e-5);
            Assert.AreEqual(values[^1], BitConverter.ToSingle(bytes, 352 + 4 * (values.Length - 1)));

            // The qform says the same as the sform.
            double b = BitConverter.ToSingle(bytes, 256), c2 = BitConverter.ToSingle(bytes, 260), d = BitConverter.ToSingle(bytes, 264);
            double a = Math.Sqrt(Math.Max(0, 1 - b * b - c2 * c2 - d * d));
            double[,] r =
            {
                { a * a + b * b - c2 * c2 - d * d, 2 * (b * c2 - a * d), 2 * (b * d + a * c2) },
                { 2 * (b * c2 + a * d), a * a + c2 * c2 - b * b - d * d, 2 * (c2 * d - a * b) },
                { 2 * (b * d - a * c2), 2 * (c2 * d + a * b), a * a + d * d - c2 * c2 - b * b },
            };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    Assert.AreEqual(srow[row * 4 + col] / 2, r[row, col], 1e-6, $"R[{row},{col}]");
        }

        [TestMethod]
        public void SamplingIsTrilinearInsideAndNaNOutside()
        {
            var g = Grid(5, 4, 3, (x, y, z) => 2 * x - y + 0.5 * z + 7);
            Assert.AreEqual(2 * -7.3 - -15.1 + 0.5 * 8.2 + 7, DifferenceMap.Sample(g, -7.3, -15.1, 8.2), 1e-9);
            Assert.AreEqual(2 * -2.0 - -14 + 0.5 * 9 + 7, DifferenceMap.Sample(g, -2, -14, 9), 1e-9);   // the far x edge
            Assert.IsTrue(double.IsNaN(DifferenceMap.Sample(g, -1.9, -14, 9)));
            Assert.IsTrue(double.IsNaN(DifferenceMap.Sample(g, -7, -21, 9)));
        }

        [TestMethod]
        public void TheMapsShowWhereAndByHowMuchTheDosesDiffer()
        {
            Func<double, double, double, double> peak = (x, y, z) => 10 * Math.Exp(-((x - 4) * (x - 4) + (y + 6) * (y + 6) + (z - 15) * (z - 15)) / 200);
            var source = Grid(15, 15, 10, peak);
            var same = DifferenceMap.Compute(source, Grid(15, 15, 10, peak), new GammaOptions(3, 2));
            Assert.IsTrue(same.PercentEvaluated > 0 && same.Evaluated > 0);
            Assert.AreEqual(0, same.GammaFailed);
            Assert.AreEqual(0, same.PercentDifference.Where(v => !float.IsNaN(v)).Max(v => Math.Abs(v)), 1e-5);

            // A 10 % hotter target fails gamma 3 %/2 mm near the peak, and the difference reads +10 % of the maximum there.
            var hot = DifferenceMap.Compute(source, Grid(15, 15, 10, (x, y, z) => 1.1 * peak(x, y, z)), new GammaOptions(3, 2));
            Assert.IsTrue(hot.GammaFailed > 0);
            int atPeak = source.DoseValues.Select((v, i) => (v, i)).MaxBy(p => p.v).i;
            Assert.AreEqual(10, hot.PercentDifference[atPeak], 0.01);
            float worst = hot.Gamma!.Where(v => !float.IsNaN(v)).Max();
            Assert.IsTrue(worst > 1 && worst <= DifferenceMap.GammaCap + 1e-6, $"max gamma {worst}");
            Assert.IsTrue(float.IsNaN(hot.PercentDifference[0]), "below the 10 % cutoff");
        }
    }
}
