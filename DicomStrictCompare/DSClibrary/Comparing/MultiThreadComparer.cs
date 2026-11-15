namespace DSClibrary.Comparing
{
    /// <summary>
    /// <see cref="HandWrittenComparer"/>'s rules with one comparison split across threads: the (i, j) lines inside
    /// the threshold box are handed out, each thread keeps its own counts, and they are merged once. Counts are the
    /// hand-written ones exactly. Combine with <see cref="BatchComparer"/> to run several pairs at once as well.
    /// </summary>
    public sealed class MultiThreadComparer : DoseComparer
    {
        /// <summary>Threads within one comparison; defaults to one per physical core (<see cref="CpuTopology"/>).</summary>
        public int Threads { get; init; } = CpuTopology.PhysicalCores;

        /// <summary>
        /// Pins each thread to its own physical core, so two never share an SMT pair. Only when there are no more
        /// threads than cores; otherwise the threads run unpinned. Defaults to <see cref="CpuTopology.PinByDefault"/>.
        /// </summary>
        public bool PinThreads { get; init; } = CpuTopology.PinByDefault;

        /// <summary>The cores to pin to, for measuring one core type against another; <see cref="CpuTopology.Cores"/> when null.</summary>
        public IReadOnlyList<CpuTopology.Core>? CoreSet { get; init; }

        /// <summary>Off evaluates every sample point, for checking the box on your own data.</summary>
        public bool UseThresholdBox { get; init; } = true;

        public override string Description => $"MultiThread, {Threads} threads{(Pinned ? ", one per core" : "")}{(UseThresholdBox ? ", threshold box" : "")}";

        private bool Pinned => PinThreads && Threads > 1 && Threads <= (CoreSet ?? CpuTopology.Cores).Count;

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
            int wy = s.WindowLinesY, lines = s.WindowLines, threads = Math.Max(1, Math.Min(Threads, Math.Max(1, lines)));
            long compared = 0, failed = 0;
            // Dedicated threads, so pinning never touches the shared thread pool. Lines are handed out in small chunks.
            int next = 0;
            const int Chunk = 16;
            Exception? error = null;
            var cores = CoreSet ?? CpuTopology.Cores;
            bool pin = Pinned && threads <= cores.Count;
            void Work(int worker)
            {
                try
                {
                    if (pin) CpuTopology.PinCurrentThread(cores[worker]);
                    long c = 0, f = 0;
                    for (int start; (start = Interlocked.Add(ref next, Chunk) - Chunk) < lines;)
                    {
                        if (cancellation.IsCancellationRequested || Volatile.Read(ref error) is not null) break;
                        for (int line = start; line < Math.Min(start + Chunk, lines); line++)
                        {
                            int i = s.Lo.X + line / wy, j = s.Lo.Y + line % wy;
                            for (int k = s.Lo.Z; k <= s.Hi.Z; k++)
                            {
                                int r = PointRules.Evaluate(s, sv, tv, new Index3(i, j, k));
                                if (r > 0) { c++; if (r == 2) f++; }
                            }
                        }
                    }
                    Interlocked.Add(ref compared, c);
                    Interlocked.Add(ref failed, f);
                }
                catch (Exception ex) { Interlocked.CompareExchange(ref error, ex, null); }
            }
            var workers = Enumerable.Range(1, threads - 1).Select(w => new Thread(() => Work(w)) { IsBackground = true, Name = $"DSC compare {w}" }).ToArray();
            foreach (var t in workers) t.Start();
            if (pin)
            {
                // The calling thread is not pinned (it may belong to the pool), so worker 0 gets its own thread too.
                var first = new Thread(() => Work(0)) { IsBackground = true, Name = "DSC compare 0" };
                first.Start();
                first.Join();
            }
            else Work(0);
            foreach (var t in workers) t.Join();
            if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            cancellation.ThrowIfCancellationRequested();
            return new SingleComparison(dta, s.TotalCount, (int)compared, (int)failed);
        }
    }
}
