using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// What <see cref="ScanHeaderCheck"/> found for one scan: the numbers it compared and, when the scan's own data
    /// disagrees with its header, why. Null numbers were not measurable (a depth dose, an edge outside the scan).
    /// </summary>
    /// <param name="HeaderFieldMm">The header's field size along the scan's direction.</param>
    /// <param name="MeasuredFieldMm">The scan's field size: the distance between the steepest points of its two penumbrae, projected to the SSD.</param>
    /// <param name="SlopePercent">How much higher the dose is on the scan's positive side than its negative side, over the
    /// central 80 % of the measured field: (right - left) / (right + left) x 200 of the two halves' mean doses.</param>
    /// <param name="Disagreements">One sentence per disagreement; empty when the header and the data agree.</param>
    public sealed record ScanHeaderCheckResult(
        double? HeaderFieldMm,
        double? MeasuredFieldMm,
        double? SlopePercent,
        IReadOnlyList<string> Disagreements)
    {
        public static ScanHeaderCheckResult NotChecked { get; } = new(null, null, null, Array.Empty<string>());

        public bool Agrees => Disagreements.Count == 0;

        /// <summary>The disagreements in one line, or null when there are none.</summary>
        public string? Summary => Agrees ? null : string.Join("; ", Disagreements);
    }

    /// <summary>
    /// Checks a water-tank profile against its own header, so a scan whose header was left at the wrong field or
    /// wedge is flagged rather than trusted (crcrewso, 2026-11-17: the Equinox "low res wedges" scans all say
    /// open 10 x 10 but are wedged, most of them 20 x 20, and so paired with open 10 x 10 doses).
    /// <para>
    /// Pairing reads the header (<see cref="ScanDoseMatching"/>); this reads the curve. Two checks, on crossplane and
    /// inplane profiles only:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Field size.</b> The distance between the steepest points of the two penumbrae (<see cref="SteepestEdges"/>),
    /// projected to the SSD as field sizes are named (<see cref="ProfileLine.MagnificationOf"/>), must be within
    /// <see cref="FieldTolerance"/> of the header's field along the scan. Skipped when the axis value is under 20 %
    /// of the scan's maximum (a scan outside the field) or an edge lies outside the scan.</item>
    /// <item><b>Wedge.</b> A photon header with no wedge must give a level field: the two halves' mean doses over the
    /// central 80 % of the measured field, about its measured centre, may differ by at most
    /// <see cref="SlopeTolerancePercent"/>. Not checked for a header with a wedge (the gradient's direction depends on
    /// the wedge), electrons (small electron fields slope up to 3.4 % on the Equinox scans), fields under
    /// <see cref="SlopeMinimumFieldMm"/> (no level region), or a field centred more than a tenth of its width off the
    /// axis (a half-blocked field isn't level).</item>
    /// </list>
    /// <para>
    /// This flags; it never changes a pair or a count. A flagged scan is still compared with the doses its header
    /// pairs it with, and the flag travels with its rows so a reader can set them aside.
    /// </para>
    /// </summary>
    public static class ScanHeaderCheck
    {
        /// <summary>The measured field may differ from the header's by this fraction of the header's.</summary>
        public const double FieldTolerance = 0.15;

        /// <summary>The largest side-to-side slope, in %, of a scan whose header says it has no wedge.</summary>
        public const double SlopeTolerancePercent = 3;

        /// <summary>Fields narrower than this have no level region, so their slope isn't checked.</summary>
        public const double SlopeMinimumFieldMm = 30;

        /// <summary>An edge is found only when the scan runs on below this fraction of the axis value beyond it.</summary>
        public const double EdgeTailFraction = 0.15;

        public static ScanHeaderCheckResult Check(Profile scan)
        {
            if (scan is null) throw new ArgumentNullException(nameof(scan));
            var curve = scan.Scan.CurveType;
            if (curve is not (CurveType.Crossplane or CurveType.Inplane) || scan.Points.Count < 5) return ScanHeaderCheckResult.NotChecked;

            var line = ProfileLine.From(scan);
            if (line.Positions[0] > 0 || line.Positions[^1] < 0) return ScanHeaderCheckResult.NotChecked;
            double cax = line.ValueAt(0);
            if (!(cax > 0.2 * line.Values.Max())) return ScanHeaderCheckResult.NotChecked;

            var ci = CultureInfo.InvariantCulture;
            string axis = curve == CurveType.Inplane ? "inplane" : "crossplane";
            double? header = (curve == CurveType.Inplane ? scan.Beam.FieldInplane : scan.Beam.FieldCrossplane)?.Millimetres;
            var disagreements = new List<string>();

            var (left, right) = SteepestEdges(line);
            double? measured = null, slope = null;
            if (left is { } l && right is { } r && r > l)
            {
                measured = (r - l) / ProfileLine.MagnificationOf(scan);
                if (header is > 0 && Math.Abs(measured.Value / header.Value - 1) > FieldTolerance)
                    disagreements.Add($"the header says a {header.Value.ToString("0.#", ci)} mm field {axis}, but the scan's field is "
                        + $"{measured.Value.ToString("0", ci)} mm at the SSD");

                slope = Slope(line, (l + r) / 2, (r - l) / 2 * 0.8);
                bool level = scan.Beam.Modality == Modality.Photon && measured >= SlopeMinimumFieldMm
                    && Math.Abs((l + r) / 2) <= 0.1 * (r - l);
                if (slope is { } s && level && Math.Abs(s) > SlopeTolerancePercent && !HasWedge(scan))
                    disagreements.Add($"the header says no wedge, but the dose is {Math.Abs(s).ToString("0.#", ci)} % higher on the "
                        + $"{(s > 0 ? "positive" : "negative")} {axis} side");
            }
            return new ScanHeaderCheckResult(header, measured, slope, disagreements);
        }

        /// <summary>The header's wedge, as pairing reads it: WEDGE_ANGLE, or the WEDGE text ("10 in").</summary>
        private static bool HasWedge(Profile scan)
        {
            scan.Conditions.RawHeaders.TryGetValue("WEDGE", out string? text);
            var (angle, _) = ScanDoseMatching.ScanWedge(scan.Beam.WedgeAngleDegrees, text);
            return angle is > 0;
        }

        /// <summary>
        /// Each field edge as the middle of the steepest falling step on its side of the axis. Unlike 50 % of the axis
        /// value, this finds the edge of an FFF field (whose dose falls from the centre, so a 40 x 40 FFF field is about
        /// 29 cm wide at 50 % of its axis) and of a wedged one, as well as of a flattened field, where the two agree.
        /// An edge is null when the scan's end on that side is still at or above <see cref="EdgeTailFraction"/> of the
        /// axis value: the scan stops in the field (a 40 x 40 FFF scan at 20 cm depth ends at 28 %), so its steepest
        /// step need not be the penumbra.
        /// </summary>
        internal static (double? Left, double? Right) SteepestEdges(ProfileLine line)
        {
            double cax = line.ValueAt(0);
            int n = line.Positions.Length;
            double? right = null, left = null;
            if (line.Values[^1] < EdgeTailFraction * cax)
            {
                double best = 0;
                for (int i = 0; i + 1 < n; i++)
                {
                    if (line.Positions[i + 1] <= 0) continue;
                    double fall = (line.Values[i] - line.Values[i + 1]) / (line.Positions[i + 1] - line.Positions[i]);
                    if (fall > best) { best = fall; right = (line.Positions[i] + line.Positions[i + 1]) / 2; }
                }
            }
            if (line.Values[0] < EdgeTailFraction * cax)
            {
                double best = 0;
                for (int i = n - 1; i > 0; i--)
                {
                    if (line.Positions[i - 1] >= 0) continue;
                    double fall = (line.Values[i] - line.Values[i - 1]) / (line.Positions[i] - line.Positions[i - 1]);
                    if (fall > best) { best = fall; left = (line.Positions[i] + line.Positions[i - 1]) / 2; }
                }
            }
            return (left, right);
        }

        /// <summary>(right - left) / (right + left) x 200 of the mean doses at mirrored points either side of a centre.</summary>
        internal static double? Slope(ProfileLine line, double centre, double reach)
        {
            if (!(reach > 0)) return null;
            const int steps = 20;
            double sumLeft = 0, sumRight = 0;
            for (int k = 1; k <= steps; k++)
            {
                double d = reach * k / steps;
                if (centre - d < line.Positions[0] || centre + d > line.Positions[^1]) return null;
                sumLeft += line.ValueAt(centre - d);
                sumRight += line.ValueAt(centre + d);
            }
            return sumLeft + sumRight > 0 ? (sumRight - sumLeft) / (sumRight + sumLeft) * 200 : null;
        }
    }
}
