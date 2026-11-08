using System;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A calculated three dimensional dose for one beam, sitting in the same collection as the
    /// measurements it will be compared against.
    /// <para>
    /// A volume is not a set of profiles and cannot enumerate them: it yields one for any line asked
    /// of it. That sampling - returning a <see cref="Profile"/> for a given
    /// <see cref="BeamKey"/>/<see cref="ScanKey"/>, so that measured and calculated are the same type
    /// and one code path compares them - is the next piece of work and is deliberately not here yet.
    /// It needs the phantom geometry convention settled (surface origin and orientation) before it
    /// can be right, and a wrong geometry would produce confident nonsense.
    /// </para>
    /// </summary>
    public sealed class DoseVolume
    {
        public BeamKey Beam { get; }
        public DoseMatrixOptimal Dose { get; }
        public MeasurementConditions Conditions { get; }
        public Provenance Source { get; }

        public DoseVolume(
            BeamKey beam,
            DoseMatrixOptimal dose,
            MeasurementConditions? conditions = null,
            Provenance? source = null)
        {
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            Dose = dose ?? throw new ArgumentNullException(nameof(dose));
            Conditions = conditions ?? MeasurementConditions.Empty;
            Source = source ?? Provenance.Unknown;
        }

        public override string ToString() => $"{Beam} dose ({Source})";
    }
}
