namespace DSClibrary.Comparing
{
    /// <summary>
    /// <see cref="HandWrittenComparer"/>'s rules for one sample point, reading cells and weights from a
    /// <see cref="ComparisonSetup"/> rather than computing them per point. Same operations in the same order, in
    /// double, so the outcome is the hand-written one exactly. Diagram: design/data-flow.md, section 3.
    /// </summary>
    internal static class PointRules
    {
        /// <summary>0 below threshold, 1 passed, 2 failed.</summary>
        public static int Evaluate(ComparisonSetup s, double[] source, double[] target, in Index3 p)
        {
            double maxSource = s.MaxSource;
            double sourcei = Dose(source, s.SourceSize, s.Source.X, s.Source.Y, s.Source.Z, p);
            if (sourcei < maxSource * s.Threshold) return 0;
            double targeti = Dose(target, s.TargetSize, s.Target.X, s.Target.Y, s.Target.Z, p);
            double allowed = s.Global ? maxSource * s.Tolerance : s.Tolerance * sourcei;
            if (Math.Abs(targeti - sourcei) <= allowed) return 1;
            if (!s.UseDta) return 2;

            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            int n = 0;
            Tables3 c = s.Source, m = s.SourceMinus, q = s.SourcePlus;
            if (s.Has.XMinus[p.X]) Add(Dose(source, s.SourceSize, m.X, c.Y, c.Z, p), ref n, ref lo, ref hi);
            if (s.Has.XPlus[p.X]) Add(Dose(source, s.SourceSize, q.X, c.Y, c.Z, p), ref n, ref lo, ref hi);
            if (s.Has.YMinus[p.Y]) Add(Dose(source, s.SourceSize, c.X, m.Y, c.Z, p), ref n, ref lo, ref hi);
            if (s.Has.YPlus[p.Y]) Add(Dose(source, s.SourceSize, c.X, q.Y, c.Z, p), ref n, ref lo, ref hi);
            if (s.Has.ZMinus[p.Z]) Add(Dose(source, s.SourceSize, c.X, c.Y, m.Z, p), ref n, ref lo, ref hi);
            if (s.Has.ZPlus[p.Z]) Add(Dose(source, s.SourceSize, c.X, c.Y, q.Z, p), ref n, ref lo, ref hi);
            return n == 0 || targeti < lo || targeti > hi ? 2 : 1;
        }

        private static void Add(double v, ref int n, ref double lo, ref double hi)
        {
            n++;
            if (v < lo) lo = v;
            if (v > hi) hi = v;
        }

        /// <summary><c>DoseMatrixOptimal.GetPointDose</c>, with the per-axis part looked up.</summary>
        private static double Dose(double[] g, in Index3 size, AxisTable tx, AxisTable ty, AxisTable tz, in Index3 p)
        {
            bool ix = tx.Interp[p.X], iy = ty.Interp[p.Y], iz = tz.Interp[p.Z];
            var cell = new Index3(tx.Cell[p.X], ty.Cell[p.Y], tz.Cell[p.Z]);
            double xd = tx.Weight[p.X], yd = ty.Weight[p.Y], zd = tz.Weight[p.Z];
            int dimX = size.X, dimY = size.Y;

            double c00 = ix ? D(cell.X, cell.Y, cell.Z) * (1 - xd) + D(cell.X + 1, cell.Y, cell.Z) * xd : D(cell.X, cell.Y, cell.Z);
            double c10 = iy ? ix ? D(cell.X, cell.Y + 1, cell.Z) * (1 - xd) + D(cell.X + 1, cell.Y + 1, cell.Z) * xd : D(cell.X, cell.Y + 1, cell.Z) : 0;
            double c01 = iz ? ix ? D(cell.X, cell.Y, cell.Z + 1) * (1 - xd) + D(cell.X + 1, cell.Y, cell.Z + 1) * xd : D(cell.X, cell.Y, cell.Z + 1) : 0;
            double c11 = iy && iz ? ix ? D(cell.X, cell.Y + 1, cell.Z + 1) * (1 - xd) + D(cell.X + 1, cell.Y + 1, cell.Z + 1) * xd : D(cell.X, cell.Y + 1, cell.Z + 1) : 0;
            double c0 = iy ? c00 * (1 - yd) + c10 * yd : c00;
            double c1 = iy ? c01 * (1 - yd) + c11 * yd : c01;
            return iz ? c0 * (1 - zd) + c1 * zd : c0;

            double D(int a, int b, int c) => g[a + b * dimX + c * dimX * dimY];
        }
    }
}
