using System;
using System.Collections.Generic;

namespace DSClibrary.Resampling
{
    /// <summary>One of a dose grid's three axes, in DICOM patient coordinates.</summary>
    public enum GridAxis { X, Y, Z }

    /// <summary>
    /// Reads a straight line out of a dose grid along one of its axes.
    /// <para>
    /// This is geometry-free on purpose. It answers "what are the values along x at this y and z"
    /// and nothing more; which line is a depth dose and which is a crossplane profile depends on the
    /// patient position, the gantry angle and where the surface is, and that convention is still
    /// being settled (see <see cref="Profiles.DoseVolume"/>). A front end that wants to draw a curve
    /// states its own reading of the geometry and asks this for the numbers.
    /// </para>
    /// </summary>
    public static class GridLines
    {
        public static double Min(DoseMatrixOptimal g, GridAxis axis) => axis switch
        {
            GridAxis.X => g.X0, GridAxis.Y => g.Y0, _ => g.Z0
        };

        public static double Max(DoseMatrixOptimal g, GridAxis axis) => axis switch
        {
            GridAxis.X => g.XMax, GridAxis.Y => g.YMax, _ => g.ZMax
        };

        public static double Resolution(DoseMatrixOptimal g, GridAxis axis) => axis switch
        {
            GridAxis.X => g.XRes, GridAxis.Y => g.YRes, _ => g.ZRes
        };

        public static int Count(DoseMatrixOptimal g, GridAxis axis) => axis switch
        {
            GridAxis.X => g.DimensionX, GridAxis.Y => g.DimensionY, _ => g.DimensionZ
        };

        /// <summary>The grid's own node positions along an axis, ascending.</summary>
        public static double[] Nodes(DoseMatrixOptimal g, GridAxis axis)
        {
            if (g is null) throw new ArgumentNullException(nameof(g));
            int n = Count(g, axis);
            double min = Min(g, axis), res = Resolution(g, axis);
            var nodes = new double[n];
            for (int i = 0; i < n; i++) nodes[i] = min + i * res;
            return nodes;
        }

        /// <summary>Whether a point lies within the grid, with a micron of slack for rounding at the edges.</summary>
        public static bool Contains(DoseMatrixOptimal g, double x, double y, double z)
        {
            const double slack = 1e-3;
            return x >= g.X0 - slack && x <= g.XMax + slack
                && y >= g.Y0 - slack && y <= g.YMax + slack
                && z >= g.Z0 - slack && z <= g.ZMax + slack;
        }

        /// <summary>
        /// Values along <paramref name="axis"/> at the given positions, holding the other two
        /// coordinates fixed. <paramref name="a"/> and <paramref name="b"/> are those two in axis
        /// order: for X they are (y, z), for Y (x, z), for Z (x, y). A position outside the grid
        /// yields NaN rather than an extrapolated number.
        /// </summary>
        public static double[] Along(DoseMatrixOptimal g, GridAxis axis, double a, double b, IReadOnlyList<double> positions)
        {
            if (g is null) throw new ArgumentNullException(nameof(g));
            if (positions is null) throw new ArgumentNullException(nameof(positions));

            var values = new double[positions.Count];
            for (int i = 0; i < positions.Count; i++)
            {
                double p = positions[i];
                var (x, y, z) = axis switch
                {
                    GridAxis.X => (p, a, b),
                    GridAxis.Y => (a, p, b),
                    _ => (a, b, p)
                };
                values[i] = Contains(g, x, y, z) ? GridResampler.ValueAt(g, x, y, z) : double.NaN;
            }
            return values;
        }
    }
}
