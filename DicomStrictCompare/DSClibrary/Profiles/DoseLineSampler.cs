using System;
using System.Collections.Generic;
using System.Linq;
using DSClibrary.Resampling;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>A calculated curve sampled along a scan's line, and how many of the scan's positions fell outside the grid.</summary>
    public sealed record SampledLine(Profile Calculated, int PointsOutsideGrid);

    /// <summary>
    /// Reads a scan's line out of a dose grid: the dose at each of a measured profile's positions, through a
    /// <see cref="PhantomFrame"/>. This is the sampling <see cref="DoseVolume"/> waited for; the geometry lives in the
    /// frame and in <see cref="ScanGeometry.DirectionFor"/>, and the interpolation is the comparison engine's own
    /// trilinear one (<see cref="GridResampler.ValueAt"/>), so a curve here and a voxel comparison read the same numbers.
    /// <para>
    /// <b>The line.</b> A depth dose starts at the surface on the beam axis and runs along +y. A profile runs through
    /// the axis at its depth below the surface, along +x (crossplane) or +z (inplane). The scan's own off-axis
    /// offsets (SCAN_OFFAXIS_CROSSPLANE and _INPLANE in a PTW file) move the line along x and z. Diagonals are refused,
    /// as <see cref="ScanGeometry"/> refuses them.
    /// </para>
    /// <para>
    /// <b>Outside the grid</b> a position has no dose. It is left out of the sampled curve and counted, never
    /// extrapolated, because an extrapolated penumbra is a plausible number that means nothing.
    /// </para>
    /// </summary>
    public static class DoseLineSampler
    {
        /// <summary>The point, in patient coordinates, that a position along a scan's line reaches.</summary>
        public static Position3 PointFor(PhantomFrame frame, ScanKey scan, double positionMm)
        {
            if (frame is null) throw new ArgumentNullException(nameof(frame));
            if (scan is null) throw new ArgumentNullException(nameof(scan));
            var (anchor, direction) = Line(frame, scan);
            return new Position3(
                anchor.X + direction.X * positionMm,
                anchor.Y + direction.Y * positionMm,
                anchor.Z + direction.Z * positionMm);
        }

        /// <summary>
        /// The dose at each of <paramref name="measured"/>'s positions, as a <see cref="Profile"/> with the measured
        /// curve's beam and scan keys, so the two sides of a comparison carry the same address. Grid values in gray are
        /// converted to centigray, the library's absolute unit; pass <paramref name="gridInGray"/> false for a grid that
        /// is already centigray (<see cref="DoseVolume.Grid"/>).
        /// </summary>
        public static SampledLine Sample(DoseMatrixOptimal grid, PhantomFrame frame, Profile measured, string dosePath,
            bool gridInGray = true)
        {
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            if (measured is null) throw new ArgumentNullException(nameof(measured));
            double toCentigray = gridInGray ? 100.0 : 1.0;
            var points = new List<ProfilePoint>(measured.Points.Count);
            int outside = 0;
            foreach (var p in measured.Points)
            {
                if (ValueAt(grid, frame, measured.Scan, p.Position.Millimetres) is { } value)
                    points.Add(new ProfilePoint(p.Position, value * toCentigray, null));
                else outside++;
            }
            if (points.Count == 0)
                throw new InvalidOperationException($"No position of {measured} lies inside the dose grid of {dosePath}.");
            var calculated = new Profile(measured.Beam, measured.Scan, points, MeasurementConditions.Empty,
                new Provenance(dosePath, "DICOM", nameof(DoseLineSampler), null),
                NormalizationType.AsMeasured, DetectorGeometryCorrection.NotApplied, null, ValueScale.AbsoluteCentigray);
            return new SampledLine(calculated, outside);
        }

        /// <summary>The dose at one position along a scan's line, or null outside the grid. In the grid's own units.</summary>
        public static double? ValueAt(DoseMatrixOptimal grid, PhantomFrame frame, ScanKey scan, double positionMm)
        {
            var point = PointFor(frame, scan, positionMm);
            return GridLines.Contains(grid, point.X, point.Y, point.Z)
                ? GridResampler.ValueAt(grid, point.X, point.Y, point.Z)
                : null;
        }

        /// <summary>
        /// The dose at the grid's own nodes along a scan's line, between two positions: evenly spaced, as the gamma
        /// search needs its evaluation side, and exact, because along a grid axis the trilinear value at a node is the
        /// line's own linear interpolation. Positions ascend. In the grid's own units.
        /// </summary>
        public static (double[] Positions, double[] Values) AtNodes(DoseMatrixOptimal grid, PhantomFrame frame, ScanKey scan,
            double fromMm, double toMm)
        {
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            var (anchor, direction) = Line(frame, scan);
            // The line runs along one grid axis; its nodes are that axis's, as positions along the line.
            var (axis, sign, origin) = Math.Abs(direction.X) > 0.5 ? (GridAxis.X, Math.Sign(direction.X), anchor.X)
                : Math.Abs(direction.Y) > 0.5 ? (GridAxis.Y, Math.Sign(direction.Y), anchor.Y)
                : (GridAxis.Z, Math.Sign(direction.Z), anchor.Z);
            var positions = GridLines.Nodes(grid, axis)
                .Select(node => (node - origin) * sign)
                .Where(p => p >= fromMm - 1e-9 && p <= toMm + 1e-9)
                .OrderBy(p => p)
                .ToArray();
            var kept = new List<double>(positions.Length);
            var values = new List<double>(positions.Length);
            foreach (double p in positions)
                if (ValueAt(grid, frame, scan, p) is { } v) { kept.Add(p); values.Add(v); }
            return (kept.ToArray(), values.ToArray());
        }

        private static (Position3 Anchor, PatientDirection Direction) Line(PhantomFrame frame, ScanKey scan)
        {
            var geometry = frame.Geometry;
            var along = geometry.DirectionFor(scan.CurveType);
            var cross = geometry.DirectionFor(CurveType.Crossplane);
            var inplane = geometry.DirectionFor(CurveType.Inplane);
            var down = geometry.DirectionFor(CurveType.Pdd);
            double offCross = scan.OffAxisCrossplane?.Millimetres ?? 0;
            double offIn = scan.OffAxisInplane?.Millimetres ?? 0;
            double depth = scan.CurveType == CurveType.Pdd ? 0 : scan.Depth?.Millimetres
                ?? throw new InvalidOperationException($"A {scan.CurveType} scan needs a depth.");
            var anchor = new Position3(
                frame.AxisXMm + cross.X * offCross + inplane.X * offIn + down.X * depth,
                frame.SurfaceYMm + cross.Y * offCross + inplane.Y * offIn + down.Y * depth,
                frame.AxisZMm + cross.Z * offCross + inplane.Z * offIn + down.Z * depth);
            return (anchor, along);
        }
    }
}
