// Where does a GPU comparison beat running whole comparisons side by side on the CPU?
//   dotnet run -c Release -- <resolution mm: 1|2|5> <pairs> [folder|-] [repeats] [options]
// Each timing is the fastest of [repeats] runs (default 3, or 1 at 1 mm), after an untimed warm-up.
// Options:
//   --crop f          keep the central fraction f of each axis (a smaller phantom at the same resolution)
//   --criteria list   tolerance%/distance-mm pairs, e.g. 0.01/0,0.1/0.5,3/2 (distance 0: dose test only)
//   --threshold pct   low-dose cutoff, % of the source maximum (default 10)
//   --quick           trivial batch only at min(pairs, cores) at once, CPU port only at all cores
//   --csv path        append one row per method to path
//   --pairlist file   pairs from a tab-separated file (name, source path, target path) instead of QAtpsPTW;
//                     resolution 0 keeps each grid's own spacing, anything else resamples both sides to it
//   --set name        a label for the pair set, written to the CSV
// Core types (P, E, LP-E, mixes) on one pair: dotnet run -c Release -- cores <source.dcm> <target.dcm>; see CoreTypes.cs.
// Pairs are QAtpsPTW SquareFieldsS (AAA, 1 mm) against SquFldsSAXB (AXB, 2 mm), resampled so both sides share
// the resolution. Loading and resampling are not timed; every timing is comparison work only.
using System.Diagnostics;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;
using DSCgpu;

if (args.Length > 0 && args[0] == "cores") { CoreTypes.Run(args[1..]); return; }
double res = double.Parse(args[0]);
int pairCount = int.Parse(args[1]);
string folder = args.Length > 2 && args[2] != "-" ? args[2] : "/home/crcrewso/Source/~Resources/PRD 15.6 Previous Commissioning Pts/QAtpsPTW";
int reps = args.Length > 3 && !args[3].StartsWith("--") ? int.Parse(args[3]) : res <= 1 ? 1 : 3;
string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
double crop = double.Parse(Opt("--crop") ?? "1");
double thresholdPct = double.Parse(Opt("--threshold") ?? "10");
bool quick = args.Contains("--quick");
string? csv = Opt("--csv");
string? pairList = Opt("--pairlist");
string setName = Opt("--set") ?? (pairList is null ? "QAtpsPTW AAA-AXB" : Path.GetFileNameWithoutExtension(pairList));
var criteria = (Opt("--criteria") ?? "2/2,1/1").Split(',')
    .Select(c => c.Split('/')).Select(c => (Tol: double.Parse(c[0]), Dist: double.Parse(c[1]))).ToArray();
double Best(Action run)
{
    double best = double.MaxValue;
    for (int r = 0; r < reps; r++) { var w = Stopwatch.StartNew(); run(); best = Math.Min(best, w.Elapsed.TotalSeconds); }
    return best;
}
var clock = Stopwatch.StartNew();
void Log(string s) => Console.WriteLine($"{clock.Elapsed:mm\\:ss} {s}");

// ---- pairs ----
(string Name, string S, string T)[] sources = pairList is not null
    ? File.ReadAllLines(pairList).Where(l => l.Length > 0).Select(l => l.Split('\t')).Select(c => (c[0], c[1], c[2])).ToArray()
    : Directory.GetFiles(folder, "RD.QAtpsPTW.SquareFieldsS.*.dcm")
        .Select(f => Path.GetFileName(f)["RD.QAtpsPTW.SquareFieldsS.".Length..])
        .Where(n => File.Exists(Path.Combine(folder, "RD.QAtpsPTW.SquFldsSAXB." + n)))
        .OrderBy(n => n, StringComparer.Ordinal)
        .Select(n => (n.Replace(".dcm", ""), Path.Combine(folder, "RD.QAtpsPTW.SquareFieldsS." + n), Path.Combine(folder, "RD.QAtpsPTW.SquFldsSAXB." + n)))
        .ToArray();
pairCount = Math.Min(pairCount, sources.Length);
var picked = Enumerable.Range(0, pairCount).Select(k => sources[k * sources.Length / pairCount]).ToArray();
var pairs = new List<(string Name, DoseMatrixOptimal S, DoseMatrixOptimal T)>();
foreach (var (n, sPath, tPath) in picked)
{
    var a = DoseGridReader.Read(sPath);
    var b = DoseGridReader.Read(tPath);
    static bool At(DoseMatrixOptimal g, double r) => Math.Abs(g.XRes - r) < 1e-6 && Math.Abs(g.YRes - r) < 1e-6 && Math.Abs(g.ZRes - r) < 1e-6;
    var (s, t) = res <= 0 ? (a, b) : (At(a, res) ? a : GridResampler.Resample(a, res, res, res), At(b, res) ? b : GridResampler.Resample(b, res, res, res));
    if (crop < 1) (s, t) = (Crop(s, s, crop), Crop(t, s, crop));
    pairs.Add((n, s, t));
    GC.Collect();
    Log($"pair {pairs.Count}/{pairCount} {n}: source {s.DimensionX}x{s.DimensionY}x{s.DimensionZ}, target {t.DimensionX}x{t.DimensionY}x{t.DimensionZ}");
}
GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
Log($"{pairs.Count} pairs of {setName} at {(res <= 0 ? "native" : res + " mm")}, crop {crop:0.##}, threshold {thresholdPct}%, {GC.GetTotalMemory(true) / 1e9:0.0} GB held, {Environment.ProcessorCount} cores");
if (csv is not null && !File.Exists(csv))
    File.WriteAllText(csv, "machine,set,res_mm,crop,pairs,criterion,threshold_pct,points_per_pair,above_pct,failed_pct,box_pct,method,seconds,s_per_pair,speedup,agrees,upload_s,kernel_s\n");

using var ilgpu = Try(() => new IlgpuComparer(), "ILGPU");
using var opencl = Try(() => new OpenClComparer(), "Silk.NET OpenCL");
Log($"ILGPU device: {ilgpu?.Device ?? "none"}; OpenCL device: {opencl?.Device ?? "none"}");

var engine = new HandWrittenComparer();
foreach (var spec in criteria)
{
    var dta = new Dta(useMM: true, threshhold: thresholdPct / 100, tolerance: spec.Tol / 100, distance: spec.Dist, relative: true);
    string label = $"{spec.Tol}%/{spec.Dist}mm/{thresholdPct}% global";
    var plans = pairs.Select(p => ComparisonPlan.For(p.S, p.T, dta, true)).ToArray();
    var proj = pairs.Select(p => Projections.Of(p.S, Environment.ProcessorCount)).ToArray();   // stands in for load time: not timed
    var ps = pairs.Select((p, i) => plans[i].Doubles(p.S, p.T)).ToArray();
    var qs = pairs.Select((p, i) => plans[i].Ints(p.S, p.T)).ToArray();
    long points = plans.Sum(p => p.Points);
    Log($"=== {label}: {points / 1e6:0.0} M points in {pairs.Count} pairs ({points / pairs.Count / 1e6:0.00} M per pair)");

    // Reference: the engine itself, one pair at a time on one thread. Pair 0 once first, to warm the JIT.
    engine.CompareRelative(pairs[0].S, pairs[0].T, dta);
    var reference = new Counts[pairs.Count];
    var sw = Stopwatch.StartNew();
    double serial = Best(() =>
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            var c = engine.CompareRelative(pairs[i].S, pairs[i].T, dta);
            reference[i] = new Counts(c.TotalCount, c.TotalCompared, c.TotalFailed);
        }
    });
    double abovePct = 100.0 * reference.Sum(r => r.Compared) / points;
    double failedPct = 100.0 * reference.Sum(r => r.Failed) / Math.Max(1, reference.Sum(r => r.Compared));
    double boxPct = 100.0 * pairs.Select((p, i) => ComparisonPlan.WindowPoints(ComparisonPlan.Boxed(qs[i], ps[i], p.S, Environment.ProcessorCount))).Sum() / points;
    Row("engine, 1 thread, pairs in turn", serial, "reference");
    for (int i = 0; i < pairs.Count; i++) Console.WriteLine($"      {pairs[i].Name}: {reference[i]}");
    Console.WriteLine($"      above threshold {abovePct:0.00}% of points; failed {failedPct:0.00}% of those; threshold box {boxPct:0.00}% of points");

    // Trivial parallelism: whole comparisons side by side, as BatchComparer does.
    var batchSizes = quick ? new[] { Math.Min(pairs.Count, Environment.ProcessorCount) }
        : new[] { 2, 4, 8, 16, 32, 64 }.Where(n => n <= Math.Max(2, pairs.Count) && n <= 2 * Environment.ProcessorCount).ToArray();
    foreach (int n in batchSizes)
    {
        var got = new Counts[pairs.Count];
        double t = Best(() => Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = n }, i =>
        {
            var c = engine.CompareRelative(pairs[i].S, pairs[i].T, dta);
            got[i] = new Counts(c.TotalCount, c.TotalCompared, c.TotalFailed);
        }));
        Row($"engine, {n} pairs at once", t, Agree(got));
    }

    // Parallel inside one comparison, on the CPU.
    CpuPort.Compare(ps[0].Length > 0 ? pairs[0].S.DoseValues : [], pairs[0].T.DoseValues, ps[0], qs[0], 1);
    var threadCounts = quick ? new[] { Environment.ProcessorCount }
        : new[] { 1, 2, 4, 8, 16, 32, 64 }.Where(n => n < Environment.ProcessorCount).Append(Environment.ProcessorCount).ToArray();
    foreach (int threads in threadCounts)
    {
        var got = new Counts[pairs.Count];
        double t = Best(() => { for (int i = 0; i < pairs.Count; i++) got[i] = CpuPort.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], qs[i], threads); });
        Row($"CPU port, {threads} thread(s) in one pair", t, Agree(got));
    }

    // The threshold box: the same rules, skipping sample points that cannot reach the threshold. The scan that
    // finds the box is inside the timing.
    int cores = Environment.ProcessorCount;
    {
        int n = Math.Min(pairs.Count, cores);
        var got = new Counts[pairs.Count];
        double t = Best(() => Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = n }, i =>
            got[i] = CpuPort.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, 1), 1)));
        Row($"CPU port boxed, {n} pairs at once", t, Agree(got));
        t = Best(() => { for (int i = 0; i < pairs.Count; i++) got[i] = CpuPort.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, cores), cores); });
        Row($"CPU port boxed, {cores} threads in one pair", t, Agree(got));
        if (!quick)
        {
            t = Best(() => { for (int i = 0; i < pairs.Count; i++) got[i] = CpuPort.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, 1), 1); });
            Row("CPU port boxed, 1 thread, pairs in turn", t, Agree(got));
        }
        t = Best(() => { for (int i = 0; i < pairs.Count; i++) ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, cores); });
        Row($"threshold box scan alone, {cores} threads", t, "not a comparison");
        t = Best(() => { for (int i = 0; i < pairs.Count; i++) got[i] = CpuPort.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, proj[i]), cores); });
        Row($"CPU port boxed from load-time maxima, {cores} threads", t, Agree(got));
        if (!quick)
        {
            t = Best(() => { for (int i = 0; i < pairs.Count; i++) got[i] = CpuPort.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, proj[i]), 1); });
            Row("CPU port boxed from load-time maxima, 1 thread", t, Agree(got));
        }

        // Zeroing below-threshold source voxels first, as asked: not the same rules, so its counts are reported, not required.
        var zeroed = new double[pairs.Max(p => p.S.DoseValues.Length)];
        t = Best(() =>
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                var v = pairs[i].S.DoseValues; double cut = ps[i][ComparisonPlan.PMax] * ps[i][ComparisonPlan.PThr];
                Parallel.For(0, v.Length / 65536 + 1, new ParallelOptions { MaxDegreeOfParallelism = cores }, b =>
                {
                    for (int k = b * 65536, e = Math.Min(v.Length, k + 65536); k < e; k++) zeroed[k] = v[k] < cut ? 0 : v[k];
                });
                got[i] = CpuPort.Compare(zeroed, pairs[i].T.DoseValues, ps[i], qs[i], cores);
            }
        });
        Row($"CPU port zeroed below threshold, {cores} threads", t, Agree(got));
    }

    // GPU, one pair at a time. Each gets one untimed warm-up on pair 0 (kernel compile).
    float[][]? fs = null, ft = null;
    if (ilgpu is not null || opencl is not null)
    {
        fs = pairs.Select(p => Array.ConvertAll(p.S.DoseValues, v => (float)v)).ToArray();
        ft = pairs.Select(p => Array.ConvertAll(p.T.DoseValues, v => (float)v)).ToArray();
    }
    if (ilgpu is not null)
    {
        Gpu("ILGPU OpenCL, double", i => ilgpu.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], qs[i]));
        Gpu("ILGPU OpenCL, float", i => ilgpu.CompareFloat(fs![i], ft![i], ps[i], qs[i]));
        Gpu("ILGPU OpenCL, double, boxed", i => ilgpu.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, cores)));
        Gpu("ILGPU OpenCL, float, boxed", i => ilgpu.CompareFloat(fs![i], ft![i], ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, cores)));
        Gpu("ILGPU OpenCL, double, boxed from load-time maxima", i => ilgpu.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, proj[i])));
    }
    if (opencl is not null)
    {
        Gpu("Silk.NET OpenCL, double", i => opencl.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], qs[i]));
        Gpu("Silk.NET OpenCL, float", i => opencl.CompareFloat(fs![i], ft![i], ps[i], qs[i]));
        Gpu("Silk.NET OpenCL, double, boxed", i => opencl.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, cores)));
        Gpu("Silk.NET OpenCL, float, boxed", i => opencl.CompareFloat(fs![i], ft![i], ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, cores)));
        Gpu("Silk.NET OpenCL, double, boxed from load-time maxima", i => opencl.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], ComparisonPlan.Boxed(qs[i], ps[i], pairs[i].S, proj[i])));
    }
    fs = ft = null;
    GC.Collect();

    void Gpu(string name, Func<int, (Counts Counts, double UploadS, double KernelS)> run)
    {
        try
        {
            run(0);
            var got = new Counts[pairs.Count];
            double upload = double.MaxValue, kernel = double.MaxValue;
            double t = Best(() =>
            {
                double u = 0, k = 0;
                for (int i = 0; i < pairs.Count; i++) { var r = run(i); got[i] = r.Counts; u += r.UploadS; k += r.KernelS; }
                upload = Math.Min(upload, u); kernel = Math.Min(kernel, k);
            });
            Row(name, t, $"{Agree(got)}; upload {upload:0.000} s, kernel {kernel:0.000} s", upload, kernel);
        }
        catch (Exception ex) { Console.WriteLine($"    {name}: FAILED {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
    }

    void Row(string name, double seconds, string note, double upload = double.NaN, double kernel = double.NaN)
    {
        Console.WriteLine($"    {name,-36} {seconds,8:0.000} s  {seconds / pairs.Count,7:0.000} s/pair  {points / seconds / 1e6,8:0.0} Mpt/s  {serial / seconds,6:0.0}x  {note}");
        if (csv is null) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string agrees = note.StartsWith("reference") ? "reference" : note.StartsWith("not a comparison") ? "n/a" : note.StartsWith("counts identical") ? "yes" : "NO";
        File.AppendAllText(csv, string.Join(",", Environment.MachineName, $"\"{setName}\"", res.ToString(inv), crop.ToString(inv), pairs.Count,
            $"{spec.Tol.ToString(inv)}/{spec.Dist.ToString(inv)}", thresholdPct.ToString(inv), (points / pairs.Count).ToString(inv),
            abovePct.ToString("0.000", inv), failedPct.ToString("0.000", inv), boxPct.ToString("0.000", inv), $"\"{name}\"", seconds.ToString("0.0000", inv),
            (seconds / pairs.Count).ToString("0.00000", inv), (serial / seconds).ToString("0.00", inv), agrees,
            double.IsNaN(upload) ? "" : upload.ToString("0.0000", inv), double.IsNaN(kernel) ? "" : kernel.ToString("0.0000", inv)) + "\n");
    }

    string Agree(Counts[] got)
    {
        int same = 0; long dc = 0, df = 0;
        for (int i = 0; i < got.Length; i++)
        {
            if (got[i] == reference[i]) same++;
            dc += got[i].Compared - reference[i].Compared;
            df += got[i].Failed - reference[i].Failed;
        }
        return same == got.Length ? "counts identical" : $"COUNTS DIFFER in {got.Length - same} pair(s): compared {dc:+#;-#;0}, failed {df:+#;-#;0}";
    }
}
Log("done");

// The box [centre - f*half, centre + f*half] of `box`'s extent per axis, cut from `g` on its own nodes.
static DoseMatrixOptimal Crop(DoseMatrixOptimal g, DoseMatrixOptimal box, double f)
{
    (int, int) Range(double o, double r, int n, double lo, double hi) =>
        (Math.Max(0, (int)Math.Ceiling((lo - o) / r - 1e-9)), Math.Min(n - 1, (int)Math.Floor((hi - o) / r + 1e-9)));
    double cx = (box.X0 + box.XMax) / 2, hx = f * (box.XMax - box.X0) / 2;
    double cy = (box.Y0 + box.YMax) / 2, hy = f * (box.YMax - box.Y0) / 2;
    double cz = (box.Z0 + box.ZMax) / 2, hz = f * (box.ZMax - box.Z0) / 2;
    var (x0, x1) = Range(g.X0, g.XRes, g.DimensionX, cx - hx, cx + hx);
    var (y0, y1) = Range(g.Y0, g.YRes, g.DimensionY, cy - hy, cy + hy);
    var (z0, z1) = Range(g.Z0, g.ZRes, g.DimensionZ, cz - hz, cz + hz);
    int nx = x1 - x0 + 1, ny = y1 - y0 + 1, nz = z1 - z0 + 1;
    var v = new double[nx * ny * nz];
    for (int k = 0; k < nz; k++)
        for (int j = 0; j < ny; j++)
            Array.Copy(g.DoseValues, x0 + (y0 + j) * g.DimensionX + (z0 + k) * g.DimensionX * g.DimensionY, v, j * nx + k * nx * ny, nx);
    return new DoseMatrixOptimal(nx, ny, nz, g.X0 + x0 * g.XRes, g.Y0 + y0 * g.YRes, g.Z0 + z0 * g.ZRes, g.XRes, g.YRes, g.ZRes, v);
}

static T? Try<T>(Func<T> make, string what) where T : class
{
    try { return make(); }
    catch (Exception ex) { Console.WriteLine($"{what} unavailable: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); return null; }
}
