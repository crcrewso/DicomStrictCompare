using System;
using System.Globalization;

namespace DSClibrary.Units
{
    public enum DistanceUnit
    {
        Millimetres,
        Centimetres,
        Metres
    }

    /// <summary>
    /// A length, stored in one canonical unit and carrying no opinion about how it was read or how it
    /// will be shown.
    /// <para>
    /// It exists to decouple three things that were previously coupled by naming convention alone:
    /// <list type="bullet">
    /// <item><b>Parsing</b> states the unit it found. A PTW mcc says SSD=1000.00 in millimetres and a
    /// Sun Nuclear prm says SSD: 100 in centimetres; the readers call
    /// <see cref="FromMillimetres"/> and <see cref="FromCentimetres"/> respectively and both produce
    /// the same length. No reader has to know what the rest of the library prefers.</item>
    /// <item><b>Storage</b> is canonical and private. Nothing downstream can be wrong about the unit
    /// because nothing downstream sees a bare number.</item>
    /// <item><b>Display</b> is chosen at the point of display, from <see cref="Distances"/>, so a
    /// report can be in centimetres without a single stored value changing.</item>
    /// </list>
    /// </para>
    /// <para>
    /// The project has already been bitten by the alternative: the 2024 PTW parser divided SSD and
    /// both field sizes by ten into centimetres, leaving centimetre values beside a dose grid and a
    /// distance-to-agreement in millimetres, with nothing in the types to notice.
    /// </para>
    /// </summary>
    public readonly struct Distance : IEquatable<Distance>, IComparable<Distance>, IFormattable
    {
        private readonly double _millimetres;

        private Distance(double millimetres)
        {
            if (double.IsNaN(millimetres) || double.IsInfinity(millimetres))
                throw new ArgumentOutOfRangeException(nameof(millimetres),
                    "A distance must be finite. An absent distance is null, not NaN.");

            // Adding zero turns -0.0 into +0.0. double.Equals treats those as unequal and record
            // equality uses Equals, so an off-axis offset of -0.0 would never match one of 0.0.
            _millimetres = millimetres + 0.0;
        }

        public static readonly Distance Zero = new(0.0);

        public static Distance FromMillimetres(double value) => new(value);

        public static Distance FromCentimetres(double value) => new(value * 10.0);

        public static Distance FromMetres(double value) => new(value * 1000.0);

        public static Distance From(double value, DistanceUnit unit) => unit switch
        {
            DistanceUnit.Millimetres => FromMillimetres(value),
            DistanceUnit.Centimetres => FromCentimetres(value),
            DistanceUnit.Metres => FromMetres(value),
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };

        public static Distance? FromMillimetres(double? value) =>
            value.HasValue ? FromMillimetres(value.Value) : null;

        public static Distance? FromCentimetres(double? value) =>
            value.HasValue ? FromCentimetres(value.Value) : null;

        public double Millimetres => _millimetres;

        public double Centimetres => _millimetres / 10.0;

        public double Metres => _millimetres / 1000.0;

        public double In(DistanceUnit unit) => unit switch
        {
            DistanceUnit.Millimetres => Millimetres,
            DistanceUnit.Centimetres => Centimetres,
            DistanceUnit.Metres => Metres,
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };

        public Distance Abs() => new(Math.Abs(_millimetres));

        /// <summary>
        /// Rounds onto a grid of the given step, so that keys built from measured distances can use
        /// exact equality. See <see cref="Distances.KeyQuantum"/> for why that is preferred to
        /// comparing with a tolerance.
        /// </summary>
        public Distance QuantizedTo(Distance step)
        {
            if (step._millimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(step), "Step must be positive.");

            double steps = Math.Round(_millimetres / step._millimetres, MidpointRounding.AwayFromZero);
            return new Distance(Math.Round(steps * step._millimetres, 6));
        }

        public static Distance operator +(Distance a, Distance b) => new(a._millimetres + b._millimetres);

        public static Distance operator -(Distance a, Distance b) => new(a._millimetres - b._millimetres);

        public static Distance operator -(Distance a) => new(-a._millimetres);

        public static Distance operator *(Distance a, double factor) => new(a._millimetres * factor);

        public static Distance operator *(double factor, Distance a) => new(a._millimetres * factor);

        public static Distance operator /(Distance a, double divisor) => new(a._millimetres / divisor);

        public static double operator /(Distance a, Distance b) => a._millimetres / b._millimetres;

        public static bool operator <(Distance a, Distance b) => a._millimetres < b._millimetres;

        public static bool operator >(Distance a, Distance b) => a._millimetres > b._millimetres;

        public static bool operator <=(Distance a, Distance b) => a._millimetres <= b._millimetres;

        public static bool operator >=(Distance a, Distance b) => a._millimetres >= b._millimetres;

        public static bool operator ==(Distance a, Distance b) => a.Equals(b);

        public static bool operator !=(Distance a, Distance b) => !a.Equals(b);

        public bool Equals(Distance other) => _millimetres.Equals(other._millimetres);

        public override bool Equals(object? obj) => obj is Distance other && Equals(other);

        public override int GetHashCode() => _millimetres.GetHashCode();

        public int CompareTo(Distance other) => _millimetres.CompareTo(other._millimetres);

        /// <summary>Formats in the unit <see cref="Distances"/> currently prefers for display.</summary>
        public override string ToString() => ToString(Distances.DisplayUnit);

        public string ToString(DistanceUnit unit) =>
            string.Format(CultureInfo.CurrentCulture, "{0} {1}",
                Math.Round(In(unit), Distances.DisplayDecimals), Distances.Suffix(unit));

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            In(Distances.DisplayUnit).ToString(format, formatProvider) + " " + Distances.Suffix(Distances.DisplayUnit);
    }
}
