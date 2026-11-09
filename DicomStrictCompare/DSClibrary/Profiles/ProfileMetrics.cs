// Ported from FauxPID (D:\FauxPID, FauxPID/algorithms/metrics.py and docs/ALGORITHMS.md, Cody Crewson,
// commit 1014da2 of 2026-08-28) to C# for DicomStrictCompare by crane-dsc on 2026-11-09 at Cody's
// request ("get the algorithms ported and I can deal with the implementation later"). The formulas
// come from the Sun Nuclear IC Profiler help and IEC 60976, as FauxPID's documentation states.
using System;
using System.Collections.Generic;
using System.Linq;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A one dimensional curve as the profile metrics need it: positions in millimetres, ascending,
    /// and a value at each. Built from a <see cref="Profile"/> (a measured scan) or from a line
    /// sampled out of a dose grid, so one set of metrics serves both sides of a comparison.
    /// </summary>
    public sealed class ProfileLine
    {
        public double[] Positions { get; }
        public double[] Values { get; }
        public int Count => Positions.Length;

        public ProfileLine(double[] positions, double[] values)
        {
            if (positions is null) throw new ArgumentNullException(nameof(positions));
            if (values is null) throw new ArgumentNullException(nameof(values));
            if (positions.Length != values.Length)
                throw new ArgumentException("positions and values must have the same length", nameof(values));
            if (positions.Length < 3)
                throw new ArgumentException("a profile needs at least three points", nameof(positions));
            for (int i = 1; i < positions.Length; i++)
                if (!(positions[i] > positions[i - 1]))
                    throw new ArgumentException("positions must be strictly ascending", nameof(positions));
            if (values.Any(v => !double.IsFinite(v)))
                throw new ArgumentException("values must be finite", nameof(values));
            Positions = positions;
            Values = values;
        }

        public static ProfileLine From(Profile profile)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            return new ProfileLine(
                profile.Points.Select(p => p.Position.Millimetres).ToArray(),
                profile.Points.Select(p => p.Value).ToArray());
        }

        /// <summary>Linear interpolation between neighbouring samples; throws outside the measured range.</summary>
        public double ValueAt(double position)
        {
            if (position < Positions[0] || position > Positions[^1])
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"{position} mm is outside the profile's range {Positions[0]} to {Positions[^1]} mm");
            int hi = Array.BinarySearch(Positions, position);
            if (hi >= 0) return Values[hi];
            hi = ~hi;
            int lo = hi - 1;
            double t = (position - Positions[lo]) / (Positions[hi] - Positions[lo]);
            return Values[lo] + t * (Values[hi] - Values[lo]);
        }

        /// <summary>The last sample at or before a position, and the first at or after it.</summary>
        internal (int Left, int Right) Straddling(double position)
        {
            int found = Array.BinarySearch(Positions, position);
            if (found >= 0) return (found, found);
            int right = ~found;
            return (right - 1, right);
        }
    }

    /// <summary>The nine FauxPID metrics of one curve, in one record, for a table or a report.</summary>
    public sealed record ProfileMetricsReport(
        double FieldSizeMm,
        double CaxOffsetMm,
        double FlatnessByVariancePercent,
        double FlatnessByRatioIecPercent,
        double FlatnessByCaxVariance,
        double FlatnessByCaxRatio,
        double SymmetryByCaxPointDifferencePercent,
        double SymmetryByPointRatioIecPercent,
        double SymmetryByAreaPercent)
    {
        public override string ToString() =>
            $"field {FieldSizeMm:0.0} mm, CAX offset {CaxOffsetMm:+0.00;-0.00} mm; flatness {FlatnessByVariancePercent:0.00}% (variance), "
            + $"{FlatnessByRatioIecPercent:0.00}% (IEC ratio), {FlatnessByCaxVariance:0.0000} (CAX variance), {FlatnessByCaxRatio:0.0000} (CAX ratio); "
            + $"symmetry {SymmetryByCaxPointDifferencePercent:+0.00;-0.00}% (CAX point), {SymmetryByPointRatioIecPercent:0.00}% (IEC ratio), "
            + $"{SymmetryByAreaPercent:+0.00;-0.00}% (area)";
    }

    /// <summary>
    /// Field size, central-axis offset, flatness and symmetry of a profile, as FauxPID computes them.
    /// <para>
    /// FauxPID works on a pylinac profile: an array of samples at one spacing, with the central axis
    /// (CAX) at the array's centre index and its value the mean of the two middle samples. Here the
    /// curve has positions, so the same definitions are stated in millimetres: the CAX is a position
    /// (<c>caxPosition</c>, 0 by default, the beam axis of a scan or the isocentre axis of a dose
    /// line) and its value is the interpolated value there, which on a grid symmetric about the axis
    /// is FauxPID's mean of the two middle samples. Three FauxPID conventions are kept on purpose, so
    /// that the numbers match FauxPID and not necessarily pylinac:
    /// </para>
    /// <list type="bullet">
    /// <item>A region boundary that falls between two samples is rounded towards the centre: a region
    /// holds the samples strictly inside it, plus those exactly on its edge.</item>
    /// <item>The field size counts the edge samples themselves, so it is the distance between the
    /// outermost samples above threshold plus one sample spacing.</item>
    /// <item>The point symmetries walk outward from the centre one sample at a time on each side and
    /// pair the k-th samples; the area symmetry sums trapezoids between successive samples from the
    /// centre through the region's edge sample and one beyond, exactly as FauxPID's loop does.</item>
    /// </list>
    /// <para>
    /// Each metric throws <see cref="InvalidOperationException"/> when the curve cannot support it (no
    /// threshold crossing, the CAX outside the field, a zero divisor), as FauxPID raises ValueError.
    /// </para>
    /// </summary>
    public static class ProfileMetrics
    {
        /// <summary>
        /// The field edges by FWXM: the first and last samples above <paramref name="fraction"/> of the
        /// maximum. If the CAX lies between them the threshold is re-taken from the CAX value and the
        /// edges found again (FauxPID's <c>get_field_indices</c>). Returns sample indices.
        /// </summary>
        public static (int Left, int Right) FieldEdgeIndices(ProfileLine line, double caxPosition = 0, double fraction = 0.5)
        {
            if (line is null) throw new ArgumentNullException(nameof(line));
            if (!(fraction > 0 && fraction < 1)) throw new ArgumentOutOfRangeException(nameof(fraction));
            double max = line.Values.Max();
            var (left, right) = Transitions(line, max * fraction);
            if (line.Positions[left] < caxPosition && line.Positions[right] > caxPosition)
            {
                double cax = line.ValueAt(caxPosition);
                (left, right) = Transitions(line, cax * fraction);
            }
            return (left, right);
        }

        /// <summary>The positions of the field edge samples, in mm.</summary>
        public static (double Left, double Right) FieldEdges(ProfileLine line, double caxPosition = 0, double fraction = 0.5)
        {
            var (l, r) = FieldEdgeIndices(line, caxPosition, fraction);
            return (line.Positions[l], line.Positions[r]);
        }

        /// <summary>
        /// Field size by FWXM (FWHM by default), in mm: the distance between the edge samples plus one
        /// sample spacing, FauxPID's <c>(right - left + 1) / dpmm</c>. On an uneven grid the spacing
        /// added is half the gap to the next sample outward on each side.
        /// </summary>
        public static double FieldSize(ProfileLine line, double caxPosition = 0, double fraction = 0.5)
        {
            var (l, r) = FieldEdgeIndices(line, caxPosition, fraction);
            return line.Positions[r] - line.Positions[l] + HalfGapOutward(line, l, -1) + HalfGapOutward(line, r, +1);
        }

        /// <summary>
        /// Beam centre minus CAX, in mm: positive when the beam centre (midpoint of the FWHM edges)
        /// lies at higher position than the axis. FauxPID: "positive values indicate the CAX is to
        /// the right or below (outward) the beam center", the same sign read from the beam's side.
        /// </summary>
        public static double CaxOffset(ProfileLine line, double caxPosition = 0)
        {
            var (l, r) = FieldEdgeIndices(line, caxPosition);
            return (line.Positions[l] + line.Positions[r]) / 2 - caxPosition;
        }

        /// <summary>(max - min) / (max + min) x 100 over <paramref name="inFieldRatio"/> of the field about the CAX. Sun Nuclear "Flatness Calculation by Variance".</summary>
        public static double FlatnessByVariance(ProfileLine line, double caxPosition = 0, double inFieldRatio = 0.8)
        {
            var roi = RegionAboutCax(line, caxPosition, inFieldRatio);
            double max = roi.Max(), min = roi.Min();
            if (max + min == 0) throw new InvalidOperationException("Cannot calculate flatness by variance: max + min is zero");
            return 100 * (max - min) / (max + min);
        }

        /// <summary>max / min x 100 over the IEC 60976 region about the CAX: per side, length - 20 mm under 100 mm, 80 % under 300 mm, length - 60 mm beyond.</summary>
        public static double FlatnessByRatioIec(ProfileLine line, double caxPosition = 0)
        {
            var (l, r) = EdgesWithCaxInside(line, caxPosition);
            double leftLength = caxPosition - line.Positions[l], rightLength = line.Positions[r] - caxPosition;
            var roi = Within(line, caxPosition - IecRegion(leftLength), caxPosition + IecRegion(rightLength));
            double min = roi.Min();
            if (min == 0) throw new InvalidOperationException("Cannot calculate IEC flatness ratio: the minimum in the region is zero");
            return 100 * roi.Max() / min;
        }

        /// <summary>(max - min) / CAX over the whole FWHM field. Sun Nuclear "CAX Variance".</summary>
        public static double FlatnessByCaxVariance(ProfileLine line, double caxPosition = 0)
        {
            var (l, r) = EdgesWithCaxInside(line, caxPosition);
            double cax = CaxValue(line, caxPosition);
            var roi = Within(line, line.Positions[l], line.Positions[r]);
            return (roi.Max() - roi.Min()) / cax;
        }

        /// <summary>max / CAX over 90 % of the field about the CAX. Sun Nuclear "CAX Ratio".</summary>
        public static double FlatnessByCaxRatio(ProfileLine line, double caxPosition = 0)
        {
            var roi = RegionAboutCax(line, caxPosition, 0.9);
            return roi.Max() / CaxValue(line, caxPosition);
        }

        /// <summary>
        /// The largest (left - right) / CAX x 100 over mirrored sample pairs within 80 % of the field
        /// about the CAX, sign kept: negative means the right (higher position) side is higher.
        /// Sun Nuclear "CAX Point Difference".
        /// </summary>
        public static double SymmetryByCaxPointDifference(ProfileLine line, double caxPosition = 0)
        {
            var (l, r) = EdgesWithCaxInside(line, caxPosition);
            double cax = CaxValue(line, caxPosition);
            double leftRoi = caxPosition - (caxPosition - line.Positions[l]) * 0.8;
            double rightRoi = caxPosition + (line.Positions[r] - caxPosition) * 0.8;
            double maxDifference = 0;
            foreach (var (li, ri) in MirroredPairs(line, caxPosition, leftRoi, rightRoi))
            {
                double difference = line.Values[li] - line.Values[ri];
                if (Math.Abs(difference) > Math.Abs(maxDifference)) maxDifference = difference;
            }
            return maxDifference / cax * 100;
        }

        /// <summary>
        /// IEC 60976 point ratio: over mirrored sample pairs about the <b>beam centre</b> within the IEC
        /// region, the pair with the largest difference, reported as higher / lower x 100.
        /// </summary>
        public static double SymmetryByPointRatioIec(ProfileLine line, double caxPosition = 0)
        {
            var (l, r) = FieldEdgeIndices(line, caxPosition);
            double centre = (line.Positions[l] + line.Positions[r]) / 2;
            double leftLength = centre - line.Positions[l], rightLength = line.Positions[r] - centre;
            double leftRoi = centre - IecRegion(leftLength), rightRoi = centre + IecRegion(rightLength);
            double maxDifference = 0, maxLeft = 1, maxRight = 1;
            foreach (var (li, ri) in MirroredPairs(line, centre, leftRoi, rightRoi))
            {
                double difference = line.Values[ri] - line.Values[li];
                if (Math.Abs(difference) > Math.Abs(maxDifference))
                {
                    maxDifference = difference;
                    maxRight = line.Values[ri];
                    maxLeft = line.Values[li];
                }
            }
            return maxDifference > 0 ? maxRight / maxLeft * 100 : maxLeft / maxRight * 100;
        }

        /// <summary>
        /// (right - left) / (right + left) x 200 with each side's area summed as trapezoids between
        /// successive samples, outward from the beam centre through <paramref name="inFieldRatio"/> of
        /// the field; negative means the left side is higher. Sun Nuclear "Area Symmetry".
        /// </summary>
        public static double SymmetryByArea(ProfileLine line, double caxPosition = 0, double inFieldRatio = 0.8)
        {
            var (l, r) = FieldEdgeIndices(line, caxPosition);
            double centre = (line.Positions[l] + line.Positions[r]) / 2;
            double leftRoi = centre - (centre - line.Positions[l]) * inFieldRatio;
            double rightRoi = centre + (line.Positions[r] - centre) * inFieldRatio;
            double leftArea = 0, rightArea = 0;
            foreach (var (li, ri) in MirroredPairs(line, centre, leftRoi, rightRoi))
            {
                if (li - 1 < 0 || ri + 1 >= line.Count) break;
                // FauxPID: left += v[l] + (v[l-1] - v[l]) / 2, right += v[r] + (v[r+1] - v[r]) / 2.
                leftArea += line.Values[li] + (line.Values[li - 1] - line.Values[li]) / 2;
                rightArea += line.Values[ri] + (line.Values[ri + 1] - line.Values[ri]) / 2;
            }
            if (leftArea + rightArea == 0) throw new InvalidOperationException("Cannot calculate area symmetry: both areas are zero");
            return (rightArea - leftArea) / (rightArea + leftArea) * 200;
        }

        /// <summary>All nine metrics at once.</summary>
        public static ProfileMetricsReport All(ProfileLine line, double caxPosition = 0) => new(
            FieldSize(line, caxPosition),
            CaxOffset(line, caxPosition),
            FlatnessByVariance(line, caxPosition),
            FlatnessByRatioIec(line, caxPosition),
            FlatnessByCaxVariance(line, caxPosition),
            FlatnessByCaxRatio(line, caxPosition),
            SymmetryByCaxPointDifference(line, caxPosition),
            SymmetryByPointRatioIec(line, caxPosition),
            SymmetryByArea(line, caxPosition));

        /// <summary>All nine metrics of a measured or sampled <see cref="Profile"/>, with the axis at position 0.</summary>
        public static ProfileMetricsReport All(Profile profile, double caxPositionMm = 0) =>
            All(ProfileLine.From(profile), caxPositionMm);

        // ---- the pieces ----

        /// <summary>The value at the CAX: the interpolated value at its position.</summary>
        public static double CaxValue(ProfileLine line, double caxPosition = 0) => line.ValueAt(caxPosition);

        /// <summary>
        /// The IEC region's half-length for a half-field of the given length, in mm, as FauxPID's code
        /// bands it: under 100 mm, the half-length less 20 mm; under 300 mm, 80 % of it; beyond, less
        /// 60 mm. <b>FauxPID's documentation states these bands on the full field size</b> (X - 2 cm up
        /// to 10 cm, 80 % up to 30 cm, X - 6 cm beyond), which would put the thresholds at 50 and
        /// 150 mm per side; the code and the docs agree for fields up to 20 cm and differ above. The
        /// port follows the code; the discrepancy is recorded for Cody (2026-11-09).
        /// </summary>
        public static double IecRegion(double halfLengthMm) =>
            halfLengthMm < 100 ? halfLengthMm - 20
            : halfLengthMm < 300 ? halfLengthMm * 0.8
            : halfLengthMm - 60;

        /// <summary>First and last samples strictly above a threshold (FauxPID's <c>get_transition_indices</c>).</summary>
        private static (int Left, int Right) Transitions(ProfileLine line, double threshold)
        {
            int left = Array.FindIndex(line.Values, v => v > threshold);
            int right = Array.FindLastIndex(line.Values, v => v > threshold);
            if (left < 0 || right < 0)
                throw new InvalidOperationException($"The profile never rises above {threshold}; no field edges");
            return (left, right);
        }

        private static (int Left, int Right) EdgesWithCaxInside(ProfileLine line, double caxPosition)
        {
            var (l, r) = FieldEdgeIndices(line, caxPosition);
            if (line.Positions[l] > caxPosition || line.Positions[r] < caxPosition)
                throw new InvalidOperationException(
                    $"The CAX at {caxPosition} mm is not between the field edges at {line.Positions[l]} and {line.Positions[r]} mm");
            return (l, r);
        }

        /// <summary>The samples within a fraction of the field about the CAX (boundaries rounded towards the centre).</summary>
        private static double[] RegionAboutCax(ProfileLine line, double caxPosition, double ratio)
        {
            var (l, r) = EdgesWithCaxInside(line, caxPosition);
            double left = caxPosition - (caxPosition - line.Positions[l]) * ratio;
            double right = caxPosition + (line.Positions[r] - caxPosition) * ratio;
            return Within(line, left, right);
        }

        /// <summary>The values of the samples whose position lies in [left, right], a hair of slack for rounding.</summary>
        private static double[] Within(ProfileLine line, double left, double right)
        {
            const double slack = 1e-9;
            var values = new List<double>();
            for (int i = 0; i < line.Count; i++)
                if (line.Positions[i] >= left - slack && line.Positions[i] <= right + slack) values.Add(line.Values[i]);
            if (values.Count == 0) throw new InvalidOperationException($"No samples between {left} and {right} mm");
            return values.ToArray();
        }

        /// <summary>
        /// Sample pairs walking outward from a centre, the k-th sample on each side together, while both
        /// stay inside their region: FauxPID's <c>while left_index >= left_roi and right_index <= right_roi</c>
        /// from <c>floor(centre)</c> and <c>ceil(centre)</c>.
        /// </summary>
        private static IEnumerable<(int Left, int Right)> MirroredPairs(ProfileLine line, double centre, double leftRoi, double rightRoi)
        {
            const double slack = 1e-9;
            var (li, ri) = line.Straddling(centre);
            while (li >= 0 && ri < line.Count && line.Positions[li] >= leftRoi - slack && line.Positions[ri] <= rightRoi + slack)
            {
                yield return (li, ri);
                li--;
                ri++;
            }
        }

        private static double HalfGapOutward(ProfileLine line, int index, int direction)
        {
            int neighbour = index + direction;
            if (neighbour < 0 || neighbour >= line.Count) neighbour = index - direction; // at the end: use the inward gap
            return Math.Abs(line.Positions[neighbour] - line.Positions[index]) / 2;
        }
    }
}
