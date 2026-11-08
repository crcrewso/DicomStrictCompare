using System;
using System.Collections.Generic;
using System.Linq;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A one dimensional dose curve, free of any vendor's nomenclature. A measured scan and a curve
    /// sampled out of a calculated dose are both this type, which is what lets a comparison run
    /// without knowing which side came from a detector.
    /// <para>
    /// Values are kept as the source gave them. Normalisation and detector correction return new
    /// instances rather than changing this one, so the same scan can be used raw for an absolute
    /// comparison and normalised for a shape comparison without being read twice.
    /// </para>
    /// </summary>
    public sealed class Profile
    {
        /// <summary>Points in ascending position order. A scan measured in the other direction is
        /// reversed on construction, so order is an invariant of this type rather than of its source.</summary>
        public IReadOnlyList<ProfilePoint> Points { get; }

        public BeamKey Beam { get; }
        public ScanKey Scan { get; }
        public MeasurementConditions Conditions { get; }
        public Provenance Source { get; }
        public NormalizationType Normalization { get; }
        public DetectorGeometryCorrection Correction { get; }

        /// <summary>
        /// What this profile's values mean. A water tank scan normally arrives as
        /// <see cref="ValueScale.ArbitraryUnits"/> - measured in the committed PTW file,
        /// <c>MEAS_UNIT=A.U.</c> with <c>DETECTOR_IS_CALIBRATED=0</c> - while a curve sampled out of a
        /// calculated dose carries that dose's scale.
        /// <para>
        /// It is read from the source rather than assumed, because some vendors and devices may provide
        /// absolutely calibrated output - Cody, 2026-11-08, adding immediately that he has <b>not yet
        /// seen</b> one. So the possibility is representable and is established per file, never by
        /// category; it is not a path anything here has exercised.
        /// <para>
        /// What is measured: the Sun Nuclear prm header has <c>Dose Per Count</c>, <c>Dose</c> and
        /// <c>Absolute Calibration</c> <i>fields</i>. Whether any file fills them with a usable
        /// calibration is unverified - the field names existing is not the same claim.
        /// </para>
        /// </para>
        /// </summary>
        public ValueScale ValueScale { get; }

        /// <summary>What correction was applied, when <see cref="Correction"/> is
        /// <see cref="DetectorGeometryCorrection.AppliedByUs"/>. Null otherwise.</summary>
        public string? CorrectionDescription { get; }

        public Profile(
            BeamKey beam,
            ScanKey scan,
            IEnumerable<ProfilePoint> points,
            MeasurementConditions? conditions = null,
            Provenance? source = null,
            NormalizationType normalization = NormalizationType.AsMeasured,
            DetectorGeometryCorrection correction = DetectorGeometryCorrection.Unknown,
            string? correctionDescription = null,
            ValueScale valueScale = ValueScale.ArbitraryUnits)
        {
            if (beam is null) throw new ArgumentNullException(nameof(beam));
            if (scan is null) throw new ArgumentNullException(nameof(scan));
            if (points is null) throw new ArgumentNullException(nameof(points));

            Beam = beam;
            Scan = scan;
            Points = points.OrderBy(p => p.Position).ToArray();
            Conditions = conditions ?? MeasurementConditions.Empty;
            Source = source ?? Provenance.Unknown;
            Normalization = normalization;
            Correction = correction;
            CorrectionDescription = correctionDescription;
            ValueScale = Enum.IsDefined(typeof(ValueScale), valueScale)
                ? valueScale
                : throw new ArgumentOutOfRangeException(nameof(valueScale));

            if (Points.Count == 0)
                throw new ArgumentException("A profile must have at least one point.", nameof(points));
        }

        public double MaximumValue => Points.Max(p => p.Value);

        /// <summary>The span the profile covers.</summary>
        public (Distance Min, Distance Max) PositionRange => (Points[0].Position, Points[^1].Position);

        /// <summary>
        /// The value at an arbitrary position, by linear interpolation between the two neighbouring
        /// points. Throws outside the measured range rather than extrapolating, because extrapolating
        /// a penumbra produces a plausible number that means nothing.
        /// </summary>
        public double ValueAt(Distance position)
        {
            var (min, max) = PositionRange;
            if (position < min || position > max)
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"{position} is outside the measured range {min} to {max}.");

            for (int i = 1; i < Points.Count; i++)
            {
                if (Points[i].Position < position) continue;

                ProfilePoint lo = Points[i - 1], hi = Points[i];
                Distance span = hi.Position - lo.Position;
                if (span <= Distance.Zero) return hi.Value;
                double t = (position - lo.Position) / span;
                return lo.Value + t * (hi.Value - lo.Value);
            }

            return Points[^1].Value;
        }

        /// <summary>
        /// Returns a copy scaled by the given rule. Normalising an already normalised profile is
        /// refused: the second scaling would silently be relative to the first.
        /// </summary>
        public Profile Normalize(NormalizationType type)
        {
            if (Normalization != NormalizationType.AsMeasured)
                throw new InvalidOperationException(
                    $"This profile is already normalized ({Normalization}). Normalize the as-measured profile instead.");

            double divisor = type switch
            {
                NormalizationType.AsMeasured => 1.0,
                NormalizationType.Maximum => MaximumValue,
                NormalizationType.CentralAxis => ValueAt(Distance.Zero),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };

            if (divisor == 0.0)
                throw new InvalidOperationException($"Cannot normalize to {type}: the reference value is zero.");

            var scaled = Points
                .Select(p => new ProfilePoint(p.Position, p.Value / divisor,
                    p.ReferenceValue.HasValue ? p.ReferenceValue.Value / divisor : null))
                .ToArray();

            return new Profile(Beam, Scan, scaled, Conditions, Source, type, Correction, CorrectionDescription, ValueScale);
        }

        /// <summary>
        /// Returns a copy with a detector geometry correction applied, recording that it was applied
        /// by us and what it was.
        /// <para>
        /// Refuses a profile that is already corrected, whether by the source or by us. That refusal
        /// is the reason the state is an enum rather than a bool: correcting twice is not detectable
        /// from the values afterwards, so it has to be prevented beforehand. A profile whose state is
        /// <see cref="DetectorGeometryCorrection.Unknown"/> may be corrected, because the caller may
        /// know what the file did not say - the result records that it started from unknown.
        /// </para>
        /// </summary>
        public Profile WithDetectorCorrection(IDetectorCorrection correction)
        {
            if (correction is null) throw new ArgumentNullException(nameof(correction));

            if (Correction is DetectorGeometryCorrection.AppliedBySource or DetectorGeometryCorrection.AppliedByUs)
                throw new InvalidOperationException(
                    $"This profile is already corrected for detector geometry ({Correction}). Applying another would double-correct it.");

            string description = Correction == DetectorGeometryCorrection.Unknown
                ? correction.Describe() + " (prior state was unknown)"
                : correction.Describe();

            return new Profile(Beam, Scan, correction.Apply(Points), Conditions, Source,
                Normalization, DetectorGeometryCorrection.AppliedByUs, description, ValueScale);
        }

        /// <summary>
        /// Whether this profile can be compared with another.
        /// <para>
        /// Cody, 2026-11-08: "for the most part profiles are treated as relative, and only RD to RD
        /// comparisons will be looking for absolute dose comparisons." So two curves in the same scale
        /// may be compared as they are, and curves in different scales only once both have been
        /// normalised - at which point the scale no longer matters, because each is a fraction of its
        /// own reference.
        /// </para>
        /// <para>
        /// This is what stops an uncalibrated detector reading of 1.7474 being compared against 101.78
        /// cGy and reported as a 99.98% failure. It is a type-level fact rather than a thing to
        /// remember.
        /// </para>
        /// </summary>
        public bool IsComparableWith(Profile other)
        {
            if (other is null) return false;
            if (ValueScale == other.ValueScale) return true;
            return Normalization != NormalizationType.AsMeasured
                   && other.Normalization != NormalizationType.AsMeasured;
        }

        /// <summary>Why <see cref="IsComparableWith"/> said no, for a message a user can act on.</summary>
        public string DescribeIncomparability(Profile other)
        {
            if (other is null) return "the other profile is null";
            if (IsComparableWith(other)) return "nothing";
            return $"this profile is in {ValueScale} and the other in {other.ValueScale}; "
                 + "normalize both before comparing, since values in different scales have no common zero";
        }

        public override string ToString() =>
            $"{Beam} / {Scan} ({Points.Count} points, {ValueScale}, {Source})";
    }
}
