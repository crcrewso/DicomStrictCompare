using DSClibrary.Results.DICOM;

namespace DSClibrary.Comparing
{
    /// <summary>
    /// The dose the threshold and the global tolerance are fractions of. Cody, 2026-11-15: a "Max" setting of
    /// Global Max or CAX, CAX decided by the jaw settings alone, falling back to Global Max.
    /// </summary>
    public enum MaximumKind
    {
        /// <summary>The source grid's volume maximum: the behaviour before this setting, and the default.</summary>
        GlobalMax,
        /// <summary>
        /// Where the source plan's jaws say the field is on axis, the CAX maximum (static beams) or the dose about the
        /// isocentre (arcs), each a 3 x 3 average; otherwise Global Max.
        /// </summary>
        CaxMax,
    }

    /// <summary>
    /// The maximum a comparison used, what it was, and why, so every result can say. See design/comparison-studies.md, F10.
    /// </summary>
    public sealed record ReferenceMaximum(double Dose, MaximumKind Used, string Note)
    {
        /// <summary>Field edges further apart than this from the CAX, on either axis, make a field off axis.</summary>
        public const double OffAxisToleranceMm = 10.0;

        public static ReferenceMaximum Global(DoseMatrixOptimal source, string note = "volume maximum") =>
            new(source.MaxPointDose.Dose, MaximumKind.GlobalMax, note);

        /// <summary>The maximum to use for <paramref name="source"/>, whose plan beam is <paramref name="beam"/> if known.</summary>
        public static ReferenceMaximum For(DoseMatrixOptimal source, RTPlanBeam? beam, MaximumKind wanted)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (wanted == MaximumKind.GlobalMax) return Global(source);

            string? why = beam is null ? "no plan" : WhyNotCax(beam);
            if (why is not null) return Global(source, $"CAX not used ({why}); volume maximum");

            var iso = beam!.IsocenterPosition;
            var at = new Position3(iso[0], iso[1], iso[2]);
            // An arc's axis turns about the isocentre, so it is normalised there (Cody, 2026-11-15).
            var value = beam.IsArc ? IsocentreAverage(source, at) : CaxMaximum(source, at);
            if (value is null)
                return Global(source, $"CAX not used (the {(beam.IsArc ? "isocentre" : "central axis")} is not inside the grid); volume maximum");
            return new ReferenceMaximum(value.Value, MaximumKind.CaxMax,
                beam.IsArc ? "arc: 3 x 3 average about the isocentre" : "CAX maximum, 3 x 3 average");
        }

        /// <summary>Why a beam's CAX cannot be used, or null when it can. The jaws alone decide on- or off-axis.</summary>
        public static string? WhyNotCax(RTPlanBeam beam)
        {
            if (beam.IsocenterPosition.Count < 3) return "no isocentre in the plan";
            if (!beam.IsArc)
            {
                // A static beam's CAX column runs along +y only at gantry 0 and couch 0.
                if (!NearZero(beam.GantryAngle)) return $"gantry {beam.GantryAngle}: only 0 is supported";
                if (!NearZero(beam.PatientSupportAngle)) return $"couch {beam.PatientSupportAngle}: only 0 is supported";
            }
            var jaws = beam.LeafJawPositions;
            return JawsOffAxis(Pair(jaws, "ASYMX") ?? Pair(jaws, "X"), Pair(jaws, "ASYMY") ?? Pair(jaws, "Y"));

            static IReadOnlyList<double>? Pair(IReadOnlyDictionary<string, IReadOnlyList<double>> all, string type) =>
                all.TryGetValue(type, out var p) && p.Count == 2 ? p : null;
            static bool NearZero(double? angle) =>
                angle is { } a && Math.Min(Math.Abs(a), Math.Abs(a - 360)) <= 0.1;
        }

        /// <summary>
        /// Null when both jaw pairs are on axis, otherwise why not. A pair is [jaw 1, jaw 2] in millimetres at the
        /// isocentre, jaw 1 normally negative. Off axis: the two edges' distances from the CAX differ by more than
        /// <see cref="OffAxisToleranceMm"/>, or the CAX lies outside the jaws.
        /// </summary>
        public static string? JawsOffAxis(IReadOnlyList<double>? x, IReadOnlyList<double>? y)
        {
            if (x is null || y is null) return "no X or Y jaw positions in the plan";
            return Check("X", x) ?? Check("Y", y);

            static string? Check(string axis, IReadOnlyList<double> p)
            {
                double near = -p[0], far = p[1];   // distances from the CAX to each edge
                if (near < 0 || far < 0) return $"{axis} jaws {p[0]}, {p[1]} mm do not contain the CAX";
                if (Math.Abs(near - far) > OffAxisToleranceMm)
                    return $"{axis} jaws {p[0]}, {p[1]} mm are off axis by more than {OffAxisToleranceMm} mm";
                return null;
            }
        }

        /// <summary>
        /// The dose about the isocentre: the voxel nearest it, averaged with its 8 neighbours in the same y plane (the
        /// same 3 x 3 block as <see cref="CaxMaximum"/>). Null when that block falls outside the grid.
        /// </summary>
        public static double? IsocentreAverage(DoseMatrixOptimal g, Position3 isocentre)
        {
            if (g is null) throw new ArgumentNullException(nameof(g));
            int ix = (int)Math.Round((isocentre.X - g.X0) / g.XRes), iy = (int)Math.Round((isocentre.Y - g.Y0) / g.YRes);
            int iz = (int)Math.Round((isocentre.Z - g.Z0) / g.ZRes);
            if (ix < 1 || iz < 1 || iy < 0 || ix > g.DimensionX - 2 || iz > g.DimensionZ - 2 || iy > g.DimensionY - 1) return null;
            return Block(g, ix, iy, iz);
        }

        // The 3 x 3 block across the y axis, centred on (ix, iy, iz).
        private static double Block(DoseMatrixOptimal g, int ix, int iy, int iz)
        {
            int dx = g.DimensionX, plane = g.DimensionX * g.DimensionY;
            double sum = 0;
            for (int z = iz - 1; z <= iz + 1; z++)
                for (int x = ix - 1; x <= ix + 1; x++)
                    sum += g.DoseValues[x + iy * dx + z * plane];
            return sum / 9;
        }

        /// <summary>
        /// The CAX maximum for a beam along +y (gantry 0, head-first supine): the largest voxel on the column through
        /// the isocentre's x and z, averaged with its 8 neighbours in the same y plane (a 3 x 3 block across the axis).
        /// Null when that column or its neighbours fall outside the grid.
        /// </summary>
        public static double? CaxMaximum(DoseMatrixOptimal g, Position3 isocentre)
        {
            if (g is null) throw new ArgumentNullException(nameof(g));
            int ix = (int)Math.Round((isocentre.X - g.X0) / g.XRes), iz = (int)Math.Round((isocentre.Z - g.Z0) / g.ZRes);
            if (ix < 1 || iz < 1 || ix > g.DimensionX - 2 || iz > g.DimensionZ - 2) return null;
            int dx = g.DimensionX, plane = g.DimensionX * g.DimensionY;
            var v = g.DoseValues;
            int best = 0;
            for (int y = 1; y < g.DimensionY; y++)
                if (v[ix + y * dx + iz * plane] > v[ix + best * dx + iz * plane]) best = y;
            return Block(g, ix, best, iz);
        }
    }
}
