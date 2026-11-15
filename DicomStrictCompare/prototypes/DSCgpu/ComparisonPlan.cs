using DSClibrary;

namespace DSCgpu;

/// <summary>
/// One pair's comparison, set up exactly as <c>X86Mathematics.Compare</c> sets it up: the overlap region,
/// sampled at the coarser spacing per axis, with the same step counts. Packed into a double and an int
/// array so every backend gets identical inputs.
/// </summary>
public sealed record ComparisonPlan(
    double XMin, double XMax, double YMin, double YMax, double ZMin, double ZMax,
    double XRes, double YRes, double ZRes, int Nx, int Ny, int Nz,
    double MaxSource, double Threshold, double Tolerance, double Distance, bool Global, bool UseDta)
{
    public long Points => (long)Nx * Ny * Nz;

    public static ComparisonPlan For(DoseMatrixOptimal s, DoseMatrixOptimal t, Dta dta, bool global)
    {
        double xMin = Math.Max(s.X0, t.X0), xMax = Math.Min(s.XMax, t.XMax), xRes = Math.Max(s.XRes, t.XRes);
        double yMin = Math.Max(s.Y0, t.Y0), yMax = Math.Min(s.YMax, t.YMax), yRes = Math.Max(s.YRes, t.YRes);
        double zMin = Math.Max(s.Z0, t.Z0), zMax = Math.Min(s.ZMax, t.ZMax), zRes = Math.Max(s.ZRes, t.ZRes);
        if (dta.TrimWidth > 0)
        {
            xMin += dta.TrimWidth * xRes; xMax -= dta.TrimWidth * xRes;
            yMin += dta.TrimWidth * yRes; yMax -= dta.TrimWidth * yRes;
            zMin += dta.TrimWidth * zRes; zMax -= dta.TrimWidth * zRes;
        }
        return new ComparisonPlan(xMin, xMax, yMin, yMax, zMin, zMax, xRes, yRes, zRes,
            StepCount(xMin, xMax, xRes), StepCount(yMin, yMax, yRes), StepCount(zMin, zMax, zRes),
            s.MaxPointDose.Dose, dta.Threshhold, dta.Tolerance, dta.Distance, global, dta.UseMM && dta.Distance > 0);
    }

    private static int StepCount(double min, double max, double res) =>
        max < min || res <= 0 ? 0 : (int)Math.Floor((max - min) / res + 1e-6) + 1;

    // Layout shared by every backend; the indices are the P* and Q* constants.
    public const int PS = 0, PT = 6, PRegion = 12, PStep = 18, PMax = 21, PThr = 22, PTol = 23, PDist = 24, PCount = 25;
    public const int QSDimX = 0, QSDimY = 1, QTDimX = 2, QTDimY = 3, QNx = 4, QNy = 5, QNz = 6, QGlobal = 7, QDta = 8;
    /// <summary>The window of sample indices evaluated, inclusive; the whole region unless <see cref="Boxed"/>.</summary>
    public const int QI0 = 9, QI1 = 10, QJ0 = 11, QJ1 = 12, QK0 = 13, QK1 = 14, QCount = 15;

    public double[] Doubles(DoseMatrixOptimal s, DoseMatrixOptimal t) => new[]
    {
        s.X0, s.Y0, s.Z0, s.XRes, s.YRes, s.ZRes,
        t.X0, t.Y0, t.Z0, t.XRes, t.YRes, t.ZRes,
        XMin, XMax, YMin, YMax, ZMin, ZMax,
        XRes, YRes, ZRes,
        MaxSource, Threshold, Tolerance, Distance,
    };

    public int[] Ints(DoseMatrixOptimal s, DoseMatrixOptimal t) => new[]
    {
        s.DimensionX, s.DimensionY, t.DimensionX, t.DimensionY, Nx, Ny, Nz, Global ? 1 : 0, UseDta ? 1 : 0,
        0, Nx - 1, 0, Ny - 1, 0, Nz - 1,
    };

    /// <summary>
    /// <paramref name="q"/> with its window shrunk to the sample points that can pass the threshold. Trilinear
    /// interpolation is a weighted average of the surrounding voxels, so a point reaches the threshold only if one
    /// of them does: the window is the bounding box of source voxels at or above it, plus a margin. The margin is
    /// 1 cm or 2 voxels, whichever is smaller (Cody, 2026-11-15), never under 1 voxel: below that a straddling
    /// interpolation cell could be cut off. Points outside are below threshold by construction; the region that
    /// bounds DTA neighbours is unchanged, so the counts must equal the engine's.
    /// </summary>
    public static int[] Boxed(int[] q, double[] p, DoseMatrixOptimal s, int threads) =>
        Boxed(q, p, s, Projections.Of(s, threads));

    /// <summary>The same window, read from per-axis maxima rather than a scan of the grid.</summary>
    public static int[] Boxed(int[] q, double[] p, DoseMatrixOptimal s, Projections m)
    {
        double cut = p[PMax] * p[PThr] * (1 - 1e-9);
        static (int, int) Span(double[] a, double cut)
        {
            int lo = Array.FindIndex(a, v => v >= cut), hi = Array.FindLastIndex(a, v => v >= cut);
            return (lo, hi);
        }
        var (xl, xh) = Span(m.X, cut); var (yl, yh) = Span(m.Y, cut); var (zl, zh) = Span(m.Z, cut);
        var w = (int[])q.Clone();
        if (xl < 0) { w[QI0] = 0; w[QI1] = -1; return w; }   // nothing reaches the threshold
        (int, int) Window(double o, double r, int lo, int hi, double min, double step, int n)
        {
            double margin = Math.Max(r, Math.Min(10.0, 2 * r));   // mm
            return (Math.Max(0, (int)Math.Ceiling((o + lo * r - margin - min) / step - 1e-9)),
                    Math.Min(n - 1, (int)Math.Floor((o + hi * r + margin - min) / step + 1e-9)));
        }
        (w[QI0], w[QI1]) = Window(s.X0, s.XRes, xl, xh, p[PRegion], p[PStep], q[QNx]);
        (w[QJ0], w[QJ1]) = Window(s.Y0, s.YRes, yl, yh, p[PRegion + 2], p[PStep + 1], q[QNy]);
        (w[QK0], w[QK1]) = Window(s.Z0, s.ZRes, zl, zh, p[PRegion + 4], p[PStep + 2], q[QNz]);
        return w;
    }

    /// <summary>Sample lines in the window, and points.</summary>
    public static long WindowPoints(int[] q) => WindowLines(q) * (long)Math.Max(0, q[QK1] - q[QK0] + 1);
    public static int WindowLines(int[] q) => Math.Max(0, q[QI1] - q[QI0] + 1) * Math.Max(0, q[QJ1] - q[QJ0] + 1);
}

/// <summary>
/// The largest voxel in each x column, y row and z slice of a grid. The bounding box of voxels at or above any
/// threshold follows exactly: an x index is in it if and only if its column's maximum reaches the threshold.
/// Cheap enough to keep from load time, which is what <see cref="ComparisonPlan.Boxed(int[], double[], DSClibrary.DoseMatrixOptimal, Projections)"/> stands in for.
/// </summary>
public sealed record Projections(double[] X, double[] Y, double[] Z)
{
    public static Projections Of(DSClibrary.DoseMatrixOptimal g, int threads)
    {
        int dx = g.DimensionX, dy = g.DimensionY, dz = g.DimensionZ;
        var v = g.DoseValues;
        var mx = new double[dx]; var my = new double[dy]; var mz = new double[dz];
        Array.Fill(mx, double.NegativeInfinity); Array.Fill(my, double.NegativeInfinity);
        object gate = new();
        Parallel.For(0, dz, new ParallelOptions { MaxDegreeOfParallelism = threads }, () =>
        {
            var ax = new double[dx]; var ay = new double[dy];
            Array.Fill(ax, double.NegativeInfinity); Array.Fill(ay, double.NegativeInfinity);
            return (ax, ay);
        }, (z, _, acc) =>
        {
            var (ax, ay) = acc;
            double sz = double.NegativeInfinity;
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
                if (row > sz) sz = row;
            }
            mz[z] = sz;
            return acc;
        }, acc =>
        {
            lock (gate)
            {
                for (int x = 0; x < dx; x++) if (acc.Item1[x] > mx[x]) mx[x] = acc.Item1[x];
                for (int y = 0; y < dy; y++) if (acc.Item2[y] > my[y]) my[y] = acc.Item2[y];
            }
        });
        return new Projections(mx, my, mz);
    }
}

/// <summary>Counts as the engine reports them.</summary>
public readonly record struct Counts(long Points, long Compared, long Failed)
{
    public override string ToString() => $"{Compared:N0} compared, {Failed:N0} failed";
}
