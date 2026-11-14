using System.Globalization;
using DSClibrary;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;
using DSClibrary.Studies;

namespace DSCprototypes.Shared.Graphs;

/// <summary>One set's values along a curve, NaN where its grid has none.</summary>
public sealed record CurveSeries(string Name, double[] Values);

/// <summary>
/// One curve drawn from every set that has the field (F7). <see cref="Differences"/> holds each other
/// set minus the reference, in percent of the reference curve's maximum.
/// </summary>
public sealed record MultiCurve(
    string Title,
    string AxisLabel,
    string ValueLabel,
    double[] Positions,
    IReadOnlyList<CurveSeries> Series,
    IReadOnlyList<CurveSeries> Differences,
    string Tag = "")
{
    /// <summary>Positions where every series has a value, for a plotting library that cannot take NaN.</summary>
    public (double[] Positions, double[][] Series) Overlap()
    {
        var keep = Enumerable.Range(0, Positions.Length).Where(i => Series.All(s => !double.IsNaN(s.Values[i]))).ToArray();
        return (keep.Select(i => Positions[i]).ToArray(), Series.Select(s => keep.Select(i => s.Values[i]).ToArray()).ToArray());
    }
}

/// <summary>A dose with the set it belongs to: the unit F7 draws.</summary>
public sealed record SetDose(string SetName, RTDoseWithPlan Dose);

/// <summary>A field's curves at the study's depths, with the depths that could not be drawn and why.</summary>
public sealed record MultiCurveSet(IReadOnlyList<MultiCurve> Curves, IReadOnlyList<string> Skipped, double DmaxMm);

/// <summary>
/// Depth doses and profiles of one field from any number of sets, sampled together (F7). The two-set
/// rule of <see cref="BeamLines"/> widened to N: each set is read through its own plan's isocentre,
/// positions are relative to it, and every set is sampled at the coarsest grid's nodes where all the
/// grids have values. The geometry reading is <see cref="BeamLines.ConventionNote"/>'s.
/// </summary>
public static class MultiCurves
{
    public static IReadOnlyList<LoadedDose> Load(IReadOnlyList<SetDose> doses, Func<string, DoseMatrixOptimal> loader)
    {
        ArgumentNullException.ThrowIfNull(doses);
        if (doses.Count == 0) throw new ArgumentException("At least one dose is needed.", nameof(doses));
        return doses.Select(d => BeamLines.LoadDose(d.Dose, loader)).ToArray();
    }

    /// <summary>
    /// Every curve the settings ask for: the depth dose, or one profile per depth with dmax first
    /// (found on the reference set's axis). A depth outside the common grid is skipped and named.
    /// </summary>
    public static MultiCurveSet For(IReadOnlyList<SetDose> sets, IReadOnlyList<LoadedDose> loaded, CurveKind kind,
        DepthSettings depths, int reference = 0)
    {
        Check(sets, loaded, reference);
        ArgumentNullException.ThrowIfNull(depths);
        double dmax = DepthOfMaximum(loaded[reference]);
        if (kind == CurveKind.DepthDose)
            return new MultiCurveSet(new[] { DepthDose(sets, loaded, reference) }, Array.Empty<string>(), dmax);

        var rel = RelativeRange(loaded, GridAxis.Y, l => l.IsoY);
        var wanted = new List<(double Mm, string Tag)>();
        if (depths.IncludeDmax) wanted.Add((dmax, $"dmax {Mm(dmax)} mm"));
        wanted.AddRange(depths.DepthsMm.Select(d => (d, $"{Mm(d)} mm")));

        var curves = new List<MultiCurve>();
        var skipped = new List<string>();
        foreach (var (mm, tag) in wanted)
        {
            if (mm < rel.Lo || mm > rel.Hi)
            {
                skipped.Add($"{tag}: outside the depths every grid covers ({Mm(Math.Max(rel.Lo, 0))} to {Mm(rel.Hi)} mm)");
                continue;
            }
            curves.Add(Profile(sets, loaded, kind, mm, reference) with { Tag = tag });
        }
        return new MultiCurveSet(curves, skipped, dmax);
    }

    public static MultiCurve DepthDose(IReadOnlyList<SetDose> sets, IReadOnlyList<LoadedDose> loaded, int reference = 0)
    {
        Check(sets, loaded, reference);
        var depths = Lattice(loaded, GridAxis.Y, l => l.IsoY);
        var series = sets.Select((s, i) => new CurveSeries(s.SetName,
            GridLines.Along(loaded[i].Grid, GridAxis.Y, loaded[i].IsoX, loaded[i].IsoZ, depths.Select(d => loaded[i].IsoY + d).ToArray()))).ToArray();
        return Make($"{Name(sets[reference])}: depth dose along the beam axis", "Depth below the isocentre plane (mm)", depths, series, reference);
    }

    public static MultiCurve Profile(IReadOnlyList<SetDose> sets, IReadOnlyList<LoadedDose> loaded, CurveKind kind, double depthMm, int reference = 0)
    {
        Check(sets, loaded, reference);
        if (kind == CurveKind.DepthDose) return DepthDose(sets, loaded, reference);
        bool cross = kind == CurveKind.Crossplane;
        var axis = cross ? GridAxis.X : GridAxis.Z;
        Func<LoadedDose, double> iso = cross ? l => l.IsoX : l => l.IsoZ;
        var positions = Lattice(loaded, axis, iso);
        var series = sets.Select((s, i) =>
        {
            var l = loaded[i];
            double y = l.IsoY + depthMm;
            var absolute = positions.Select(p => iso(l) + p).ToArray();
            var values = cross
                ? GridLines.Along(l.Grid, GridAxis.X, y, l.IsoZ, absolute)
                : GridLines.Along(l.Grid, GridAxis.Z, l.IsoX, y, absolute);
            return new CurveSeries(s.SetName, values);
        }).ToArray();
        string what = cross ? "crossplane" : "inplane";
        return Make($"{Name(sets[reference])}: {what} profile at {Mm(depthMm)} mm depth",
            cross ? "Crossplane x from the axis (mm)" : "Inplane z from the axis (mm)", positions, series, reference);
    }

    /// <summary>The depth of maximum dose on a set's own axis, at its own nodes.</summary>
    public static double DepthOfMaximum(LoadedDose l)
    {
        var ys = GridLines.Nodes(l.Grid, GridAxis.Y);
        var values = GridLines.Along(l.Grid, GridAxis.Y, l.IsoX, l.IsoZ, ys);
        int best = 0;
        for (int i = 1; i < values.Length; i++) if (values[i] > values[best]) best = i;
        return ys[best] - l.IsoY;
    }

    /// <summary>
    /// Positions relative to each set's isocentre: the coarsest grid's nodes along the axis, within
    /// the range every grid covers. With one set, that set's own nodes.
    /// </summary>
    public static double[] Lattice(IReadOnlyList<LoadedDose> loaded, GridAxis axis, Func<LoadedDose, double> iso)
    {
        var (lo, hi) = RelativeRange(loaded, axis, iso);
        var coarsest = loaded.OrderByDescending(l => GridLines.Resolution(l.Grid, axis)).First();
        return GridLines.Nodes(coarsest.Grid, axis).Select(p => p - iso(coarsest)).Where(p => p >= lo - 1e-6 && p <= hi + 1e-6).ToArray();
    }

    private static (double Lo, double Hi) RelativeRange(IReadOnlyList<LoadedDose> loaded, GridAxis axis, Func<LoadedDose, double> iso) =>
        (loaded.Max(l => GridLines.Min(l.Grid, axis) - iso(l)), loaded.Min(l => GridLines.Max(l.Grid, axis) - iso(l)));

    private static MultiCurve Make(string title, string axisLabel, double[] positions, IReadOnlyList<CurveSeries> series, int reference)
    {
        var refValues = series[reference].Values;
        double refMax = refValues.Where(v => !double.IsNaN(v)).DefaultIfEmpty(double.NaN).Max();
        var differences = series.Where((_, i) => i != reference).Select(s => new CurveSeries(
            $"{s.Name} - {series[reference].Name}",
            s.Values.Select((v, i) => refMax > 0 && !double.IsNaN(v) && !double.IsNaN(refValues[i]) ? 100.0 * (v - refValues[i]) / refMax : double.NaN).ToArray()))
            .ToArray();
        return new MultiCurve(title, axisLabel, "Dose (Gy)", positions, series, differences);
    }

    private static void Check(IReadOnlyList<SetDose> sets, IReadOnlyList<LoadedDose> loaded, int reference)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(loaded);
        if (sets.Count == 0 || sets.Count != loaded.Count) throw new ArgumentException("One loaded grid is needed for each set.");
        if (reference < 0 || reference >= sets.Count) throw new ArgumentOutOfRangeException(nameof(reference));
    }

    private static string Name(SetDose d) => d.Dose.Beam?.BeamName is { Length: > 0 } n ? n : Path.GetFileName(d.Dose.Dose.SourcePath);

    private static string Mm(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
}
