namespace DSClibrary.Comparing.Gpu
{
    /// <summary>
    /// A <see cref="ComparisonSetup"/> packed for a kernel: a header of sizes and offsets, one int array (cells,
    /// interpolation flags, neighbour flags) and one real array (criterion values, weights). The cells and weights
    /// were chosen in double on the host, so a float kernel interpolates between the same voxels the engine does.
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

        public GpuTables(ComparisonSetup s)
        {
            AxisTable[] tables =
            {
                s.Source.X, s.Source.Y, s.Source.Z, s.Target.X, s.Target.Y, s.Target.Z,
                s.SourceMinus.X, s.SourcePlus.X, s.SourceMinus.Y, s.SourcePlus.Y, s.SourceMinus.Z, s.SourcePlus.Z,
            };
            bool[][] has = { s.Has.XMinus, s.Has.XPlus, s.Has.YMinus, s.Has.YPlus, s.Has.ZMinus, s.Has.ZPlus };

            var ints = new List<int>();
            var reals = new List<double> { s.MaxSource, s.Threshold, s.Tolerance };
            for (int t = 0; t < tables.Length; t++)
            {
                Header[HCell + t] = ints.Count; ints.AddRange(tables[t].Cell);
                Header[HInterp + t] = ints.Count; ints.AddRange(tables[t].Interp.Select(b => b ? 1 : 0));
                Header[HWeight + t] = reals.Count; reals.AddRange(tables[t].Weight);
            }
            for (int u = 0; u < has.Length; u++) { Header[HHas + u] = ints.Count; ints.AddRange(has[u].Select(b => b ? 1 : 0)); }
            if (ints.Count == 0) ints.Add(0);   // a buffer may not be empty

            Header[HSourceX] = s.SourceSize.X; Header[HSourceY] = s.SourceSize.Y;
            Header[HTargetX] = s.TargetSize.X; Header[HTargetY] = s.TargetSize.Y;
            Header[HNx] = s.Samples.X; Header[HNy] = s.Samples.Y; Header[HNz] = s.Samples.Z;
            Header[HGlobal] = s.Global ? 1 : 0; Header[HDta] = s.UseDta ? 1 : 0;
            Header[HLo] = s.Lo.X; Header[HLo + 1] = s.Lo.Y; Header[HLo + 2] = s.Lo.Z;
            Header[HHi] = s.Hi.X; Header[HHi + 1] = s.Hi.Y; Header[HHi + 2] = s.Hi.Z;
            Ints = ints.ToArray();
            Reals = reals.ToArray();
            Lines = s.WindowLines;
            LinesY = s.WindowLinesY;
        }
    }

    /// <summary>Float copies of grids, kept while the grid lives, so one source compared many times converts once.</summary>
    internal static class FloatGrids
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DoseMatrixOptimal, float[]> Cache = new();
        public static float[] Of(DoseMatrixOptimal g) => Cache.GetValue(g, x => Array.ConvertAll(x.DoseValues, v => (float)v));
    }
}
