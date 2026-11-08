using System;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// How the patient - or, for commissioning, the phantom the planning system treats as one - was
    /// positioned. DICOM's <c>PatientPosition</c> (0018,5100) defined terms.
    /// <para>
    /// It matters because the DICOM patient coordinate system is defined <b>anatomically</b>: +x to
    /// the patient's left, +y posterior, +z toward the head. Those directions cannot be mapped onto a
    /// room, or onto a water tank, without knowing how the patient lay. Under head first supine a
    /// vertical beam travels along +y, so depth is +y; under head first prone the same beam travels
    /// along -y and a depth dose read as +y would come out backwards - plausible and silent.
    /// </para>
    /// <para>
    /// Measured: every setup in all nine reference plans is <see cref="HeadFirstSupine"/>. Cody,
    /// 2026-11-08, on why that is not a guarantee: "HFS is normally chosen because it's the planning
    /// system default." So it is the common case and not a safe assumption, which is why it is read
    /// and checked rather than presumed.
    /// </para>
    /// </summary>
    public enum PatientOrientation
    {
        Unknown = 0,
        HeadFirstSupine,
        HeadFirstProne,
        HeadFirstDecubitusRight,
        HeadFirstDecubitusLeft,
        FeetFirstSupine,
        FeetFirstProne,
        FeetFirstDecubitusRight,
        FeetFirstDecubitusLeft
    }

    /// <summary>A unit direction in the DICOM patient coordinate system.</summary>
    public readonly record struct PatientDirection(double X, double Y, double Z)
    {
        public static readonly PatientDirection PatientLeft = new(1, 0, 0);
        public static readonly PatientDirection PatientRight = new(-1, 0, 0);
        public static readonly PatientDirection Posterior = new(0, 1, 0);
        public static readonly PatientDirection Anterior = new(0, -1, 0);
        public static readonly PatientDirection TowardHead = new(0, 0, 1);
        public static readonly PatientDirection TowardFeet = new(0, 0, -1);

        public override string ToString() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0}, {1}, {2})", X, Y, Z);
    }

    public static class PatientOrientations
    {
        /// <summary>Reads a DICOM <c>PatientPosition</c> value. Anything unrecognised is Unknown.</summary>
        public static PatientOrientation Parse(string? patientPosition) =>
            (patientPosition ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "HFS" => PatientOrientation.HeadFirstSupine,
                "HFP" => PatientOrientation.HeadFirstProne,
                "HFDR" => PatientOrientation.HeadFirstDecubitusRight,
                "HFDL" => PatientOrientation.HeadFirstDecubitusLeft,
                "FFS" => PatientOrientation.FeetFirstSupine,
                "FFP" => PatientOrientation.FeetFirstProne,
                "FFDR" => PatientOrientation.FeetFirstDecubitusRight,
                "FFDL" => PatientOrientation.FeetFirstDecubitusLeft,
                _ => PatientOrientation.Unknown
            };

        public static string ToDicom(PatientOrientation orientation) => orientation switch
        {
            PatientOrientation.HeadFirstSupine => "HFS",
            PatientOrientation.HeadFirstProne => "HFP",
            PatientOrientation.HeadFirstDecubitusRight => "HFDR",
            PatientOrientation.HeadFirstDecubitusLeft => "HFDL",
            PatientOrientation.FeetFirstSupine => "FFS",
            PatientOrientation.FeetFirstProne => "FFP",
            PatientOrientation.FeetFirstDecubitusRight => "FFDR",
            PatientOrientation.FeetFirstDecubitusLeft => "FFDL",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Where a scan's three axes point in DICOM patient coordinates, given how the phantom was
    /// positioned and where the gantry was.
    /// <para>
    /// <b>Deliberately small.</b> This project compares RT Dose to RT Dose like for like, or RT Dose
    /// to profiles in matching geometry (Cody, 2026-11-08) - in both cases the two sides are already
    /// in the same frame. So nothing here rotates a dose grid or resamples one geometry into another:
    /// the only things needed are a compatibility check for the first case and an axis mapping for the
    /// second. A general transform engine would be larger, untestable against the files we have, and
    /// unused.
    /// </para>
    /// <para>
    /// Only head first supine at gantry zero is supported, because it is the only combination the
    /// reference data exercises. Everything else is refused by name rather than guessed, since a
    /// wrong axis mapping produces a curve that looks like a measurement and is not one.
    /// </para>
    /// </summary>
    public sealed record ScanGeometry(PatientOrientation Orientation, double GantryAngleDegrees)
    {
        /// <summary>Head first supine at gantry zero: the arrangement the reference plans all use.</summary>
        public static readonly ScanGeometry HeadFirstSupineGantryZero =
            new(PatientOrientation.HeadFirstSupine, 0.0);

        public bool IsSupported =>
            Orientation == PatientOrientation.HeadFirstSupine
            && Math.Abs(GantryAngleDegrees) <= 0.1;

        public string DescribeUnsupported()
        {
            if (IsSupported) return "nothing";
            if (Orientation != PatientOrientation.HeadFirstSupine)
                return $"patient orientation {Orientation} is not supported; only HeadFirstSupine is, "
                     + "because it is the only one the reference data exercises";
            return $"gantry angle {GantryAngleDegrees} is not supported; only 0 is, because the depth "
                 + "axis turns with the gantry and no reference file exercises another angle";
        }

        /// <summary>
        /// The direction a scan of this curve type runs, as a unit vector in patient coordinates.
        /// <para>
        /// For head first supine at gantry zero: crossplane is the patient's left-right axis so +x,
        /// inplane is gun-target so +z, and depth runs into the phantom which for a supine patient
        /// under a vertical beam is posterior, so +y. That is the convention swift's addendum of
        /// 2026-10-02 took from pymedphys, and it holds <b>because</b> of this orientation rather than
        /// in general.
        /// </para>
        /// </summary>
        public PatientDirection DirectionFor(CurveType curveType)
        {
            if (!IsSupported)
                throw new InvalidOperationException(DescribeUnsupported());

            return curveType switch
            {
                CurveType.Crossplane => PatientDirection.PatientLeft,
                CurveType.Inplane => PatientDirection.TowardHead,
                CurveType.Pdd => PatientDirection.Posterior,
                CurveType.PositiveDiagonal or CurveType.NegativeDiagonal =>
                    throw new NotSupportedException(
                        "A diagonal scan's direction is not yet defined. Whether diagonals are "
                        + "first-class is an open question (D17's list), and the angle convention has "
                        + "not been settled."),
                _ => throw new ArgumentOutOfRangeException(nameof(curveType),
                        $"No direction is defined for {curveType}.")
            };
        }

        public override string ToString() =>
            $"{PatientOrientations.ToDicom(Orientation)} at gantry {GantryAngleDegrees}";
    }
}
