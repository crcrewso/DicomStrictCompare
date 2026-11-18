using System;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Maps
{
    /// <summary>A unit vector in DICOM patient coordinates (mm).</summary>
    public readonly record struct Axis3(double X, double Y, double Z)
    {
        public double Dot(double x, double y, double z) => X * x + Y * y + Z * z;
        public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###}, {Z:0.###})");
    }

    /// <summary>
    /// The frame a difference map is shown in: what matters is the axis relative to the beam (crcrewso, 2026-11-18).
    /// x runs along the collimator-0 X jaws, y along the collimator-0 Y jaws, z along the beam axis, positive toward the
    /// source (IEC 61217's gantry system, so depth runs down the screen). The origin is the isocentre for an arc and the
    /// phantom's centre for a static gantry. The collimator angle is ignored on purpose.
    /// <para>Head first supine at couch 0 only, as <see cref="ScanGeometry"/>; other setups are refused, not guessed.</para>
    /// </summary>
    public sealed record BeamFrame(Position3 Origin, Axis3 X, Axis3 Y, Axis3 Z, double GantryDegrees, string Note)
    {
        /// <summary>A patient-coordinate point in this frame.</summary>
        public Position3 ToFrame(double x, double y, double z)
        {
            double dx = x - Origin.X, dy = y - Origin.Y, dz = z - Origin.Z;
            return new Position3(X.Dot(dx, dy, dz), Y.Dot(dx, dy, dz), Z.Dot(dx, dy, dz));
        }

        /// <summary>
        /// The frame for one dose from its plan beam and setup, or why not. With no plan the dose's axes are used as head
        /// first supine at gantry 0, centred on the phantom, and the note says it was assumed.
        /// </summary>
        public static (BeamFrame? Frame, string? Refusal) For(RTPlanBeam? beam, RTPatientSetup? setup, DoseMatrixOptimal grid)
        {
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            if (beam is null)
                return (AtGantry(0, PhantomCentre(grid, out string how), "no plan: assumed head first supine at gantry 0; origin " + how), null);

            var orientation = PatientOrientations.Parse(setup?.PatientPosition);
            if (orientation != PatientOrientation.HeadFirstSupine)
                return (null, $"patient position {(setup?.PatientPosition is { Length: > 0 } p ? p : "unknown")}: only HFS is supported");
            if (beam.PatientSupportAngle is { } couch && Math.Min(Math.Abs(couch), Math.Abs(couch - 360)) > 0.1)
                return (null, FormattableString.Invariant($"couch angle {couch}: only 0 is supported"));

            if (beam.IsArc)
            {
                if (beam.IsocenterPosition.Count < 3) return (null, "an arc with no isocentre in the plan");
                var iso = new Position3(beam.IsocenterPosition[0], beam.IsocenterPosition[1], beam.IsocenterPosition[2]);
                return (AtGantry(0, iso, "arc: axes at gantry 0, origin the isocentre"), null);
            }
            if (beam.GantryAngle is not { } gantry) return (null, "the plan beam has no gantry angle");
            return (AtGantry(gantry, PhantomCentre(grid, out string from), FormattableString.Invariant($"gantry {gantry:0.#}; origin {from}")), null);
        }

        /// <summary>
        /// The axes at a gantry angle, head first supine, couch 0. At gantry 0 x is the patient's left (+x), y toward the
        /// head and gantry (+z), z anterior toward the source (-y); the gantry turns them about y, so at 90 the source is
        /// on the patient's left.
        /// </summary>
        public static BeamFrame AtGantry(double gantryDegrees, Position3 origin, string note)
        {
            double g = gantryDegrees * Math.PI / 180, c = Clean(Math.Cos(g)), s = Clean(Math.Sin(g));
            // In patient coordinates IEC fixed X is (1,0,0), Y is (0,0,1) and Z is (0,-1,0).
            var x = new Axis3(c, s, 0);      // cos g Xf - sin g Zf
            var z = new Axis3(s, -c, 0);     // sin g Xf + cos g Zf
            return new BeamFrame(origin, x, new Axis3(0, 0, 1), z, gantryDegrees, note);
            static double Clean(double v) => Math.Abs(v) < 1e-12 ? 0 : v;
        }

        /// <summary>
        /// The phantom's centre: the middle of the box of voxels with any dose (the planning system leaves dose outside
        /// the body at zero), else the grid's centre.
        /// </summary>
        public static Position3 PhantomCentre(DoseMatrixOptimal grid, out string how)
        {
            int nx = grid.DimensionX, ny = grid.DimensionY, nz = grid.DimensionZ;
            int x0 = nx, x1 = -1, y0 = ny, y1 = -1, z0 = nz, z1 = -1;
            var v = grid.DoseValues;
            for (int k = 0, i = 0; k < nz; k++)
                for (int j = 0; j < ny; j++)
                    for (int ii = 0; ii < nx; ii++, i++)
                    {
                        if (!(v[i] > 0)) continue;
                        if (ii < x0) x0 = ii; if (ii > x1) x1 = ii;
                        if (j < y0) y0 = j; if (j > y1) y1 = j;
                        if (k < z0) z0 = k; if (k > z1) z1 = k;
                    }
            if (x1 < 0)
            {
                how = "the grid's centre (no dose)";
                return new Position3((grid.X0 + grid.XMax) / 2, (grid.Y0 + grid.YMax) / 2, (grid.Z0 + grid.ZMax) / 2);
            }
            how = "the centre of the dosed region";
            return new Position3(grid.X0 + (x0 + x1) / 2.0 * grid.XRes, grid.Y0 + (y0 + y1) / 2.0 * grid.YRes, grid.Z0 + (z0 + z1) / 2.0 * grid.ZRes);
        }
    }
}
