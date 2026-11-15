using static DSCgpu.ComparisonPlan;
using DSClibrary.Comparing;

namespace DSCgpu;

/// <summary>
/// <c>HandWrittenComparer.EvaluatePoint</c> and <c>DoseMatrixOptimal.GetPointDose</c>, ported line for line onto
/// plain arrays, so one pair can be split across threads. Work is one (i, j) line along z, as in the kernels.
/// Checked against the engine's counts before any timing is trusted.
/// </summary>
public static class CpuPort
{
    public static Counts Compare(double[] s, double[] t, double[] p, int[] q, int threads)
    {
        int nx = q[QNx], ny = q[QNy], nz = q[QNz];
        int wy = Math.Max(0, q[QJ1] - q[QJ0] + 1);
        long compared = 0, failed = 0;
        Parallel.For(0, WindowLines(q), new ParallelOptions { MaxDegreeOfParallelism = threads },
            () => (0L, 0L),
            (line, _, acc) =>
            {
                var (c, f) = Line(s, t, p, q, q[QI0] + line / wy, q[QJ0] + line % wy);
                return (acc.Item1 + c, acc.Item2 + f);
            },
            acc => { Interlocked.Add(ref compared, acc.Item1); Interlocked.Add(ref failed, acc.Item2); });
        return new Counts((long)nx * ny * nz, compared, failed);
    }

    public static (int Compared, int Failed) Line(double[] s, double[] t, double[] p, int[] q, int i, int j)
    {
        int compared = 0, failed = 0;
        double x = p[PRegion] + i * p[PStep];
        double y = p[PRegion + 2] + j * p[PStep + 1];
        for (int k = q[QK0]; k <= q[QK1]; k++)
        {
            double z = p[PRegion + 4] + k * p[PStep + 2];
            int r = Evaluate(s, t, p, q, x, y, z);
            if (r > 0) { compared++; if (r == 2) failed++; }
        }
        return (compared, failed);
    }

    /// <summary>0 below threshold, 1 passed, 2 failed.</summary>
    private static int Evaluate(double[] s, double[] t, double[] p, int[] q, double x, double y, double z)
    {
        double maxSource = p[PMax];
        double sourcei = Dose(s, p, PS, q[QSDimX], q[QSDimY], x, y, z);
        if (sourcei < maxSource * p[PThr]) return 0;
        double targeti = Dose(t, p, PT, q[QTDimX], q[QTDimY], x, y, z);
        double allowed = q[QGlobal] != 0 ? maxSource * p[PTol] : p[PTol] * sourcei;
        if (Math.Abs(targeti - sourcei) <= allowed) return 1;
        if (q[QDta] == 0) return 2;

        double d = p[PDist];
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        int n = 0;
        void Add(double v) { n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (x - d >= p[PRegion]) Add(Dose(s, p, PS, q[QSDimX], q[QSDimY], x - d, y, z));
        if (x + d <= p[PRegion + 1]) Add(Dose(s, p, PS, q[QSDimX], q[QSDimY], x + d, y, z));
        if (y - d >= p[PRegion + 2]) Add(Dose(s, p, PS, q[QSDimX], q[QSDimY], x, y - d, z));
        if (y + d <= p[PRegion + 3]) Add(Dose(s, p, PS, q[QSDimX], q[QSDimY], x, y + d, z));
        if (z - d >= p[PRegion + 4]) Add(Dose(s, p, PS, q[QSDimX], q[QSDimY], x, y, z - d));
        if (z + d <= p[PRegion + 5]) Add(Dose(s, p, PS, q[QSDimX], q[QSDimY], x, y, z + d));
        return n == 0 || targeti < lo || targeti > hi ? 2 : 1;
    }

    /// <summary><c>DoseMatrixOptimal.GetPointDose</c>, same operations in the same order.</summary>
    private static double Dose(double[] g, double[] p, int o, int dimX, int dimY, double x, double y, double z)
    {
        double x0 = p[o], y0 = p[o + 1], z0 = p[o + 2], xr = p[o + 3], yr = p[o + 4], zr = p[o + 5];
        bool ix = (x - x0) / xr % 1 != 0.0; int xs = (int)((x - x0) / xr);
        double lowX = x0 + xs * xr, highX = x0 + (xs + 1) * xr;
        bool iy = (y - y0) / yr % 1 != 0; int ys = (int)((y - y0) / yr);
        double lowY = y0 + ys * yr, highY = y0 + (ys + 1) * yr;
        bool iz = (z - z0) / zr % 1 != 0; int zs = (int)((z - z0) / zr);
        double lowZ = z0 + zs * zr, highZ = z0 + (zs + 1) * zr;

        double xd = ix ? (x - lowX) / (highX - lowX) : 0;
        double yd = iy ? (y - lowY) / (highY - lowY) : 0;
        double zd = iz ? (z - lowZ) / (highZ - lowZ) : 0;

        double c00 = ix ? D(xs, ys, zs) * (1 - xd) + D(xs + 1, ys, zs) * xd : D(xs, ys, zs);
        double c10 = iy ? ix ? D(xs, ys + 1, zs) * (1 - xd) + D(xs + 1, ys + 1, zs) * xd : D(xs, ys + 1, zs) : 0;
        double c01 = iz ? ix ? D(xs, ys, zs + 1) * (1 - xd) + D(xs + 1, ys, zs + 1) * xd : D(xs, ys, zs + 1) : 0;
        double c11 = iy && iz ? ix ? D(xs, ys + 1, zs + 1) * (1 - xd) + D(xs + 1, ys + 1, zs + 1) * xd : D(xs, ys + 1, zs + 1) : 0;
        double c0 = iy ? c00 * (1 - yd) + c10 * yd : c00;
        double c1 = iy ? c01 * (1 - yd) + c11 * yd : c01;
        return iz ? c0 * (1 - zd) + c1 * zd : c0;

        double D(int a, int b, int c) => g[a + b * dimX + c * dimX * dimY];
    }
}
