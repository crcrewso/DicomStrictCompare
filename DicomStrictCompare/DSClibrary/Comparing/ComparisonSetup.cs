namespace DSClibrary.Comparing
{
    /// <summary>
    /// For one grid along one axis of the sample lattice: the cell each sample index falls in, whether it is
    /// interpolated, and its weight. Computed exactly as <c>DoseMatrixOptimal.GetPointDose</c> computes them, in
    /// double, so a comparer that reads these chooses the same cells as <see cref="HandWrittenComparer"/>.
    /// </summary>
    internal sealed class AxisTable
    {
        public int[] Cell { get; }
        public bool[] Interp { get; }
        public double[] Weight { get; }

        private AxisTable(int n)
        {
            Cell = new int[n];
            Interp = new bool[n];
            Weight = new double[n];
        }

        public static readonly AxisTable Empty = new(0);

        public static AxisTable For(double[] coords, double origin, double res)
        {
            var t = new AxisTable(coords.Length);
            for (int i = 0; i < coords.Length; i++)
            {
                double v = coords[i];
                bool interp = (v - origin) / res % 1 != 0.0;
                int steps = (int)((v - origin) / res);
                double low = origin + steps * res;
                double high = origin + (steps + 1) * res;
                t.Cell[i] = steps;
                t.Interp[i] = interp;
                t.Weight[i] = interp ? (v - low) / (high - low) : 0;
            }
            return t;
        }
    }

    /// <summary>The three axis tables for one grid at one set of sample positions.</summary>
    internal sealed record Tables3(AxisTable X, AxisTable Y, AxisTable Z)
    {
        public static readonly Tables3 Empty = new(AxisTable.Empty, AxisTable.Empty, AxisTable.Empty);
    }

    /// <summary>Whether a DTA neighbour, one per axis direction, lies inside the compared region.</summary>
    internal sealed record Neighbours3(bool[] XMinus, bool[] XPlus, bool[] YMinus, bool[] YPlus, bool[] ZMinus, bool[] ZPlus)
    {
        public static readonly Neighbours3 Empty = new(Array.Empty<bool>(), Array.Empty<bool>(), Array.Empty<bool>(),
            Array.Empty<bool>(), Array.Empty<bool>(), Array.Empty<bool>());
    }

    /// <summary>
    /// One pair's comparison, set up as <see cref="HandWrittenComparer"/> sets it up (overlap region, coarser
    /// spacing, integer step counts, trim), plus per-axis tables for the source, the target and the DTA neighbours,
    /// and the window of sample indices to evaluate.
    /// <para>
    /// <b>The threshold box.</b> Trilinear interpolation is a weighted average of the surrounding voxels, so a
    /// sample point reaches the threshold only if one of them does. The window is therefore the bounding box of
    /// source voxels at or above the threshold, plus a margin of 1 cm or 2 voxels, whichever is smaller (Cody,
    /// 2026-11-15), and never under 1 voxel. Points outside it are below threshold by construction and are counted
    /// but not visited. The region that bounds DTA neighbours is unchanged, so counts equal the hand-written ones.
    /// </para>
    /// </summary>
    internal sealed class ComparisonSetup
    {
        /// <summary>Sample points per axis.</summary>
        public Index3 Samples { get; private init; }
        public int TotalCount => (int)Samples.Count;

        public Position3 RegionMin { get; private init; }
        public Position3 RegionMax { get; private init; }
        public Position3 Step { get; private init; }

        public double MaxSource { get; private init; }
        public double Threshold { get; private init; }
        public double Tolerance { get; private init; }
        public bool Global { get; private init; }
        public bool UseDta { get; private init; }

        public Index3 SourceSize { get; private init; }
        public Index3 TargetSize { get; private init; }

        public Tables3 Source { get; private init; } = Tables3.Empty;
        public Tables3 Target { get; private init; } = Tables3.Empty;
        /// <summary>The source at the DTA neighbours, distance d below and above each sample point, per axis.</summary>
        public Tables3 SourceMinus { get; private init; } = Tables3.Empty;
        public Tables3 SourcePlus { get; private init; } = Tables3.Empty;
        public Neighbours3 Has { get; private init; } = Neighbours3.Empty;

        /// <summary>The window evaluated, inclusive at both ends; the whole lattice without the box. Empty when Hi.X &lt; Lo.X.</summary>
        public Index3 Lo { get; private set; }
        public Index3 Hi { get; private set; }

        public int WindowLinesY => Math.Max(0, Hi.Y - Lo.Y + 1);
        public int WindowLines => Math.Max(0, Hi.X - Lo.X + 1) * WindowLinesY;
        public long WindowPoints => (long)WindowLines * Math.Max(0, Hi.Z - Lo.Z + 1);

        public static ComparisonSetup For(DoseMatrixOptimal source, DoseMatrixOptimal target, Dta dta, bool global, bool thresholdBox) =>
            For(source, target, dta, global, thresholdBox, source?.MaxPointDose.Dose ?? 0);

        /// <param name="referenceMaximum">The dose the threshold and the global tolerance are fractions of (<see cref="ReferenceMaximum"/>).</param>
        public static ComparisonSetup For(DoseMatrixOptimal source, DoseMatrixOptimal target, Dta dta, bool global, bool thresholdBox, double referenceMaximum)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (dta == null) throw new ArgumentNullException(nameof(dta));

            // As HandWrittenComparer.Compare.
            double xMin = Math.Max(source.X0, target.X0), xMax = Math.Min(source.XMax, target.XMax), xRes = Math.Max(source.XRes, target.XRes);
            double yMin = Math.Max(source.Y0, target.Y0), yMax = Math.Min(source.YMax, target.YMax), yRes = Math.Max(source.YRes, target.YRes);
            double zMin = Math.Max(source.Z0, target.Z0), zMax = Math.Min(source.ZMax, target.ZMax), zRes = Math.Max(source.ZRes, target.ZRes);
            if (dta.TrimWidth > 0)
            {
                xMin += dta.TrimWidth * xRes; xMax -= dta.TrimWidth * xRes;
                yMin += dta.TrimWidth * yRes; yMax -= dta.TrimWidth * yRes;
                zMin += dta.TrimWidth * zRes; zMax -= dta.TrimWidth * zRes;
            }
            var n = new Index3(StepCount(xMin, xMax, xRes), StepCount(yMin, yMax, yRes), StepCount(zMin, zMax, zRes));
            double[] xs = Coords(xMin, xRes, n.X), ys = Coords(yMin, yRes, n.Y), zs = Coords(zMin, zRes, n.Z);
            bool useDta = dta.UseMM && dta.Distance > 0;
            double d = dta.Distance;

            Tables3 For(DoseMatrixOptimal g, double dx, double dy, double dz) => new(
                AxisTable.For(Shift(xs, dx), g.X0, g.XRes), AxisTable.For(Shift(ys, dy), g.Y0, g.YRes), AxisTable.For(Shift(zs, dz), g.Z0, g.ZRes));

            var setup = new ComparisonSetup
            {
                Samples = n,
                RegionMin = new Position3(xMin, yMin, zMin),
                RegionMax = new Position3(xMax, yMax, zMax),
                Step = new Position3(xRes, yRes, zRes),
                MaxSource = referenceMaximum,
                Threshold = dta.Threshhold,
                Tolerance = dta.Tolerance,
                Global = global,
                UseDta = useDta,
                SourceSize = new Index3(source.DimensionX, source.DimensionY, source.DimensionZ),
                TargetSize = new Index3(target.DimensionX, target.DimensionY, target.DimensionZ),
                Source = For(source, 0, 0, 0),
                Target = For(target, 0, 0, 0),
                // Only the shifted axis of each is used; the other two are the unshifted source tables.
                SourceMinus = useDta ? For(source, -d, -d, -d) : Tables3.Empty,
                SourcePlus = useDta ? For(source, d, d, d) : Tables3.Empty,
                Has = useDta
                    ? new Neighbours3(xs.Select(x => x - d >= xMin).ToArray(), xs.Select(x => x + d <= xMax).ToArray(),
                                      ys.Select(y => y - d >= yMin).ToArray(), ys.Select(y => y + d <= yMax).ToArray(),
                                      zs.Select(z => z - d >= zMin).ToArray(), zs.Select(z => z + d <= zMax).ToArray())
                    : Neighbours3.Empty,
                Lo = new Index3(0, 0, 0),
                Hi = new Index3(n.X - 1, n.Y - 1, n.Z - 1),
            };
            if (thresholdBox && n.Count > 0) setup.ApplyThresholdBox(source);
            return setup;
        }

        private void ApplyThresholdBox(DoseMatrixOptimal source)
        {
            double t = MaxSource * Threshold;
            double cut = t - Math.Abs(t) * 1e-9;   // allows for rounding in the interpolation
            var m = source.AxisMaxima;
            int xl = Array.FindIndex(m.X, v => v >= cut), xh = Array.FindLastIndex(m.X, v => v >= cut);
            int yl = Array.FindIndex(m.Y, v => v >= cut), yh = Array.FindLastIndex(m.Y, v => v >= cut);
            int zl = Array.FindIndex(m.Z, v => v >= cut), zh = Array.FindLastIndex(m.Z, v => v >= cut);
            if (xl < 0 || yl < 0 || zl < 0) { Lo = new Index3(0, 0, 0); Hi = new Index3(-1, -1, -1); return; }   // nothing reaches it
            var (x0, x1) = Window(source.X0, source.XRes, xl, xh, RegionMin.X, Step.X, Samples.X);
            var (y0, y1) = Window(source.Y0, source.YRes, yl, yh, RegionMin.Y, Step.Y, Samples.Y);
            var (z0, z1) = Window(source.Z0, source.ZRes, zl, zh, RegionMin.Z, Step.Z, Samples.Z);
            Lo = new Index3(x0, y0, z0);
            Hi = new Index3(x1, y1, z1);
        }

        private static (int, int) Window(double origin, double res, int lo, int hi, double min, double step, int n)
        {
            double margin = Math.Max(res, Math.Min(10.0, 2 * res));   // mm
            return (Math.Max(0, (int)Math.Ceiling((origin + lo * res - margin - min) / step - 1e-9)),
                    Math.Min(n - 1, (int)Math.Floor((origin + hi * res + margin - min) / step + 1e-9)));
        }

        private static int StepCount(double min, double max, double res) =>
            max < min || res <= 0 ? 0 : (int)Math.Floor((max - min) / res + 1e-6) + 1;

        // As the hand-written loops: min + i * res, never accumulated.
        private static double[] Coords(double min, double res, int n)
        {
            var c = new double[n];
            for (int i = 0; i < n; i++) c[i] = min + i * res;
            return c;
        }

        // Adding -d is exactly subtracting d in IEEE arithmetic, so these are the hand-written x - d and x + d.
        private static double[] Shift(double[] c, double by) => by == 0 ? c : c.Select(v => v + by).ToArray();
    }
}
