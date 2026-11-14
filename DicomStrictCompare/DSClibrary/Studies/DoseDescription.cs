using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Studies
{
    /// <summary>
    /// What an RT Dose says it is (F4): units, dose type, summation, and the grid spacing on all three
    /// axes. Read from the header only; nothing here touches the voxels.
    /// </summary>
    public sealed record DoseDescription(
        string? Units,
        string? DoseType,
        string? SummationType,
        double? ColumnSpacingMm,
        double? RowSpacingMm,
        double? FrameSpacingMm)
    {
        public static DoseDescription Of(RTDoseResult dose)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));
            // PixelSpacing is row spacing then column spacing (PS3.3 C.7.6.2.1.1).
            var pixel = dose.PixelSpacing;
            var frames = dose.GridFrameOffsetVector;
            double? frame = frames.Count >= 2 ? Math.Abs(frames[1] - frames[0]) : null;
            return new DoseDescription(
                dose.DoseUnits?.Trim(),
                dose.DoseType?.Trim(),
                dose.DoseSummationType?.Trim(),
                pixel.Count >= 2 ? pixel[1] : null,
                pixel.Count >= 1 ? pixel[0] : null,
                frame);
        }

        /// <summary>"2 x 2 x 2 mm", in column, row and frame order.</summary>
        public string Spacing =>
            $"{Show(ColumnSpacingMm)} x {Show(RowSpacingMm)} x {Show(FrameSpacingMm)} mm";

        private static string Show(double? v) => v?.ToString("0.###", CultureInfo.InvariantCulture) ?? "?";
    }

    /// <summary>
    /// The F4 warnings for one pair: things that do not stop a comparison but change what its number
    /// means. A warning, never a refusal; each goes on the pair's row and into the CSV.
    /// </summary>
    public static class DoseWarnings
    {
        public const double SpacingToleranceMm = 1e-3;

        public static IReadOnlyList<string> For(DoseSet sourceSet, DoseSet targetSet, RTDoseWithPlan source, RTDoseWithPlan target)
        {
            if (sourceSet is null) throw new ArgumentNullException(nameof(sourceSet));
            if (targetSet is null) throw new ArgumentNullException(nameof(targetSet));
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));

            var warnings = new List<string>();
            var s = DoseDescription.Of(source.Dose);
            var t = DoseDescription.Of(target.Dose);

            Differ(warnings, "dose units", s.Units, t.Units);
            Differ(warnings, "dose type", s.DoseType, t.DoseType);
            Differ(warnings, "dose summation", s.SummationType, t.SummationType);

            bool spacingDiffers =
                Differs(s.ColumnSpacingMm, t.ColumnSpacingMm) || Differs(s.RowSpacingMm, t.RowSpacingMm) || Differs(s.FrameSpacingMm, t.FrameSpacingMm);
            if (spacingDiffers)
                warnings.Add($"grid spacing {s.Spacing} vs {t.Spacing}: compared on the coarser grid");

            warnings.AddRange(ForSets(sourceSet, targetSet));
            return warnings;
        }

        /// <summary>The set-level warnings, which every pair of a comparison shares.</summary>
        public static IReadOnlyList<string> ForSets(DoseSet sourceSet, DoseSet targetSet)
        {
            var warnings = new List<string>();
            bool acuros = sourceSet.LooksLikeAcuros || targetSet.LooksLikeAcuros;
            if (sourceSet.Reporting != DoseReporting.NotStated && targetSet.Reporting != DoseReporting.NotStated
                && sourceSet.Reporting != targetSet.Reporting)
                warnings.Add($"dose reporting differs: {Say(sourceSet.Reporting)} vs {Say(targetSet.Reporting)}");
            else if (acuros && (sourceSet.Reporting == DoseReporting.NotStated || targetSet.Reporting == DoseReporting.NotStated))
                warnings.Add("an Acuros set is involved and dose to water or medium is not stated for "
                             + string.Join(" and ", new[] { sourceSet, targetSet }.Where(x => x.Reporting == DoseReporting.NotStated).Select(x => x.Name)));
            return warnings;
        }

        private static void Differ(List<string> warnings, string what, string? a, string? b)
        {
            if (!string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase))
                warnings.Add($"{what} {a ?? "not stated"} vs {b ?? "not stated"}");
        }

        private static bool Differs(double? a, double? b) =>
            a is null != b is null || (a is not null && b is not null && Math.Abs(a.Value - b.Value) > SpacingToleranceMm);

        private static string Say(DoseReporting r) => r switch
        {
            DoseReporting.DoseToWater => "dose to water",
            DoseReporting.DoseToMedium => "dose to medium",
            _ => "not stated"
        };
    }
}
