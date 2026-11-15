namespace DSClibrary.Comparing.Gpu
{
    /// <summary>
    /// A <see cref="ComparisonSetup"/> packed for a kernel: a header of sizes and offsets, one int array (cells,
    /// interpolation flags, neighbour flags) and one real array (criterion values, weights). The cells and weights
    /// were chosen in double on the host, so a float kernel interpolates between the same voxels the engine does.
    /// <para>
    /// <b>Only the calculation box is uploaded</b> (Cody, 2026-11-15: bus bandwidth matters on PCIe and older GPUs).
    /// For each grid, the sub-block of voxels the window's sample points and their DTA neighbours read is copied
    /// out, and the cells are shifted to it. The kernel reads the same values as it would from the whole grid.
    /// </para>
    /// </summary>
    internal sealed class GpuTables
    {
        // Header layout, shared with both kernels.
        public const int HSourceX = 0, HSourceY = 1, HTargetX = 2, HTargetY = 3, HNx = 4, HNy = 5, HNz = 6, HGlobal = 7, HDta = 8;
        public const int HLo = 9, HHi = 12;            // Lo.X, Lo.Y, Lo.Z; Hi.X, Hi.Y, Hi.Z
        public const int HCell = 15, HInterp = 27, HWeight = 39, HHas = 51, HCount = 57;
        // Table order: source x,y,z; target x,y,z; source x-d, x+d, y-d, y+d, z-d, z+d.
        // Neighbour order: x-d, x+d, y-d, y+d, z-d, z+d.
        // Reals: [0] source maximum, [1] threshold, [2] tolerance, then the weights.

        public int[] Header { get; } = new int[HCount];
        public int[] Ints { get; }
        public double[] Reals { get; }
        public int Lines { get; }
        public int LinesY { get; }

        /// <summary>The source and target voxels the window needs, copied out of the full grids.</summary>
        public double[] Source { get; } = Array.Empty<double>();
        public double[] Target { get; } = Array.Empty<double>();

        /// <summary>Voxels uploaded, as a fraction of the two full grids: what the box saves on the bus.</summary>
        public double UploadedFraction { get; }

        public GpuTables(ComparisonSetup s, DoseMatrixOptimal source, DoseMatrixOptimal target)
        {
            // A window can be empty along z alone (the box between sample planes): then nothing is evaluated.
            Lines = s.WindowPoints > 0 ? s.WindowLines : 0;
            LinesY = s.WindowLinesY;
            bool[][] has = { s.Has.XMinus, s.Has.XPlus, s.Has.YMinus, s.Has.YPlus, s.Has.ZMinus, s.Has.ZPlus };

            // The voxel ranges each grid's window reads, per axis.
            Index3 sLo = default, sHi = default, tLo = default, tHi = default;
            if (Lines > 0)
            {
                (int, int) Range(int from, int to, AxisTable centre, AxisTable? minus, AxisTable? plus, bool[]? hasMinus, bool[]? hasPlus)
                {
                    int lo = int.MaxValue, hi = int.MinValue;
                    void Take(AxisTable t, int i) { lo = Math.Min(lo, t.Cell[i]); hi = Math.Max(hi, t.Cell[i] + (t.Interp[i] ? 1 : 0)); }
                    for (int i = from; i <= to; i++)
                    {
                        Take(centre, i);
                        if (minus is not null && hasMinus![i]) Take(minus, i);
                        if (plus is not null && hasPlus![i]) Take(plus, i);
                    }
                    return (lo, hi);
                }
                bool dta = s.UseDta;
                var (sx0, sx1) = Range(s.Lo.X, s.Hi.X, s.Source.X, dta ? s.SourceMinus.X : null, dta ? s.SourcePlus.X : null, s.Has.XMinus, s.Has.XPlus);
                var (sy0, sy1) = Range(s.Lo.Y, s.Hi.Y, s.Source.Y, dta ? s.SourceMinus.Y : null, dta ? s.SourcePlus.Y : null, s.Has.YMinus, s.Has.YPlus);
                var (sz0, sz1) = Range(s.Lo.Z, s.Hi.Z, s.Source.Z, dta ? s.SourceMinus.Z : null, dta ? s.SourcePlus.Z : null, s.Has.ZMinus, s.Has.ZPlus);
                var (tx0, tx1) = Range(s.Lo.X, s.Hi.X, s.Target.X, null, null, null, null);
                var (ty0, ty1) = Range(s.Lo.Y, s.Hi.Y, s.Target.Y, null, null, null, null);
                var (tz0, tz1) = Range(s.Lo.Z, s.Hi.Z, s.Target.Z, null, null, null, null);
                sLo = new Index3(sx0, sy0, sz0); sHi = new Index3(sx1, sy1, sz1);
                tLo = new Index3(tx0, ty0, tz0); tHi = new Index3(tx1, ty1, tz1);
                // A sample a rounding error past a grid's last node is "interpolated" and reads the voxel after the
                // last column or row: in the flat array, the next row's first voxel, weighted about 1e-15. The
                // hand-written engine does this, so to match it exactly such a grid is uploaded whole.
                (sLo, sHi) = WholeIfPastEdge(source, sLo, sHi);
                (tLo, tHi) = WholeIfPastEdge(target, tLo, tHi);
                Source = Crop(source, sLo, sHi);
                Target = Crop(target, tLo, tHi);
                UploadedFraction = (double)(Source.LongLength + Target.LongLength) / (source.DoseValues.LongLength + target.DoseValues.LongLength);
            }

            // Tables, with cells shifted to the copied blocks. Source tables shift by sLo, target tables by tLo.
            (AxisTable Table, int Shift)[] tables =
            {
                (s.Source.X, sLo.X), (s.Source.Y, sLo.Y), (s.Source.Z, sLo.Z),
                (s.Target.X, tLo.X), (s.Target.Y, tLo.Y), (s.Target.Z, tLo.Z),
                (s.SourceMinus.X, sLo.X), (s.SourcePlus.X, sLo.X), (s.SourceMinus.Y, sLo.Y),
                (s.SourcePlus.Y, sLo.Y), (s.SourceMinus.Z, sLo.Z), (s.SourcePlus.Z, sLo.Z),
            };
            var ints = new List<int>();
            var reals = new List<double> { s.MaxSource, s.Threshold, s.Tolerance };
            for (int t = 0; t < tables.Length; t++)
            {
                var (table, shift) = tables[t];
                Header[HCell + t] = ints.Count; ints.AddRange(table.Cell.Select(c => c - shift));
                Header[HInterp + t] = ints.Count; ints.AddRange(table.Interp.Select(b => b ? 1 : 0));
                Header[HWeight + t] = reals.Count; reals.AddRange(table.Weight);
            }
            for (int u = 0; u < has.Length; u++) { Header[HHas + u] = ints.Count; ints.AddRange(has[u].Select(b => b ? 1 : 0)); }
            if (ints.Count == 0) ints.Add(0);   // a buffer may not be empty

            Header[HSourceX] = sHi.X - sLo.X + 1; Header[HSourceY] = sHi.Y - sLo.Y + 1;
            Header[HTargetX] = tHi.X - tLo.X + 1; Header[HTargetY] = tHi.Y - tLo.Y + 1;
            Header[HNx] = s.Samples.X; Header[HNy] = s.Samples.Y; Header[HNz] = s.Samples.Z;
            Header[HGlobal] = s.Global ? 1 : 0; Header[HDta] = s.UseDta ? 1 : 0;
            Header[HLo] = s.Lo.X; Header[HLo + 1] = s.Lo.Y; Header[HLo + 2] = s.Lo.Z;
            Header[HHi] = s.Hi.X; Header[HHi + 1] = s.Hi.Y; Header[HHi + 2] = s.Hi.Z;
            Ints = ints.ToArray();
            Reals = reals.ToArray();
        }

        private static (Index3, Index3) WholeIfPastEdge(DoseMatrixOptimal g, Index3 lo, Index3 hi) =>
            lo.X < 0 || lo.Y < 0 || lo.Z < 0 || hi.X > g.DimensionX - 1 || hi.Y > g.DimensionY - 1 || hi.Z > g.DimensionZ - 1
                ? (new Index3(0, 0, 0), new Index3(g.DimensionX - 1, g.DimensionY - 1, g.DimensionZ - 1))
                : (lo, hi);

        /// <summary>The block [lo, hi] of a grid, x fastest, as its own array.</summary>
        private static double[] Crop(DoseMatrixOptimal g, Index3 lo, Index3 hi)
        {
            if (lo == new Index3(0, 0, 0) && hi == new Index3(g.DimensionX - 1, g.DimensionY - 1, g.DimensionZ - 1)) return g.DoseValues;
            int nx = hi.X - lo.X + 1, ny = hi.Y - lo.Y + 1, nz = hi.Z - lo.Z + 1;
            var v = new double[nx * ny * nz];
            int gx = g.DimensionX, plane = g.DimensionX * g.DimensionY;
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    Array.Copy(g.DoseValues, lo.X + (lo.Y + y) * gx + (lo.Z + z) * plane, v, (y + z * ny) * nx, nx);
            return v;
        }
    }
}
