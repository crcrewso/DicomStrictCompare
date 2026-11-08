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
        /// The dose grid, <b>in centigray</b> for an absolute volume (Cody, 2026-11-08: "DoseVolume
        /// should store the values in cGy inherently"). Named Grid rather than Dose because
        /// <c>volume.Dose.Dose</c> read badly.
        /// <para>
        /// <b>Scaled once, never per voxel.</b> The conversion from stored integers happens in a
        /// single traversal when the file is read, not on each access. X86Mathematics calls
        /// GetPointDose eight times per evaluated point - source, target and six DTA neighbours - so
        /// at 8,080,200 voxels that is about 65 million reads per comparison, and a multiply plus a
        /// bounds-checked decode on each would be real cost for no benefit. Scaling once is also what
        /// removes the double-scaling trap: a grid already in dose units has no stray factor left to
        /// apply by accident.
        /// </para>
        /// <para>
        /// Doubles rather than anything wider. The source carries at most about seven significant
        /// figures - the reference grids reach 2,904,474 of the 32-bit range - against a double's
        /// fifteen or sixteen, so there is no precision to be gained; C# has no binary128, and decimal
        /// is a decimal format with no hardware support and would not work with the existing engine.
        /// </para>
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

        /// <summary>
        /// Takes a grid whose values are <b>already in centigray</b>. Private, so that every caller
        /// goes through a factory that names the unit it is handing over - the same principle as
        /// <see cref="Dose.FromGray"/>, and the thing that prevents a silent factor of a hundred.
        /// </summary>
        private DoseVolume(
            BeamKey beam,
            DoseMatrixOptimal grid,
            ValueScale scale,
            double? monitorUnits,
            MeasurementConditions? conditions,
            Provenance? source)
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

        /// <summary>
        /// For a grid whose values are already centigray - what this library's own DICOM reader
        /// produces, converting in the same pass that decodes the voxels.
        /// </summary>
        public static DoseVolume FromCentigrayGrid(
            BeamKey beam,
            DoseMatrixOptimal grid,
            ValueScale scale = ValueScale.AbsoluteCentigray,
            double? monitorUnits = null,
            MeasurementConditions? conditions = null,
            Provenance? source = null) =>
            new(beam, grid, scale, monitorUnits, conditions, source);

        /// <summary>
        /// For a grid whose values are in gray - which is what EvilDICOM returns, since it applies
        /// <c>DoseGridScaling</c> on read. Allocates a converted copy, so prefer the centigray
        /// factory where the values can be produced in centigray in the first place.
        /// <para>
        /// This exists because mixing a gray grid with a centigray one in a single comparison would be
        /// a hundredfold error that nothing would report, and the only reliable guard is to make every
        /// caller state the unit it holds.
        /// </para>
        /// </summary>
        public static DoseVolume FromGrayGrid(
            BeamKey beam,
            DoseMatrixOptimal grayGrid,
            ValueScale scale = ValueScale.AbsoluteCentigray,
            double? monitorUnits = null,
            MeasurementConditions? conditions = null,
            Provenance? source = null)
        {
            if (grayGrid is null) throw new ArgumentNullException(nameof(grayGrid));

            var centigray = new double[grayGrid.DoseValues.Length];
            for (int i = 0; i < centigray.Length; i++)
                centigray[i] = grayGrid.DoseValues[i] * 100.0;

            var converted = new DoseMatrixOptimal(
                grayGrid.DimensionX, grayGrid.DimensionY, grayGrid.DimensionZ,
                grayGrid.X0, grayGrid.Y0, grayGrid.Z0,
                grayGrid.XRes, grayGrid.YRes, grayGrid.ZRes,
                centigray);

            return new DoseVolume(beam, converted, scale, monitorUnits, conditions, source);
        }

        public bool IsAbsolute => Scale == ValueScale.AbsoluteCentigray;

        /// <summary>
        /// The maximum dose. The grid is already centigray, so this reads it rather than converting.
        /// Null for a relative volume, whose maximum is a percentage and not a dose - see
        /// <see cref="MaximumRelativePercent"/>.
        /// </summary>
        public Dose? MaximumDose =>
            IsAbsolute ? Dose.FromCentigray(Grid.MaxPointDose.Dose) : null;

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
