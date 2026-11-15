using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;

namespace DSClibrary.Comparing.Gpu
{
    /// <summary>
    /// The comparison as an ILGPU kernel written in C#, run through ILGPU's OpenCL backend. One work item per (i, j)
    /// line in the threshold box, reading the same packed tables as <see cref="OpenClComparer"/>. Measured
    /// 2026-11-14 to 15 on Intel's Arc B390: in double it reproduced the engine's counts in every run, but unlike
    /// <see cref="OpenClComparer"/> its floating-point contraction is not set by this code. One pair at a time per
    /// instance, uploaded once for all its criteria (<see cref="CompareAll"/>).
    /// </summary>
    public sealed class IlgpuComparer : DoseComparer, IDisposable
    {
        private readonly Context _context;
        private readonly Accelerator _accelerator;
        private readonly Action<Index2D, ArrayView<double>, ArrayView<double>, ArrayView<int>, ArrayView<int>, ArrayView<double>, ArrayView<int>, ArrayView<int>>? _double;
        private readonly Action<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<int>, ArrayView<float>, ArrayView<int>, ArrayView<int>>? _float;
        private readonly object _gate = new();

        public GpuPrecision Precision { get; }
        public string DeviceName => _accelerator.Name;
        public bool UseThresholdBox { get; init; } = true;

        public override string Description => $"OpenCL (ILGPU), {Precision.ToString().ToLowerInvariant()}, {DeviceName}{(UseThresholdBox ? ", threshold box" : "")}";

        static IlgpuComparer()
        {
            // ILGPU imports "OpenCL", which Linux resolves only through libOpenCL.so, a symlink from the dev package
            // (ocl-icd-opencl-dev); libOpenCL.so.1 is always installed. Measured 2026-11-14.
            if (OperatingSystem.IsLinux())
                NativeLibrary.SetDllImportResolver(typeof(Context).Assembly, (name, _, _) =>
                    name.Contains("OpenCL", StringComparison.OrdinalIgnoreCase) && NativeLibrary.TryLoad("libOpenCL.so.1", out var h) ? h : IntPtr.Zero);
        }

        public static IlgpuComparer Create(ComparerSettings settings)
        {
            try { return new IlgpuComparer(settings.Precision, settings.Device) { UseThresholdBox = settings.UseThresholdBox }; }
            catch (ComparerUnavailableException) { throw; }
            catch (Exception ex) { throw new ComparerUnavailableException($"ILGPU is unavailable: {ex.Message}", ex); }
        }

        public IlgpuComparer(GpuPrecision precision = GpuPrecision.Double, string? device = null)
        {
            Precision = precision;
            _context = Context.Create(b => b.OpenCL());
            var chosen = _context.Devices.FirstOrDefault(d => d.AcceleratorType == AcceleratorType.OpenCL
                && (device is null || d.Name.Contains(device, StringComparison.OrdinalIgnoreCase)));
            if (chosen is null)
            {
                _context.Dispose();
                throw new ComparerUnavailableException(device is null ? "ILGPU found no OpenCL device." : $"ILGPU found no OpenCL device named like '{device}'.");
            }
            _accelerator = chosen.CreateAccelerator(_context);
            if (precision == GpuPrecision.Double)
                _double = _accelerator.LoadAutoGroupedStreamKernel<Index2D, ArrayView<double>, ArrayView<double>, ArrayView<int>, ArrayView<int>, ArrayView<double>, ArrayView<int>, ArrayView<int>>(KernelDouble);
            else
                _float = _accelerator.LoadAutoGroupedStreamKernel<Index2D, ArrayView<float>, ArrayView<float>, ArrayView<int>, ArrayView<int>, ArrayView<float>, ArrayView<int>, ArrayView<int>>(KernelFloat);
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
                    counts = _double is not null
                        ? RunAll(upload.Source, upload.Target, setups, upload, v => v, _double, cancellation)
                        : RunAll(Array.ConvertAll(upload.Source, v => (float)v), Array.ConvertAll(upload.Target, v => (float)v), setups, upload, v => (float)v, _float!, cancellation);
                }
            }
            return setups.Select((s, n) => new SingleComparison(runs[n].Dta, s.TotalCount, counts[n].Compared, counts[n].Failed)).ToArray();
        }

        private (int, int)[] RunAll<T>(T[] s, T[] tv, ComparisonSetup[] setups, GpuUpload upload, Func<double, T> real,
            Action<Index2D, ArrayView<T>, ArrayView<T>, ArrayView<int>, ArrayView<int>, ArrayView<T>, ArrayView<int>, ArrayView<int>> kernel,
            CancellationToken cancellation) where T : unmanaged
        {
            long bytes = (s.LongLength + tv.LongLength) * Interop.SizeOf<T>();
            if (bytes > _accelerator.MemorySize)
                throw new InvalidOperationException($"This pair needs {bytes / 1048576.0:0} MB on {DeviceName}, which has {_accelerator.MemorySize / 1048576.0:0} MB.");
            using var sb = _accelerator.Allocate1D(s);
            using var tb = _accelerator.Allocate1D(tv);
            var results = new (int, int)[setups.Length];
            for (int n = 0; n < setups.Length; n++)
            {
                cancellation.ThrowIfCancellationRequested();
                var t = new GpuTables(setups[n], upload);
                if (t.Lines == 0) continue;
                using var hb = _accelerator.Allocate1D(t.Header);
                using var ib = _accelerator.Allocate1D(t.Ints);
                using var rb = _accelerator.Allocate1D(Array.ConvertAll(t.Reals, v => real(v)));
                using var cb = _accelerator.Allocate1D<int>(t.Lines);
                using var fb = _accelerator.Allocate1D<int>(t.Lines);
                kernel(new Index2D(t.Lines / t.LinesY, t.LinesY), sb.View, tb.View, hb.View, ib.View, rb.View, cb.View, fb.View);
                _accelerator.Synchronize();
                results[n] = (cb.GetAsArray1D().Sum(), fb.GetAsArray1D().Sum());
            }
            return results;
        }

        // ---- kernels: the header layout is GpuTables'. Double first, then the same in float. ----

        private static void KernelDouble(Index2D idx, ArrayView<double> s, ArrayView<double> t, ArrayView<int> h, ArrayView<int> ia,
            ArrayView<double> ra, ArrayView<int> comparedOut, ArrayView<int> failedOut)
        {
            int wy = h[13] - h[10] + 1, i = h[9] + idx.X, j = h[10] + idx.Y;
            int compared = 0, failed = 0;
            for (int k = h[11]; k <= h[14]; k++)
            {
                int r = EvaluateD(s, t, h, ia, ra, i, j, k);
                if (r > 0) { compared++; if (r == 2) failed++; }
            }
            comparedOut[idx.X * wy + idx.Y] = compared;
            failedOut[idx.X * wy + idx.Y] = failed;
        }

        private static int EvaluateD(ArrayView<double> s, ArrayView<double> t, ArrayView<int> h, ArrayView<int> ia, ArrayView<double> ra, int i, int j, int k)
        {
            double maxSource = ra[0];
            double sourcei = DoseD(s, h[0], h[1], h, ia, ra, 0, 1, 2, i, j, k);
            if (sourcei < maxSource * ra[1]) return 0;
            double targeti = DoseD(t, h[2], h[3], h, ia, ra, 3, 4, 5, i, j, k);
            double allowed = h[7] != 0 ? maxSource * ra[2] : ra[2] * sourcei;
            if (Math.Abs(targeti - sourcei) <= allowed) return 1;
            if (h[8] == 0) return 2;
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity, v;
            int n = 0;
            if (ia[h[51] + i] != 0) { v = DoseD(s, h[0], h[1], h, ia, ra, 6, 1, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[52] + i] != 0) { v = DoseD(s, h[0], h[1], h, ia, ra, 7, 1, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[53] + j] != 0) { v = DoseD(s, h[0], h[1], h, ia, ra, 0, 8, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[54] + j] != 0) { v = DoseD(s, h[0], h[1], h, ia, ra, 0, 9, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[55] + k] != 0) { v = DoseD(s, h[0], h[1], h, ia, ra, 0, 1, 10, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[56] + k] != 0) { v = DoseD(s, h[0], h[1], h, ia, ra, 0, 1, 11, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            return n == 0 || targeti < lo || targeti > hi ? 2 : 1;
        }

        private static double DoseD(ArrayView<double> g, int dimX, int dimY, ArrayView<int> h, ArrayView<int> ia, ArrayView<double> ra,
            int tx, int ty, int tz, int i, int j, int k)
        {
            int xs = ia[h[15 + tx] + i]; bool ix = ia[h[27 + tx] + i] != 0; double xd = ra[h[39 + tx] + i];
            int ys = ia[h[15 + ty] + j]; bool iy = ia[h[27 + ty] + j] != 0; double yd = ra[h[39 + ty] + j];
            int zs = ia[h[15 + tz] + k]; bool iz = ia[h[27 + tz] + k] != 0; double zd = ra[h[39 + tz] + k];
            int sy = dimX, sz = dimX * dimY, b = xs + ys * sy + zs * sz;
            double c00 = ix ? g[b] * (1 - xd) + g[b + 1] * xd : g[b];
            double c10 = iy ? ix ? g[b + sy] * (1 - xd) + g[b + sy + 1] * xd : g[b + sy] : 0;
            double c01 = iz ? ix ? g[b + sz] * (1 - xd) + g[b + sz + 1] * xd : g[b + sz] : 0;
            double c11 = iy && iz ? ix ? g[b + sy + sz] * (1 - xd) + g[b + sy + sz + 1] * xd : g[b + sy + sz] : 0;
            double c0 = iy ? c00 * (1 - yd) + c10 * yd : c00;
            double c1 = iy ? c01 * (1 - yd) + c11 * yd : c01;
            return iz ? c0 * (1 - zd) + c1 * zd : c0;
        }

        private static void KernelFloat(Index2D idx, ArrayView<float> s, ArrayView<float> t, ArrayView<int> h, ArrayView<int> ia,
            ArrayView<float> ra, ArrayView<int> comparedOut, ArrayView<int> failedOut)
        {
            int wy = h[13] - h[10] + 1, i = h[9] + idx.X, j = h[10] + idx.Y;
            int compared = 0, failed = 0;
            for (int k = h[11]; k <= h[14]; k++)
            {
                int r = EvaluateF(s, t, h, ia, ra, i, j, k);
                if (r > 0) { compared++; if (r == 2) failed++; }
            }
            comparedOut[idx.X * wy + idx.Y] = compared;
            failedOut[idx.X * wy + idx.Y] = failed;
        }

        private static int EvaluateF(ArrayView<float> s, ArrayView<float> t, ArrayView<int> h, ArrayView<int> ia, ArrayView<float> ra, int i, int j, int k)
        {
            float maxSource = ra[0];
            float sourcei = DoseF(s, h[0], h[1], h, ia, ra, 0, 1, 2, i, j, k);
            if (sourcei < maxSource * ra[1]) return 0;
            float targeti = DoseF(t, h[2], h[3], h, ia, ra, 3, 4, 5, i, j, k);
            float allowed = h[7] != 0 ? maxSource * ra[2] : ra[2] * sourcei;
            if (Math.Abs(targeti - sourcei) <= allowed) return 1;
            if (h[8] == 0) return 2;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity, v;
            int n = 0;
            if (ia[h[51] + i] != 0) { v = DoseF(s, h[0], h[1], h, ia, ra, 6, 1, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[52] + i] != 0) { v = DoseF(s, h[0], h[1], h, ia, ra, 7, 1, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[53] + j] != 0) { v = DoseF(s, h[0], h[1], h, ia, ra, 0, 8, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[54] + j] != 0) { v = DoseF(s, h[0], h[1], h, ia, ra, 0, 9, 2, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[55] + k] != 0) { v = DoseF(s, h[0], h[1], h, ia, ra, 0, 1, 10, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            if (ia[h[56] + k] != 0) { v = DoseF(s, h[0], h[1], h, ia, ra, 0, 1, 11, i, j, k); n++; if (v < lo) lo = v; if (v > hi) hi = v; }
            return n == 0 || targeti < lo || targeti > hi ? 2 : 1;
        }

        private static float DoseF(ArrayView<float> g, int dimX, int dimY, ArrayView<int> h, ArrayView<int> ia, ArrayView<float> ra,
            int tx, int ty, int tz, int i, int j, int k)
        {
            int xs = ia[h[15 + tx] + i]; bool ix = ia[h[27 + tx] + i] != 0; float xd = ra[h[39 + tx] + i];
            int ys = ia[h[15 + ty] + j]; bool iy = ia[h[27 + ty] + j] != 0; float yd = ra[h[39 + ty] + j];
            int zs = ia[h[15 + tz] + k]; bool iz = ia[h[27 + tz] + k] != 0; float zd = ra[h[39 + tz] + k];
            int sy = dimX, sz = dimX * dimY, b = xs + ys * sy + zs * sz;
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
}
