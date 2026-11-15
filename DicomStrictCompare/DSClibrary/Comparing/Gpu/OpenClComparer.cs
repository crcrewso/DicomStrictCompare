using System.Text;
using Silk.NET.OpenCL;
using static DSClibrary.Comparing.Gpu.GpuTables;

namespace DSClibrary.Comparing.Gpu
{
    /// <summary>
    /// The comparison as a hand-written OpenCL C kernel, through Silk.NET's bindings. One work item per (i, j) line
    /// in the threshold box. FP_CONTRACT is off so a*b+c is never fused, which the engine never does: in double this
    /// reproduces <see cref="HandWrittenComparer"/> exactly. One pair at a time per instance, uploaded once for all its
    /// criteria (<see cref="CompareAll"/>).
    /// </summary>
    public sealed unsafe class OpenClComparer : DoseComparer, IDisposable
    {
        private const string Source = """
            #pragma OPENCL FP_CONTRACT OFF
            #ifdef USE_DOUBLE
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            typedef double real;
            #else
            typedef float real;
            #endif

            real dose(__global const real* g, int dimX, int dimY, __global const int* h, __global const int* ia, __global const real* ra,
                      int tx, int ty, int tz, int i, int j, int k)
            {
                int xs = ia[h[15 + tx] + i], ix = ia[h[27 + tx] + i]; real xd = ra[h[39 + tx] + i];
                int ys = ia[h[15 + ty] + j], iy = ia[h[27 + ty] + j]; real yd = ra[h[39 + ty] + j];
                int zs = ia[h[15 + tz] + k], iz = ia[h[27 + tz] + k]; real zd = ra[h[39 + tz] + k];
                int sy = dimX, sz = dimX * dimY, b = xs + ys * sy + zs * sz;
                real c00 = ix ? g[b] * (1 - xd) + g[b + 1] * xd : g[b];
                real c10 = iy ? (ix ? g[b + sy] * (1 - xd) + g[b + sy + 1] * xd : g[b + sy]) : 0;
                real c01 = iz ? (ix ? g[b + sz] * (1 - xd) + g[b + sz + 1] * xd : g[b + sz]) : 0;
                real c11 = (iy && iz) ? (ix ? g[b + sy + sz] * (1 - xd) + g[b + sy + sz + 1] * xd : g[b + sy + sz]) : 0;
                real c0 = iy ? c00 * (1 - yd) + c10 * yd : c00;
                real c1 = iy ? c01 * (1 - yd) + c11 * yd : c01;
                return iz ? c0 * (1 - zd) + c1 * zd : c0;
            }

            #define ADD(v) { n++; if ((v) < lo) lo = (v); if ((v) > hi) hi = (v); }

            int evaluate(__global const real* s, __global const real* t, __global const int* h, __global const int* ia, __global const real* ra,
                         int i, int j, int k)
            {
                real maxSource = ra[0];
                real sourcei = dose(s, h[0], h[1], h, ia, ra, 0, 1, 2, i, j, k);
                if (sourcei < maxSource * ra[1]) return 0;
                real targeti = dose(t, h[2], h[3], h, ia, ra, 3, 4, 5, i, j, k);
                real allowed = h[7] != 0 ? maxSource * ra[2] : ra[2] * sourcei;
                if (fabs(targeti - sourcei) <= allowed) return 1;
                if (h[8] == 0) return 2;
                real lo = INFINITY, hi = -INFINITY, v;
                int n = 0;
                if (ia[h[51] + i]) { v = dose(s, h[0], h[1], h, ia, ra, 6, 1, 2, i, j, k); ADD(v) }
                if (ia[h[52] + i]) { v = dose(s, h[0], h[1], h, ia, ra, 7, 1, 2, i, j, k); ADD(v) }
                if (ia[h[53] + j]) { v = dose(s, h[0], h[1], h, ia, ra, 0, 8, 2, i, j, k); ADD(v) }
                if (ia[h[54] + j]) { v = dose(s, h[0], h[1], h, ia, ra, 0, 9, 2, i, j, k); ADD(v) }
                if (ia[h[55] + k]) { v = dose(s, h[0], h[1], h, ia, ra, 0, 1, 10, i, j, k); ADD(v) }
                if (ia[h[56] + k]) { v = dose(s, h[0], h[1], h, ia, ra, 0, 1, 11, i, j, k); ADD(v) }
                return (n == 0 || targeti < lo || targeti > hi) ? 2 : 1;
            }

            __kernel void compare(__global const real* s, __global const real* t, __global const int* h, __global const int* ia,
                                  __global const real* ra, __global int* comparedOut, __global int* failedOut)
            {
                int gi = get_global_id(0), gj = get_global_id(1), wy = h[13] - h[10] + 1;
                int i = h[9] + gi, j = h[10] + gj;
                int compared = 0, failed = 0;
                for (int k = h[11]; k <= h[14]; k++)
                {
                    int r = evaluate(s, t, h, ia, ra, i, j, k);
                    if (r > 0) { compared++; if (r == 2) failed++; }
                }
                comparedOut[gi * wy + gj] = compared;
                failedOut[gi * wy + gj] = failed;
            }
            """;

        private readonly CL _cl = CL.GetApi();
        private readonly nint _device, _context, _queue, _program, _kernel;
        private readonly object _gate = new();
        private readonly long _maxAllocation;
        // The grid blocks' device buffers, kept between pairs and grown to the largest block so far.
        private (nint Buffer, long Bytes) _sourceBlock, _targetBlock;

        public GpuPrecision Precision { get; }
        public string DeviceName { get; }
        public bool UseThresholdBox { get; init; } = true;

        public override string Description => $"OpenCL (Silk.NET), {Precision.ToString().ToLowerInvariant()}, {DeviceName}{(UseThresholdBox ? ", threshold box" : "")}";

        public static OpenClComparer Create(ComparerSettings settings)
        {
            try { return new OpenClComparer(settings.Precision, settings.Device) { UseThresholdBox = settings.UseThresholdBox }; }
            catch (ComparerUnavailableException) { throw; }
            catch (Exception ex) { throw new ComparerUnavailableException($"OpenCL (Silk.NET) is unavailable: {ex.Message}", ex); }
        }

        public OpenClComparer(GpuPrecision precision = GpuPrecision.Double, string? device = null)
        {
            Precision = precision;
            uint count;
            if (_cl.GetPlatformIDs(0, null, &count) != 0 || count == 0) throw new ComparerUnavailableException("No OpenCL platform.");
            var platforms = new nint[count];
            fixed (nint* pp = platforms) Check(_cl.GetPlatformIDs(count, pp, null), "GetPlatformIDs");
            // Microsoft's OpenCLOn12 layer (Windows) offers OpenCL C 1.2, no fp64 and a 1 GiB allocation limit; crane
            // found it beside AMD's own platform on 2026-11-15. Vendor platforms first, it last.
            foreach (var platform in platforms.OrderBy(p => PlatformName(p).Contains("OpenCLOn12", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
            {
                uint n;
                if (_cl.GetDeviceIDs(platform, DeviceType.Gpu, 0, null, &n) != 0 || n == 0) continue;
                var devices = new nint[n];
                fixed (nint* dp = devices) Check(_cl.GetDeviceIDs(platform, DeviceType.Gpu, n, dp, null), "GetDeviceIDs");
                foreach (var d in devices)
                {
                    if (device is not null && !Info(d).Contains(device, StringComparison.OrdinalIgnoreCase)) continue;
                    if (precision == GpuPrecision.Double && !Extensions(d).Contains("cl_khr_fp64")) continue;
                    _device = d;
                    break;
                }
                if (_device != 0) break;
            }
            if (_device == 0)
                throw new ComparerUnavailableException((device is null ? "No OpenCL GPU" : $"No OpenCL GPU named like '{device}'")
                    + (precision == GpuPrecision.Double ? " with double precision (cl_khr_fp64)." : "."));
            DeviceName = Info(_device);
            ulong maxAlloc;
            Check(_cl.GetDeviceInfo(_device, DeviceInfo.MaxMemAllocSize, (nuint)sizeof(ulong), &maxAlloc, null), "GetDeviceInfo");
            _maxAllocation = (long)Math.Min(maxAlloc, long.MaxValue / 4);

            int err;
            nint dev = _device;
            _context = _cl.CreateContext(null, 1, &dev, null, null, &err); Check(err, "CreateContext");
            _queue = _cl.CreateCommandQueue(_context, _device, CommandQueueProperties.None, &err); Check(err, "CreateCommandQueue");
            _program = _cl.CreateProgramWithSource(_context, 1, new[] { Source }, null, &err); Check(err, "CreateProgramWithSource");
            int built = _cl.BuildProgram(_program, 1, &dev, precision == GpuPrecision.Double ? "-D USE_DOUBLE" : "", null, null);
            if (built != 0)
            {
                nuint size;
                _cl.GetProgramBuildInfo(_program, _device, ProgramBuildInfo.BuildLog, 0, null, &size);
                var log = new byte[(int)size];
                fixed (byte* l = log) _cl.GetProgramBuildInfo(_program, _device, ProgramBuildInfo.BuildLog, size, l, null);
                throw new ComparerUnavailableException($"OpenCL build failed on {DeviceName} ({precision}): {Encoding.UTF8.GetString(log).Trim()}");
            }
            _kernel = _cl.CreateKernel(_program, "compare", &err); Check(err, "CreateKernel");
        }

        public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta) =>
            Compare(source, target, dta, false, source.MaxPointDose.Dose, CancellationToken.None);
        public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta) =>
            Compare(source, target, dta, true, source.MaxPointDose.Dose, CancellationToken.None);
        public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, CancellationToken cancellation) =>
            Compare(source, target, dta, false, source.MaxPointDose.Dose, cancellation);
        public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, CancellationToken cancellation) =>
            Compare(source, target, dta, true, source.MaxPointDose.Dose, cancellation);

        public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, CancellationToken cancellation) =>
            Compare(source, target, dta, false, referenceMaximum, cancellation);
        public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, CancellationToken cancellation) =>
            Compare(source, target, dta, true, referenceMaximum, cancellation);

        private SingleComparison Compare(DoseMatrixOptimal source, DoseMatrixOptimal target, Dta dta, bool global, double referenceMaximum, CancellationToken cancellation) =>
            CompareAll(source, target, new[] { new ComparisonRun(dta, global, referenceMaximum) }, cancellation)[0];

        /// <summary>Uploads the pair once (the union of every criterion's reads), then runs one kernel per criterion.</summary>
        public override IReadOnlyList<SingleComparison> CompareAll(DoseMatrixOptimal source, DoseMatrixOptimal target,
            IReadOnlyList<ComparisonRun> runs, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            var setups = runs.Select(r => ComparisonSetup.For(source, target, r.Dta, r.Global, UseThresholdBox, r.ReferenceMaximum)).ToArray();
            var upload = GpuUpload.For(setups, source, target);
            var counts = new (int Compared, int Failed)[setups.Length];
            if (upload is not null)
            {
                lock (_gate)
                {
                    counts = Precision == GpuPrecision.Double
                        ? RunAll(upload.Source, upload.Target, setups, upload, v => v, cancellation)
                        : RunAll(Array.ConvertAll(upload.Source, v => (float)v), Array.ConvertAll(upload.Target, v => (float)v), setups, upload, v => (float)v, cancellation);
                }
            }
            return setups.Select((s, n) => new SingleComparison(runs[n].Dta, s.TotalCount, counts[n].Compared, counts[n].Failed)).ToArray();
        }

        private (int, int)[] RunAll<T>(T[] s, T[] tv, ComparisonSetup[] setups, GpuUpload upload, Func<double, T> real, CancellationToken cancellation) where T : unmanaged
        {
            nint Buffer<TB>(List<nint> owner, TB[] data) where TB : unmanaged
            {
                int err;
                nint b;
                fixed (TB* d = data) b = _cl.CreateBuffer(_context, MemFlags.ReadOnly | MemFlags.CopyHostPtr, (nuint)(data.LongLength * sizeof(TB)), d, &err);
                Check(err, "CreateBuffer");
                owner.Add(b);
                return b;
            }
            try
            {
                // A clear failure on the pair's row, rather than OpenCL's -61, when one grid's block exceeds what the device allows.
                long largest = Math.Max(s.LongLength, tv.LongLength) * sizeof(T);
                if (largest > _maxAllocation)
                    throw new InvalidOperationException($"A grid block of {largest / 1048576.0:0} MB exceeds {DeviceName}'s {_maxAllocation / 1048576.0:0} MB per buffer.");
                nint sb = Block(ref _sourceBlock, s), tb = Block(ref _targetBlock, tv);
                var results = new (int, int)[setups.Length];
                for (int n = 0; n < setups.Length; n++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var t = new GpuTables(setups[n], upload);
                    if (t.Lines == 0) continue;
                    var run = new List<nint>();
                    try
                    {
                        nint hb = Buffer(run, t.Header), ib = Buffer(run, t.Ints), rb = Buffer(run, Array.ConvertAll(t.Reals, v => real(v)));
                        int err;
                        nint cb = _cl.CreateBuffer(_context, MemFlags.WriteOnly, (nuint)(t.Lines * 4), null, &err); Check(err, "CreateBuffer"); run.Add(cb);
                        nint fb = _cl.CreateBuffer(_context, MemFlags.WriteOnly, (nuint)(t.Lines * 4), null, &err); Check(err, "CreateBuffer"); run.Add(fb);
                        nint[] args = { sb, tb, hb, ib, rb, cb, fb };
                        for (uint a = 0; a < args.Length; a++)
                        {
                            nint arg = args[a];
                            Check(_cl.SetKernelArg(_kernel, a, (nuint)sizeof(nint), &arg), "SetKernelArg");
                        }
                        nuint* global = stackalloc nuint[2] { (nuint)(t.Lines / t.LinesY), (nuint)t.LinesY };
                        Check(_cl.EnqueueNdrangeKernel(_queue, _kernel, 2, null, global, null, 0, null, null), "EnqueueNdrangeKernel");
                        var c = new int[t.Lines];
                        var f = new int[t.Lines];
                        fixed (int* cp = c) Check(_cl.EnqueueReadBuffer(_queue, cb, true, 0, (nuint)(t.Lines * 4), cp, 0, null, null), "EnqueueReadBuffer");
                        fixed (int* fp = f) Check(_cl.EnqueueReadBuffer(_queue, fb, true, 0, (nuint)(t.Lines * 4), fp, 0, null, null), "EnqueueReadBuffer");
                        results[n] = (c.Sum(), f.Sum());
                    }
                    finally
                    {
                        foreach (var b in run) _cl.ReleaseMemObject(b);
                    }
                }
                return results;
            }
            catch
            {
                // After a failure the blocks' contents are unknown; start the next pair with new ones.
                ReleaseBlocks();
                throw;
            }
        }

        /// <summary>Writes a grid block into its kept device buffer, replacing the buffer only when it is too small.</summary>
        private nint Block<T>(ref (nint Buffer, long Bytes) block, T[] data) where T : unmanaged
        {
            long bytes = Math.Max(1, data.LongLength) * sizeof(T);
            if (block.Buffer == 0 || block.Bytes < bytes)
            {
                if (block.Buffer != 0) _cl.ReleaseMemObject(block.Buffer);
                block = (0, 0);
                int err;
                nint b = _cl.CreateBuffer(_context, MemFlags.ReadOnly, (nuint)bytes, null, &err);
                Check(err, "CreateBuffer");
                block = (b, bytes);
            }
            fixed (T* d = data)
                Check(_cl.EnqueueWriteBuffer(_queue, block.Buffer, true, 0, (nuint)(data.LongLength * sizeof(T)), d, 0, null, null), "EnqueueWriteBuffer");
            return block.Buffer;
        }

        private void ReleaseBlocks()
        {
            if (_sourceBlock.Buffer != 0) _cl.ReleaseMemObject(_sourceBlock.Buffer);
            if (_targetBlock.Buffer != 0) _cl.ReleaseMemObject(_targetBlock.Buffer);
            _sourceBlock = _targetBlock = (0, 0);
        }

        private string Extensions(nint device)
        {
            nuint size;
            _cl.GetDeviceInfo(device, DeviceInfo.Extensions, 0, null, &size);
            var text = new byte[(int)size];
            fixed (byte* t = text) _cl.GetDeviceInfo(device, DeviceInfo.Extensions, size, t, null);
            return Encoding.UTF8.GetString(text);
        }

        private string PlatformName(nint platform)
        {
            nuint size;
            _cl.GetPlatformInfo(platform, PlatformInfo.Name, 0, null, &size);
            var text = new byte[(int)size];
            fixed (byte* t = text) _cl.GetPlatformInfo(platform, PlatformInfo.Name, size, t, null);
            return Encoding.UTF8.GetString(text).TrimEnd('\0');
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
            ReleaseBlocks();
            if (_kernel != 0) _cl.ReleaseKernel(_kernel);
            if (_program != 0) _cl.ReleaseProgram(_program);
            if (_queue != 0) _cl.ReleaseCommandQueue(_queue);
            if (_context != 0) _cl.ReleaseContext(_context);
            _cl.Dispose();
        }
    }
}
