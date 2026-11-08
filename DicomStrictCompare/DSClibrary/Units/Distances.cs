using System;

namespace DSClibrary.Units
{
    /// <summary>
    /// The one place that holds policy about distances: how they are shown, and how finely they are
    /// quantised when they form part of a key.
    /// <para>
    /// The split here is deliberate. <see cref="DisplayUnit"/> and <see cref="DisplayDecimals"/> are
    /// settable because they are cosmetic - changing them changes a report and nothing else.
    /// <see cref="KeyQuantum"/> is a constant because it decides whether two measurements are the
    /// same measurement: if it could be changed at runtime, two keys built either side of the change
    /// would compare unequal while describing the same beam, and anything already in a dictionary
    /// would be unreachable.
    /// </para>
    /// <para>
    /// Being mutable global state, the display settings are for an application to set once at
    /// startup. Library code should never set them, and a test that does must put them back - the
    /// canonical storage in <see cref="Distance"/> does not depend on them, so nothing but formatting
    /// is at stake.
    /// </para>
    /// </summary>
    public static class Distances
    {
        private static DistanceUnit _displayUnit = DistanceUnit.Millimetres;
        private static int _displayDecimals = 2;

        /// <summary>
        /// The unit distances are rendered in. Defaults to millimetres, which is what the dose grids
        /// and the distance-to-agreement use, so a displayed number matches a compared one unless an
        /// application chooses otherwise.
        /// </summary>
        public static DistanceUnit DisplayUnit
        {
            get => _displayUnit;
            set => _displayUnit = Enum.IsDefined(typeof(DistanceUnit), value)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value));
        }

        public static int DisplayDecimals
        {
            get => _displayDecimals;
            set => _displayDecimals = value is >= 0 and <= 9
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), "Between 0 and 9 decimals.");
        }

        /// <summary>
        /// The grid that distances are rounded onto before they are compared as part of a
        /// <c>BeamKey</c> or <c>ScanKey</c>.
        /// <para>
        /// A scan recorded at SCAN_DEPTH=30.00 and a dose calculated at 30 mm need not agree to the
        /// bit, so keys need slack. A tolerance is the wrong way to provide it: tolerant equality is
        /// not transitive, so it cannot back a dictionary or a group-by. At half a millimetre of
        /// tolerance 30.0 matches 30.4 and 30.4 matches 30.8, yet 30.0 does not match 30.8, so which
        /// group a value joined would depend on the order the values arrived. Rounding on
        /// construction and then comparing exactly gives order-independent grouping, a working hash
        /// code, and one reviewable constant instead of a threshold at every call site.
        /// </para>
        /// </summary>
        public static readonly Distance KeyQuantum = Distance.FromMillimetres(0.5);

        /// <summary>Degrees. Angles are not yet a distinct type: see the note on
        /// <see cref="AngleQuantumDegrees"/>.</summary>
        public const double AngleQuantumDegrees = 0.1;

        /// <summary>MV or MeV.</summary>
        public const double EnergyQuantum = 0.01;

        public static string Suffix(DistanceUnit unit) => unit switch
        {
            DistanceUnit.Millimetres => "mm",
            DistanceUnit.Centimetres => "cm",
            DistanceUnit.Metres => "m",
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };

        /// <summary>Rounds a distance onto <see cref="KeyQuantum"/>.</summary>
        public static Distance Quantize(Distance value) => value.QuantizedTo(KeyQuantum);

        public static Distance? Quantize(Distance? value) =>
            value.HasValue ? Quantize(value.Value) : null;

        /// <summary>
        /// Rounds a plain number onto a grid. Used for the quantities that are not distances - angles
        /// in degrees and energies - which have no unit ambiguity in any format read so far, and so
        /// have not earned a value type of their own. Distances earned one: the same quantity arrives
        /// as 1000 from PTW and 100 from Sun Nuclear, and a mistake there has already been made once.
        /// </summary>
        public static double Round(double value, double step)
        {
            if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step), "Step must be positive.");
            if (double.IsNaN(value) || double.IsInfinity(value)) return value;
            return Math.Round(Math.Round(value / step, MidpointRounding.AwayFromZero) * step, 6) + 0.0;
        }

        public static double? Round(double? value, double step) =>
            value.HasValue ? Round(value.Value, step) : null;

        public static double? Angle(double? value) => Round(value, AngleQuantumDegrees);

        public static double? Energy(double? value) => Round(value, EnergyQuantum);
    }
}
