using System;
using System.Linq;
using DSClibrary.Resampling;

namespace DSClibrary.Gamma
{
    /// <summary>
    /// A dose grid as the gamma calculation sees it: one coordinate axis per dimension and the values
    /// in C order (the last axis varies fastest), the shape pymedphys' <c>gamma</c> takes.
    /// <para>
    /// One, two or three dimensions. An axis may be ascending or descending; the evaluation side is
    /// put in ascending order internally. An axis with a single value makes a plane, line or point,
    /// which is how a single-frame RT Dose or a film plane enters a 3D comparison.
    /// </para>
    /// </summary>
    public sealed class GammaGrid
    {
        public double[][] Axes { get; }
        public double[] Values { get; }
        public int[] Shape { get; }
        public int Dimensions => Axes.Length;

        public GammaGrid(double[][] axes, double[] values)
        {
            if (axes is null) throw new ArgumentNullException(nameof(axes));
            if (values is null) throw new ArgumentNullException(nameof(values));
            if (axes.Length < 1 || axes.Length > 3)
                throw new ArgumentException("A gamma grid has 1, 2 or 3 dimensions", nameof(axes));
            long expected = 1;
            foreach (var (axis, d) in axes.Select((a, i) => (a, i)))
            {
                if (axis is null || axis.Length == 0 || axis.Any(v => !double.IsFinite(v)))
                    throw new ArgumentException($"Axis {d} must be a non-empty finite array", nameof(axes));
                expected *= axis.Length;
            }
            if (values.Length != expected)
                throw new ArgumentException(
                    $"The axes describe {expected} points but there are {values.Length} values", nameof(values));
            Axes = axes;
            Values = values;
            Shape = axes.Select(a => a.Length).ToArray();
        }

        /// <summary>
        /// A dose grid read by the library, as axes (z, y, x) in that order because
        /// <see cref="DoseMatrixOptimal.DoseValues"/> is x-fastest. Values are the grid's own.
        /// </summary>
        public static GammaGrid FromDose(DoseMatrixOptimal dose)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));
            return new GammaGrid(
                new[] { GridLines.Nodes(dose, GridAxis.Z), GridLines.Nodes(dose, GridAxis.Y), GridLines.Nodes(dose, GridAxis.X) },
                dose.DoseValues);
        }

        /// <summary>One plane of a dose grid at a fixed coordinate, as a 2D grid over the other two axes.</summary>
        public static GammaGrid PlaneOf(DoseMatrixOptimal dose, GridAxis fixedAxis, int index)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));
            int nx = dose.DimensionX, ny = dose.DimensionY, nz = dose.DimensionZ;
            double[] xs = GridLines.Nodes(dose, GridAxis.X), ys = GridLines.Nodes(dose, GridAxis.Y), zs = GridLines.Nodes(dose, GridAxis.Z);
            switch (fixedAxis)
            {
                case GridAxis.Z:
                {
                    if (index < 0 || index >= nz) throw new ArgumentOutOfRangeException(nameof(index));
                    var values = new double[ny * nx];
                    Array.Copy(dose.DoseValues, index * nx * ny, values, 0, nx * ny);
                    return new GammaGrid(new[] { ys, xs }, values);
                }
                case GridAxis.Y:
                {
                    if (index < 0 || index >= ny) throw new ArgumentOutOfRangeException(nameof(index));
                    var values = new double[nz * nx];
                    for (int z = 0; z < nz; z++)
                        Array.Copy(dose.DoseValues, z * nx * ny + index * nx, values, z * nx, nx);
                    return new GammaGrid(new[] { zs, xs }, values);
                }
                default:
                {
                    if (index < 0 || index >= nx) throw new ArgumentOutOfRangeException(nameof(index));
                    var values = new double[nz * ny];
                    for (int z = 0; z < nz; z++)
                        for (int y = 0; y < ny; y++)
                            values[z * ny + y] = dose.DoseValues[z * nx * ny + y * nx + index];
                    return new GammaGrid(new[] { zs, ys }, values);
                }
            }
        }
    }
}
