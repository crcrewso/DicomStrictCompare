// Where does a GPU comparison beat running whole comparisons side by side on the CPU?
//   dotnet run -c Release -- <resolution mm: 1|2|5> <pairs> [folder] [repeats]
// Each timing is the fastest of [repeats] runs (default 3, or 1 at 1 mm), after an untimed warm-up.
// Pairs are QAtpsPTW SquareFieldsS (AAA, 1 mm) against SquFldsSAXB (AXB, 2 mm), resampled so both sides share
// the resolution. Loading and resampling are not timed; every timing is comparison work only.
using System.Diagnostics;
using DSClibrary;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;
using DSCgpu;

double res = double.Parse(args[0]);
int pairCount = int.Parse(args[1]);
string folder = args.Length > 2 && args[2] != "-" ? args[2] : "/home/crcrewso/Source/~Resources/PRD 15.6 Previous Commissioning Pts/QAtpsPTW";
int reps = args.Length > 3 ? int.Parse(args[3]) : res <= 1 ? 1 : 3;
double Best(Action run)
{
    double best = double.MaxValue;
    for (int r = 0; r < reps; r++) { var w = Stopwatch.StartNew(); run(); best = Math.Min(best, w.Elapsed.TotalSeconds); }
    return best;
}
var clock = Stopwatch.StartNew();
void Log(string s) => Console.WriteLine($"{clock.Elapsed:mm\\:ss} {s}");

// ---- pairs ----
var names = Directory.GetFiles(folder, "RD.QAtpsPTW.SquareFieldsS.*.dcm")
    .Select(f => Path.GetFileName(f)["RD.QAtpsPTW.SquareFieldsS.".Length..])
    .Where(n => File.Exists(Path.Combine(folder, "RD.QAtpsPTW.SquFldsSAXB." + n)))
    .OrderBy(n => n, StringComparer.Ordinal).ToArray();
var picked = Enumerable.Range(0, pairCount).Select(k => names[k * names.Length / pairCount]).ToArray();
var pairs = new List<(string Name, DoseMatrixOptimal S, DoseMatrixOptimal T)>();
foreach (var n in picked)
{
    var aaa = DoseGridReader.Read(Path.Combine(folder, "RD.QAtpsPTW.SquareFieldsS." + n));
    var axb = DoseGridReader.Read(Path.Combine(folder, "RD.QAtpsPTW.SquFldsSAXB." + n));
    var (s, t) = res switch
    {
        1 => (aaa, GridResampler.Resample(axb, 1, 1, 1)),
        2 => (GridResampler.Resample(aaa, 2, 2, 2), axb),
        _ => (GridResampler.Resample(aaa, res, res, res), GridResampler.Resample(axb, res, res, res)),
    };
    pairs.Add((n.Replace(".dcm", ""), s, t));
    GC.Collect();
    Log($"pair {pairs.Count}/{pairCount} {n}: source {s.DimensionX}x{s.DimensionY}x{s.DimensionZ}, target {t.DimensionX}x{t.DimensionY}x{t.DimensionZ}");
}
GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
Log($"{pairs.Count} pairs at {res} mm, {GC.GetTotalMemory(true) / 1e9:0.0} GB held, {Environment.ProcessorCount} cores");

using var ilgpu = Try(() => new IlgpuComparer(), "ILGPU");
using var opencl = Try(() => new OpenClComparer(), "Silk.NET OpenCL");
Log($"ILGPU device: {ilgpu?.Device ?? "none"}; OpenCL device: {opencl?.Device ?? "none"}");

var engine = new X86Mathematics();
foreach (var spec in new[] { (Tol: 2.0, Dist: 2.0), (Tol: 1.0, Dist: 1.0) })
{
    var dta = new Dta(useMM: true, threshhold: 0.10, tolerance: spec.Tol / 100, distance: spec.Dist, relative: true);
    string label = $"{spec.Tol}%/{spec.Dist}mm/10% global";
    var plans = pairs.Select(p => ComparisonPlan.For(p.S, p.T, dta, true)).ToArray();
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
    Row("engine, 1 thread, pairs in turn", serial, "reference");
    for (int i = 0; i < pairs.Count; i++) Console.WriteLine($"      {pairs[i].Name}: {reference[i]}");

    // Trivial parallelism: whole comparisons side by side, as BatchComparer does.
    foreach (int n in new[] { 2, 4, 8, 16, 32, 64 }.Where(n => n <= Math.Max(2, pairs.Count) && n <= 2 * Environment.ProcessorCount))
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
    foreach (int threads in new[] { 1, 2, 4, 8, 16, 32, 64 }.Where(n => n < Environment.ProcessorCount).Append(Environment.ProcessorCount))
    {
        var got = new Counts[pairs.Count];
        double t = Best(() => { for (int i = 0; i < pairs.Count; i++) got[i] = CpuPort.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], qs[i], threads); });
        Row($"CPU port, {threads} thread(s) in one pair", t, Agree(got));
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
    }
    if (opencl is not null)
    {
        Gpu("Silk.NET OpenCL, double", i => opencl.Compare(pairs[i].S.DoseValues, pairs[i].T.DoseValues, ps[i], qs[i]));
        Gpu("Silk.NET OpenCL, float", i => opencl.CompareFloat(fs![i], ft![i], ps[i], qs[i]));
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
            Row(name, t, $"{Agree(got)}; upload {upload:0.000} s, kernel {kernel:0.000} s");
        }
        catch (Exception ex) { Console.WriteLine($"    {name}: FAILED {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
    }

    void Row(string name, double seconds, string note) =>
        Console.WriteLine($"    {name,-36} {seconds,8:0.000} s  {seconds / pairs.Count,7:0.000} s/pair  {points / seconds / 1e6,8:0.0} Mpt/s  {serial / seconds,6:0.0}x  {note}");

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

static T? Try<T>(Func<T> make, string what) where T : class
{
    try { return make(); }
    catch (Exception ex) { Console.WriteLine($"{what} unavailable: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); return null; }
}
