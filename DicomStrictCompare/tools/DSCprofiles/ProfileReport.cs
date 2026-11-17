using System.Globalization;
using System.Text;
using DSClibrary.Profiles;

namespace DSCprofiles;

/// <summary>
/// Writes a batch as CSV: <c>&lt;stem&gt;-results.csv</c>, one row per scan and dose; <c>&lt;stem&gt;-unpaired.csv</c>, the
/// scans no dose paired with and the files that couldn't be read; <c>&lt;stem&gt;-settings.txt</c>, what was run; and with
/// curves, <c>&lt;stem&gt;-curves.csv</c>, every compared curve point by point, for plotting. A row's image, when plots were
/// asked for, is named in its <c>plot</c> column.
/// </summary>
internal static class ProfileReport
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    public static void Write(string folder, string stem, string scans, string doses, ProfileComparisonOptions options,
        string? plans, string? exclude, ProfileBatchResult result, bool curves, IReadOnlyDictionary<int, string> plots)
    {
        WriteResults(Path.Combine(folder, stem + "-results.csv"), scans, result, plots);
        WriteUnpaired(Path.Combine(folder, stem + "-unpaired.csv"), scans, result);
        if (curves) WriteCurves(Path.Combine(folder, stem + "-curves.csv"), scans, result);
        File.WriteAllLines(Path.Combine(folder, stem + "-settings.txt"), new[]
        {
            $"DSCprofiles {typeof(ProfileBatch).Assembly.GetName().Version}, run {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z",
            $"scans: {scans}",
            $"doses: {doses}",
            $"criterion: {options}",
            $"plan label filter: {plans ?? "(none)"}; scan folders excluded: {exclude ?? "(none)"}",
            $"{result.Scans} scans in {result.ScanFiles} files; {result.Doses} doses; {result.Rows.Count} pairs; {result.Unmatched.Count} scans unpaired",
            $"{result.Threads} thread(s), {result.Elapsed.TotalSeconds.ToString("0.0", Ci)} s",
            $"plots: {(plots.Count == 0 ? "none" : $"{plots.Count} in {stem}-plots")}",
        });
    }

    private static readonly string[] ResultColumns =
    {
        "row", "scan_file", "scan_folder", "scan_number", "curve", "depth_mm", "offaxis_crossplane_mm", "offaxis_inplane_mm",
        "beam", "detector", "dose_file", "plan_label", "beam_name", "algorithm", "grid_mm", "surface", "pairing_notes",
        "normalisation", "points", "outside_grid", "analysed", "passed", "pass_rate", "mean_gamma", "max_gamma",
        "depth_shift_mm", "constancy_percent",
        "field_size_measured_mm", "field_size_calculated_mm", "cax_offset_measured_mm", "cax_offset_calculated_mm",
        "flatness_variance_measured", "flatness_variance_calculated", "symmetry_point_measured", "symmetry_point_calculated",
        "dmax_measured_mm", "dmax_calculated_mm", "d100_measured", "d100_calculated", "d200_measured", "d200_calculated",
        "r50_measured_mm", "r50_calculated_mm", "notes", "failure", "plot"
    };

    private static void WriteResults(string path, string scans, ProfileBatchResult result, IReadOnlyDictionary<int, string> plots)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", ResultColumns));
        int row = 0;
        foreach (var r in result.Rows)
        {
            row++;
            var s = r.Scan.Profile.Scan;
            var x = r.Result;
            var g = r.Dose.Dose;
            string grid = g.PixelSpacing.Count >= 2 && g.GridFrameOffsetVector.Count >= 2
                ? $"{N(g.PixelSpacing[1])}/{N(g.PixelSpacing[0])}/{N(Math.Abs(g.GridFrameOffsetVector[1] - g.GridFrameOffsetVector[0]))}"
                : "";
            var fields = new List<string?>
            {
                row.ToString(Ci),
                Path.GetRelativePath(scans, r.Scan.File),
                r.Scan.RelativeFolder,
                r.Scan.ScanNumber.ToString(Ci),
                s.CurveType.ToString(),
                N(s.Depth?.Millimetres),
                N(s.OffAxisCrossplane?.Millimetres),
                N(s.OffAxisInplane?.Millimetres),
                ScanDoseMatching.FromScan(r.Scan.Profile).Setup?.ToString(),
                r.Scan.Profile.Conditions.Detector.Name,
                Path.GetFileName(g.SourcePath),
                r.Dose.Plan?.RTPlanLabel,
                r.Dose.Beam?.BeamName,
                r.Algorithm,
                grid,
                r.Frame?.Describe(),
                string.Join("; ", r.PairingNotes),
                x?.Normalization.ToString(),
                x?.Measured.Points.Count.ToString(Ci),
                x?.PointsOutsideGrid.ToString(Ci),
                x?.Analysed.ToString(Ci),
                x?.Passed.ToString(Ci),
                N(x?.PassRate, "0.00"),
                N(x?.MeanGamma, "0.0000"),
                N(x?.MaxGammaFound, "0.0000"),
                N(x?.DepthShiftMm, "0.00"),
                N(x?.ConstancyPercent, "0.000"),
                N(x?.MeasuredMetrics?.FieldSizeMm, "0.00"), N(x?.CalculatedMetrics?.FieldSizeMm, "0.00"),
                N(x?.MeasuredMetrics?.CaxOffsetMm, "0.00"), N(x?.CalculatedMetrics?.CaxOffsetMm, "0.00"),
                N(x?.MeasuredMetrics?.FlatnessByVariancePercent, "0.000"), N(x?.CalculatedMetrics?.FlatnessByVariancePercent, "0.000"),
                N(x?.MeasuredMetrics?.SymmetryByCaxPointDifferencePercent, "0.000"), N(x?.CalculatedMetrics?.SymmetryByCaxPointDifferencePercent, "0.000"),
                N(x?.MeasuredDepthDose?.DmaxMm, "0.0"), N(x?.CalculatedDepthDose?.DmaxMm, "0.0"),
                N(x?.MeasuredDepthDose?.D100Percent, "0.00"), N(x?.CalculatedDepthDose?.D100Percent, "0.00"),
                N(x?.MeasuredDepthDose?.D200Percent, "0.00"), N(x?.CalculatedDepthDose?.D200Percent, "0.00"),
                N(x?.MeasuredDepthDose?.R50Mm, "0.0"), N(x?.CalculatedDepthDose?.R50Mm, "0.0"),
                x is null ? "" : string.Join("; ", x.Notes),
                r.Failure,
                plots.TryGetValue(row, out string? plot) ? plot : ""
            };
            sb.AppendLine(string.Join(",", fields.Select(Csv)));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteUnpaired(string path, string scans, ProfileBatchResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("scan_file,scan_number,curve,depth_mm,beam,reason");
        foreach (var u in result.Unmatched)
            sb.AppendLine(string.Join(",", new[]
            {
                Path.GetRelativePath(scans, u.Scan.File), u.Scan.ScanNumber.ToString(Ci), u.Scan.Profile.Scan.CurveType.ToString(),
                N(u.Scan.Profile.Scan.Depth?.Millimetres), ScanDoseMatching.FromScan(u.Scan.Profile).Setup?.ToString(), u.Reason
            }.Select(Csv)));
        foreach (var (p, reason) in result.UnreadableScans)
            sb.AppendLine(string.Join(",", new[] { p, "", "", "", "", "unreadable: " + reason }.Select(Csv)));
        foreach (var (p, reason) in result.UnreadableDoses)
            sb.AppendLine(string.Join(",", new[] { p, "", "", "", "", "unreadable dose: " + reason }.Select(Csv)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static void WriteCurves(string path, string scans, ProfileBatchResult result)
    {
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("row,position_mm,measured,calculated,point_value");
        int row = 0;
        foreach (var r in result.Rows)
        {
            row++;
            if (r.Result is not { } x) continue;
            var values = new Dictionary<double, double>();
            for (int i = 0; i < x.PointPositionsMm.Length; i++) values[x.PointPositionsMm[i]] = x.PointValues[i];
            var calc = x.Calculated.Points.ToDictionary(p => p.Position.Millimetres, p => p.Value);
            foreach (var p in x.Measured.Points)
            {
                double pos = p.Position.Millimetres;
                w.WriteLine(string.Join(",", row.ToString(Ci), N(pos, "0.###"), N(p.Value, "0.######"),
                    N(calc.TryGetValue(pos, out double c) ? c : null, "0.######"),
                    N(values.TryGetValue(pos, out double v) && double.IsFinite(v) ? v : null, "0.####")));
            }
        }
    }

    private static string N(double? v, string format = "0.##") => v is { } d && double.IsFinite(d) ? d.ToString(format, Ci) : "";

    private static string Csv(string? s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
