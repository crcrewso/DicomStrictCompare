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

        public GpuTables(ComparisonSetup s, GpuUpload? upload)
        {
            // A window can be empty along z alone (the box between sample planes): then nothing is evaluated.
            Lines = s.WindowPoints > 0 ? s.WindowLines : 0;
            LinesY = s.WindowLinesY;
            if (Lines > 0 && upload is null) throw new ArgumentNullException(nameof(upload), "A window with lines needs its upload.");
            bool[][] has = { s.Has.XMinus, s.Has.XPlus, s.Has.YMinus, s.Has.YPlus, s.Has.ZMinus, s.Has.ZPlus };
            Index3 sLo = upload?.SourceLo ?? default, sHi = upload?.SourceHi ?? default;
            Index3 tLo = upload?.TargetLo ?? default, tHi = upload?.TargetHi ?? default;

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

        /// <summary>The voxel ranges one criterion's window reads in each grid, with its DTA neighbours; null when it reads none.</summary>
        public static (Index3 SLo, Index3 SHi, Index3 TLo, Index3 THi)? Reads(ComparisonSetup s)
        {
            if (s.WindowPoints <= 0) return null;
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
            return (new Index3(sx0, sy0, sz0), new Index3(sx1, sy1, sz1), new Index3(tx0, ty0, tz0), new Index3(tx1, ty1, tz1));
        }
    }

    /// <summary>
    /// The source and target blocks one pair uploads, once for all its criteria: the union of the voxels every
    /// criterion's window and DTA neighbours read. Each criterion's <see cref="GpuTables"/> shifts its cells to it.
    /// </summary>
    internal sealed class GpuUpload
    {
        public Index3 SourceLo { get; }
        public Index3 SourceHi { get; }
        public Index3 TargetLo { get; }
        public Index3 TargetHi { get; }
        public double[] Source { get; }
        public double[] Target { get; }

        /// <summary>Voxels uploaded, as a fraction of the two full grids: what the box saves on the bus.</summary>
        public double UploadedFraction { get; }

        public long Bytes(int realSize) => (Source.LongLength + Target.LongLength) * realSize;

        private GpuUpload(DoseMatrixOptimal source, DoseMatrixOptimal target, Index3 sLo, Index3 sHi, Index3 tLo, Index3 tHi)
        {
            // A sample a rounding error past a grid's last node is "interpolated" and reads the voxel after the
            // last column or row: in the flat array, the next row's first voxel, weighted about 1e-15. The
            // hand-written engine does this, so to match it exactly such a grid is uploaded whole.
            (SourceLo, SourceHi) = WholeIfPastEdge(source, sLo, sHi);
            (TargetLo, TargetHi) = WholeIfPastEdge(target, tLo, tHi);
            Source = Crop(source, SourceLo, SourceHi);
            Target = Crop(target, TargetLo, TargetHi);
            UploadedFraction = (double)(Source.LongLength + Target.LongLength) / (source.DoseValues.LongLength + target.DoseValues.LongLength);
        }

        /// <summary>The union of the criteria's reads, or null when none of them evaluates a point.</summary>
        public static GpuUpload? For(IEnumerable<ComparisonSetup> setups, DoseMatrixOptimal source, DoseMatrixOptimal target)
        {
            static Index3 Min(Index3 a, Index3 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
            static Index3 Max(Index3 a, Index3 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
            (Index3 SLo, Index3 SHi, Index3 TLo, Index3 THi)? union = null;
            foreach (var s in setups)
            {
                if (GpuTables.Reads(s) is not { } r) continue;
                union = union is { } u ? (Min(u.SLo, r.SLo), Max(u.SHi, r.SHi), Min(u.TLo, r.TLo), Max(u.THi, r.THi)) : r;
            }
            return union is { } w ? new GpuUpload(source, target, w.SLo, w.SHi, w.TLo, w.THi) : null;
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
