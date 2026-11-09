using System;
using System.Linq;
using DSClibrary;
using DSClibrary.Resampling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    [TestClass]
    public class GridLinesTests
    {
        private static double Field(double x, double y, double z) => 1 + 2 * x + 3 * y + 5 * z;

        private static DoseMatrixOptimal Ramp()
        {
            const int n = 11;
            var values = new double[n * n * n];
            for (int k = 0; k < n; k++)
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                        values[i + j * n + k * n * n] = Field(-10 + i * 2, -20 + j * 2, -30 + k * 2);
            return new DoseMatrixOptimal(n, n, n, -10, -20, -30, 2, 2, 2, values);
        }

        [TestMethod]
        public void NodesAreTheGridsOwnPositions()
        {
            var g = Ramp();
            var xs = GridLines.Nodes(g, GridAxis.X);
            Assert.AreEqual(11, xs.Length);
            Assert.AreEqual(-10, xs[0]);
            Assert.AreEqual(10, xs[^1]);
            Assert.AreEqual(g.ZMax, GridLines.Nodes(g, GridAxis.Z)[^1]);
        }

        [TestMethod]
        public void AlongEachAxisReadsTheFieldAtNodesAndBetweenThem()
        {
            var g = Ramp();
            var positions = new[] { -10.0, -9.0, 0.0, 3.5, 10.0 };

            // The ramp spans x -10..10, y -20..0, z -30..-10; the fixed coordinates must be inside it,
            // and the positions along y and z are shifted into their ranges.
            var alongX = GridLines.Along(g, GridAxis.X, -4, -16, positions);
            var alongY = GridLines.Along(g, GridAxis.Y, 4, -16, positions.Select(p => p - 10).ToArray());
            var alongZ = GridLines.Along(g, GridAxis.Z, 4, -4, positions.Select(p => p - 20).ToArray());

            for (int i = 0; i < positions.Length; i++)
            {
                Assert.AreEqual(Field(positions[i], -4, -16), alongX[i], 1e-9, $"x at {positions[i]}");
                Assert.AreEqual(Field(4, positions[i] - 10, -16), alongY[i], 1e-9, $"y at {positions[i] - 10}");
                Assert.AreEqual(Field(4, -4, positions[i] - 20), alongZ[i], 1e-9, $"z at {positions[i] - 20}");
            }
        }

        [TestMethod]
        public void OutsideTheGridIsNaNNotAnExtrapolation()
        {
            var g = Ramp();
            var values = GridLines.Along(g, GridAxis.X, -10, -20, new[] { -10.0001, -10.0, 10.0, 10.001, 50.0 });
            Assert.IsTrue(double.IsNaN(values[0]) || Math.Abs(values[0] - Field(-10, -10, -20)) < 1e-6, "a micron outside is tolerated as the edge");
            Assert.IsFalse(double.IsNaN(values[1]));
            Assert.IsFalse(double.IsNaN(values[2]), "the far edge itself is inside");
            Assert.IsTrue(double.IsNaN(values[4]));
            Assert.AreEqual(Field(10, -10, -20), values[2], 1e-9, "the far edge reads the last voxel, not one past it");
        }

        [TestMethod]
        public void AFixedCoordinateOutsideTheGridMakesTheWholeLineNaN()
        {
            var g = Ramp();
            var values = GridLines.Along(g, GridAxis.X, 99, -20, GridLines.Nodes(g, GridAxis.X));
            Assert.IsTrue(values.All(double.IsNaN));
        }
    }
}
