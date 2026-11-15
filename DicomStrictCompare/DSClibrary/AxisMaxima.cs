namespace DSClibrary
{
    /// <summary>
    /// The largest dose in each x column, y row and z slice of a grid. The bounding box of voxels at or above any
    /// threshold follows exactly from these: an x index is in it if and only if its column's maximum reaches the
    /// threshold. <see cref="Results.DICOM.DoseGridReader"/> fills them while it reads; otherwise they are computed
    /// on first use (<see cref="DoseMatrixOptimal.AxisMaxima"/>).
    /// </summary>
    public sealed class AxisMaxima
    {
        public double[] X { get; }
        public double[] Y { get; }
        public double[] Z { get; }

        public AxisMaxima(double[] x, double[] y, double[] z)
        {
            X = x ?? throw new ArgumentNullException(nameof(x));
            Y = y ?? throw new ArgumentNullException(nameof(y));
            Z = z ?? throw new ArgumentNullException(nameof(z));
        }

        /// <summary>One pass over the grid, split by z slice.</summary>
        public static AxisMaxima Of(DoseMatrixOptimal g)
        {
            if (g is null) throw new ArgumentNullException(nameof(g));
            int dx = g.DimensionX, dy = g.DimensionY, dz = g.DimensionZ;
            var v = g.DoseValues;
            var mx = NegativeInfinity(dx);
            var my = NegativeInfinity(dy);
            var mz = NegativeInfinity(dz);
            object gate = new();
            // Each thread starts from its own -infinity arrays. (A shortcut that re-filled them when an entry
            // read 0 wiped the maxima on grids with zero-dose edges; measured 2026-11-15.)
            Parallel.For(0, dz, () => (NegativeInfinity(dx), NegativeInfinity(dy)), (z, _, acc) =>
            {
                var (ax, ay) = acc;
                double slice = double.NegativeInfinity;
                for (int y = 0, i = z * dx * dy; y < dy; y++)
                {
                    double row = double.NegativeInfinity;
                    for (int x = 0; x < dx; x++, i++)
                    {
                        double d = v[i];
                        if (d > ax[x]) ax[x] = d;
                        if (d > row) row = d;
                    }
                    if (row > ay[y]) ay[y] = row;
                    if (row > slice) slice = row;
                }
                mz[z] = slice;
                return acc;
            }, acc =>
            {
                lock (gate)
                {
                    for (int x = 0; x < dx; x++) if (acc.Item1[x] > mx[x]) mx[x] = acc.Item1[x];
                    for (int y = 0; y < dy; y++) if (acc.Item2[y] > my[y]) my[y] = acc.Item2[y];
                }
            });
            return new AxisMaxima(mx, my, mz);
        }

        internal static double[] NegativeInfinity(int n)
        {
            var a = new double[n];
            Array.Fill(a, double.NegativeInfinity);
            return a;
        }
    }
}
