// A whole batch as a user runs it, read and compared through the library's BatchComparer.CompareMany:
//   dotnet run -c Release -- batch <pairlist.tsv> [--engine MultiThread] [--float] [--workers n] [--threads t] [--readers k] [--held m]
//                                  [--partial] [--criteria 3/2,0.1/0.5] [--threshold 10] [--csv path] [--label text]
// pairlist.tsv: name, source path, target path, tab-separated (the --pairlist format). Each pair is read once for all
// criteria. Wall clock includes reading; caches are not purged here, so purge them first for cold reads.
using System.Diagnostics;
using System.Globalization;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;

namespace DSCgpu;

internal static class Batch
{
    public static void Run(string[] args)
    {
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var lines = File.ReadAllLines(args[0]).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToArray();
        var kind = Enum.Parse<ComparerKind>(Opt("--engine") ?? "MultiThread", ignoreCase: true);
        bool gpuFloat = args.Contains("--float"), partial = args.Contains("--partial");
        int? workers = Opt("--workers") is { } w ? int.Parse(w) : null;
        int? threads = Opt("--threads") is { } th ? int.Parse(th) : null;   // the GUI's Threads; null is the recommended count
        int readers = int.Parse(Opt("--readers") ?? "0"), held = int.Parse(Opt("--held") ?? "2");
        double threshold = double.Parse(Opt("--threshold") ?? "10", CultureInfo.InvariantCulture) / 100;
        var criteria = (Opt("--criteria") ?? "3/2,0.1/0.5").Split(',').Select(c => c.Split('/'))
            .Select(c => new Criterion(new Dta(true, threshold, double.Parse(c[0], CultureInfo.InvariantCulture) / 100,
                double.Parse(c[1], CultureInfo.InvariantCulture), relative: true), true))
            .ToArray();

        // Headers (and plans) once per folder, then each listed pair by path.
        var doses = lines.SelectMany(l => new[] { l[1], l[2] }).Select(Path.GetDirectoryName).Distinct()
            .SelectMany(d => RTFolderReader.Read(d!, headersOnly: true).Doses)
            .GroupBy(d => Path.GetFullPath(d.Dose.SourcePath)).ToDictionary(g => g.Key, g => g.First());
        // Keys only order the rows; a source without one (no plan) is keyed by UID instead.
        var pairs = lines.Select(l => (S: doses[Path.GetFullPath(l[1])], T: doses[Path.GetFullPath(l[2])]))
            .Select(p => new MatchedDosePair(DoseMatchKey.For(p.S, DoseMatchStrategy.Identifier) ?? DoseMatchKey.For(p.S, DoseMatchStrategy.PlanAndBeamUid)
                ?? throw new InvalidOperationException($"{p.S.Dose.SourcePath} has no key under either strategy."), p.S, p.T)).ToArray();

        var comparer = DoseComparers.Create(new ComparerSettings(kind, threads, Precision: gpuFloat ? GpuPrecision.Float : GpuPrecision.Double));
        using var release = comparer as IDisposable;   // GPU comparers hold a device
        var batch = new BatchComparer(DoseGridReader.Read, comparer)
        {
            PartialTargetReads = partial,
            Threads = threads,
            Queue = readers > 0 ? new BatchComparer.ReadQueue(readers, held) : null,
        };
        Console.WriteLine($"{Environment.MachineName}: {CpuTopology.Summary}");
        string setup = $"{comparer.Description}; workers {workers?.ToString() ?? "auto"}; "
            + (readers > 0 ? $"{readers} reader(s), {held} held" : "each worker reads") + (partial ? "; target box only" : "")
            + (CpuTopology.PinByDefault ? "; threads pinned" : "; threads unpinned");   // read by perf/Measure-DscPerformance.ps1
        Console.WriteLine($"{pairs.Length} pairs from {Path.GetFileName(args[0])}; {setup}");

        var clock = Stopwatch.StartNew();
        var result = batch.CompareMany(pairs, criteria, workers);
        clock.Stop();
        var proc = Process.GetCurrentProcess();
        double? read = result.Outcomes.Where(o => o.TargetFractionRead is not null).Select(o => o.TargetFractionRead!.Value).DefaultIfEmpty(double.NaN).Average();
        Console.WriteLine($"{clock.Elapsed.TotalSeconds:0.00} s wall clock at {result.DegreeOfParallelism} worker(s); peak working set {proc.PeakWorkingSet64 / 1e9:0.0} GB"
            + (double.IsNaN(read ?? double.NaN) ? "" : $"; targets {read:P1} read"));
        for (int n = 0; n < criteria.Length; n++)
        {
            var rows = result.Outcomes.Where(o => o.CriterionIndex == n).ToArray();
            Console.WriteLine($"  {criteria[n].Dta.ShortToString()}: compared {rows.Sum(o => (long)(o.Comparison?.TotalCompared ?? 0)):N0}, "
                + $"failed {rows.Sum(o => (long)(o.Comparison?.TotalFailed ?? 0)):N0}, {rows.Count(o => !o.Succeeded)} failed pair(s)");
        }
        if (Opt("--csv") is { } csv)
        {
            bool header = !File.Exists(csv);
            using var f = File.AppendText(csv);
            if (header) f.WriteLine("Machine,Label,Pairs,Engine,Workers,Readers,Held,Partial,Seconds,PeakGB,TargetFractionRead,Compared,Failed");
            f.WriteLine(string.Join(",", Environment.MachineName, Opt("--label") ?? "", pairs.Length, $"\"{comparer.Description}\"",
                result.DegreeOfParallelism, readers, held, partial, clock.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                (proc.PeakWorkingSet64 / 1e9).ToString("0.00", CultureInfo.InvariantCulture),
                double.IsNaN(read ?? double.NaN) ? "" : read!.Value.ToString("0.0000", CultureInfo.InvariantCulture),
                string.Join("|", Enumerable.Range(0, criteria.Length).Select(n => result.Outcomes.Where(o => o.CriterionIndex == n).Sum(o => (long)(o.Comparison?.TotalCompared ?? 0)))),
                string.Join("|", Enumerable.Range(0, criteria.Length).Select(n => result.Outcomes.Where(o => o.CriterionIndex == n).Sum(o => (long)(o.Comparison?.TotalFailed ?? 0))))));
        }
    }
}
