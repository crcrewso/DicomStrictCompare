using DSClibrary.Results.DICOM;

namespace DSClibrary.Studies
{
    /// <summary>One field edge: where the plan's jaw puts it at the check depth, and where the dose's 50 % point is.</summary>
    public sealed record FieldEdge(string Jaw, double ExpectedMm, double MeasuredMm)
    {
        public double DifferenceMm => MeasuredMm - ExpectedMm;
    }

    /// <summary>The result of <see cref="JawEdgeCheck"/>; a warning, never a refusal.</summary>
    public sealed record JawEdgeResult(bool Checked, IReadOnlyList<FieldEdge> Edges, string? NotCheckedReason = null, string Note = "")
    {
        public IEnumerable<FieldEdge> Disagreeing => Edges.Where(e => Math.Abs(e.DifferenceMm) > JawEdgeCheck.ToleranceMm);
        public bool Agrees => Checked && !Disagreeing.Any();

        public string Summary => !Checked ? $"not checked: {NotCheckedReason}"
            : Agrees ? "jaws and 50 % edges agree"
            : string.Join("; ", Disagreeing.Select(e => $"{e.Jaw} jaw {e.ExpectedMm:0.0} mm, 50 % edge {e.MeasuredMm:0.0} mm"));
    }

    /// <summary>
    /// Does the dose's field match its plan's jaws? Cody, 2026-11-15: the jaw position and the profile's 50 % point
    /// should agree within 2 mm, at 10 cm depth. A larger difference suggests the dose and plan don't belong
    /// together, or the geometry was read wrongly (design/comparison-studies.md, F10).
    /// <para>
    /// <b>How:</b> at 10 cm below the surface, a profile along each jaw pair's axis runs through the field centre.
    /// For a symmetric field that is the CAX. The 50 % level is half the dose at the field centre, and each edge is
    /// interpolated between samples. Jaw positions are defined at the isocentre plane, so they are scaled by
    /// (SSD + 100 mm) / SAD.
    /// </para>
    /// <para>
    /// <b>The surface</b> is the plan's SurfaceEntryPoint, else its SSD, else, as in isocentric phantom plans that
    /// state neither, the first voxel on the CAX column with dose above 1 % of that column's maximum. A voxel's error
    /// there moves the plane about 1 mm and the scaling by about 0.1 %.
    /// </para>
    /// <para>
    /// <b>Axes</b> (IEC 61217, head-first supine, gantry 0): at collimator 0 the X jaws lie along +DICOM x and the Y
    /// jaws along +z. A collimator angle turns both by that angle about the beam, as seen from the source. Only
    /// multiples of 45 degrees are checked; 90 is common on older Varian machines because of the EPID's geometry. Only static photon beams at gantry and couch 0 are checked: electrons are
    /// not, because the applicator defines the field; nor wedged fields, whose profile is tilted; nor FFF fields, for
    /// now; nor fields the MLC shapes.
    /// </para>
    /// </summary>
    public static class JawEdgeCheck
    {
        /// <summary>Both doses of a pair, as one line for a result row: empty when both agree or neither is checkable.</summary>
        public static string Describe(JawEdgeResult? source, JawEdgeResult? target)
        {
            var parts = new List<string>();
            if (source is { Checked: true, Agrees: false }) parts.Add("source: " + source.Summary);
            if (target is { Checked: true, Agrees: false }) parts.Add("target: " + target.Summary);
            if (parts.Count > 0) return string.Join("; ", parts);
            if (source is { Agrees: true } && target is { Agrees: true }) return "agree";
            return "not checked" + (source?.NotCheckedReason is { } r ? $" ({r})" : "");
        }

        public const double ToleranceMm = 2.0;
        public const double DepthMm = 100.0;
        public static JawEdgeResult For(DoseMatrixOptimal dose, RTPlanBeam? beam)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));
            if (beam is null) return NotChecked("no plan");
            if (string.Equals(beam.RadiationType, "ELECTRON", StringComparison.OrdinalIgnoreCase)) return NotChecked("electron: the applicator defines the field");
            if (beam.WedgeIDs.Count > 0) return NotChecked("wedged field");
            // FFF field size is quoted as if the beam had been flattened, so neither the plain 50 % point nor the
            // jaw is directly comparable. Left out for now (Cody, 2026-11-15); see comparison-studies.md, F10.
            if (beam.IsFluenceModeFff == true) return NotChecked("FFF");
            if (beam.IsArc) return NotChecked("arc");
            if (!Zero(beam.GantryAngle) || !Zero(beam.PatientSupportAngle)) return NotChecked("gantry and couch must be 0");
            // Realistic collimator angles are 0, 90 and 270, rarely 45 or 315 (Cody, 2026-11-15).
            if (beam.BeamLimitingDeviceAngle is not { } collimator || Math.Abs(collimator / 45 - Math.Round(collimator / 45)) * 45 > 0.1)
                return NotChecked("collimator angle is not a multiple of 45");
            if (beam.SourceAxisDistance is not { } sad) return NotChecked("no SAD in the plan");
            if (beam.IsocenterPosition.Count < 3) return NotChecked("no isocentre in the plan");
            var jaws = beam.LeafJawPositions;
            var xj = Pair(jaws, "ASYMX") ?? Pair(jaws, "X");
            var yj = Pair(jaws, "ASYMY") ?? Pair(jaws, "Y");
            if (xj is null || yj is null) return NotChecked("no X or Y jaw positions");
            if (MlcDefinesTheField(jaws, xj) is { } mlc) return NotChecked(mlc);
            var iso = new Position3(beam.IsocenterPosition[0], beam.IsocenterPosition[1], beam.IsocenterPosition[2]);
            double sourceY = iso.Y - sad;   // gantry 0: the beam runs along +y

            // The surface.
            double surfaceY; string note;
            var entry = beam.SurfaceEntryPoint;
            if (entry.Count >= 3) { surfaceY = entry[1]; note = "surface from the plan's entry point"; }
            else if (beam.SourceToSurfaceDistance is { } ssdStated) { surfaceY = sourceY + ssdStated; note = "surface from the plan's SSD"; }
            else if (SurfaceFromDose(dose, iso) is { } y) { surfaceY = y; note = "surface from the dose (no SSD in the plan)"; }
            else return NotChecked("no surface entry point or SSD, and the CAX column has no dose");

            double plane = surfaceY + DepthMm;
            double scale = (surfaceY - sourceY + DepthMm) / sad;
            if (plane < dose.Y0 || plane > dose.YMax) return NotChecked($"the {DepthMm / 10:0} cm plane is outside the grid");

            // The jaw axes in DICOM (x, z) at this collimator angle.
            double t = collimator * Math.PI / 180;
            static double Clean(double v) => Math.Abs(v) < 1e-12 ? 0 : v;   // exact 0 and 1 at multiples of 90
            var xAxis = (X: Clean(Math.Cos(t)), Z: Clean(Math.Sin(t)));
            var yAxis = (X: -Clean(Math.Sin(t)), Z: Clean(Math.Cos(t)));
            if (Math.Abs(collimator % 90) > 0.1) note += "; collimator 45 or 315 is untested";
            // The field centre, at the plane.
            double cu = (xj[0] + xj[1]) / 2 * scale, cv = (yj[0] + yj[1]) / 2 * scale;
            double centreX = iso.X + cu * xAxis.X + cv * yAxis.X, centreZ = iso.Z + cu * xAxis.Z + cv * yAxis.Z;
            if (!Inside(dose, centreX, plane, centreZ)) return NotChecked("the field centre at that depth is outside the grid");
            double centre = dose.GetPointDose(centreX, plane, centreZ).Dose;
            if (!(centre > 0)) return NotChecked("no dose at the field centre at that depth");

            double step = Math.Min(dose.XRes, dose.ZRes);
            var (x1, x2) = Edges(dose, centre / 2, centreX, plane, centreZ, xAxis, step);
            var (y1, y2) = Edges(dose, centre / 2, centreX, plane, centreZ, yAxis, step);
            if (x1 is null || x2 is null || y1 is null || y2 is null) return NotChecked("a 50 % edge is outside the grid");
            // Edges are distances along each axis from the field centre; expected ones from the jaws, the same way.
            var edges = new[]
            {
                new FieldEdge("X1", xj[0] * scale, cu + x1.Value),
                new FieldEdge("X2", xj[1] * scale, cu + x2.Value),
                new FieldEdge("Y1", yj[0] * scale, cv + y1.Value),
                new FieldEdge("Y2", yj[1] * scale, cv + y2.Value),
            };
            return new JawEdgeResult(true, edges, null, note);

            static JawEdgeResult NotChecked(string why) => new(false, Array.Empty<FieldEdge>(), why);
            static bool Zero(double? a) => a is { } v && Math.Min(Math.Abs(v), Math.Abs(v - 360)) <= 0.1;
            static IReadOnlyList<double>? Pair(IReadOnlyDictionary<string, IReadOnlyList<double>> all, string type) =>
                all.TryGetValue(type, out var p) && p.Count == 2 ? p : null;
        }

        /// <summary>
        /// Why the MLC rather than the jaws defines the X edges, or null. An MLC field (QAEclipseDcf's closed-leaf
        /// transmission fields, for example) is the off-axis case jaws cannot see. Leaves are counted open above 0.5 mm,
        /// and the MLC defines an edge when its outermost open leaf sits more than the tolerance inside that jaw.
        /// </summary>
        private static string? MlcDefinesTheField(IReadOnlyDictionary<string, IReadOnlyList<double>> jaws, IReadOnlyList<double> x)
        {
            if (!jaws.TryGetValue("MLCX", out var leaves) || leaves.Count < 2 || leaves.Count % 2 != 0) return null;
            int n = leaves.Count / 2;   // bank A (X1 side) then bank B (X2 side)
            var open = Enumerable.Range(0, n).Where(i => leaves[n + i] - leaves[i] > 0.5).ToArray();
            if (open.Length == 0) return "MLC closed: the leaves, not the jaws, define the field";
            double a = open.Min(i => leaves[i]), b = open.Max(i => leaves[n + i]);
            return a > x[0] + ToleranceMm || b < x[1] - ToleranceMm ? "MLC-shaped: the leaves, not the jaws, define the field" : null;
        }

        /// <summary>The first y on the CAX column (nearest voxels to the isocentre's x and z) with dose above 1 % of the column's maximum.</summary>
        private static double? SurfaceFromDose(DoseMatrixOptimal g, Position3 iso)
        {
            int ix = (int)Math.Round((iso.X - g.X0) / g.XRes), iz = (int)Math.Round((iso.Z - g.Z0) / g.ZRes);
            if (ix < 0 || iz < 0 || ix >= g.DimensionX || iz >= g.DimensionZ) return null;
            int dx = g.DimensionX, plane = g.DimensionX * g.DimensionY;
            double max = 0;
            for (int y = 0; y < g.DimensionY; y++) max = Math.Max(max, g.DoseValues[ix + y * dx + iz * plane]);
            if (!(max > 0)) return null;
            for (int y = 0; y < g.DimensionY; y++)
                if (g.DoseValues[ix + y * dx + iz * plane] > 0.01 * max) return g.Y0 + y * g.YRes;
            return null;
        }

        /// <summary>
        /// The two 50 % points either side of the centre along one axis, as signed distances from it: walk out in
        /// steps until the dose falls below <paramref name="level"/>, then interpolate linearly.
        /// </summary>
        private static (double? Low, double? High) Edges(DoseMatrixOptimal g, double level, double cx, double y, double cz,
            (double X, double Z) axis, double step)
        {
            double? Walk(int direction)
            {
                double prev = 0, prevDose = Dose(0);
                for (double u = direction * step; ; u += direction * step)
                {
                    double x = cx + u * axis.X, z = cz + u * axis.Z;
                    if (!Inside(g, x, y, z)) return null;
                    double d = Dose(u);
                    if (d < level) return prev + (u - prev) * (prevDose - level) / (prevDose - d);
                    prev = u; prevDose = d;
                }
            }
            double Dose(double u) => g.GetPointDose(cx + u * axis.X, y, cz + u * axis.Z).Dose;
            return (Walk(-1), Walk(+1));
        }

        private static bool Inside(DoseMatrixOptimal g, double x, double y, double z) =>
            x >= g.X0 && x <= g.XMax && y >= g.Y0 && y <= g.YMax && z >= g.Z0 && z <= g.ZMax;
    }
}
