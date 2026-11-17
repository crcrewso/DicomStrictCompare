using System.Globalization;
using DSClibrary;
using DSClibrary.Matching;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;

namespace DSCprototypes.Shared.Graphs;

public enum CurveKind
{
    /// <summary>Along the beam axis, into the phantom.</summary>
    DepthDose,
    /// <summary>Across the beam along patient x, at a depth.</summary>
    Crossplane,
    /// <summary>Across the beam along patient z, at a depth.</summary>
    Inplane
}

/// <summary>One curve of a matched pair, both sides sampled at the same positions. NaN where a grid has no value.</summary>
public sealed record ProfileCurve(
    string Title,
    string AxisLabel,
    string ValueLabel,
    double[] Positions,
    double[] Source,
    double[] Target,
    string SourceName,
    string TargetName,
    string Tag = "")
{
    /// <summary>Points where both grids have a value, for a plotting library that cannot take NaN.</summary>
    public (double[] Positions, double[] Source, double[] Target) Overlap()
    {
        var keep = Enumerable.Range(0, Positions.Length)
            .Where(i => !double.IsNaN(Source[i]) && !double.IsNaN(Target[i])).ToArray();
        return (keep.Select(i => Positions[i]).ToArray(),
                keep.Select(i => Source[i]).ToArray(),
                keep.Select(i => Target[i]).ToArray());
    }
}

/// <summary>One dose grid in memory with the point its curves are taken through.</summary>
public sealed class LoadedDose
{
    public required RTDoseWithPlan Dose { get; init; }
    public required DoseMatrixOptimal Grid { get; init; }
    public required double IsoX { get; init; }
    public required double IsoY { get; init; }
    public required double IsoZ { get; init; }
    public required bool IsocentreFromPlan { get; init; }
}

/// <summary>A matched pair with both grids in memory and the point the curves are taken through.</summary>
public sealed class LoadedPair
{
    public required MatchedDosePair Pair { get; init; }
    public required DoseMatrixOptimal Source { get; init; }
    public required DoseMatrixOptimal Target { get; init; }
    public required double IsoX { get; init; }
    public required double IsoY { get; init; }
    public required double IsoZ { get; init; }
    public required bool IsocentreFromPlan { get; init; }
}

/// <summary>
/// Which line out of a dose grid is which curve. <b>This is a reading of the geometry, stated here
/// once and shown in the window</b>, not a settled convention of the library: DSClibrary's
/// <c>DoseVolume</c> deliberately does not yet yield profiles because the phantom geometry
/// convention is still being decided, and the prototypes should not pre-empt that quietly.
/// <para>
/// The reading: head-first supine with the gantry at zero, which is what every plan in
/// <c>resources/</c> states (measured 2026-11-08). The beam then travels along +y in the patient
/// coordinate system, so depth increases with y; x is crossplane and z is inplane at collimator
/// zero. The surface is taken at the plan's isocentre plane, which is the SSD = SAD = 100 cm
/// assumption <c>RTDoseVolumeMapper</c> also makes and flags for revisiting. Curves run through
/// the isocentre's x and z. Where a plan is absent the grid centre stands in for the isocentre and
/// the window says so.
/// </para>
/// <para>
/// Both sides are sampled at the same positions: the nodes of the coarser grid along the axis,
/// restricted to where both grids have values - the same choice the comparison engine makes.
/// </para>
/// </summary>
public static class BeamLines
{
    public const string ConventionNote =
        "Reading of the geometry (not yet a settled library convention): head-first supine, gantry 0, so the beam "
        + "runs along +y and depth = y - isocentre y; x is crossplane and z inplane at collimator 0; the surface is "
        + "taken at the isocentre plane (SSD = SAD assumed). Both sides are sampled at the coarser grid's nodes "
        + "where the grids overlap. Values are Gy as stored.";

    /// <summary>The depths crcrewso asked the dose-to-dose plots to show (2026-11-09): dmax, then 5, 10 and 20 cm.</summary>
    public static readonly IReadOnlyList<(string Label, double? Mm)> FixedDepths = new (string, double?)[]
    {
        ("Dmax", null), ("5 cm", 50), ("10 cm", 100), ("20 cm", 200)
    };

    public static LoadedPair Load(MatchedDosePair pair, Func<string, DoseMatrixOptimal> loader)
    {
        ArgumentNullException.ThrowIfNull(pair);
        ArgumentNullException.ThrowIfNull(loader);
        var source = LoadDose(pair.Source, loader);
        var target = loader(pair.Target.Dose.SourcePath);
        return new LoadedPair
        {
            Pair = pair, Source = source.Grid, Target = target,
            IsoX = source.IsoX, IsoY = source.IsoY, IsoZ = source.IsoZ, IsocentreFromPlan = source.IsocentreFromPlan
        };
    }

    public static LoadedDose LoadDose(RTDoseWithPlan dose, Func<string, DoseMatrixOptimal> loader)
    {
        ArgumentNullException.ThrowIfNull(dose);
        ArgumentNullException.ThrowIfNull(loader);
        var grid = loader(dose.Dose.SourcePath);
        var iso = dose.Beam?.IsocenterPosition;
        bool fromPlan = iso is { Count: >= 3 };
        return new LoadedDose
        {
            Dose = dose, Grid = grid,
            IsoX = fromPlan ? iso![0] : (grid.X0 + grid.XMax) / 2,
            IsoY = fromPlan ? iso![1] : grid.Y0,
            IsoZ = fromPlan ? iso![2] : (grid.Z0 + grid.ZMax) / 2,
            IsocentreFromPlan = fromPlan
        };
    }

    /// <summary>
    /// Every fixed depth on one plot (crcrewso, 2026-11-09: "All depths for Inplane or Crossplane in a
    /// single plot should also be an option"): one curve per depth, tagged with its label, sharing
    /// the axis and value labels of the first.
    /// </summary>
    public static IReadOnlyList<ProfileCurve> ProfilesAtAllDepths(LoadedPair lp, CurveKind kind) =>
        ProfilesAtAllDepths(lp, kind, FixedDepths);

    /// <summary>The same at a chosen list of depths (F7: depths are a setting); a null depth is dmax.</summary>
    public static IReadOnlyList<ProfileCurve> ProfilesAtAllDepths(LoadedPair lp, CurveKind kind, IReadOnlyList<(string Label, double? Mm)> depths)
    {
        if (kind == CurveKind.DepthDose) return new[] { DepthDose(lp) };
        double dmax = DepthOfMaximum(lp);
        return depths.Select(d =>
        {
            double depth = d.Mm ?? dmax;
            string tag = d.Mm is null ? $"dmax {dmax.ToString("0.#", CultureInfo.InvariantCulture)} mm" : d.Label;
            return ProfileAt(lp, kind, depth) with { Tag = tag };
        }).ToArray();
    }

    /// <summary>
    /// The depth of maximum dose along the source's axis, from its own nodes. The grid's dmax, not
    /// a fitted one: on a 5 mm grid it is the nearest node, which is what the plot can show anyway.
    /// </summary>
    public static double DepthOfMaximum(LoadedPair lp)
    {
        var ys = GridLines.Nodes(lp.Source, GridAxis.Y);
        var values = GridLines.Along(lp.Source, GridAxis.Y, lp.IsoX, lp.IsoZ, ys);
        int best = 0;
        for (int i = 1; i < values.Length; i++) if (values[i] > values[best]) best = i;
        return ys[best] - lp.IsoY;
    }

    // A scan over a dose is no longer drawn here: since 2026-11-17 the Graphs tab compares it through the library
    // (ScanComparison, ProfileComparison), which places the water surface from the plan instead of at the isocentre plane.

    /// <summary>
    /// The sampling positions along an axis: the coarser grid's nodes, within the overlap of both.
    /// </summary>
    public static double[] Lattice(LoadedPair lp, GridAxis axis)
    {
        var coarser = GridLines.Resolution(lp.Source, axis) >= GridLines.Resolution(lp.Target, axis) ? lp.Source : lp.Target;
        double lo = Math.Max(GridLines.Min(lp.Source, axis), GridLines.Min(lp.Target, axis)) - 1e-6;
        double hi = Math.Min(GridLines.Max(lp.Source, axis), GridLines.Max(lp.Target, axis)) + 1e-6;
        return GridLines.Nodes(coarser, axis).Where(p => p >= lo && p <= hi).ToArray();
    }

    /// <summary>Depths available for a profile: the y lattice relative to the isocentre plane, ascending.</summary>
    public static double[] Depths(LoadedPair lp) => Lattice(lp, GridAxis.Y).Select(y => y - lp.IsoY).ToArray();

    public static ProfileCurve DepthDose(LoadedPair lp)
    {
        var ys = Lattice(lp, GridAxis.Y);
        return new ProfileCurve(
            $"{Name(lp)}: depth dose along the beam axis",
            "Depth below the isocentre plane (mm)",
            "Dose (Gy)",
            ys.Select(y => y - lp.IsoY).ToArray(),
            GridLines.Along(lp.Source, GridAxis.Y, lp.IsoX, lp.IsoZ, ys),
            GridLines.Along(lp.Target, GridAxis.Y, lp.IsoX, lp.IsoZ, ys),
            Path.GetFileName(lp.Pair.Source.Dose.SourcePath),
            Path.GetFileName(lp.Pair.Target.Dose.SourcePath));
    }

    public static ProfileCurve ProfileAt(LoadedPair lp, CurveKind kind, double depthMm)
    {
        if (kind == CurveKind.DepthDose) return DepthDose(lp);

        double y = lp.IsoY + depthMm;
        string depth = depthMm.ToString("0.#", CultureInfo.InvariantCulture);
        if (kind == CurveKind.Crossplane)
        {
            var xs = Lattice(lp, GridAxis.X);
            return new ProfileCurve(
                $"{Name(lp)}: crossplane profile at {depth} mm depth",
                "Crossplane x from the axis (mm)",
                "Dose (Gy)",
                xs.Select(x => x - lp.IsoX).ToArray(),
                GridLines.Along(lp.Source, GridAxis.X, y, lp.IsoZ, xs),
                GridLines.Along(lp.Target, GridAxis.X, y, lp.IsoZ, xs),
                Path.GetFileName(lp.Pair.Source.Dose.SourcePath),
                Path.GetFileName(lp.Pair.Target.Dose.SourcePath));
        }

        var zs = Lattice(lp, GridAxis.Z);
        return new ProfileCurve(
            $"{Name(lp)}: inplane profile at {depth} mm depth",
            "Inplane z from the axis (mm)",
            "Dose (Gy)",
            zs.Select(z => z - lp.IsoZ).ToArray(),
            GridLines.Along(lp.Source, GridAxis.Z, lp.IsoX, y, zs),
            GridLines.Along(lp.Target, GridAxis.Z, lp.IsoX, y, zs),
            Path.GetFileName(lp.Pair.Source.Dose.SourcePath),
            Path.GetFileName(lp.Pair.Target.Dose.SourcePath));
    }

    private static string Name(LoadedPair lp) =>
        lp.Pair.Source.Beam?.BeamName is { Length: > 0 } n ? n : lp.Pair.Key.ToString();
}
