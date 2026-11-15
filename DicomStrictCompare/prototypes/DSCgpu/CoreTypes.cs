// Core types: the multi-thread comparer pinned to each kind of core, and to mixes, on one pair.
//   dotnet run -c Release -- cores <source.dcm> <target.dcm> [repeats] [--criteria 3/2,0.1/0.5] [--threshold 10] [--nobox]
// --nobox evaluates every sample point, a heavier load that separates the core types more clearly.
// Every row must give the same counts; a row that differs is marked. Each time is the fastest of [repeats] (default 5).
using System.Diagnostics;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Results.DICOM;

namespace DSCgpu;

internal static class CoreTypes
{
    public static void Run(string[] args)
    {
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var source = DoseGridReader.Read(args[0]);
        var target = DoseGridReader.Read(args[1]);
        int reps = args.Length > 2 && !args[2].StartsWith("--") ? int.Parse(args[2]) : 5;
        double threshold = double.Parse(Opt("--threshold") ?? "10") / 100;
        bool box = !args.Contains("--nobox");
        var criteria = (Opt("--criteria") ?? "3/2,0.1/0.5").Split(',').Select(c => c.Split('/'))
            .Select(c => new ComparisonRun(new Dta(true, threshold, double.Parse(c[0]) / 100, double.Parse(c[1]), relative: true), true, source.MaxPointDose.Dose))
            .ToArray();

        var all = CpuTopology.All;
        Console.WriteLine($"{Environment.MachineName}: {CpuTopology.Summary}");
        foreach (var core in all) Console.WriteLine($"  {core}");
        Console.WriteLine($"{Path.GetFileName(args[0])} ({source.DimensionX}x{source.DimensionY}x{source.DimensionZ}) against "
            + $"{Path.GetFileName(args[1])} ({target.DimensionX}x{target.DimensionY}x{target.DimensionZ}); "
            + string.Join(", ", criteria.Select(c => c.Dta.ShortToString())) + $", threshold {threshold:P0}{(box ? "" : ", no box")}; best of {reps}");

        CpuTopology.Core[] Of(params CoreKind[] kinds) => all.Where(c => kinds.Contains(c.Kind)).ToArray();
        var rows = new List<(string Name, DoseComparer Comparer)>
        {
            (box ? "single thread, boxed" : "single thread", new SingleThreadComparer { UseThresholdBox = box }),
        };
        void Pinned(string name, CpuTopology.Core[] cores)
        {
            if (cores.Length > 0) rows.Add(($"{name}: {cores.Length} pinned", new MultiThreadComparer { Threads = cores.Length, CoreSet = cores, PinThreads = true, UseThresholdBox = box }));
        }
        Pinned("P", Of(CoreKind.Performance));
        Pinned("E", Of(CoreKind.Efficient));
        Pinned("LP-E", Of(CoreKind.LowPowerEfficient));
        Pinned("P + E (the default)", Of(CoreKind.Performance, CoreKind.Efficient));
        Pinned("P + E + LP-E", all.ToArray());
        Pinned("unknown kind (no hybrid)", Of(CoreKind.Unknown));
        // The recommended count unpinned: the default on Windows, where the OS schedules (Cody, 2026-11-15).
        rows.Add(($"recommended, unpinned ({CpuTopology.PhysicalCores})", new MultiThreadComparer { Threads = CpuTopology.PhysicalCores, PinThreads = false, UseThresholdBox = box }));
        rows.Add(($"every logical processor, unpinned ({Environment.ProcessorCount})", new MultiThreadComparer { Threads = Environment.ProcessorCount, PinThreads = false, UseThresholdBox = box }));

        IReadOnlyList<SingleComparison>? expected = null;
        double? baseline = null;
        foreach (var (name, comparer) in rows)
        {
            comparer.CompareAll(source, target, criteria);   // warm-up
            double best = double.MaxValue;
            IReadOnlyList<SingleComparison> got = Array.Empty<SingleComparison>();
            for (int r = 0; r < reps; r++)
            {
                var w = Stopwatch.StartNew();
                got = comparer.CompareAll(source, target, criteria);
                best = Math.Min(best, w.Elapsed.TotalSeconds);
            }
            expected ??= got;
            baseline ??= best;
            bool same = got.Zip(expected).All(p => p.First.TotalCompared == p.Second.TotalCompared && p.First.TotalFailed == p.Second.TotalFailed);
            Console.WriteLine($"{name,-44} {best,8:0.000} s  {baseline / best,6:0.00}x  "
                + string.Join("  ", got.Select(g => $"{g.TotalCompared}/{g.TotalFailed}")) + (same ? "" : "  COUNTS DIFFER"));
        }
    }
}
