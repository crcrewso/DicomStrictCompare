namespace DSClibrary.Comparing
{
    /// <summary>
    /// <see cref="HandWrittenComparer"/>'s rules with one comparison split across threads: the (i, j) lines inside
    /// the threshold box are handed out, each thread keeps its own counts, and they are merged once. Counts are the
    /// hand-written ones exactly. Combine with <see cref="BatchComparer"/> to run several pairs at once as well.
    /// </summary>
    public sealed class MultiThreadComparer : DoseComparer
    {
        /// <summary>Threads within one comparison; defaults to every core.</summary>
        public int Threads { get; init; } = Environment.ProcessorCount;

        /// <summary>Off evaluates every sample point, for checking the box on your own data.</summary>
        public bool UseThresholdBox { get; init; } = true;

        public override string Description => $"MultiThread, {Threads} threads{(UseThresholdBox ? ", threshold box" : "")}";

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

        private SingleComparison Compare(DoseMatrixOptimal source, DoseMatrixOptimal target, Dta dta, bool global, double referenceMaximum, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var s = ComparisonSetup.For(source, target, dta, global, UseThresholdBox, referenceMaximum);
            double[] sv = source.DoseValues, tv = target.DoseValues;
            int wy = s.WindowLinesY;
            long compared = 0, failed = 0;
            Parallel.For(0, s.WindowLines,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Threads), CancellationToken = cancellation },
                () => (0L, 0L),
                (line, _, acc) =>
                {
                    int i = s.Lo.X + line / wy, j = s.Lo.Y + line % wy;
                    long c = acc.Item1, f = acc.Item2;
                    for (int k = s.Lo.Z; k <= s.Hi.Z; k++)
                    {
                        int r = PointRules.Evaluate(s, sv, tv, new Index3(i, j, k));
                        if (r > 0) { c++; if (r == 2) f++; }
                    }
                    return (c, f);
                },
                acc => { Interlocked.Add(ref compared, acc.Item1); Interlocked.Add(ref failed, acc.Item2); });
            return new SingleComparison(dta, s.TotalCount, (int)compared, (int)failed);
        }
    }
}
