using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.OpenCL;
using static DSCgpu.ComparisonPlan;

namespace DSCgpu;

/// <summary>
/// The engine's point rules as an ILGPU kernel: one work item per (i, j) line along z, as in
/// <see cref="CpuPort"/>. Double is the engine's arithmetic; float is a possible screening pass.
/// </summary>
public sealed class IlgpuComparer : IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index2D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<int>, ArrayView<int>, ArrayView<int>> _double;
    private readonly Action<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<int>, ArrayView<int>> _float;

    public string Device => _accelerator.Name;

    static IlgpuComparer()
    {
        // ILGPU imports "OpenCL", which Linux resolves only through libOpenCL.so, a dev-package symlink
        // (ocl-icd-opencl-dev) that runtime-only installs lack; libOpenCL.so.1 is always there. Measured 2026-11-14.
        if (OperatingSystem.IsLinux())
            System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(Context).Assembly, (name, _, _) =>
                name.Contains("OpenCL", StringComparison.OrdinalIgnoreCase)
                && System.Runtime.InteropServices.NativeLibrary.TryLoad("libOpenCL.so.1", out var h) ? h : IntPtr.Zero);
    }

    public IlgpuComparer(bool cpu = false)
    {
        _context = Context.Create(b => { if (cpu) b.CPU(); else b.OpenCL(); });
        var device = _context.Devices.First(d => cpu ? d.AcceleratorType == AcceleratorType.CPU : d.AcceleratorType == AcceleratorType.OpenCL);
        _accelerator = device.CreateAccelerator(_context);
        _double = _accelerator.LoadAutoGroupedStreamKernel<Index2D, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<int>, ArrayView<int>, ArrayView<int>>(KernelDouble);
        _float = _accelerator.LoadAutoGroupedStreamKernel<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<int>, ArrayView<int>>(KernelFloat);
    }

    public (Counts Counts, double UploadS, double KernelS) Compare(double[] s, double[] t, double[] p, int[] q)
    {
        var clock = Stopwatch.StartNew();
        using var sb = _accelerator.Allocate1D(s);
        using var tb = _accelerator.Allocate1D(t);
        using var pb = _accelerator.Allocate1D(p);
        using var qb = _accelerator.Allocate1D(q);
        if (WindowLines(q) == 0) return (Sum(Array.Empty<int>(), Array.Empty<int>(), q), 0, 0);
        using var cb = _accelerator.Allocate1D<int>(WindowLines(q));
        using var fb = _accelerator.Allocate1D<int>(WindowLines(q));
        _accelerator.Synchronize();
        double upload = clock.Elapsed.TotalSeconds;
        _double(new Index2D(q[QI1] - q[QI0] + 1, q[QJ1] - q[QJ0] + 1), sb.View, tb.View, pb.View, qb.View, cb.View, fb.View);
        _accelerator.Synchronize();
        double kernel = clock.Elapsed.TotalSeconds - upload;
        return (Sum(cb.GetAsArray1D(), fb.GetAsArray1D(), q), upload, kernel);
    }

    public (Counts Counts, double UploadS, double KernelS) CompareFloat(float[] s, float[] t, double[] p, int[] q)
    {
        var clock = Stopwatch.StartNew();
        using var sb = _accelerator.Allocate1D(s);
        using var tb = _accelerator.Allocate1D(t);
        using var pb = _accelerator.Allocate1D(p.Select(v => (float)v).ToArray());
        using var qb = _accelerator.Allocate1D(q);
        if (WindowLines(q) == 0) return (Sum(Array.Empty<int>(), Array.Empty<int>(), q), 0, 0);
        using var cb = _accelerator.Allocate1D<int>(WindowLines(q));
        using var fb = _accelerator.Allocate1D<int>(WindowLines(q));
        _accelerator.Synchronize();
        double upload = clock.Elapsed.TotalSeconds;
        _float(new Index2D(q[QI1] - q[QI0] + 1, q[QJ1] - q[QJ0] + 1), sb.View, tb.View, pb.View, qb.View, cb.View, fb.View);
        _accelerator.Synchronize();
        double kernel = clock.Elapsed.TotalSeconds - upload;
        return (Sum(cb.GetAsArray1D(), fb.GetAsArray1D(), q), upload, kernel);
    }

    internal static Counts Sum(int[] compared, int[] failed, int[] q) =>
        new((long)q[QNx] * q[QNy] * q[QNz], compared.Sum(c => (long)c), failed.Sum(f => (long)f));

    // ---- double ----

    private static void KernelDouble(Index2D idx, ArrayView<double> s, ArrayView<double> t, ArrayView<double> p, ArrayView<int> q,
        ArrayView<int> comparedOut, ArrayView<int> failedOut)
    {
        int i = q[QI0] + idx.X, j = q[QJ0] + idx.Y, wy = q[QJ1] - q[QJ0] + 1;
        int compared = 0, failed = 0;
        double x = p[PRegion] + i * p[PStep];
        double y = p[PRegion + 2] + j * p[PStep + 1];
        for (int k = q[QK0]; k <= q[QK1]; k++)
        {
            double z = p[PRegion + 4] + k * p[PStep + 2];
            int r = EvaluateD(s, t, p, q, x, y, z);
            if (r > 0) { compared++; if (r == 2) failed++; }
        }
        comparedOut[idx.X * wy + idx.Y] = compared;
        failedOut[idx.X * wy + idx.Y] = failed;
    }

    private static int EvaluateD(ArrayView<double> s, ArrayView<double> t, ArrayView<double> p, ArrayView<int> q, double x, double y, double z)
    {
        double maxSource = p[PMax];
        double sourcei = DoseD(s, p, PS, q[QSDimX], q[QSDimY], x, y, z);
        if (sourcei < maxSource * p[PThr]) return 0;
        double targeti = DoseD(t, p, PT, q[QTDimX], q[QTDimY], x, y, z);
        double allowed = q[QGlobal] != 0 ? maxSource * p[PTol] : p[PTol] * sourcei;
        if (Math.Abs(targeti - sourcei) <= allowed) return 1;
        if (q[QDta] == 0) return 2;

        double d = p[PDist];
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity, v;
        int n = 0;
        if (x - d >= p[PRegion]) { v = DoseD(s, p, PS, q[QSDimX], q[QSDimY], x - d, y, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (x + d <= p[PRegion + 1]) { v = DoseD(s, p, PS, q[QSDimX], q[QSDimY], x + d, y, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (y - d >= p[PRegion + 2]) { v = DoseD(s, p, PS, q[QSDimX], q[QSDimY], x, y - d, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (y + d <= p[PRegion + 3]) { v = DoseD(s, p, PS, q[QSDimX], q[QSDimY], x, y + d, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (z - d >= p[PRegion + 4]) { v = DoseD(s, p, PS, q[QSDimX], q[QSDimY], x, y, z - d); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (z + d <= p[PRegion + 5]) { v = DoseD(s, p, PS, q[QSDimX], q[QSDimY], x, y, z + d); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        return n == 0 || targeti < lo || targeti > hi ? 2 : 1;
    }

    private static double DoseD(ArrayView<double> g, ArrayView<double> p, int o, int dimX, int dimY, double x, double y, double z)
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

        int sy = dimX, sz = dimX * dimY;
        int b = xs + ys * sy + zs * sz;
        double c00 = ix ? g[b] * (1 - xd) + g[b + 1] * xd : g[b];
        double c10 = iy ? ix ? g[b + sy] * (1 - xd) + g[b + sy + 1] * xd : g[b + sy] : 0;
        double c01 = iz ? ix ? g[b + sz] * (1 - xd) + g[b + sz + 1] * xd : g[b + sz] : 0;
        double c11 = iy && iz ? ix ? g[b + sy + sz] * (1 - xd) + g[b + sy + sz + 1] * xd : g[b + sy + sz] : 0;
        double c0 = iy ? c00 * (1 - yd) + c10 * yd : c00;
        double c1 = iy ? c01 * (1 - yd) + c11 * yd : c01;
        return iz ? c0 * (1 - zd) + c1 * zd : c0;
    }

    // ---- float: the same rules in single precision ----

    private static void KernelFloat(Index2D idx, ArrayView<float> s, ArrayView<float> t, ArrayView<float> p, ArrayView<int> q,
        ArrayView<int> comparedOut, ArrayView<int> failedOut)
    {
        int i = q[QI0] + idx.X, j = q[QJ0] + idx.Y, wy = q[QJ1] - q[QJ0] + 1;
        int compared = 0, failed = 0;
        float x = p[PRegion] + i * p[PStep];
        float y = p[PRegion + 2] + j * p[PStep + 1];
        for (int k = q[QK0]; k <= q[QK1]; k++)
        {
            float z = p[PRegion + 4] + k * p[PStep + 2];
            int r = EvaluateF(s, t, p, q, x, y, z);
            if (r > 0) { compared++; if (r == 2) failed++; }
        }
        comparedOut[idx.X * wy + idx.Y] = compared;
        failedOut[idx.X * wy + idx.Y] = failed;
    }

    private static int EvaluateF(ArrayView<float> s, ArrayView<float> t, ArrayView<float> p, ArrayView<int> q, float x, float y, float z)
    {
        float maxSource = p[PMax];
        float sourcei = DoseF(s, p, PS, q[QSDimX], q[QSDimY], x, y, z);
        if (sourcei < maxSource * p[PThr]) return 0;
        float targeti = DoseF(t, p, PT, q[QTDimX], q[QTDimY], x, y, z);
        float allowed = q[QGlobal] != 0 ? maxSource * p[PTol] : p[PTol] * sourcei;
        if (Math.Abs(targeti - sourcei) <= allowed) return 1;
        if (q[QDta] == 0) return 2;

        float d = p[PDist];
        float lo = float.PositiveInfinity, hi = float.NegativeInfinity, v;
        int n = 0;
        if (x - d >= p[PRegion]) { v = DoseF(s, p, PS, q[QSDimX], q[QSDimY], x - d, y, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (x + d <= p[PRegion + 1]) { v = DoseF(s, p, PS, q[QSDimX], q[QSDimY], x + d, y, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (y - d >= p[PRegion + 2]) { v = DoseF(s, p, PS, q[QSDimX], q[QSDimY], x, y - d, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (y + d <= p[PRegion + 3]) { v = DoseF(s, p, PS, q[QSDimX], q[QSDimY], x, y + d, z); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (z - d >= p[PRegion + 4]) { v = DoseF(s, p, PS, q[QSDimX], q[QSDimY], x, y, z - d); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        if (z + d <= p[PRegion + 5]) { v = DoseF(s, p, PS, q[QSDimX], q[QSDimY], x, y, z + d); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
        return n == 0 || targeti < lo || targeti > hi ? 2 : 1;
    }

    private static float DoseF(ArrayView<float> g, ArrayView<float> p, int o, int dimX, int dimY, float x, float y, float z)
    {
        float x0 = p[o], y0 = p[o + 1], z0 = p[o + 2], xr = p[o + 3], yr = p[o + 4], zr = p[o + 5];
        bool ix = (x - x0) / xr % 1 != 0f; int xs = (int)((x - x0) / xr);
        float lowX = x0 + xs * xr, highX = x0 + (xs + 1) * xr;
        bool iy = (y - y0) / yr % 1 != 0f; int ys = (int)((y - y0) / yr);
        float lowY = y0 + ys * yr, highY = y0 + (ys + 1) * yr;
        bool iz = (z - z0) / zr % 1 != 0f; int zs = (int)((z - z0) / zr);
        float lowZ = z0 + zs * zr, highZ = z0 + (zs + 1) * zr;

        float xd = ix ? (x - lowX) / (highX - lowX) : 0;
        float yd = iy ? (y - lowY) / (highY - lowY) : 0;
        float zd = iz ? (z - lowZ) / (highZ - lowZ) : 0;

        int sy = dimX, sz = dimX * dimY;
        int b = xs + ys * sy + zs * sz;
        float c00 = ix ? g[b] * (1 - xd) + g[b + 1] * xd : g[b];
        float c10 = iy ? ix ? g[b + sy] * (1 - xd) + g[b + sy + 1] * xd : g[b + sy] : 0;
        float c01 = iz ? ix ? g[b + sz] * (1 - xd) + g[b + sz + 1] * xd : g[b + sz] : 0;
        float c11 = iy && iz ? ix ? g[b + sy + sz] * (1 - xd) + g[b + sy + sz + 1] * xd : g[b + sy + sz] : 0;
        float c0 = iy ? c00 * (1 - yd) + c10 * yd : c00;
        float c1 = iy ? c01 * (1 - yd) + c11 * yd : c01;
        return iz ? c0 * (1 - zd) + c1 * zd : c0;
    }

    public void Dispose()
    {
        _accelerator.Dispose();
        _context.Dispose();
    }
}
