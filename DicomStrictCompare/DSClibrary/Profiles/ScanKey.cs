using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// Identifies which curve within a beam a profile is: the inner level of its address, paired with
    /// a <see cref="BeamKey"/>.
    /// <para>
    /// Distances are <see cref="Distance"/> and quantised on construction, as in
    /// <see cref="BeamKey"/>. <see cref="Depth"/> is null for a <see cref="CurveType.Pdd"/>, which
    /// runs through every depth rather than sitting at one - null rather than NaN or zero, because a
    /// PDD has no depth and zero is a real depth.
    /// </para>
    /// </summary>
    public sealed record ScanKey
    {
        public CurveType CurveType { get; }
        public Distance? Depth { get; }
        public Distance? OffAxisInplane { get; }
        public Distance? OffAxisCrossplane { get; }
        public double? ScanAngleDegrees { get; }

        public ScanKey(
            CurveType curveType,
            Distance? depth = null,
            Distance? offAxisInplane = null,
            Distance? offAxisCrossplane = null,
            double? scanAngleDegrees = null)
        {
            CurveType = curveType;
            Depth = curveType == CurveType.Pdd ? null : Distances.Quantize(depth);
            OffAxisInplane = Distances.Quantize(offAxisInplane);
            OffAxisCrossplane = Distances.Quantize(offAxisCrossplane);
            ScanAngleDegrees = Distances.Angle(scanAngleDegrees);
        }

        public override string ToString() =>
            CurveType == CurveType.Pdd
                ? "PDD"
                : Depth.HasValue
                    ? $"{CurveType} at {Depth.Value}"
                    : $"{CurveType} at unknown depth";
    }
}
