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
            string? correctionDescription = null)
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

            return new Profile(Beam, Scan, scaled, Conditions, Source, type, Correction, CorrectionDescription);
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
                Normalization, DetectorGeometryCorrection.AppliedByUs, description);
        }

        public override string ToString() => $"{Beam} / {Scan} ({Points.Count} points, {Source})";
    }
}
