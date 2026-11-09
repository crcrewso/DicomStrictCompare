using System.Globalization;
using DSClibrary;
using DSClibrary.Matching;
using DSClibrary.Resampling;

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
    string TargetName)
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

    public static LoadedPair Load(MatchedDosePair pair, Func<string, DoseMatrixOptimal> loader)
    {
        ArgumentNullException.ThrowIfNull(pair);
        ArgumentNullException.ThrowIfNull(loader);
        var source = loader(pair.Source.Dose.SourcePath);
        var target = loader(pair.Target.Dose.SourcePath);

        var iso = pair.Source.Beam?.IsocenterPosition;
        bool fromPlan = iso is { Count: >= 3 };
        double isoX = fromPlan ? iso![0] : (source.X0 + source.XMax) / 2;
        double isoY = fromPlan ? iso![1] : source.Y0;
        double isoZ = fromPlan ? iso![2] : (source.Z0 + source.ZMax) / 2;

        return new LoadedPair
        {
            Pair = pair, Source = source, Target = target,
            IsoX = isoX, IsoY = isoY, IsoZ = isoZ, IsocentreFromPlan = fromPlan
        };
    }

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
