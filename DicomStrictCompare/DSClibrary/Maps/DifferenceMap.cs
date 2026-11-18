using System;
using System.Threading;
using System.Threading.Tasks;
using DSClibrary.Gamma;

namespace DSClibrary.Maps
{
    /// <summary>
    /// Where two doses disagree, voxel by voxel on the source (reference) grid: the gamma index, capped at
    /// <see cref="GammaCap"/>, and the signed dose difference as a percent of the source maximum. NaN marks points not
    /// evaluated (below the cutoff, or outside the target grid). The source dose comes along as the underlay.
    /// </summary>
    public sealed class DifferenceMap
    {
        /// <summary>The highest gamma kept (crcrewso, 2026-11-18: "gamma capped at 3").</summary>
        public const double GammaCap = 3;

        public DoseMatrixOptimal Grid { get; }
        public float[] SourceDose { get; }
        public float[]? Gamma { get; }
        public float[] PercentDifference { get; }
        public GammaOptions? GammaOptions { get; }
        public double SourceMaximum { get; }
        public double CutoffPercent { get; }

        /// <summary>Points evaluated, and of those with gamma above 1 (gamma) or |difference| above the dose criterion.</summary>
        public int Evaluated { get; }
        public int GammaFailed { get; }
        public int PercentEvaluated { get; }

        private DifferenceMap(DoseMatrixOptimal grid, float[] source, float[]? gamma, float[] percent, GammaOptions? options,
            double maximum, double cutoff, int evaluated, int failed, int percentEvaluated)
        {
            Grid = grid; SourceDose = source; Gamma = gamma; PercentDifference = percent; GammaOptions = options;
            SourceMaximum = maximum; CutoffPercent = cutoff; Evaluated = evaluated; GammaFailed = failed; PercentEvaluated = percentEvaluated;
        }

        /// <summary>
        /// The maps for one pair. Gamma runs with <paramref name="gamma"/>'s criteria but always to the cap and without
        /// stopping once a point passes, so each value is the point's gamma; null skips it.
        /// </summary>
        public static DifferenceMap Compute(DoseMatrixOptimal source, DoseMatrixOptimal target, GammaOptions? gamma,
            double cutoffPercent = 10, CancellationToken cancellation = default)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));
            int n = source.DoseValues.Length;
            double max = 0;
            foreach (double v in source.DoseValues) if (v > max) max = v;
            double cutoff = cutoffPercent / 100 * max;

            var dose = new float[n];
            var percent = new float[n];
            int percentEvaluated = 0;
            int nx = source.DimensionX, ny = source.DimensionY;
            Parallel.For(0, source.DimensionZ, new ParallelOptions { CancellationToken = cancellation }, () => 0, (k, _, count) =>
            {
                double z = source.Z0 + k * source.ZRes;
                for (int j = 0; j < ny; j++)
                {
                    double y = source.Y0 + j * source.YRes;
                    for (int i = 0, at = (k * ny + j) * nx; i < nx; i++, at++)
                    {
                        double s = source.DoseValues[at];
                        dose[at] = (float)s;
                        double t = s >= cutoff && max > 0 ? Sample(target, source.X0 + i * source.XRes, y, z) : double.NaN;
                        percent[at] = double.IsNaN(t) ? float.NaN : (float)(100 * (t - s) / max);
                        if (!double.IsNaN(t)) count++;
                    }
                }
                return count;
            }, count => Interlocked.Add(ref percentEvaluated, count));

            float[]? map = null;
            int evaluated = 0, failed = 0;
            GammaOptions? used = null;
            if (gamma is not null)
            {
                used = gamma with { MaxGamma = GammaCap, SkipOncePassed = false, RandomSubset = null, LowerPercentDoseCutoff = cutoffPercent };
                var result = GammaShell.Compare(source, target, used, cancellation);
                map = new float[n];
                for (int i = 0; i < n; i++) map[i] = (float)result.Gamma[i];
                evaluated = result.Analysed;
                failed = result.Analysed - result.Passed;
            }
            return new DifferenceMap(source, dose, map, percent, used, max, cutoffPercent, evaluated, failed, percentEvaluated);
        }

        /// <summary>Trilinear dose at a patient point, NaN outside the grid (no rounding past the far edge).</summary>
        public static double Sample(DoseMatrixOptimal g, double x, double y, double z)
        {
            double fx = (x - g.X0) / g.XRes, fy = (y - g.Y0) / g.YRes, fz = (z - g.Z0) / g.ZRes;
            const double slack = 1e-9;
            if (fx < -slack || fy < -slack || fz < -slack || fx > g.DimensionX - 1 + slack || fy > g.DimensionY - 1 + slack || fz > g.DimensionZ - 1 + slack)
                return double.NaN;
            int i = Math.Clamp((int)Math.Floor(fx), 0, Math.Max(0, g.DimensionX - 2));
            int j = Math.Clamp((int)Math.Floor(fy), 0, Math.Max(0, g.DimensionY - 2));
            int k = Math.Clamp((int)Math.Floor(fz), 0, Math.Max(0, g.DimensionZ - 2));
            double tx = g.DimensionX > 1 ? Math.Clamp(fx - i, 0, 1) : 0, ty = g.DimensionY > 1 ? Math.Clamp(fy - j, 0, 1) : 0, tz = g.DimensionZ > 1 ? Math.Clamp(fz - k, 0, 1) : 0;
            int sx = g.DimensionX > 1 ? 1 : 0, sy = g.DimensionY > 1 ? g.DimensionX : 0, sz = g.DimensionZ > 1 ? g.DimensionX * g.DimensionY : 0;
            int at = i + g.DimensionX * (j + g.DimensionY * k);
            var v = g.DoseValues;
            double c00 = v[at] * (1 - tx) + v[at + sx] * tx;
            double c10 = v[at + sy] * (1 - tx) + v[at + sy + sx] * tx;
            double c01 = v[at + sz] * (1 - tx) + v[at + sz + sx] * tx;
            double c11 = v[at + sz + sy] * (1 - tx) + v[at + sz + sy + sx] * tx;
            return (c00 * (1 - ty) + c10 * ty) * (1 - tz) + (c01 * (1 - ty) + c11 * ty) * tz;
        }
    }
}
