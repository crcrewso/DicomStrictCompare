using System.Globalization;
using DSClibrary.Profiles;
using ScottPlot;
using SkiaSharp;

namespace DSCplots;

/// <summary>Where a plot's two curves came from, for its title and legend. The comparison carries everything else.</summary>
/// <param name="Title">The figure's title: the beam and the curve, for example "6 MV 10 x 10 cm: crossplane at 100 mm".</param>
/// <param name="MeasuredName">The scan, for example "X06 OPEN 10X10.mcc scan 3".</param>
/// <param name="CalculatedName">The dose, for example "RD.QAtpsPTW.6x 10x10.dcm".</param>
public sealed record ProfilePlotLabels(string Title, string MeasuredName, string CalculatedName);

/// <summary>
/// The figure of one <see cref="ProfileComparisonResult"/>, two plots sharing the scan axis.
/// <para>
/// <b>Above</b>, the measured and calculated curves as compared: normalised the same way, in % of the central axis or
/// of the maximum, the calculation read at the measured positions (after any depth shift), with the cutoff below which
/// reference points aren't tested. A box gives the metrics of both sides.
/// </para>
/// <para>
/// <b>Below</b>, what each tested reference point scored. For gamma, gamma itself, with the pass line at 1. For the
/// strict rule, the dose difference (evaluated minus reference, in % of the reference maximum, or of the point's own
/// dose when local) with the dose tolerance shaded, each point marked by whether it passed; a point outside the
/// band can still pass on distance. Untested points (below the cutoff, out of reach) aren't drawn.
/// </para>
/// <para>
/// One drawing for every caller: a front end passes its plot control's <see cref="IMultiplot"/> to
/// <see cref="Fill"/>, a tool calls <see cref="Save(ProfileComparisonResult, ProfilePlotLabels, string, int, int)"/>,
/// so what is on screen and what is exported are the same figure.
/// </para>
/// </summary>
public static class ProfilePlot
{
    public const int DefaultWidth = 1200;
    public const int DefaultHeight = 900;

    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private static readonly ScottPlot.Palettes.Category10 Palette = new();
    private static Color MeasuredColour => Palette.GetColor(0);
    private static Color CalculatedColour => Palette.GetColor(1);
    private static Color PassColour => Palette.GetColor(2);
    private static Color FailColour => Palette.GetColor(3);

    /// <summary>Draws the comparison into a multiplot, making it two plots (curves above, points below) if it isn't.</summary>
    public static void Fill(IMultiplot multiplot, ProfileComparisonResult result, ProfilePlotLabels labels)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(labels);
        var (curves, points) = TwoPlots(multiplot);

        double[] xs = Positions(result.Measured);
        DrawCurves(curves, result, labels);
        DrawPoints(points, result);

        // The same span on both, so a point lines up with the curve above it.
        double pad = Math.Max(1, 0.02 * (xs[^1] - xs[0]));
        curves.Axes.SetLimitsX(xs[0] - pad, xs[^1] + pad);
        points.Axes.SetLimitsX(xs[0] - pad, xs[^1] + pad);
    }

    /// <summary>Clears a multiplot to the same two plots with a message instead of curves.</summary>
    public static void Empty(IMultiplot multiplot, string message)
    {
        var (curves, points) = TwoPlots(multiplot);
        curves.Title(message);
        curves.XLabel("");
        curves.YLabel("");
        curves.Legend.IsVisible = false;
        points.Title("");
        points.XLabel("");
        points.YLabel("");
        points.Legend.IsVisible = false;
    }

    /// <summary>Saves the comparison's figure; the format is the extension's, .png or .svg.</summary>
    public static void Save(ProfileComparisonResult result, ProfilePlotLabels labels, string path,
        int width = DefaultWidth, int height = DefaultHeight)
    {
        var multiplot = new Multiplot();
        Fill(multiplot, result, labels);
        Save(multiplot, path, width, height);
    }

    /// <summary>Saves a multiplot already drawn, such as the one a window shows; .png or .svg by the extension.</summary>
    public static void Save(IMultiplot multiplot, string path, int width = DefaultWidth, int height = DefaultHeight)
    {
        ArgumentNullException.ThrowIfNull(multiplot);
        if (width < 100 || height < 100) throw new ArgumentOutOfRangeException(nameof(width), "a plot needs at least 100 x 100 pixels");
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".png":
                multiplot.SavePng(path, width, height);
                break;
            case ".svg":
                // ScottPlot's multiplot saves only raster images; its renderer draws onto any Skia canvas, an SVG one included.
                using (var stream = File.Create(path))
                using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, width, height), stream))
                    multiplot.Render(canvas, new PixelRect(0, width, height, 0));
                break;
            default:
                throw new ArgumentException($"'{Path.GetExtension(path)}' isn't a plot format: use .png or .svg", nameof(path));
        }
    }

    /// <summary>The verdict in one line: "Gamma 1%/1 mm, measured reference: 97.31% of 412 points pass (mean 0.34, max 1.83)".</summary>
    public static string Verdict(ProfileComparisonResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var o = r.Options;
        string test = o.Test == ProfileTest.Gamma ? "Gamma" : o.DistanceMm > 0 ? "Strict (dose, then distance)" : "Dose difference";
        string criterion = o.Test == ProfileTest.Strict && o.DistanceMm == 0
            ? F($"{o.DosePercent:0.###}%")
            : F($"{o.DosePercent:0.###}%/{o.DistanceMm:0.###} mm");
        string local = o.Local ? " local" : "";
        string pass = r.Analysed == 0 ? "no points tested" : F($"{r.PassRate:0.00}% of {r.Analysed} points pass");
        string gamma = r.MeanGamma is { } mean && r.MaxGammaFound is { } max ? F($" (mean {mean:0.00}, max {max:0.00})") : "";
        return $"{test} {criterion}{local}, {o.Reference.ToString().ToLowerInvariant()} reference: {pass}{gamma}";
    }

    /// <summary>The metrics box: normalisation, constancy and FauxPID metrics for a profile, depth-dose metrics for a PDD.</summary>
    public static IReadOnlyList<string> Summary(ProfileComparisonResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var lines = new List<string>
        {
            r.NormalizationNote ?? (r.Normalization == NormalizationType.CentralAxis ? "normalised at the central axis" : "normalised to the maximum")
        };
        if (r.MeasuredDepthDose is not null || r.CalculatedDepthDose is not null)
        {
            var m = r.MeasuredDepthDose;
            var c = r.CalculatedDepthDose;
            lines.Add("measured / calculated:");
            lines.Add($"  dmax {Pair(m?.DmaxMm, c?.DmaxMm, "0.0")} mm");
            lines.Add($"  D100 {Pair(m?.D100Percent, c?.D100Percent, "0.00")} %");
            lines.Add($"  D200 {Pair(m?.D200Percent, c?.D200Percent, "0.00")} %");
            if (m?.R50Mm is not null || c?.R50Mm is not null) lines.Add($"  R50 {Pair(m?.R50Mm, c?.R50Mm, "0.0")} mm");
        }
        if (r.MeasuredMetrics is not null || r.CalculatedMetrics is not null)
        {
            var m = r.MeasuredMetrics;
            var c = r.CalculatedMetrics;
            lines.Add("measured / calculated:");
            lines.Add($"  field size {Pair(m?.FieldSizeMm, c?.FieldSizeMm, "0.0")} mm");
            lines.Add($"  CAX offset {Pair(m?.CaxOffsetMm, c?.CaxOffsetMm, "0.00")} mm");
            lines.Add($"  flatness (variance) {Pair(m?.FlatnessByVariancePercent, c?.FlatnessByVariancePercent, "0.00")} %");
            lines.Add($"  symmetry (CAX point) {Pair(m?.SymmetryByCaxPointDifferencePercent, c?.SymmetryByCaxPointDifferencePercent, "0.00")} %");
        }
        if (r.ConstancyPercent is { } constancy) lines.Add(F($"constancy {constancy:0.000} %"));
        if (r.DepthShiftMm is { } shift) lines.Add(F($"depth shift: measured {shift:+0.0;-0.0;0.0} mm deeper"));
        if (r.PointsOutsideGrid > 0) lines.Add($"{r.PointsOutsideGrid} scan point(s) outside the dose grid, left out");
        return lines;
    }

    /// <summary>
    /// The usual labels for a scan against a dose: the title is the scan's beam and curve ("6 MV 10 x 10 cm, wedge 15 IN:
    /// crossplane at 100 mm"), the measured side the scan's name, the calculated side the dose file with its plan and beam.
    /// </summary>
    public static ProfilePlotLabels LabelsFor(Profile scan, string scanName, DSClibrary.Results.DICOM.RTDoseWithPlan dose)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(dose);
        string beam = ScanDoseMatching.FromScan(scan).Setup?.ToString() ?? scan.Beam.ToString();
        string plan = string.Join(" / ", new[] { dose.Plan?.RTPlanLabel, dose.Beam?.BeamName }.Where(s => s is { Length: > 0 }));
        string calculated = Path.GetFileName(dose.Dose.SourcePath) + (plan.Length > 0 ? $" ({plan})" : "");
        return new ProfilePlotLabels($"{beam}: {Describe(scan.Scan)}", scanName, calculated);
    }

    /// <summary>
    /// A file name for one scan against one dose: "X06 OPEN 10X10 s3 crossplane 100mm - RD.QAtpsPTW.6x 10x10.png". Both
    /// stems are clipped to 60 characters so the whole stays inside Windows' path limit.
    /// </summary>
    public static string FileName(string scanFile, int scanNumber, ScanKey scan, string dosePath, string extension)
    {
        ArgumentNullException.ThrowIfNull(scan);
        string curve = scan.CurveType switch { CurveType.Pdd => "pdd", CurveType.Crossplane => "crossplane", CurveType.Inplane => "inplane", _ => "scan" };
        string depth = scan.CurveType != CurveType.Pdd && scan.Depth is { } d ? " " + d.Millimetres.ToString("0.#", Ci) + "mm" : "";
        string name = $"{Clip(Path.GetFileNameWithoutExtension(scanFile))} s{scanNumber} {curve}{depth} - {Clip(Path.GetFileNameWithoutExtension(dosePath))}.{extension.TrimStart('.')}";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;

        static string Clip(string s) => s.Length <= 60 ? s : s[..60].TrimEnd();
    }

    /// <summary>A scan in words: "depth dose", "crossplane at 100 mm", "inplane at 50 mm, 30 mm off axis crossplane".</summary>
    public static string Describe(ScanKey scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        string what = scan.CurveType switch
        {
            CurveType.Pdd => "depth dose",
            CurveType.Crossplane => "crossplane",
            CurveType.Inplane => "inplane",
            _ => scan.CurveType.ToString().ToLowerInvariant()
        };
        if (scan.CurveType != CurveType.Pdd && scan.Depth is { } depth) what += F($" at {depth.Millimetres:0.#} mm");
        var off = new List<string>();
        if (scan.OffAxisCrossplane is { } x && Math.Abs(x.Millimetres) > 0.05) off.Add(F($"{x.Millimetres:0.#} mm off axis crossplane"));
        if (scan.OffAxisInplane is { } z && Math.Abs(z.Millimetres) > 0.05) off.Add(F($"{z.Millimetres:0.#} mm off axis inplane"));
        return off.Count > 0 ? $"{what}, {string.Join(", ", off)}" : what;
    }

    /// <summary>The axis a scan runs along, as a label.</summary>
    public static string AxisLabel(ScanKey scan) => scan.CurveType switch
    {
        CurveType.Pdd => "Depth below the water surface (mm)",
        CurveType.Crossplane => "Crossplane position (mm)",
        CurveType.Inplane => "Inplane position (mm)",
        _ => "Position along the scan (mm)"
    };

    private static void DrawCurves(Plot plot, ProfileComparisonResult r, ProfilePlotLabels labels)
    {
        double[] mx = Positions(r.Measured), my = Percent(r.Measured);
        double[] cx = Positions(r.Calculated), cy = Percent(r.Calculated);

        var measured = plot.Add.Scatter(mx, my);
        measured.LegendText = "measured: " + labels.MeasuredName;
        measured.Color = MeasuredColour;
        measured.LineWidth = 1;
        measured.MarkerSize = 3;

        var calculated = plot.Add.ScatterLine(cx, cy);
        calculated.LegendText = "calculated: " + labels.CalculatedName + (r.DepthShiftMm is { } s && s != 0 ? F($" (read {-s:+0.0;-0.0} mm deeper)") : "");
        calculated.Color = CalculatedColour;
        calculated.LineWidth = 2;

        bool pdd = r.Measured.Scan.CurveType == CurveType.Pdd;
        double cutoff = pdd ? r.Options.DepthDoseCutoffPercent : r.Options.ProfileCutoffPercent;
        if (cutoff > 0)
        {
            var reference = r.Options.Reference == ProfileReference.Measured ? my : cy;
            var line = plot.Add.HorizontalLine(reference.Max() * cutoff / 100, 1, Colors.Gray, LinePattern.Dotted);
            line.LegendText = F($"cutoff, {cutoff:0.#}% of the reference maximum");
        }

        var box = plot.Add.Annotation(string.Join("\n", Summary(r)), pdd ? Alignment.UpperRight : Alignment.LowerCenter);
        box.LabelFontSize = 11;
        box.LabelBackgroundColor = Colors.White.WithAlpha(0.85);
        box.LabelBorderColor = Colors.Gray;
        box.LabelShadowColor = Colors.Transparent;

        plot.Title(labels.Title);
        plot.XLabel("");   // the plot below carries the axis label
        plot.YLabel(r.Normalization == NormalizationType.CentralAxis ? "Relative dose (% of central axis)" : "Relative dose (% of maximum)");
        plot.Legend.IsVisible = true;
        // Where the curves aren't: under the build-up of a depth dose, beside the tail of a profile.
        plot.Legend.Alignment = pdd ? Alignment.LowerLeft : Alignment.LowerRight;
        plot.Axes.AutoScaleY();
    }

    private static void DrawPoints(Plot plot, ProfileComparisonResult r)
    {
        bool gamma = r.Options.Test == ProfileTest.Gamma;
        var passX = new List<double>();
        var passY = new List<double>();
        var failX = new List<double>();
        var failY = new List<double>();

        double[] y = gamma ? r.PointValues : Differences(r);
        for (int i = 0; i < r.PointPositionsMm.Length; i++)
        {
            double v = r.PointValues[i];
            if (!double.IsFinite(v) || !double.IsFinite(y[i])) continue;
            bool pass = gamma ? v <= 1 : v == 0;
            (pass ? passX : failX).Add(r.PointPositionsMm[i]);
            (pass ? passY : failY).Add(y[i]);
        }

        if (gamma)
            plot.Add.HorizontalLine(1, 1, Colors.Gray, LinePattern.Dashed).LegendText = "gamma = 1";
        else
        {
            // ScottPlot names a span by the axis it runs along: a vertical span covers a range of y.
            var band = plot.Add.VerticalSpan(-r.Options.DosePercent, r.Options.DosePercent, Colors.Gray.WithAlpha(0.2));
            band.LegendText = F($"+-{r.Options.DosePercent:0.###}% dose tolerance");
        }
        if (passX.Count > 0)
        {
            var s = plot.Add.ScatterPoints(passX.ToArray(), passY.ToArray(), PassColour);
            s.LegendText = F($"pass ({passX.Count})");
            s.MarkerSize = 4;
        }
        if (failX.Count > 0)
        {
            var s = plot.Add.ScatterPoints(failX.ToArray(), failY.ToArray(), FailColour);
            s.LegendText = F($"fail ({failX.Count})");
            s.MarkerSize = 5;
        }

        plot.Title(Verdict(r));
        plot.XLabel(AxisLabel(r.Measured.Scan));
        plot.YLabel(gamma ? "Gamma" : r.Options.Local ? "Eval. - ref. (% of point)" : "Eval. - ref. (% of max)");
        plot.Legend.IsVisible = true;
        plot.Legend.Alignment = Alignment.UpperRight;
        plot.Axes.AutoScaleY();
        if (gamma)
        {
            // Gamma starts at 0, and the pass line should be in view even when every point is far below it.
            double top = Math.Max(1.5, passY.Concat(failY).DefaultIfEmpty(0).Max() * 1.1);
            plot.Axes.SetLimitsY(0, top);
        }
    }

    /// <summary>
    /// Evaluated minus reference at each tested point, in % of the reference maximum (or of the point, when local). The
    /// measured and calculated curves of a result share their positions, and the reference's are the tested ones.
    /// </summary>
    private static double[] Differences(ProfileComparisonResult r)
    {
        bool refIsMeasured = r.Options.Reference == ProfileReference.Measured;
        var reference = (refIsMeasured ? r.Measured : r.Calculated).Points.ToDictionary(p => p.Position.Millimetres, p => p.Value);
        var evaluated = (refIsMeasured ? r.Calculated : r.Measured).Points.ToDictionary(p => p.Position.Millimetres, p => p.Value);
        double max = reference.Values.Max();
        var d = new double[r.PointPositionsMm.Length];
        for (int i = 0; i < d.Length; i++)
        {
            double x = r.PointPositionsMm[i];
            d[i] = reference.TryGetValue(x, out double rv) && evaluated.TryGetValue(x, out double ev)
                ? 100 * (ev - rv) / (r.Options.Local ? rv : max)
                : double.NaN;
        }
        return d;
    }

    private static (Plot Curves, Plot Points) TwoPlots(IMultiplot multiplot)
    {
        ArgumentNullException.ThrowIfNull(multiplot);
        bool added = false;
        while (multiplot.Count() < 2) { multiplot.AddPlot(); added = true; }
        while (multiplot.Count() > 2) multiplot.RemovePlot(multiplot.GetPlot(multiplot.Count() - 1));
        var curves = multiplot.GetPlot(0);
        var points = multiplot.GetPlot(1);

        // Three fifths for the curves, two for the points: the lower plot still has room when a window is small.
        var layout = new ScottPlot.MultiplotLayouts.CustomGrid();
        layout.Set(curves, new GridCell(0, 0, 5, 1, 3, 1));
        layout.Set(points, new GridCell(3, 0, 5, 1, 2, 1));
        multiplot.Layout = layout;
        if (added) multiplot.SharedAxes.ShareX(new[] { curves, points });

        curves.Clear();
        points.Clear();
        Style(curves);
        Style(points);
        return (curves, points);
    }

    /// <summary>Text a size smaller than ScottPlot's defaults, so a figure still reads in a window a third of a screen high.</summary>
    private static void Style(Plot plot)
    {
        plot.Axes.Title.Label.FontSize = 15;
        plot.Axes.Left.Label.FontSize = 13;
        plot.Axes.Bottom.Label.FontSize = 13;
        plot.Axes.Left.TickLabelStyle.FontSize = 11;
        plot.Axes.Bottom.TickLabelStyle.FontSize = 11;
        plot.Legend.FontSize = 11;
    }

    private static double[] Positions(Profile p) => p.Points.Select(x => x.Position.Millimetres).ToArray();

    private static double[] Percent(Profile p) => p.Points.Select(x => 100 * x.Value).ToArray();

    private static string Pair(double? measured, double? calculated, string format) =>
        $"{N(measured, format)} / {N(calculated, format)}";

    private static string N(double? v, string format) => v is { } d && double.IsFinite(d) ? d.ToString(format, Ci) : "-";

    private static string F(FormattableString s) => s.ToString(Ci);
}
