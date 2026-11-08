using System;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A calculated three dimensional dose for one beam, sitting in the same collection as the
    /// measurements it will be compared against.
    /// <para>
    /// A volume is not a set of profiles and cannot enumerate them: it yields one for any line asked
    /// of it. That sampling - returning a <see cref="Profile"/> so measured and calculated are the
    /// same type and one code path compares them - is the next piece of work and is deliberately not
    /// here yet. It needs the phantom geometry convention settled first, and a wrong geometry would
    /// produce confident nonsense.
    /// </para>
    /// </summary>
    public sealed class DoseVolume
    {
        public BeamKey Beam { get; }

        /// <summary>
        /// The dose grid as the existing engine holds it. Named Grid rather than Dose because its
        /// values are in <b>gray</b>, not this project's canonical centigray: EvilDICOM applies
        /// <c>DoseGridScaling</c> when it reads and hands back gray, and the comparison engine in
        /// X86Mathematics works in those units. The conversion to centigray happens at this type's
        /// boundary, in <see cref="MaximumDose"/> and in anything else that reports a dose.
        /// <para>
        /// <b>Do not multiply by <c>DoseMatrixOptimal.Scaling</c>.</b> Measured 2026-11-08: the values
        /// are already scaled - the maximum of <c>RD.UnitTest.P1Ref.X-100A-10.0-1</c> reads 1.017791
        /// Gy against a stored integer maximum of 2,904,474 and a factor of 3.5042182425e-07 - and
        /// that property carries the factor without it being applied to anything. Applying it again
        /// would scale every dose by about three ten-millionths and nothing would complain.
        /// </para>
        /// </summary>
        public DoseMatrixOptimal Grid { get; }

        /// <summary>
        /// Whether this volume's values are absolute centigray or a relative percentage. Required:
        /// there is no sensible default, and reading one as the other is silent.
        /// <see cref="ValueScale.ArbitraryUnits"/> is refused - a calculated dose always has units.
        /// </summary>
        public ValueScale Scale { get; }

        /// <summary>
        /// The monitor units of the delivery this dose was calculated for, where the source provides
        /// them - from an RT Plan's <c>BeamMeterset</c>.
        /// <para>
        /// <b>Recorded, never applied</b> (Cody, 2026-11-08): "MU should be a parameter of the
        /// DoseVolume, if it exists, but should not be used to change the dose." A dose from a plan is
        /// the dose that plan calculated; MU is metadata about the delivery that produced it.
        /// </para>
        /// <para>
        /// In particular the convention that 1 MU gives 1 cGy at the depth of maximum dose is a
        /// calibration a site adopts, not a property of the data, so nothing derives dose from MU or
        /// MU from dose. Measured, as an illustration: the 100 MU beam in
        /// <c>RD.UnitTest.P1Ref.X-100A-10.0-1</c> peaks at 101.7791 cGy, so this dataset sits at
        /// 1.018 cGy/MU rather than 1.000.
        /// </para>
        /// </summary>
        public double? MonitorUnits { get; }

        public MeasurementConditions Conditions { get; }
        public Provenance Source { get; }

        public DoseVolume(
            BeamKey beam,
            DoseMatrixOptimal grid,
            ValueScale scale,
            double? monitorUnits = null,
            MeasurementConditions? conditions = null,
            Provenance? source = null)
        {
            Beam = beam ?? throw new ArgumentNullException(nameof(beam));
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (!Enum.IsDefined(typeof(ValueScale), scale))
                throw new ArgumentOutOfRangeException(nameof(scale), "A dose volume must state its scale.");
            if (scale == ValueScale.ArbitraryUnits)
                throw new ArgumentOutOfRangeException(nameof(scale),
                    "A calculated dose is never in arbitrary units - that is what an uncalibrated detector produces. "
                    + "Use AbsoluteCentigray or RelativePercent.");
            Scale = scale;
            MonitorUnits = monitorUnits;
            Conditions = conditions ?? MeasurementConditions.Empty;
            Source = source ?? Provenance.Unknown;
        }

        public bool IsAbsolute => Scale == ValueScale.AbsoluteCentigray;

        /// <summary>
        /// The maximum dose, converted from the grid's gray to canonical centigray. Null for a
        /// relative volume, whose maximum is a percentage and not a dose - see
        /// <see cref="MaximumRelativePercent"/>.
        /// </summary>
        public Dose? MaximumDose =>
            IsAbsolute ? Dose.FromGray(Grid.MaxPointDose.Dose) : null;

        /// <summary>The maximum value of a relative volume, as a percentage. Null for an absolute one.</summary>
        public double? MaximumRelativePercent =>
            IsAbsolute ? null : Grid.MaxPointDose.Dose;

        /// <summary>
        /// Whether this volume can be compared with another. Two volumes in different scales cannot
        /// be: converting between absolute and relative needs a normalisation point that only the user
        /// knows, so the mismatch is an error to report rather than something to paper over.
        /// </summary>
        public bool IsComparableWith(DoseVolume other) =>
            other is not null && other.Scale == Scale;

        public override string ToString()
        {
            string mu = MonitorUnits.HasValue
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture, ", {0:0.##} MU", MonitorUnits.Value)
                : string.Empty;
            return $"{Beam} dose ({Scale}{mu}, {Source})";
        }
    }
}
