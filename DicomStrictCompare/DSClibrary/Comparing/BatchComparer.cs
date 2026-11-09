using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSClibrary.Gamma;
using DSClibrary.Matching;
using DSClibrary.Profiles;

namespace DSClibrary.Comparing
{
    /// <summary>
    /// The result of comparing one pair, kept as an object.
    /// <para>
    /// Retained rather than rendered, because Cody wants an interface that can swap between and
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
            GammaResult? gamma = null)
        {
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
        private readonly IMathematics _mathematics;

        /// <param name="load">
        /// Loads a grid from a path. Injected so a caller can choose its reader - this library's own
        /// or EvilDICOM - and so tests can supply small grids.
        /// </param>
        /// <param name="mathematics">The comparison engine. Defaults to <see cref="X86Mathematics"/>.</param>
        public BatchComparer(Func<string, DoseMatrixOptimal> load, IMathematics? mathematics = null)
        {
            _load = load ?? throw new ArgumentNullException(nameof(load));
            _mathematics = mathematics ?? new X86Mathematics();
        }

        /// <summary>
        /// Four is the default rather than <see cref="Environment.ProcessorCount"/>, because the
        /// constraint here is memory and not cores: at 61 MB per grid and two grids per comparison,
        /// a sixteen-core machine would try to hold about two gigabytes of dose.
        /// </summary>
        public static int DefaultDegreeOfParallelism => Math.Min(4, Environment.ProcessorCount);

        public BatchComparisonResult Compare(
            IEnumerable<MatchedDosePair> pairs,
            Dta dta,
            bool global = true,
            int? degreeOfParallelism = null,
            CancellationToken cancellation = default)
        {
            if (pairs is null) throw new ArgumentNullException(nameof(pairs));
            if (dta is null) throw new ArgumentNullException(nameof(dta));

            var work = pairs.ToArray();
            int parallelism = Math.Max(1, degreeOfParallelism ?? DefaultDegreeOfParallelism);
            var outcomes = new ConcurrentBag<ComparisonOutcome>();
            var batch = Stopwatch.StartNew();

            Parallel.ForEach(
                work,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism,
                    CancellationToken = cancellation
                },
                pair => outcomes.Add(CompareOne(pair, dta, global, cancellation)));

            batch.Stop();

            // A stable order, so two runs of the same batch produce the same report.
            var ordered = outcomes
                .OrderBy(o => o.Key.ToString(), StringComparer.Ordinal)
                .ToArray();

            return new BatchComparisonResult(ordered, batch.Elapsed, parallelism);
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
            CancellationToken cancellation = default)
        {
            if (pairs is null) throw new ArgumentNullException(nameof(pairs));
            if (options is null) throw new ArgumentNullException(nameof(options));
            options.Validate();

            var work = pairs.ToArray();
            int parallelism = Math.Max(1, degreeOfParallelism ?? DefaultDegreeOfParallelism);
            int perGamma = options.DegreeOfParallelism ?? Math.Max(1, Environment.ProcessorCount / parallelism);
            var perPair = options with { DegreeOfParallelism = perGamma };
            var outcomes = new ConcurrentBag<ComparisonOutcome>();
            var batch = Stopwatch.StartNew();

            Parallel.ForEach(
                work,
                new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellation },
                pair => outcomes.Add(CompareOneGamma(pair, perPair, cancellation)));

            batch.Stop();
            var ordered = outcomes.OrderBy(o => o.Key.ToString(), StringComparer.Ordinal).ToArray();
            return new BatchComparisonResult(ordered, batch.Elapsed, parallelism);
        }

        /// <summary>The <see cref="Dta"/> that describes a gamma comparison's parameters in the library's older terms.</summary>
        public static Dta DtaFor(GammaOptions options) => new(
            useMM: true,
            threshhold: options.LowerPercentDoseCutoff / 100.0,
            tolerance: options.DosePercentThreshold / 100.0,
            distance: options.DistanceThreshold,
            relative: !options.LocalGamma,
            gamma: true);

        private ComparisonOutcome CompareOneGamma(MatchedDosePair pair, GammaOptions options, CancellationToken cancellation)
        {
            var timer = Stopwatch.StartNew();
            Dta dta = DtaFor(options);
            bool global = !options.LocalGamma;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                DoseMatrixOptimal source = _load(pair.Source.Dose.SourcePath);
                cancellation.ThrowIfCancellationRequested();
                DoseMatrixOptimal target = _load(pair.Target.Dose.SourcePath);

                GammaResult gamma = GammaShell.Compare(source, target, options, cancellation);
                var comparison = new SingleComparison(dta, source.DoseValues.Length, gamma.Analysed, gamma.Analysed - gamma.Passed);

                timer.Stop();
                return new ComparisonOutcome(
                    pair.Key, pair.Source.Dose.SourcePath, pair.Target.Dose.SourcePath,
                    dta, global, comparison, null, timer.Elapsed,
                    pair.Source.MonitorUnits, pair.Target.MonitorUnits, gamma);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                timer.Stop();
                return new ComparisonOutcome(
                    pair.Key, pair.Source.Dose.SourcePath, pair.Target.Dose.SourcePath,
                    dta, global, null, $"{ex.GetType().Name}: {ex.Message}", timer.Elapsed,
                    pair.Source.MonitorUnits, pair.Target.MonitorUnits);
            }
        }

        private ComparisonOutcome CompareOne(MatchedDosePair pair, Dta dta, bool global, CancellationToken cancellation)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                cancellation.ThrowIfCancellationRequested();
                DoseMatrixOptimal source = _load(pair.Source.Dose.SourcePath);
                cancellation.ThrowIfCancellationRequested();
                DoseMatrixOptimal target = _load(pair.Target.Dose.SourcePath);

                // The token goes into the engine, which checks it once per plane, so a cancel stops a
                // comparison that is under way rather than waiting for it to finish.
                SingleComparison comparison = global
                    ? _mathematics.CompareRelative(source, target, dta, cancellation)
                    : _mathematics.CompareAbsolute(source, target, dta, cancellation);

                timer.Stop();
                return new ComparisonOutcome(
                    pair.Key, pair.Source.Dose.SourcePath, pair.Target.Dose.SourcePath,
                    dta, global, comparison, null, timer.Elapsed,
                    pair.Source.MonitorUnits, pair.Target.MonitorUnits);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A cancel is not a failed pair: let it out, so Parallel.ForEach ends the batch and the
                // caller sees OperationCanceledException rather than a report full of "cancelled" rows.
                throw;
            }
            catch (Exception ex)
            {
                // One bad pair must not lose the rest of the batch; the failure is carried in the
                // outcome so it appears in the report rather than vanishing.
                timer.Stop();
                return new ComparisonOutcome(
                    pair.Key, pair.Source.Dose.SourcePath, pair.Target.Dose.SourcePath,
                    dta, global, null, $"{ex.GetType().Name}: {ex.Message}", timer.Elapsed,
                    pair.Source.MonitorUnits, pair.Target.MonitorUnits);
            }
        }
    }
}
