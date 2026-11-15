using System.Diagnostics;
using System.Text;
using Silk.NET.OpenCL;
using static DSCgpu.ComparisonPlan;

namespace DSCgpu;

/// <summary>
/// The engine's point rules as a hand-written OpenCL C kernel through Silk.NET's bindings. One source,
/// built for double and for float. FP_CONTRACT is off so the compiler cannot fuse a*b+c, which the engine
/// does not do.
/// </summary>
public sealed unsafe class OpenClComparer : IDisposable
{
    private const string Source = """
        #pragma OPENCL FP_CONTRACT OFF
        #ifdef USE_DOUBLE
        #pragma OPENCL EXTENSION cl_khr_fp64 : enable
        typedef double real;
        #else
        typedef float real;
        #endif

        real dose(__global const real* g, __global const real* p, int o, int dimX, int dimY, real x, real y, real z)
        {
            real x0 = p[o], y0 = p[o + 1], z0 = p[o + 2], xr = p[o + 3], yr = p[o + 4], zr = p[o + 5];
            int ix = fmod((x - x0) / xr, (real)1) != (real)0; int xs = (int)((x - x0) / xr);
            real lowX = x0 + xs * xr, highX = x0 + (xs + 1) * xr;
            int iy = fmod((y - y0) / yr, (real)1) != (real)0; int ys = (int)((y - y0) / yr);
            real lowY = y0 + ys * yr, highY = y0 + (ys + 1) * yr;
            int iz = fmod((z - z0) / zr, (real)1) != (real)0; int zs = (int)((z - z0) / zr);
            real lowZ = z0 + zs * zr, highZ = z0 + (zs + 1) * zr;

            real xd = ix ? (x - lowX) / (highX - lowX) : 0;
            real yd = iy ? (y - lowY) / (highY - lowY) : 0;
            real zd = iz ? (z - lowZ) / (highZ - lowZ) : 0;

            int sy = dimX, sz = dimX * dimY;
            int b = xs + ys * sy + zs * sz;
            real c00 = ix ? g[b] * (1 - xd) + g[b + 1] * xd : g[b];
            real c10 = iy ? (ix ? g[b + sy] * (1 - xd) + g[b + sy + 1] * xd : g[b + sy]) : 0;
            real c01 = iz ? (ix ? g[b + sz] * (1 - xd) + g[b + sz + 1] * xd : g[b + sz]) : 0;
            real c11 = (iy && iz) ? (ix ? g[b + sy + sz] * (1 - xd) + g[b + sy + sz + 1] * xd : g[b + sy + sz]) : 0;
            real c0 = iy ? c00 * (1 - yd) + c10 * yd : c00;
            real c1 = iy ? c01 * (1 - yd) + c11 * yd : c01;
            return iz ? c0 * (1 - zd) + c1 * zd : c0;
        }

        #define ADD(v) { n++; if ((v) < lo) lo = (v); if ((v) > hi) hi = (v); }

        int evaluate(__global const real* s, __global const real* t, __global const real* p, __global const int* q, real x, real y, real z)
        {
            real maxSource = p[21];
            real sourcei = dose(s, p, 0, q[0], q[1], x, y, z);
            if (sourcei < maxSource * p[22]) return 0;
            real targeti = dose(t, p, 6, q[2], q[3], x, y, z);
            real allowed = q[7] != 0 ? maxSource * p[23] : p[23] * sourcei;
            if (fabs(targeti - sourcei) <= allowed) return 1;
            if (q[8] == 0) return 2;

            real d = p[24], lo = INFINITY, hi = -INFINITY, v;
            int n = 0;
            if (x - d >= p[12]) { v = dose(s, p, 0, q[0], q[1], x - d, y, z); ADD(v) }
            if (x + d <= p[13]) { v = dose(s, p, 0, q[0], q[1], x + d, y, z); ADD(v) }
            if (y - d >= p[14]) { v = dose(s, p, 0, q[0], q[1], x, y - d, z); ADD(v) }
            if (y + d <= p[15]) { v = dose(s, p, 0, q[0], q[1], x, y + d, z); ADD(v) }
            if (z - d >= p[16]) { v = dose(s, p, 0, q[0], q[1], x, y, z - d); ADD(v) }
            if (z + d <= p[17]) { v = dose(s, p, 0, q[0], q[1], x, y, z + d); ADD(v) }
            return (n == 0 || targeti < lo || targeti > hi) ? 2 : 1;
        }

        __kernel void compare(__global const real* s, __global const real* t, __global const real* p, __global const int* q,
                              __global int* comparedOut, __global int* failedOut)
        {
            int gi = get_global_id(0), gj = get_global_id(1), wy = q[12] - q[11] + 1;
            int i = q[9] + gi, j = q[11] + gj;
            int compared = 0, failed = 0;
            real x = p[12] + i * p[18];
            real y = p[14] + j * p[19];
            for (int k = q[13]; k <= q[14]; k++)
            {
                real z = p[16] + k * p[20];
                int r = evaluate(s, t, p, q, x, y, z);
                if (r > 0) { compared++; if (r == 2) failed++; }
            }
            comparedOut[gi * wy + gj] = compared;
            failedOut[gi * wy + gj] = failed;
        }
        """;

    private readonly CL _cl = CL.GetApi();
    private readonly nint _device, _context, _queue, _programD, _programF, _kernelD, _kernelF;

    public string Device { get; }

    public OpenClComparer()
    {
        uint count;
        Check(_cl.GetPlatformIDs(0, null, &count), "GetPlatformIDs");
        var platforms = new nint[count];
        fixed (nint* pp = platforms) Check(_cl.GetPlatformIDs(count, pp, null), "GetPlatformIDs");
        foreach (var platform in platforms)
        {
            uint n;
            if (_cl.GetDeviceIDs(platform, DeviceType.Gpu, 0, null, &n) != 0 || n == 0) continue;
            nint dev;
            Check(_cl.GetDeviceIDs(platform, DeviceType.Gpu, 1, &dev, null), "GetDeviceIDs");
            _device = dev;
            break;
        }
        if (_device == 0) throw new InvalidOperationException("No OpenCL GPU found.");
        Device = Info(_device);

        int err;
        nint device = _device;
        _context = _cl.CreateContext(null, 1, &device, null, null, &err); Check(err, "CreateContext");
        _queue = _cl.CreateCommandQueue(_context, _device, CommandQueueProperties.None, &err); Check(err, "CreateCommandQueue");
        (_programD, _kernelD) = Build("-D USE_DOUBLE");
        (_programF, _kernelF) = Build("");
    }

    private (nint Program, nint Kernel) Build(string options)
    {
        int err;
        nint program = _cl.CreateProgramWithSource(_context, 1, new[] { Source }, null, &err); Check(err, "CreateProgramWithSource");
        nint device = _device;
        int built = _cl.BuildProgram(program, 1, &device, options, null, null);
        if (built != 0)
        {
            nuint size;
            _cl.GetProgramBuildInfo(program, _device, ProgramBuildInfo.BuildLog, 0, null, &size);
            var log = new byte[(int)size];
            fixed (byte* l = log) _cl.GetProgramBuildInfo(program, _device, ProgramBuildInfo.BuildLog, size, l, null);
            throw new InvalidOperationException($"OpenCL build failed ({built}): {Encoding.UTF8.GetString(log)}");
        }
        nint kernel = _cl.CreateKernel(program, "compare", &err); Check(err, "CreateKernel");
        return (program, kernel);
    }

    public (Counts Counts, double UploadS, double KernelS) Compare(double[] s, double[] t, double[] p, int[] q) =>
        Run(_kernelD, s, t, p, q, sizeof(double));

    public (Counts Counts, double UploadS, double KernelS) CompareFloat(float[] s, float[] t, double[] p, int[] q) =>
        Run(_kernelF, s, t, p.Select(v => (float)v).ToArray(), q, sizeof(float));

    private (Counts, double, double) Run<T>(nint kernel, T[] s, T[] t, T[] p, int[] q, int bytes) where T : unmanaged
    {
        var clock = Stopwatch.StartNew();
        int lines = WindowLines(q);
        if (lines == 0) return (IlgpuComparer.Sum(Array.Empty<int>(), Array.Empty<int>(), q), 0, 0);
        var buffers = new List<nint>();
        nint Buffer<TB>(TB[] data, MemFlags flags) where TB : unmanaged
        {
            int err;
            nint b;
            fixed (TB* d = data) b = _cl.CreateBuffer(_context, flags | MemFlags.CopyHostPtr, (nuint)(data.LongLength * sizeof(TB)), d, &err);
            Check(err, "CreateBuffer");
            buffers.Add(b);
            return b;
        }
        try
        {
            nint sb = Buffer(s, MemFlags.ReadOnly), tb = Buffer(t, MemFlags.ReadOnly), pb = Buffer(p, MemFlags.ReadOnly), qb = Buffer(q, MemFlags.ReadOnly);
            int err;
            nint cb = _cl.CreateBuffer(_context, MemFlags.WriteOnly, (nuint)(lines * 4), null, &err); Check(err, "CreateBuffer"); buffers.Add(cb);
            nint fb = _cl.CreateBuffer(_context, MemFlags.WriteOnly, (nuint)(lines * 4), null, &err); Check(err, "CreateBuffer"); buffers.Add(fb);
            Check(_cl.Finish(_queue), "Finish");
            double upload = clock.Elapsed.TotalSeconds;

            nint[] args = { sb, tb, pb, qb, cb, fb };
            for (uint a = 0; a < args.Length; a++)
            {
                nint arg = args[a];
                Check(_cl.SetKernelArg(kernel, a, (nuint)sizeof(nint), &arg), "SetKernelArg");
            }
            nuint* global = stackalloc nuint[2] { (nuint)(q[QI1] - q[QI0] + 1), (nuint)(q[QJ1] - q[QJ0] + 1) };
            Check(_cl.EnqueueNdrangeKernel(_queue, kernel, 2, null, global, null, 0, null, null), "EnqueueNdrangeKernel");
            Check(_cl.Finish(_queue), "Finish");
            double kernelS = clock.Elapsed.TotalSeconds - upload;

            var compared = new int[lines];
            var failed = new int[lines];
            fixed (int* c = compared) Check(_cl.EnqueueReadBuffer(_queue, cb, true, 0, (nuint)(lines * 4), c, 0, null, null), "EnqueueReadBuffer");
            fixed (int* f = failed) Check(_cl.EnqueueReadBuffer(_queue, fb, true, 0, (nuint)(lines * 4), f, 0, null, null), "EnqueueReadBuffer");
            return (IlgpuComparer.Sum(compared, failed, q), upload, kernelS);
        }
        finally
        {
            foreach (var b in buffers) _cl.ReleaseMemObject(b);
        }
    }

    private string Info(nint device)
    {
        nuint size;
        _cl.GetDeviceInfo(device, DeviceInfo.Name, 0, null, &size);
        var name = new byte[(int)size];
        fixed (byte* n = name) _cl.GetDeviceInfo(device, DeviceInfo.Name, size, n, null);
        return Encoding.UTF8.GetString(name).TrimEnd('\0');
    }

    private static void Check(int err, string what)
    {
        if (err != 0) throw new InvalidOperationException($"OpenCL {what} failed: {err}");
    }

    public void Dispose()
    {
        _cl.ReleaseKernel(_kernelD); _cl.ReleaseKernel(_kernelF);
        _cl.ReleaseProgram(_programD); _cl.ReleaseProgram(_programF);
        _cl.ReleaseCommandQueue(_queue); _cl.ReleaseContext(_context);
        _cl.Dispose();
    }
}
