using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Studies
{
    /// <summary>One setting the two plans disagree on, for a pair's row.</summary>
    public sealed record PlanDifference(string Setting, string Source, string Target)
    {
        public override string ToString() => $"{Setting} {Source} vs {Target}";
    }

    /// <summary>
    /// Whether two paired doses were planned as the same field (F3). <see cref="Checked"/> is false
    /// when either side has no plan or no beam: "not checked" is never reported as "agrees".
    /// </summary>
    public sealed record PlanCheckResult(bool Checked, IReadOnlyList<PlanDifference> Differences, string? NotCheckedReason = null)
    {
        public bool Agrees => Checked && Differences.Count == 0;

        /// <summary>"agrees", "not checked: …", or the differences joined by "; ".</summary>
        public string Summary => !Checked
            ? $"not checked: {NotCheckedReason}"
            : Differences.Count == 0 ? "agrees" : string.Join("; ", Differences.Select(d => d.ToString()));

        public override string ToString() => Summary;
    }

    /// <summary>
    /// Compares the beam settings behind a matched pair, so a planning difference is not read as an
    /// algorithm difference (design/comparison-studies.md, F3). The comparison of doses still runs
    /// whatever this finds; the result goes on the pair's row.
    /// <para>
    /// Tolerances are for values a TPS writes with rounding, not for physics: 0.1 mm for positions,
    /// 0.1 degree for angles, 0.01 MU. Identities (machine, radiation type, energy, fluence mode,
    /// applicator, wedges, block count) must be equal.
    /// </para>
    /// </summary>
    public static class PlanCheck
    {
        public const double PositionToleranceMm = 0.1;
        public const double AngleToleranceDegrees = 0.1;
        public const double MonitorUnitTolerance = 0.01;

        public static PlanCheckResult Compare(MatchedDosePair pair)
        {
            if (pair is null) throw new ArgumentNullException(nameof(pair));
            return Compare(pair.Source, pair.Target);
        }

        public static PlanCheckResult Compare(RTDoseWithPlan source, RTDoseWithPlan target)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));

            string? missing = (source.Beam, target.Beam) switch
            {
                (null, null) => "neither dose's plan and beam were found",
                (null, _) => "the source dose's plan and beam were not found",
                (_, null) => "the target dose's plan and beam were not found",
                _ => null
            };
            if (missing is not null) return new PlanCheckResult(false, Array.Empty<PlanDifference>(), missing);

            var s = source.Beam!;
            var t = target.Beam!;
            var d = new List<PlanDifference>();

            Same(d, "machine", s.TreatmentMachineName, t.TreatmentMachineName);
            Same(d, "radiation", s.RadiationType, t.RadiationType);
            Near(d, "energy", s.NominalBeamEnergy, t.NominalBeamEnergy, 1e-6, "");
            Same(d, "FFF", Show(s.IsFluenceModeFff), Show(t.IsFluenceModeFff));
            Near(d, "MU", source.MonitorUnits, target.MonitorUnits, MonitorUnitTolerance, "");
            Near(d, "gantry", s.GantryAngle, t.GantryAngle, AngleToleranceDegrees, "°");
            Near(d, "collimator", s.BeamLimitingDeviceAngle, t.BeamLimitingDeviceAngle, AngleToleranceDegrees, "°");
            Near(d, "couch", s.PatientSupportAngle, t.PatientSupportAngle, AngleToleranceDegrees, "°");
            Same(d, "applicator", s.ApplicatorID, t.ApplicatorID);
            Same(d, "wedges", string.Join("+", s.WedgeIDs), string.Join("+", t.WedgeIDs));
            Same(d, "blocks", Show(s.NumberOfBlocks), Show(t.NumberOfBlocks));
            Outlines(d, s.BlockOutlines, t.BlockOutlines);
            Devices(d, s.LeafJawPositions, t.LeafJawPositions);
            Vector(d, "isocentre", s.IsocenterPosition, t.IsocenterPosition);
            // SSD only where both plans state it: Eclipse writes it after a calculation, and many plans have none.
            if (s.SourceToSurfaceDistance is not null && t.SourceToSurfaceDistance is not null)
                Near(d, "SSD", s.SourceToSurfaceDistance, t.SourceToSurfaceDistance, PositionToleranceMm, " mm");

            return new PlanCheckResult(true, d);
        }

        private static void Same(List<PlanDifference> d, string setting, string? a, string? b)
        {
            string x = (a ?? "").Trim(), y = (b ?? "").Trim();
            if (!string.Equals(x, y, StringComparison.Ordinal)) d.Add(new PlanDifference(setting, Show(x), Show(y)));
        }

        private static void Near(List<PlanDifference> d, string setting, double? a, double? b, double tolerance, string unit)
        {
            if (a is null && b is null) return;
            if (a is null || b is null || Math.Abs(a.Value - b.Value) > tolerance)
                d.Add(new PlanDifference(setting, Show(a) + unit, Show(b) + unit));
        }

        private static void Vector(List<PlanDifference> d, string setting, IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            if (a.Count == 0 && b.Count == 0) return;
            if (a.Count != b.Count || a.Zip(b).Any(p => Math.Abs(p.First - p.Second) > PositionToleranceMm))
                d.Add(new PlanDifference(setting, Show(a), Show(b)));
        }

        /// <summary>Every jaw and leaf bank by device type. MLC banks report only their largest leaf difference.</summary>
        private static void Devices(List<PlanDifference> d,
            IReadOnlyDictionary<string, IReadOnlyList<double>> a,
            IReadOnlyDictionary<string, IReadOnlyList<double>> b)
        {
            foreach (string type in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.Ordinal))
            {
                bool hasA = a.TryGetValue(type, out var pa), hasB = b.TryGetValue(type, out var pb);
                if (!hasA || !hasB)
                {
                    d.Add(new PlanDifference(type, hasA ? "present" : "absent", hasB ? "present" : "absent"));
                    continue;
                }
                if (pa!.Count != pb!.Count)
                {
                    d.Add(new PlanDifference(type, $"{pa.Count} positions", $"{pb.Count} positions"));
                    continue;
                }
                bool isMlc = type.StartsWith("MLC", StringComparison.OrdinalIgnoreCase);
                if (isMlc)
                {
                    double worst = pa.Zip(pb).Select(p => Math.Abs(p.First - p.Second)).DefaultIfEmpty(0).Max();
                    if (worst > PositionToleranceMm)
                        d.Add(new PlanDifference(type, "leaves", $"differ by up to {Show(worst)} mm"));
                }
                else if (pa.Zip(pb).Any(p => Math.Abs(p.First - p.Second) > PositionToleranceMm))
                {
                    d.Add(new PlanDifference(type, Show(pa) + " mm", Show(pb) + " mm"));
                }
            }
        }

        private static void Outlines(List<PlanDifference> d, IReadOnlyList<IReadOnlyList<double>> a, IReadOnlyList<IReadOnlyList<double>> b)
        {
            if (a.Count != b.Count) return;   // already reported as a block count difference
            for (int i = 0; i < a.Count; i++)
                if (a[i].Count != b[i].Count || a[i].Zip(b[i]).Any(p => Math.Abs(p.First - p.Second) > PositionToleranceMm))
                {
                    d.Add(new PlanDifference($"block {i + 1} outline", $"{a[i].Count / 2} points", $"{b[i].Count / 2} points, differing"));
                }
        }

        private static string Show(string? s) => string.IsNullOrEmpty(s) ? "none" : s;
        private static string Show(bool? b) => b is null ? "not stated" : b.Value ? "yes" : "no";
        private static string Show(int? i) => i is null ? "none" : i.Value.ToString(CultureInfo.InvariantCulture);
        private static string Show(double? v) => v is null ? "none" : v.Value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Show(IReadOnlyList<double> v) => v.Count == 0 ? "none" : string.Join(", ", v.Select(x => x.ToString("0.##", CultureInfo.InvariantCulture)));
    }
}
