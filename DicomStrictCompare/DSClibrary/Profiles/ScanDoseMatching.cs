using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A beam as a water tank sees it: the field's size and centre along the tank's crossplane (DICOM x) and inplane
    /// (DICOM z) axes at the isocentre, in mm, whatever collimator angle made it; the energy, filter and wedge. The
    /// common ground on which a scan and a plan beam are paired.
    /// </summary>
    public sealed record BeamSetup(
        Modality Modality,
        double Energy,
        bool IsFff,
        double CrossplaneMm,
        double InplaneMm,
        double CentreCrossplaneMm,
        double CentreInplaneMm,
        double? CollimatorDegrees,
        double? WedgeAngleDegrees,
        string? WedgeDirection,
        double? SsdMm)
    {
        public override string ToString()
        {
            var ci = CultureInfo.InvariantCulture;
            string energy = Energy.ToString("0.##", ci) + (Modality == Modality.Electron ? " MeV" : " MV") + (IsFff ? " FFF" : "");
            string field = $"{(CrossplaneMm / 10).ToString("0.##", ci)} x {(InplaneMm / 10).ToString("0.##", ci)} cm";
            if (Math.Abs(CentreCrossplaneMm) > 0.05 || Math.Abs(CentreInplaneMm) > 0.05)
                field += $" centred ({CentreCrossplaneMm.ToString("0.#", ci)}, {CentreInplaneMm.ToString("0.#", ci)}) mm";
            string wedge = WedgeAngleDegrees is { } w && w > 0 ? $", wedge {w.ToString("0.#", ci)} {WedgeDirection}".TrimEnd() : "";
            return $"{energy} {field}{wedge}";
        }
    }

    /// <summary>A dose that pairs with a scan, and anything about the pairing a reader should know.</summary>
    public sealed record ScanDoseCandidate(RTDoseWithPlan Dose, BeamSetup Plan, IReadOnlyList<string> Notes);

    /// <summary>
    /// Pairs a measured scan with the calculated doses of the same beam, by what both describe rather than by names.
    /// <para>
    /// <b>The rule</b> (prototype, 2026-11-17): same modality; energy within 0.01; the same filter (a scan with no FILTER,
    /// as PTW's EDW scans have, counts as flattened); the field's size and centre along the tank's two axes within
    /// 0.5 mm, the plan's jaws turned by its collimator angle, or for electrons its applicator (A10 is 10 x 10 cm,
    /// A10X6 is 10 cm along X and 6 cm along Y); the same wedge angle and IN/OUT direction (an Eclipse EDW is
    /// orientation 0 for IN and 180 for OUT, a PTW scan writes "WEDGE=10 in"); and the SSD within 0.5 mm when the plan
    /// states one.
    /// </para>
    /// <para>
    /// <b>The collimator angle itself isn't compared.</b> Measured on the Equinox abutting-jaw scans: the scan says
    /// collimator 0 with the field offset 50 mm inplane, while the plan beam that makes that field uses collimator 90 and
    /// its X jaws. The field in the tank is the same, so they pair, with a note that the collimator differs, because
    /// X and Y jaws' penumbrae need not be identical.
    /// </para>
    /// <para>
    /// An MLC-shaped or arc beam is never paired: the jaws don't describe its field. Every dose that passes is returned,
    /// so a field calculated twice (two grid spacings, two algorithms) gives two pairs, each labelled.
    /// </para>
    /// </summary>
    public static class ScanDoseMatching
    {
        public const double FieldToleranceMm = 0.5;
        public const double EnergyTolerance = 0.01;

        /// <summary>The scan's beam, from its key and its file's headers. Null, with a reason, when it can't be described.</summary>
        public static (BeamSetup? Setup, string? Why) FromScan(Profile scan)
        {
            if (scan is null) throw new ArgumentNullException(nameof(scan));
            var b = scan.Beam;
            if (b.Modality == Modality.Unknown) return (null, "the scan's modality isn't known");
            if (b.Energy is not { } energy) return (null, "the scan has no energy");
            if (b.FieldCrossplane is not { } cross || b.FieldInplane is not { } inplane) return (null, "the scan has no field size");
            var headers = scan.Conditions.RawHeaders;
            double offCross = Number(headers, "COLL_OFFSET_CROSSPLANE") ?? 0;
            double offIn = Number(headers, "COLL_OFFSET_INPLANE") ?? 0;
            var (angle, direction) = ScanWedge(b.WedgeAngleDegrees, Text(headers, "WEDGE"));
            return (new BeamSetup(b.Modality, energy, b.IsFff == true, cross.Millimetres, inplane.Millimetres, offCross, offIn,
                b.CollimatorAngleDegrees, angle, direction, b.Ssd?.Millimetres), null);
        }

        /// <summary>The plan beam's field in tank axes. Null, with a reason, for a beam this can't describe.</summary>
        public static (BeamSetup? Setup, string? Why) FromPlan(RTPlanBeam beam)
        {
            if (beam is null) throw new ArgumentNullException(nameof(beam));
            var modality = (beam.RadiationType ?? "").Trim().ToUpperInvariant() switch
            {
                "PHOTON" => Modality.Photon,
                "ELECTRON" => Modality.Electron,
                _ => Modality.Unknown
            };
            if (modality == Modality.Unknown) return (null, $"radiation type {beam.RadiationType}");
            if (beam.NominalBeamEnergy is not { } energy) return (null, "no nominal energy");
            if (beam.IsArc) return (null, "arc");
            double collimator = beam.BeamLimitingDeviceAngle ?? 0;
            if (Math.Abs(collimator / 90 - Math.Round(collimator / 90)) * 90 > 0.1) return (null, "collimator angle not a multiple of 90");

            double x1, x2, y1, y2;
            if (modality == Modality.Electron)
            {
                if (ApplicatorSize(beam.ApplicatorID) is not var (ax, ay)) return (null, $"electron applicator '{beam.ApplicatorID}' not recognised");
                (x1, x2, y1, y2) = (-ax / 2, ax / 2, -ay / 2, ay / 2);
            }
            else
            {
                var jaws = beam.LeafJawPositions;
                var xj = Pair(jaws, "ASYMX") ?? Pair(jaws, "X");
                var yj = Pair(jaws, "ASYMY") ?? Pair(jaws, "Y");
                if (xj is null || yj is null) return (null, "no X or Y jaw positions");
                if (jaws.Keys.Any(k => k.StartsWith("MLC", StringComparison.OrdinalIgnoreCase)))
                    return (null, "MLC in the plan: the jaws may not be the field");
                (x1, x2, y1, y2) = (xj[0], xj[1], yj[0], yj[1]);
            }

            var (crossplane, inplane, centreCross, centreIn) = TankField(x1, x2, y1, y2, collimator);

            var wedge = beam.Wedges.FirstOrDefault();
            string? direction = wedge?.OrientationDegrees switch
            {
                null => null,
                var o when Math.Abs(o.Value) < 0.1 || Math.Abs(o.Value - 360) < 0.1 => "IN",
                var o when Math.Abs(o.Value - 180) < 0.1 => "OUT",
                var o => o.Value.ToString("0.#", CultureInfo.InvariantCulture)
            };
            return (new BeamSetup(modality, energy, beam.IsFluenceModeFff == true, crossplane, inplane,
                centreCross, centreIn, collimator, wedge?.AngleDegrees, direction, beam.SourceToSurfaceDistance), null);
        }

        /// <summary>
        /// A jaw rectangle (X1, X2 along the collimator's X, Y1, Y2 along its Y, at the isocentre) as a field on the tank's
        /// axes. IEC 61217 at gantry 0, head-first supine: at collimator 0 the X jaws lie along +x (crossplane) and the Y jaws
        /// along +z (inplane), as <see cref="Studies.JawEdgeCheck"/> takes them; a collimator angle, a multiple of 90, turns
        /// both. The extent on each axis is that of the rectangle's corners.
        /// </summary>
        internal static (double CrossplaneMm, double InplaneMm, double CentreCrossplaneMm, double CentreInplaneMm) TankField(
            double x1, double x2, double y1, double y2, double collimatorDegrees)
        {
            double t = collimatorDegrees * Math.PI / 180;
            double c = Math.Round(Math.Cos(t)), s = Math.Round(Math.Sin(t));
            var corners = new[] { (x1, y1), (x1, y2), (x2, y1), (x2, y2) }
                .Select(p => (X: p.Item1 * c - p.Item2 * s, Z: p.Item1 * s + p.Item2 * c)).ToArray();
            double xmin = corners.Min(p => p.X), xmax = corners.Max(p => p.X);
            double zmin = corners.Min(p => p.Z), zmax = corners.Max(p => p.Z);
            return (xmax - xmin, zmax - zmin, (xmin + xmax) / 2, (zmin + zmax) / 2);
        }

        /// <summary>Whether a scan's beam and a plan beam's are the same field, and if not, the first thing that differs.</summary>
        public static (bool Same, string? Difference, IReadOnlyList<string> Notes) Compare(BeamSetup scan, BeamSetup plan)
        {
            var ci = CultureInfo.InvariantCulture;
            if (scan.Modality != plan.Modality) return (false, "modality", Array.Empty<string>());
            if (Math.Abs(scan.Energy - plan.Energy) > EnergyTolerance) return (false, "energy", Array.Empty<string>());
            if (scan.IsFff != plan.IsFff) return (false, "filter (FFF)", Array.Empty<string>());
            if (Math.Abs(scan.CrossplaneMm - plan.CrossplaneMm) > FieldToleranceMm
                || Math.Abs(scan.InplaneMm - plan.InplaneMm) > FieldToleranceMm) return (false, "field size", Array.Empty<string>());
            if (Math.Abs(scan.CentreCrossplaneMm - plan.CentreCrossplaneMm) > FieldToleranceMm
                || Math.Abs(scan.CentreInplaneMm - plan.CentreInplaneMm) > FieldToleranceMm) return (false, "field centre", Array.Empty<string>());
            double sw = scan.WedgeAngleDegrees ?? 0, pw = plan.WedgeAngleDegrees ?? 0;
            if (Math.Abs(sw - pw) > 0.1) return (false, "wedge angle", Array.Empty<string>());
            if (sw > 0 && !string.Equals(scan.WedgeDirection, plan.WedgeDirection, StringComparison.OrdinalIgnoreCase))
                return (false, "wedge direction", Array.Empty<string>());
            if (scan.SsdMm is { } a && plan.SsdMm is { } b && Math.Abs(a - b) > FieldToleranceMm) return (false, "SSD", Array.Empty<string>());

            var notes = new List<string>();
            if (scan.CollimatorDegrees is { } sc && plan.CollimatorDegrees is { } pc && AngleApart(sc, pc) > 0.1)
                notes.Add($"collimator differs: scan {sc.ToString("0.#", ci)}, plan {pc.ToString("0.#", ci)} (the same field in the tank, made by other jaws)");
            if (scan.SsdMm is not null && plan.SsdMm is null) notes.Add("the plan states no SSD; the scan's is used");
            return (true, null, notes);
        }

        /// <summary>
        /// Every dose whose plan beam is the scan's beam. When none is, <c>Reason</c> names the furthest any dose got: the
        /// first criterion that the closest dose failed.
        /// </summary>
        public static (IReadOnlyList<ScanDoseCandidate> Candidates, string? Reason) Match(Profile scan, IEnumerable<RTDoseWithPlan> doses)
        {
            var (setup, why) = FromScan(scan);
            if (setup is null) return (Array.Empty<ScanDoseCandidate>(), why);
            var found = new List<ScanDoseCandidate>();
            string[] order = { "modality", "energy", "filter (FFF)", "field size", "field centre", "wedge angle", "wedge direction", "SSD" };
            int furthest = -1;
            string? furthestWhat = null;
            foreach (var dose in doses)
            {
                if (dose.Beam is null) continue;
                var (plan, _) = FromPlan(dose.Beam);
                if (plan is null) continue;
                var (same, difference, notes) = Compare(setup, plan);
                if (same) { found.Add(new ScanDoseCandidate(dose, plan, notes)); continue; }
                int stage = Array.IndexOf(order, difference);
                if (stage > furthest) { furthest = stage; furthestWhat = difference; }
            }
            if (found.Count > 0) return (found, null);
            return (found, furthestWhat is null
                ? $"no dose with a plan beam to pair with {setup}"
                : $"no dose for {setup}; the closest differ in {furthestWhat}");
        }

        /// <summary>A PTW scan's wedge: WEDGE_ANGLE when it's set, else the WEDGE text ("10 in", "60 out").</summary>
        internal static (double? Angle, string? Direction) ScanWedge(double? wedgeAngle, string? wedgeText)
        {
            var m = Regex.Match(wedgeText ?? "", @"^\s*(\d+(?:\.\d+)?)\s*(in|out)?\s*$", RegexOptions.IgnoreCase);
            if (m.Success)
                return (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                        m.Groups[2].Success ? m.Groups[2].Value.ToUpperInvariant() : null);
            return (wedgeAngle is > 0 ? wedgeAngle : null, null);
        }

        /// <summary>An Eclipse electron applicator's size in mm: A10 is 100 x 100, A10X6 is 100 along X by 60 along Y.</summary>
        internal static (double X, double Y)? ApplicatorSize(string? id)
        {
            var m = Regex.Match(id ?? "", @"^\s*A(\d+(?:\.\d+)?)(?:X(\d+(?:\.\d+)?))?\s*$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            double x = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 10;
            double y = m.Groups[2].Success ? double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 10 : x;
            return (x, y);
        }

        private static IReadOnlyList<double>? Pair(IReadOnlyDictionary<string, IReadOnlyList<double>> all, string type) =>
            all.TryGetValue(type, out var p) && p.Count == 2 ? p : null;

        private static double AngleApart(double a, double b)
        {
            double d = Math.Abs(a - b) % 360;
            return Math.Min(d, 360 - d);
        }

        private static string? Text(IReadOnlyDictionary<string, string> headers, string key) =>
            headers.TryGetValue(key, out var v) ? v : null;

        private static double? Number(IReadOnlyDictionary<string, string> headers, string key) =>
            headers.TryGetValue(key, out var v) && double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
    }
}
