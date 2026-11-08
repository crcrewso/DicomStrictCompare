using System.Collections.Generic;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// Identifies the beam a measurement was made in: what was irradiated and how. It is the outer
    /// level of a profile's address; <see cref="ScanKey"/> is the inner one.
    /// <para>
    /// Distances are <see cref="Distance"/>, so no caller can be wrong about a unit, and every one is
    /// quantised onto <see cref="Distances.KeyQuantum"/> on construction. Two measurements of the same
    /// beam therefore compare equal and hash alike however their files spelled the numbers - which is
    /// what lets this type be a dictionary key.
    /// </para>
    /// <para>
    /// Fields are nullable because some formats do not carry them. A Sun Nuclear prm records its
    /// collimator as "Left: 0 Right: 0 Top: 0 Bottom: 0 cm" for a 10x10 field and its energy as
    /// "Undefined", so field size and energy are genuinely absent there. Absent is null and
    /// <see cref="IsComplete"/> is false; it is never a zero standing in for a real number.
    /// </para>
    /// </summary>
    public sealed record BeamKey
    {
        public Modality Modality { get; }
        public double? Energy { get; }
        public bool? IsFff { get; }
        public Distance? FieldInplane { get; }
        public Distance? FieldCrossplane { get; }
        public Distance? Ssd { get; }
        public double? GantryAngleDegrees { get; }
        public double? CollimatorAngleDegrees { get; }
        public double? WedgeAngleDegrees { get; }

        public BeamKey(
            Modality modality = Modality.Unknown,
            double? energy = null,
            bool? isFff = null,
            Distance? fieldInplane = null,
            Distance? fieldCrossplane = null,
            Distance? ssd = null,
            double? gantryAngleDegrees = null,
            double? collimatorAngleDegrees = null,
            double? wedgeAngleDegrees = null)
        {
            Modality = modality;
            Energy = Distances.Energy(energy);
            IsFff = isFff;
            FieldInplane = Distances.Quantize(fieldInplane);
            FieldCrossplane = Distances.Quantize(fieldCrossplane);
            Ssd = Distances.Quantize(ssd);
            GantryAngleDegrees = Distances.Angle(gantryAngleDegrees);
            CollimatorAngleDegrees = Distances.Angle(collimatorAngleDegrees);
            WedgeAngleDegrees = Distances.Angle(wedgeAngleDegrees);
        }

        /// <summary>
        /// True when every field needed to pair a measurement with a calculated dose is present. A
        /// profile whose key is incomplete cannot be grouped by beam and belongs in
        /// <see cref="MeasurementSet.Unresolved"/>.
        /// </summary>
        public bool IsComplete =>
            Modality != Modality.Unknown
            && Energy.HasValue
            && FieldInplane.HasValue
            && FieldCrossplane.HasValue
            && Ssd.HasValue;

        /// <summary>Names the fields that are missing, for a message a user can act on.</summary>
        public string DescribeMissing()
        {
            var missing = new List<string>();
            if (Modality == Modality.Unknown) missing.Add(nameof(Modality));
            if (!Energy.HasValue) missing.Add(nameof(Energy));
            if (!FieldInplane.HasValue) missing.Add(nameof(FieldInplane));
            if (!FieldCrossplane.HasValue) missing.Add(nameof(FieldCrossplane));
            if (!Ssd.HasValue) missing.Add(nameof(Ssd));
            return missing.Count == 0 ? "nothing" : string.Join(", ", missing);
        }

        public override string ToString()
        {
            string energy = Energy.HasValue
                ? Energy.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                  + (Modality == Modality.Electron ? "MeV" : "MV")
                  + (IsFff == true ? " FFF" : string.Empty)
                : "energy?";
            string field = FieldInplane.HasValue && FieldCrossplane.HasValue
                ? $"{FieldInplane.Value} x {FieldCrossplane.Value}"
                : "field?";
            return $"{Modality} {energy} {field}";
        }
    }
}
