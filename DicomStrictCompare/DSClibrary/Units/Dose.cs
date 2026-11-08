using System;
using System.Globalization;

namespace DSClibrary.Units
{
    public enum DoseUnit
    {
        Centigray,
        Gray
    }

    /// <summary>
    /// An absolute dose, stored in one canonical unit and carrying no opinion about how it was read
    /// or how it will be shown. The counterpart of <see cref="Distance"/>, and there for the same
    /// reason: the unit lives in the type instead of in a property name.
    /// <para>
    /// <b>Canonically centigray</b>, because that is what this project's dose volumes are specified
    /// in (Cody, 2026-11-08). DICOM states dose in gray - <c>DoseUnits</c> is <c>GY</c> in every
    /// reference file measured - and EvilDICOM hands back gray, so the factor of a hundred is real and
    /// has to be applied somewhere. Applying it in a named conversion at the boundary is the whole
    /// point: a bare double that is sometimes Gy and sometimes cGy is the same class of mistake as the
    /// centimetre SSD sitting beside a millimetre dose grid.
    /// </para>
    /// <para>
    /// <b>Absolute only.</b> A relative dose is a percentage of something and is not a dose in
    /// centigray, so it is not this type - a volume says which it holds through
    /// <see cref="DSClibrary.Profiles.DoseScale"/>. Calling 50% "50 cGy" would be false.
    /// </para>
    /// <para>
    /// <b>On precision.</b> RT Dose stores unsigned integers with a scale factor because the format
    /// predates cheap doubles, and the factor was how precision was preserved (Cody, 2026-11-08).
    /// Once read, that device has done its job: the measured files use at most 2,904,474 of the 32-bit
    /// range, about seven significant figures, where a double carries about sixteen. So holding
    /// physical dose in a double loses nothing the file contained, and there is no reason to keep the
    /// integer-and-factor pair downstream.
    /// </para>
    /// </summary>
    public readonly struct Dose : IEquatable<Dose>, IComparable<Dose>
    {
        private readonly double _centigray;

        private Dose(double centigray)
        {
            if (double.IsNaN(centigray) || double.IsInfinity(centigray))
                throw new ArgumentOutOfRangeException(nameof(centigray),
                    "A dose must be finite. An absent dose is null, not NaN.");
            _centigray = centigray + 0.0;   // normalises -0.0, which double.Equals treats as distinct
        }

        public static readonly Dose Zero = new(0.0);

        public static Dose FromCentigray(double value) => new(value);

        /// <summary>From gray, which is what DICOM's <c>DoseUnits</c> of <c>GY</c> means.</summary>
        public static Dose FromGray(double value) => new(value * 100.0);

        public static Dose From(double value, DoseUnit unit) => unit switch
        {
            DoseUnit.Centigray => FromCentigray(value),
            DoseUnit.Gray => FromGray(value),
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };

        public static Dose? FromCentigray(double? value) => value.HasValue ? FromCentigray(value.Value) : null;

        public static Dose? FromGray(double? value) => value.HasValue ? FromGray(value.Value) : null;

        public double Centigray => _centigray;

        public double Gray => _centigray / 100.0;

        public double In(DoseUnit unit) => unit switch
        {
            DoseUnit.Centigray => Centigray,
            DoseUnit.Gray => Gray,
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };

        /// <summary>This dose as a fraction of a reference dose. Throws when the reference is zero.</summary>
        public double FractionOf(Dose reference) =>
            reference._centigray != 0.0
                ? _centigray / reference._centigray
                : throw new ArgumentOutOfRangeException(nameof(reference), "Cannot take a fraction of zero dose.");

        public static Dose operator +(Dose a, Dose b) => new(a._centigray + b._centigray);
        public static Dose operator -(Dose a, Dose b) => new(a._centigray - b._centigray);
        public static Dose operator -(Dose a) => new(-a._centigray);
        public static Dose operator *(Dose a, double f) => new(a._centigray * f);
        public static Dose operator *(double f, Dose a) => new(a._centigray * f);
        public static Dose operator /(Dose a, double d) => new(a._centigray / d);
        public static double operator /(Dose a, Dose b) => a._centigray / b._centigray;
        public static bool operator <(Dose a, Dose b) => a._centigray < b._centigray;
        public static bool operator >(Dose a, Dose b) => a._centigray > b._centigray;
        public static bool operator <=(Dose a, Dose b) => a._centigray <= b._centigray;
        public static bool operator >=(Dose a, Dose b) => a._centigray >= b._centigray;
        public static bool operator ==(Dose a, Dose b) => a.Equals(b);
        public static bool operator !=(Dose a, Dose b) => !a.Equals(b);

        public bool Equals(Dose other) => _centigray.Equals(other._centigray);
        public override bool Equals(object? obj) => obj is Dose other && Equals(other);
        public override int GetHashCode() => _centigray.GetHashCode();
        public int CompareTo(Dose other) => _centigray.CompareTo(other._centigray);

        public override string ToString() => ToString(Doses.DisplayUnit);

        public string ToString(DoseUnit unit) =>
            string.Format(CultureInfo.CurrentCulture, "{0} {1}",
                Math.Round(In(unit), Doses.DisplayDecimals), Doses.Suffix(unit));
    }

    /// <summary>
    /// Display policy for doses, the counterpart of <see cref="Distances"/>. Cosmetic only: nothing
    /// here changes what <see cref="Dose"/> stores or how two doses compare.
    /// </summary>
    public static class Doses
    {
        private static DoseUnit _displayUnit = DoseUnit.Centigray;
        private static int _displayDecimals = 2;

        public static DoseUnit DisplayUnit
        {
            get => _displayUnit;
            set => _displayUnit = Enum.IsDefined(typeof(DoseUnit), value)
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

        public static string Suffix(DoseUnit unit) => unit switch
        {
            DoseUnit.Centigray => "cGy",
            DoseUnit.Gray => "Gy",
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };
    }
}
