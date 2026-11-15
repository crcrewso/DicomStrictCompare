using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DSClibrary.Gamma;
using DSClibrary.Matching;
using DSClibrary.Profiles;

namespace DSClibrary.Comparing
{
    /// <summary>
    /// The result of comparing one pair, kept as an object.
    /// <para>
    /// Retained rather than rendered, because crcrewso wants an interface that can swap between and
    /// compare multiple comparisons (backlog D15) and that is only possible if a comparison's result
    /// survives being computed. It carries what it was given as well as what it found, so it can be
    /// re-rendered, re-tabulated or re-sorted without being recomputed - and so a number in a report
    /// can be traced back to the two files that produced it.
    /// </para>
    /// </summary>
    public sealed record ComparisonOutcome
    {
        public DoseMatchKey Key { get; }
        public string SourcePath { get; }
        public string TargetPath { get; }

        /// <summary>The parameters used, so the result can say what question it answered.</summary>
        public Dta Dta { get; }

        /// <summary>Whether the tolerance was a fraction of the source maximum or of the local dose.</summary>
        public bool Global { get; }

        /// <summary>
        /// The counts. For a gamma comparison, <c>TotalCompared</c> is the number of reference points
        /// analysed and <c>TotalFailed</c> those with gamma above 1, so a gamma row reads like a DTA row.
        /// </summary>
        public SingleComparison? Comparison { get; }

        /// <summary>The full gamma result when the comparison was a gamma one; null for the strict engine.</summary>
        public GammaResult? Gamma { get; }

        /// <summary>Why this pair produced no comparison, when it did not. Null on success.</summary>
        public string? Failure { get; }

        public TimeSpan Elapsed { get; }

        /// <summary>Which comparer produced this (<see cref="DoseComparer.Description"/>), so a number can be traced to its engine.</summary>
        public string Comparer { get; }

        /// <summary>The maximum the threshold and global tolerance were fractions of, and why; null for failures.</summary>
        public ReferenceMaximum? Maximum { get; }

        /// <summary>Each dose's field edges against its own plan's jaws (Studies.JawEdgeCheck); null for failures.</summary>
        public Studies.JawEdgeResult? SourceEdges { get; init; }
        public Studies.JawEdgeResult? TargetEdges { get; init; }

        /// <summary>The two edge checks as one line: "agree", the disagreements, or why not checked.</summary>
        public string EdgeCheck => Succeeded ? Studies.JawEdgeCheck.Describe(SourceEdges, TargetEdges) : "";

        /// <summary>The fraction of the target's voxels read from disk, with <see cref="BatchComparer.PartialTargetReads"/>; null for a full read.</summary>
        public double? TargetFractionRead { get; init; }

        /// <summary>Which of <see cref="BatchComparer.CompareMany"/>'s criteria this row answers; 0 for a single-criterion batch.</summary>
        public int CriterionIndex { get; init; }

        /// <summary>Monitor units of each side where the plans supplied them. Recorded, never applied.</summary>
        public double? SourceMonitorUnits { get; }
        public double? TargetMonitorUnits { get; }

        internal ComparisonOutcome(
            DoseMatchKey key,
            string sourcePath,
            string targetPath,
            Dta dta,
            bool global,
            SingleComparison? comparison,
            string? failure,
            TimeSpan elapsed,
            double? sourceMonitorUnits,
            double? targetMonitorUnits,
            GammaResult? gamma = null,
            string comparer = "",
            ReferenceMaximum? maximum = null)
        {
            Comparer = comparer;
            Maximum = maximum;
            Key = key;
            SourcePath = sourcePath;
            TargetPath = targetPath;
            Dta = dta;
            Global = global;
            Comparison = comparison;
            Gamma = gamma;
            Failure = failure;
            Elapsed = elapsed;
            SourceMonitorUnits = sourceMonitorUnits;
            TargetMonitorUnits = targetMonitorUnits;
        }

        public bool IsGamma => Gamma is not null || Dta.Algorithm == Dta.CalcAlgorithm.gamma;

        public bool Succeeded => Comparison is not null;

        /// <summary>Percent of evaluated points that failed, or null when the comparison did not run.</summary>
        public double? PercentFailed =>
            Comparison is { TotalCompared: > 0 } c ? 100.0 * c.TotalFailed / c.TotalCompared : null;

        public override string ToString() => Succeeded
            ? $"{Key}: {PercentFailed:0.00}% failed of {Comparison!.TotalCompared:N0} ({Elapsed.TotalSeconds:0.0}s)"
            : $"{Key}: {Failure}";
    }

    /// <summary>Which beams a criterion compares, by the source plan's RadiationType (crcrewso, 2026-11-15).</summary>
    public enum BeamsCompared { Both, Photon, Electron }

    /// <summary>
    /// One criterion for <see cref="BatchComparer.CompareMany"/>: the strict DTA rules, or gamma when
    /// <see cref="Gamma"/> is set. A pair whose source has no plan, so no known RadiationType, is compared only by
    /// <see cref="BeamsCompared.Both"/> criteria.
    /// </summary>
    public sealed record Criterion(Dta Dta, bool Global, GammaOptions? Gamma = null, BeamsCompared AppliesTo = BeamsCompared.Both)
    {
        public static Criterion ForGamma(GammaOptions options, BeamsCompared appliesTo = BeamsCompared.Both) =>
            new(BatchComparer.DtaFor(options), !options.LocalGamma, options, appliesTo);

        public bool AppliesToBeam(Results.DICOM.RTPlanBeam? beam) => AppliesTo == BeamsCompared.Both
            || string.Equals(beam?.RadiationType, AppliesTo.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The outcome of a whole batch.</summary>
    public sealed class BatchComparisonResult
    {
        public IReadOnlyList<ComparisonOutcome> Outcomes { get; }
        public TimeSpan Elapsed { get; }
        public int DegreeOfParallelism { get; }

        internal BatchComparisonResult(
            IReadOnlyList<ComparisonOutcome> outcomes, TimeSpan elapsed, int degreeOfParallelism)
        {
            Outcomes = outcomes;
            Elapsed = elapsed;
            DegreeOfParallelism = degreeOfParallelism;
        }

        public IEnumerable<ComparisonOutcome> Succeeded => Outcomes.Where(o => o.Succeeded);
        public IEnumerable<ComparisonOutcome> Failed => Outcomes.Where(o => !o.Succeeded);

        public override string ToString() =>
            $"{Succeeded.Count()} of {Outcomes.Count} compared in {Elapsed.TotalSeconds:0.0}s "
            + $"at parallelism {DegreeOfParallelism}";
    }

    /// <summary>
    /// Runs a batch of matched pairs, one pair per task.
    /// <para>
    /// This is the parallelism <c>decisions/2026-10-02-comparison-rules.md</c> item 7 asked for:
    /// <i>"Parallelization should be handled by batching comparisons, not by trying to expedite a
    /// single file comparison."</i> Measured 2026-11-08, a single comparison runs at about 1.1M
    /// points per second with a 2 mm DTA and takes some 2.6 seconds on an 8.08M-voxel grid - so a
    /// batch of twenty-six is minutes of work that the decision already permits spreading across
    /// cores, and nothing was doing so.
    /// </para>
    /// <para>
    /// <b>Memory is the limit, not cores.</b> Each comparison holds two grids, 61 MB each as doubles
    /// for the reference files, so eight in flight is about a gigabyte. The default degree of
    /// parallelism is therefore conservative and the grids are loaded inside the task and released
    /// when it ends, rather than all being read up front.
    /// </para>
    /// </summary>
    public sealed class BatchComparer
    {
        private readonly Func<string, DoseMatrixOptimal> _load;
        private readonly DoseComparer _mathematics;

        /// <param name="load">
        /// Loads a grid from a path. Injected so a caller can choose its reader - this library's own
        /// or EvilDICOM - and so tests can supply small grids.
        /// </param>
        /// <param name="mathematics">The comparison engine, usually from <see cref="DoseComparers.Create"/>. Defaults to
        /// <see cref="HandWrittenComparer"/>, so no one's numbers change unless they choose another.</param>
        public BatchComparer(Func<string, DoseMatrixOptimal> load, DoseComparer? mathematics = null)
        {
            _load = load ?? throw new ArgumentNullException(nameof(load));
            _mathematics = mathematics ?? new HandWrittenComparer();
        }

        /// <summary>
        /// Read only the target voxels the pair's criteria and the edge check use (<see cref="OpenTarget"/>), when
        /// none of its criteria is gamma, which checks and flips the whole evaluation grid. Off by default; the counts
        /// are the same either way.
        /// </summary>
        public bool PartialTargetReads { get; init; }

        /// <summary>
        /// Opens a target and reads the rows that matter, the lowest useful region (crcrewso, 2026-11-15): every
        /// criterion's reads, from its threshold box and the source (<see cref="Gpu.GpuTables.Reads"/>), and the rows
        /// <see cref="Studies.JawEdgeCheck"/> reads for the target's beam. A criterion that reads past a grid edge (the
        /// engine's rounding quirk) has the target read whole.
        /// </summary>
        public static DSClibrary.Results.DICOM.DoseGridReader.PartialDoseGrid OpenTarget(string path, DoseMatrixOptimal source,
            IEnumerable<ComparisonRun> runs, Results.DICOM.RTPlanBeam? targetBeam)
        {
            var partial = Results.DICOM.DoseGridReader.Open(path);
            try
            {
                var g = partial.Grid;
                int z0 = int.MaxValue, z1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
                bool whole = false;
                foreach (var run in runs)
                {
                    var setup = ComparisonSetup.For(source, g, run.Dta, run.Global, true, run.ReferenceMaximum);
                    if (Gpu.GpuTables.Reads(setup) is not { } r) continue;
                    if (r.TLo.X < 0 || r.TLo.Y < 0 || r.TLo.Z < 0 || r.THi.X > g.DimensionX - 1 || r.THi.Y > g.DimensionY - 1 || r.THi.Z > g.DimensionZ - 1)
                        whole = true;
                    z0 = Math.Min(z0, r.TLo.Z); z1 = Math.Max(z1, r.THi.Z);
                    y0 = Math.Min(y0, r.TLo.Y); y1 = Math.Max(y1, r.THi.Y);
                }
                if (whole) partial.ReadAll();
                else if (z0 <= z1) partial.ReadRows(z0, z1, y0, y1);
                if (Studies.JawEdgeCheck.DepthRows(g, targetBeam, partial.ReadPlane) is { } rows)
                    partial.ReadRows(0, g.DimensionZ - 1, rows.Y0, rows.Y1);
                return partial;
            }
            catch { partial.Dispose(); throw; }
        }

        /// <summary>
        /// Four is the default rather than the core count, because the
        /// constraint here is memory and not cores: at 61 MB per grid and two grids per comparison,
        /// a sixteen-core machine would try to hold about two gigabytes of dose.
        /// </summary>
        public static int DefaultDegreeOfParallelism => Math.Min(4, CpuTopology.PhysicalCores);

        /// <summary>
        /// How many pairs to run at once when none is set: as many as half the free memory holds, the largest pair's
        /// two grids at 8 bytes a voxel (from the headers), up to the thread budget (by default the physical core count, SMT siblings ignored). Comparers that already use the cores
        /// within a pair (multi-thread, GPU) get at most 2, so one pair loads while another compares.
        /// <para>
        /// Measured 2026-11-14 to 15: a fixed 4 ran a 31 GB machine out of memory on 1 mm grids, and with the box a
        /// single pair needs little beyond its two grids.
        /// </para>
        /// </summary>
        public static int PairsAtOnce(IReadOnlyCollection<MatchedDosePair> pairs, DoseComparer comparer, long? availableBytes = null, int? threads = null)
        {
            int cores = Math.Max(1, threads ?? CpuTopology.PhysicalCores);
            if (pairs.Count == 0) return 1;
            long largest = pairs.Max(p => (long)(p.Source.Dose.DeclaredVoxelCount ?? 0) + (p.Target.Dose.DeclaredVoxelCount ?? 0)) * sizeof(double);
            long budget = (availableBytes ?? GC.GetGCMemoryInfo().TotalAvailableMemoryBytes) / 2;
            int byMemory = largest > 0 ? (int)Math.Clamp(budget / largest, 1, int.MaxValue) : cores;
            int cap = comparer is HandWrittenComparer or SingleThreadComparer ? cores : Math.Min(2, cores);
            return Math.Max(1, Math.Min(Math.Min(byMemory, cap), pairs.Count));
        }

        /// <summary>One criterion over the batch; see <see cref="CompareMany"/>.</summary>
        public BatchComparisonResult Compare(
            IEnumerable<MatchedDosePair> pairs,
            Dta dta,
            bool global = true,
            int? degreeOfParallelism = null,
            CancellationToken cancellation = default,
            MaximumKind maximum = MaximumKind.GlobalMax)
        {
            if (dta is null) throw new ArgumentNullException(nameof(dta));
            return CompareMany(pairs, new[] { new Criterion(dta, global) }, degreeOfParallelism, cancellation, maximum);
        }

        /// <summary>
        /// The same batch with the gamma index (<see cref="GammaShell"/>) in place of the strict engine.
        /// <para>
        /// Each pair's gamma already runs its reference points in parallel, so the pairs run with the
        /// given degree and each gamma gets the remaining cores; the default is still memory-led. The
        /// outcome's counts are reference points analysed and points with gamma above 1, so the row
        /// reads like a DTA row; the whole <see cref="GammaResult"/> is kept on the outcome.
        /// </para>
        /// </summary>
        public BatchComparisonResult CompareGamma(
            IEnumerable<MatchedDosePair> pairs,
            GammaOptions options,
            int? degreeOfParallelism = null,
            CancellationToken cancellation = default,
            MaximumKind maximum = MaximumKind.GlobalMax)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            return CompareMany(pairs, new[] { Criterion.ForGamma(options) }, degreeOfParallelism, cancellation, maximum);
        }

        /// <summary>
        /// Every criterion over the batch, <b>loading each pair once</b>: its grids, Max and edge checks serve all
        /// the criteria, the DTA ones go to the comparer together (<see cref="DoseComparer.CompareAll"/>, one GPU
        /// upload per pair), and the gamma ones run on the same grids. A criterion that does not apply to a pair's
        /// beam (<see cref="Criterion.AppliesTo"/>) produces no row for it. Outcomes are ordered by key, then
        /// criterion. <see cref="ComparisonOutcome.Elapsed"/> is the pair's whole time, shared by its rows.
        /// </summary>
        public BatchComparisonResult CompareMany(
            IEnumerable<MatchedDosePair> pairs,
            IReadOnlyList<Criterion> criteria,
            int? degreeOfParallelism = null,
            CancellationToken cancellation = default,
            MaximumKind maximum = MaximumKind.GlobalMax)
        {
            if (pairs is null) throw new ArgumentNullException(nameof(pairs));
            if (criteria is null) throw new ArgumentNullException(nameof(criteria));
            foreach (var c in criteria) c.Gamma?.Validate();

            var work = pairs.ToArray();
            bool anyDta = criteria.Any(c => c.Gamma is null);
            int threadBudget = Math.Max(1, Threads ?? CpuTopology.PhysicalCores);
            int parallelism = Math.Max(1, degreeOfParallelism ?? PairsAtOnce(work, anyDta ? _mathematics : new HandWrittenComparer(), threads: threadBudget));
            int perGamma = Math.Max(1, threadBudget / parallelism);
            var outcomes = new ConcurrentBag<ComparisonOutcome>();
            var batch = Stopwatch.StartNew();

            // Dedicated threads rather than the pool, so a worker can be pinned. A comparer that runs on one thread
            // gets one worker per core, pinned on Linux, which keeps them off SMT siblings and low-power E-cores.
            var cores = CpuTopology.Cores;
            bool pin = CpuTopology.PinByDefault && _mathematics is HandWrittenComparer or SingleThreadComparer
                       && parallelism > 1 && parallelism <= cores.Count;
            var threads = new List<Thread>();
            Exception? error = null;
            // Cancelled by the caller, or by an unexpected error in any thread, so no reader waits on a queue nobody empties.
            using var abort = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var token = abort.Token;
            void Start(string name, Action body) => threads.Add(new Thread(() =>
            {
                try { body(); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception ex) { Interlocked.CompareExchange(ref error, ex, null); abort.Cancel(); }
            }) { IsBackground = true, Name = name });
            void Keep(IEnumerable<ComparisonOutcome> rows) { foreach (var o in rows) outcomes.Add(o); }

            int next = -1;
            if (Queue is null)
            {
                // Each worker reads a pair, then compares it.
                for (int w = 0; w < parallelism; w++)
                {
                    int worker = w;
                    Start($"DSC pair {w}", () =>
                    {
                        if (pin) CpuTopology.PinCurrentThread(cores[worker]);
                        for (int i; (i = Interlocked.Increment(ref next)) < work.Length && !token.IsCancellationRequested;)
                            Keep(Evaluate(Load(work[i], criteria, maximum, token), criteria, perGamma, token));
                    });
                }
            }
            else
            {
                // Readers fill a bounded queue of loaded pairs; workers compare them (crane's read queue, 2026-11-15).
                var queue = Channel.CreateBounded<Loaded>(new BoundedChannelOptions(Math.Max(1, Queue.PairsHeld)) { FullMode = BoundedChannelFullMode.Wait });
                int readersLeft = Math.Max(1, Queue.Readers);
                for (int r = 0; r < Math.Max(1, Queue.Readers); r++)
                    Start($"DSC reader {r}", () =>
                    {
                        try
                        {
                            for (int i; (i = Interlocked.Increment(ref next)) < work.Length && !token.IsCancellationRequested;)
                                queue.Writer.WriteAsync(Load(work[i], criteria, maximum, token), token).AsTask().GetAwaiter().GetResult();
                        }
                        finally { if (Interlocked.Decrement(ref readersLeft) == 0) queue.Writer.TryComplete(); }
                    });
                for (int w = 0; w < parallelism; w++)
                {
                    int worker = w;
                    Start($"DSC pair {w}", () =>
                    {
                        if (pin) CpuTopology.PinCurrentThread(cores[worker]);
                        while (queue.Reader.WaitToReadAsync(token).AsTask().GetAwaiter().GetResult())
                            while (queue.Reader.TryRead(out var loaded))
                                Keep(Evaluate(loaded, criteria, perGamma, token));
                    });
                }
            }
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();
            cancellation.ThrowIfCancellationRequested();
            if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();

            batch.Stop();
            // A stable order, so two runs of the same batch produce the same report.
            var ordered = outcomes
                .OrderBy(o => o.Key.ToString(), StringComparer.Ordinal)
                .ThenBy(o => o.CriterionIndex)
                .ToArray();
            return new BatchComparisonResult(ordered, batch.Elapsed, parallelism);
        }

        /// <summary>
        /// Reading ahead: <see cref="Readers"/> threads read pairs into a queue that holds up to <see cref="PairsHeld"/>
        /// loaded pairs, while the workers compare. Up to Readers + PairsHeld + workers pairs are in memory at once.
        /// </summary>
        public sealed record ReadQueue(int Readers, int PairsHeld);

        /// <summary>Read ahead with a queue; null (the default) has each worker read its own pair, then compare it.</summary>
        public ReadQueue? Queue { get; init; }

        /// <summary>
        /// The CPU thread budget: the most pairs at once for the single-thread engines, and the threads each gamma
        /// gets. Null for <see cref="CpuTopology.PhysicalCores"/>; front ends pass <see cref="ComparerSettings.Threads"/>.
        /// </summary>
        public int? Threads { get; init; }

        /// <summary>The <see cref="Dta"/> that describes a gamma comparison's parameters in the library's older terms.</summary>
        public static Dta DtaFor(GammaOptions options) => new(
            useMM: true,
            threshhold: options.LowerPercentDoseCutoff / 100.0,
            tolerance: options.DosePercentThreshold / 100.0,
            distance: options.DistanceThreshold,
            relative: !options.LocalGamma,
            gamma: true);

        /// <summary>A pair read and ready to compare, or why it couldn't be read.</summary>
        private sealed record Loaded(MatchedDosePair Pair, int[] Applicable, DoseMatrixOptimal? Source, DoseMatrixOptimal? Target,
            ReferenceMaximum? Reference, ComparisonRun[] Runs, int[] Dta, double? FractionRead, TimeSpan Elapsed, string? Failure);

        private Loaded Load(MatchedDosePair pair, IReadOnlyList<Criterion> criteria, MaximumKind maximum, CancellationToken cancellation)
        {
            int[] applicable = Enumerable.Range(0, criteria.Count).Where(n => criteria[n].AppliesToBeam(pair.Source.Beam)).ToArray();
            int[] dta = applicable.Where(n => criteria[n].Gamma is null).ToArray();
            if (applicable.Length == 0) return new Loaded(pair, applicable, null, null, null, Array.Empty<ComparisonRun>(), dta, null, TimeSpan.Zero, null);
            var timer = Stopwatch.StartNew();
            try
            {
                cancellation.ThrowIfCancellationRequested();
                DoseMatrixOptimal source = _load(pair.Source.Dose.SourcePath);
                var reference = ReferenceMaximum.For(source, pair.Source.Beam, maximum);
                var runs = dta.Select(n => new ComparisonRun(criteria[n].Dta, criteria[n].Global, reference.Dose)).ToArray();

                cancellation.ThrowIfCancellationRequested();
                DoseMatrixOptimal target;
                double? fractionRead = null;
                if (PartialTargetReads && dta.Length == applicable.Length)
                {
                    using var partial = OpenTarget(pair.Target.Dose.SourcePath, source, runs, pair.Target.Beam);
                    target = partial.Grid;
                    fractionRead = partial.FractionRead;
                }
                else target = _load(pair.Target.Dose.SourcePath);
                return new Loaded(pair, applicable, source, target, reference, runs, dta, fractionRead, timer.Elapsed, null);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                return new Loaded(pair, applicable, null, null, null, Array.Empty<ComparisonRun>(), dta, null, timer.Elapsed, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private IEnumerable<ComparisonOutcome> Evaluate(Loaded loaded, IReadOnlyList<Criterion> criteria, int perGamma, CancellationToken cancellation)
        {
            var (pair, applicable) = (loaded.Pair, loaded.Applicable);
            if (applicable.Length == 0) return Array.Empty<ComparisonOutcome>();
            string EngineOf(Criterion c) => c.Gamma is null ? _mathematics.Description : "GammaShell";
            IEnumerable<ComparisonOutcome> Failed(string failure, TimeSpan elapsed) => applicable.Select(n => new ComparisonOutcome(
                pair.Key, pair.Source.Dose.SourcePath, pair.Target.Dose.SourcePath,
                criteria[n].Dta, criteria[n].Global, null, failure, elapsed,
                pair.Source.MonitorUnits, pair.Target.MonitorUnits, comparer: EngineOf(criteria[n]))
            { CriterionIndex = n }).ToArray();
            if (loaded.Failure is not null) return Failed(loaded.Failure, loaded.Elapsed);

            var timer = Stopwatch.StartNew();
            try
            {
                var (source, target, reference) = (loaded.Source!, loaded.Target!, loaded.Reference!);
                var sourceEdges = Studies.JawEdgeCheck.For(source, pair.Source.Beam);
                var targetEdges = Studies.JawEdgeCheck.For(target, pair.Target.Beam);

                // The DTA criteria together, so a GPU comparer uploads the pair once. The token goes into the engine,
                // which checks it as it runs, so a cancel stops a comparison under way.
                var counts = new Dictionary<int, SingleComparison>();
                var gammas = new Dictionary<int, GammaResult>();
                if (loaded.Dta.Length > 0)
                {
                    var results = _mathematics.CompareAll(source, target, loaded.Runs, cancellation);
                    for (int r = 0; r < loaded.Dta.Length; r++) counts[loaded.Dta[r]] = results[r];
                }

                // Global gamma divides by the global normalisation and local by each reference voxel; the lower dose
                // cutoff is a percentage of the normalisation in both. The Max setting supplies it, as it does the
                // DTA threshold, unless a caller set one. Global Max leaves pymedphys' default, the reference maximum.
                foreach (int n in applicable.Where(n => criteria[n].Gamma is not null))
                {
                    var options = criteria[n].Gamma!;
                    var run = options with { DegreeOfParallelism = options.DegreeOfParallelism ?? perGamma };
                    if (reference.Used == MaximumKind.CaxMax && options.GlobalNormalisation is null)
                        run = run with { GlobalNormalisation = reference.Dose };
                    var gamma = GammaShell.Compare(source, target, run, cancellation);
                    gammas[n] = gamma;
                    counts[n] = new SingleComparison(criteria[n].Dta, source.DoseValues.Length, gamma.Analysed, gamma.Analysed - gamma.Passed);
                }

                // The pair's whole time: reading it and every criterion, shared by its rows.
                var elapsed = loaded.Elapsed + timer.Elapsed;
                return applicable.Select(n => new ComparisonOutcome(
                    pair.Key, pair.Source.Dose.SourcePath, pair.Target.Dose.SourcePath,
                    criteria[n].Dta, criteria[n].Global, counts[n], null, elapsed,
                    pair.Source.MonitorUnits, pair.Target.MonitorUnits, gammas.GetValueOrDefault(n), EngineOf(criteria[n]), reference)
                {
                    CriterionIndex = n,
                    SourceEdges = sourceEdges,
                    TargetEdges = targetEdges,
                    TargetFractionRead = loaded.FractionRead,
                }).ToArray();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A cancel is not a failed pair: let it out, so the batch ends and the caller sees
                // OperationCanceledException rather than a report full of "cancelled" rows.
                throw;
            }
            catch (Exception ex)
            {
                // One bad pair must not lose the rest of the batch; the failure is carried in each of its
                // rows so it appears in the report rather than vanishing.
                return Failed($"{ex.GetType().Name}: {ex.Message}", loaded.Elapsed + timer.Elapsed);
            }
        }
    }
}
