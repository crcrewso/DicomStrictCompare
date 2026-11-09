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

        /// <summary>
        /// The same curve with every position divided by <paramref name="magnification"/>: a profile
        /// measured at depth, projected back to the plane where field sizes are named.
        /// <para>
        /// Cody, 2026-11-09: "The beams we're measuring are divergent. The field size is the nominal
        /// field size of the projected square at 100 SSD, so a 10 cm depth would be 110 cm from target
        /// and the field would be 11 cm wide (plus scattering) it would still be called a 10 cm field."
        /// So the metrics are taken in the nominal plane: positions scaled by SSD / (SSD + depth),
        /// values untouched. Scatter widens the penumbra a little beyond the projection; that is in the
        /// measured number, as it is in the TPS's.
        /// </para>
        /// </summary>
        public ProfileLine ProjectedToNominal(double magnification)
        {
            if (!(magnification > 0) || !double.IsFinite(magnification))
                throw new ArgumentOutOfRangeException(nameof(magnification), "must be a positive finite factor");
            if (magnification == 1) return this;
            return new ProfileLine(Positions.Select(p => p / magnification).ToArray(), Values);
        }

        /// <summary>
        /// (SSD + depth) / SSD for a profile scanned at a depth with a known SSD; 1 when either is
        /// unknown or the curve is a depth dose (whose positions are depths, not widths).
        /// </summary>
        public static double MagnificationOf(Profile profile)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            if (profile.Scan.CurveType == CurveType.Pdd) return 1;
            if (profile.Beam.Ssd is not { } ssd || profile.Scan.Depth is not { } depth) return 1;
            if (!(ssd.Millimetres > 0)) return 1;
            return (ssd.Millimetres + depth.Millimetres) / ssd.Millimetres;
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

    /// <summary>
    /// How the IEC 60976 flattened region is taken from the field. Both are offered because each has
    /// its place (Cody, 2026-11-09).
    /// </summary>
    public enum IecBanding
    {
        /// <summary>
        /// On the nominal full field size, as the standard and FauxPID's documentation state it: a
        /// field up to 10 cm less 2 cm, 80 % of one up to 30 cm, a larger one less 6 cm; each side
        /// gives up half the reduction. The default.
        /// </summary>
        FullField,

        /// <summary>
        /// On each half-field, as FauxPID's <c>metrics.py</c> does it: a side under 100 mm less 20 mm,
        /// 80 % of one under 300 mm, a longer one less 60 mm. Matches FauxPID's numbers.
        /// </summary>
        HalfField,
    }

    /// <summary>
    /// The nine FauxPID metrics of one curve, in one record, for a table or a report. Lengths are in
    /// the nominal plane when <see cref="Magnification"/> is not 1; the two IEC metrics were banded as
    /// <see cref="Banding"/> says.
    /// </summary>
    public sealed record ProfileMetricsReport(
        double FieldSizeMm,
        double CaxOffsetMm,
        double FlatnessByVariancePercent,
        double FlatnessByRatioIecPercent,
        double FlatnessByCaxVariance,
        double FlatnessByCaxRatio,
        double SymmetryByCaxPointDifferencePercent,
        double SymmetryByPointRatioIecPercent,
        double SymmetryByAreaPercent,
        double Magnification = 1,
        IecBanding Banding = IecBanding.FullField)
    {
        public override string ToString() =>
            (Magnification == 1 ? "" : $"at nominal plane (/{Magnification:0.000}): ")
            + (Banding == IecBanding.FullField ? "" : "IEC on half-fields: ")
            + $"field {FieldSizeMm:0.0} mm, CAX offset {CaxOffsetMm:+0.00;-0.00} mm; flatness {FlatnessByVariancePercent:0.00}% (variance), "
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
    /// Two things are not FauxPID's. Field sizes are nominal: <see cref="All(Profile, double)"/>
    /// projects a profile scanned at depth back to the plane field sizes are named in
    /// (<see cref="ProfileLine.ProjectedToNominal"/>), because a 10 cm field at 10 cm depth is 11 cm
    /// wide and is still the 10 cm field (Cody, 2026-11-09). And the IEC 60976 region can be banded
    /// either on that nominal full field size, as the standard and FauxPID's documentation state it,
    /// or on each half-field as FauxPID's code does it (<see cref="IecBanding"/>, a parameter of the two
    /// IEC metrics; full-field is the default). The two differ: the half-field rule subtracts twice
    /// the standard's margin and moves the band changes to 20 and 60 cm fields.
    /// </para>
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

        /// <summary>max / min x 100 over the IEC 60976 region about the CAX (see <see cref="IecRegion"/>).</summary>
        public static double FlatnessByRatioIec(ProfileLine line, double caxPosition = 0, IecBanding banding = IecBanding.FullField)
        {
            var (l, r) = EdgesWithCaxInside(line, caxPosition);
            double leftLength = caxPosition - line.Positions[l], rightLength = line.Positions[r] - caxPosition;
            double field = leftLength + rightLength;
            var roi = Within(line, caxPosition - IecRegion(field, leftLength, banding), caxPosition + IecRegion(field, rightLength, banding));
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
        public static double SymmetryByPointRatioIec(ProfileLine line, double caxPosition = 0, IecBanding banding = IecBanding.FullField)
        {
            var (l, r) = FieldEdgeIndices(line, caxPosition);
            double centre = (line.Positions[l] + line.Positions[r]) / 2;
            double leftLength = centre - line.Positions[l], rightLength = line.Positions[r] - centre;
            double field = leftLength + rightLength;
            double leftRoi = centre - IecRegion(field, leftLength, banding), rightRoi = centre + IecRegion(field, rightLength, banding);
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
        public static ProfileMetricsReport All(ProfileLine line, double caxPosition = 0, IecBanding banding = IecBanding.FullField) => new(
            FieldSize(line, caxPosition),
            CaxOffset(line, caxPosition),
            FlatnessByVariance(line, caxPosition),
            FlatnessByRatioIec(line, caxPosition, banding),
            FlatnessByCaxVariance(line, caxPosition),
            FlatnessByCaxRatio(line, caxPosition),
            SymmetryByCaxPointDifference(line, caxPosition),
            SymmetryByPointRatioIec(line, caxPosition, banding),
            SymmetryByArea(line, caxPosition),
            Banding: banding);

        /// <summary>
        /// All nine metrics of a measured or sampled <see cref="Profile"/>, with the axis at position 0,
        /// in the nominal plane: a transverse profile scanned at a depth with a known SSD is first
        /// projected by SSD / (SSD + depth) (<see cref="ProfileLine.MagnificationOf"/>).
        /// </summary>
        public static ProfileMetricsReport All(Profile profile, double caxPositionMm = 0, IecBanding banding = IecBanding.FullField) =>
            All(ProfileLine.From(profile), caxPositionMm, ProfileLine.MagnificationOf(profile), banding);

        /// <summary>All nine metrics of a line with an explicit magnification to divide positions by first.</summary>
        public static ProfileMetricsReport All(ProfileLine line, double caxPosition, double magnification, IecBanding banding = IecBanding.FullField) =>
            All(line.ProjectedToNominal(magnification), caxPosition / magnification, banding) with { Magnification = magnification };

        // ---- the pieces ----

        /// <summary>The value at the CAX: the interpolated value at its position.</summary>
        public static double CaxValue(ProfileLine line, double caxPosition = 0) => line.ValueAt(caxPosition);

        /// <summary>
        /// One side's share of the IEC 60976 flattened region, in mm, for a field of nominal size
        /// <paramref name="fieldSizeMm"/> whose side extends <paramref name="sideLengthMm"/> from the
        /// centre.
        /// <para>
        /// <see cref="IecBanding.FullField"/>: the standard's wording and FauxPID's
        /// <c>docs/ALGORITHMS.md</c>. A field up to 10 cm is reduced by 2 cm, one up to 30 cm is taken
        /// at 80 %, a larger one is reduced by 6 cm; each side gives up half the reduction, so an
        /// asymmetric field keeps its asymmetry.
        /// </para>
        /// <para>
        /// <see cref="IecBanding.HalfField"/>: FauxPID's <c>metrics.py</c>. The side itself is banded:
        /// under 100 mm less 20 mm, under 300 mm at 80 %, otherwise less 60 mm. Matches FauxPID's
        /// numbers; takes 2 cm off each side of a 10 cm field.
        /// </para>
        /// </summary>
        public static double IecRegion(double fieldSizeMm, double sideLengthMm, IecBanding banding = IecBanding.FullField) =>
            banding switch
            {
                IecBanding.FullField =>
                    fieldSizeMm <= 100 ? sideLengthMm - 10
                    : fieldSizeMm <= 300 ? sideLengthMm * 0.8
                    : sideLengthMm - 30,
                IecBanding.HalfField =>
                    sideLengthMm < 100 ? sideLengthMm - 20
                    : sideLengthMm < 300 ? sideLengthMm * 0.8
                    : sideLengthMm - 60,
                _ => throw new ArgumentOutOfRangeException(nameof(banding))
            };

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
