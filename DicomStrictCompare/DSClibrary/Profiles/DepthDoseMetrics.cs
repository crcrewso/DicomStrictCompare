using System;
using System.Linq;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// Depth-dose metrics of one curve, read from its own samples with linear interpolation between them: the depth of
    /// maximum, the dose at 100 and 200 mm in % of the maximum, their ratio (D20/D10), and the depths beyond the maximum
    /// where the dose falls to 80 % and 50 % (R80, R50). Null where the curve doesn't reach. Depths in mm.
    /// <para>
    /// These are the standard definitions (design/comparison-studies.md, F8 lists them); nothing here fits a curve, so
    /// dmax is the deepest-reaching sample at the maximum on the curve's own positions, and on a coarse scan it is only as
    /// fine as the scan.
    /// </para>
    /// </summary>
    public sealed record DepthDoseMetrics(
        double DmaxMm,
        double? D100Percent,
        double? D200Percent,
        double? D20OverD10,
        double? R80Mm,
        double? R50Mm)
    {
        public static DepthDoseMetrics Of(Profile pdd)
        {
            if (pdd is null) throw new ArgumentNullException(nameof(pdd));
            if (pdd.Scan.CurveType != CurveType.Pdd) throw new ArgumentException("Depth-dose metrics need a depth dose.", nameof(pdd));
            double[] depth = pdd.Points.Select(p => p.Position.Millimetres).ToArray();
            double[] value = pdd.Points.Select(p => p.Value).ToArray();
            int iMax = 0;
            for (int i = 1; i < value.Length; i++) if (value[i] > value[iMax]) iMax = i;
            double max = value[iMax];
            if (!(max > 0)) throw new InvalidOperationException("The depth dose has no positive maximum.");

            double? At(double d)
            {
                if (d < depth[0] || d > depth[^1]) return null;
                for (int i = 1; i < depth.Length; i++)
                {
                    if (depth[i] < d) continue;
                    double span = depth[i] - depth[i - 1];
                    double t = span > 0 ? (d - depth[i - 1]) / span : 1;
                    return 100 * (value[i - 1] + t * (value[i] - value[i - 1])) / max;
                }
                return 100 * value[^1] / max;
            }

            double? FallsTo(double fraction)
            {
                double level = fraction * max;
                for (int i = iMax + 1; i < value.Length; i++)
                {
                    if (value[i] > level) continue;
                    double drop = value[i - 1] - value[i];
                    return drop > 0 ? depth[i - 1] + (depth[i] - depth[i - 1]) * (value[i - 1] - level) / drop : depth[i];
                }
                return null;
            }

            double? d100 = At(100), d200 = At(200);
            return new DepthDoseMetrics(depth[iMax], d100, d200,
                d100 is > 0 && d200 is { } d2 ? d2 / d100.Value : null,
                FallsTo(0.8), FallsTo(0.5));
        }

        public override string ToString() =>
            FormattableString.Invariant($"dmax {DmaxMm:0.0} mm, D100 {D100Percent:0.00}%, D200 {D200Percent:0.00}%, D20/D10 {D20OverD10:0.0000}, R80 {R80Mm:0.0} mm, R50 {R50Mm:0.0} mm");
    }
}
