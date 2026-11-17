using System;
using DSClibrary.Results.DICOM;
using DSClibrary.Studies;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>Where <see cref="PhantomFrame.SurfaceYMm"/> came from, in the order it is looked for.</summary>
    public enum SurfaceSource
    {
        /// <summary>The plan's SurfaceEntryPoint (300A,012E).</summary>
        PlanSurfaceEntryPoint,
        /// <summary>The plan's SourceToSurfaceDistance (300A,0130), from the source at isocentre y minus SAD.</summary>
        PlanSsd,
        /// <summary>The measurement's own SSD, placed the same way. The plan states neither, as in isocentric phantom plans.</summary>
        MeasuredSsd,
        /// <summary>The dose: the first voxel on the beam axis above 1 % of that column's maximum.</summary>
        Dose
    }

    /// <summary>
    /// Where a calculated dose's water phantom sits in DICOM patient coordinates: the beam axis (x, z) and the water
    /// surface (y), in millimetres. A water-tank scan's positions are measured from that axis and that surface, so
    /// this is what turns "crossplane at 100 mm depth" into a line through the dose grid.
    /// <para>
    /// <b>The convention</b> (prototype, 2026-11-17, for crcrewso's review): head-first supine at gantry and couch 0,
    /// as <see cref="ScanGeometry"/> requires, so the beam travels along +y. The axis is the plan's isocentre x and z.
    /// The surface is the plan's SurfaceEntryPoint, else its SSD, else the measurement's SSD, each placed from the
    /// source at isocentre y minus SAD; else the dose. The dose's own surface is always worked out too
    /// (<see cref="DoseSurfaceYMm"/>), so a frame built on an SSD that doesn't fit the dose says so
    /// (<see cref="SurfaceDisagreementMm"/>) instead of sampling the wrong depth quietly.
    /// </para>
    /// <para>
    /// Measured on QAtpsPTW (TST-After, 2026-11-17): photon plans are ISOCENTRIC with the isocentre at y = -200 and
    /// no SSD; the scans say SSD 1000 and the plans' SAD is 1000, so the surface is placed at y = -200. The dose
    /// grids start at y = -201 with 2 mm voxels, so the dose's own surface is the first voxel in water, -199.
    /// </para>
    /// </summary>
    public sealed record PhantomFrame(
        double AxisXMm,
        double SurfaceYMm,
        double AxisZMm,
        ScanGeometry Geometry,
        SurfaceSource SurfaceFrom,
        double? DoseSurfaceYMm,
        double GridYResolutionMm)
    {
        /// <summary>The dose's surface minus the frame's, in mm; null when the dose has no surface on the axis.</summary>
        public double? SurfaceDisagreementMm => DoseSurfaceYMm is { } d ? d - SurfaceYMm : null;

        /// <summary>
        /// How far the dose's surface may sit from the frame's before it is a warning: one voxel along y, plus half a
        /// millimetre. The dose's surface is a voxel position, the first one in water, so it can lie up to a voxel below
        /// the true surface.
        /// </summary>
        public double SurfaceToleranceMm => GridYResolutionMm + 0.5;

        /// <summary>True when the dose's surface agrees with the frame's, or there is none to check against.</summary>
        public bool SurfaceAgrees => SurfaceDisagreementMm is not { } d || Math.Abs(d) <= SurfaceToleranceMm;

        /// <summary>One line for a result row: where the surface came from, and any disagreement with the dose.</summary>
        public string Describe()
        {
            string from = SurfaceFrom switch
            {
                SurfaceSource.PlanSurfaceEntryPoint => "plan's surface entry point",
                SurfaceSource.PlanSsd => "plan's SSD",
                SurfaceSource.MeasuredSsd => "measurement's SSD (the plan states none)",
                _ => "dose (no surface entry point or SSD)"
            };
            string text = FormattableString.Invariant($"surface y {SurfaceYMm:0.0} mm from the {from}");
            if (SurfaceDisagreementMm is { } d && !SurfaceAgrees)
                text += FormattableString.Invariant($"; WARNING the dose's surface is {d:+0.0;-0.0} mm from it");
            return text;
        }

        /// <summary>
        /// The frame for one dose, from its plan beam and setup. Refused, with a reason, when the geometry isn't the
        /// supported one, when the plan has no isocentre, or when no surface can be found.
        /// </summary>
        /// <param name="beam">The dose's plan beam.</param>
        /// <param name="setup">The beam's patient setup, for the patient position.</param>
        /// <param name="grid">The dose, for the surface check and as a last resort for the surface itself.</param>
        /// <param name="measuredSsd">The measurement's SSD, used when the plan states none.</param>
        public static (PhantomFrame? Frame, string? Refusal) For(
            RTPlanBeam? beam, RTPatientSetup? setup, DoseMatrixOptimal grid, Distance? measuredSsd)
        {
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            if (beam is null) return (null, "no plan beam: the beam axis and the surface can't be placed");

            var geometry = new ScanGeometry(PatientOrientations.Parse(setup?.PatientPosition), beam.GantryAngle ?? double.NaN);
            if (!geometry.IsSupported) return (null, geometry.DescribeUnsupported());
            if (beam.PatientSupportAngle is { } couch && Math.Min(Math.Abs(couch), Math.Abs(couch - 360)) > 0.1)
                return (null, FormattableString.Invariant($"couch angle {couch} is not supported; only 0 is"));
            if (beam.IsocenterPosition.Count < 3) return (null, "the plan beam has no isocentre");

            var iso = new Position3(beam.IsocenterPosition[0], beam.IsocenterPosition[1], beam.IsocenterPosition[2]);
            double? doseSurface = JawEdgeCheck.SurfaceFromDose(grid, iso);

            double surface;
            SurfaceSource from;
            if (beam.SurfaceEntryPoint.Count >= 3)
            {
                surface = beam.SurfaceEntryPoint[1];
                from = SurfaceSource.PlanSurfaceEntryPoint;
            }
            else if (beam.SourceToSurfaceDistance is { } planSsd && beam.SourceAxisDistance is { } sad1)
            {
                surface = iso.Y - sad1 + planSsd;   // gantry 0: the source is SAD above the isocentre, along -y
                from = SurfaceSource.PlanSsd;
            }
            else if (measuredSsd is { } ssd && beam.SourceAxisDistance is { } sad2)
            {
                surface = iso.Y - sad2 + ssd.Millimetres;
                from = SurfaceSource.MeasuredSsd;
            }
            else if (doseSurface is { } d)
            {
                surface = d;
                from = SurfaceSource.Dose;
            }
            else return (null, "no surface entry point, SSD or SAD, and the dose has no surface on the beam axis");

            return (new PhantomFrame(iso.X, surface, iso.Z, geometry, from, doseSurface, grid.YRes), null);
        }
    }
}
