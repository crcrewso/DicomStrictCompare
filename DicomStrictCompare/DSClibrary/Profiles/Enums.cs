namespace DSClibrary.Profiles
{
    /// <summary>What kind of curve a profile is. Vendor spellings are translated to this by a mapper.</summary>
    public enum CurveType
    {
        Unknown = 0,
        /// <summary>Percentage depth dose: dose along the beam axis, into the phantom.</summary>
        Pdd,
        /// <summary>Across the beam, in the direction the vendor calls crossplane (left-right).</summary>
        Crossplane,
        /// <summary>Across the beam, in the direction the vendor calls inplane (gun-target).</summary>
        Inplane,
        PositiveDiagonal,
        NegativeDiagonal
    }

    public enum Modality
    {
        Unknown = 0,
        Photon,
        Electron
    }

    /// <summary>
    /// How a profile's values have been scaled. <see cref="AsMeasured"/> is the state a reader
    /// produces; everything else is the result of an explicit transform.
    /// </summary>
    public enum NormalizationType
    {
        AsMeasured = 0,
        /// <summary>Scaled so the largest value in the profile is 1.</summary>
        Maximum,
        /// <summary>Scaled so the value at position 0 is 1.</summary>
        CentralAxis
    }

    /// <summary>
    /// How the points of a profile were acquired. This is not a label: it decides where a point's
    /// position comes from, and which corrections are meaningful.
    /// </summary>
    public enum AcquisitionMode
    {
        Unknown = 0,
        /// <summary>
        /// One detector moved to each position in turn, as a PTW water tank does with a three axis
        /// gantry. Positions are measured data. Output drift over the scan is real, which is why such
        /// files carry a reference chamber reading per point.
        /// </summary>
        Sequential,
        /// <summary>
        /// A fixed array of detectors read in one exposure, as a Sun Nuclear IC Profiler does.
        /// Positions are a property of the device, not of the file. There is no drift between points,
        /// but inter-detector calibration matters.
        /// </summary>
        Simultaneous
    }

    /// <summary>
    /// Whether the values have been adjusted for the detector's geometry (its finite volume).
    /// <para>
    /// <see cref="Unknown"/> is the zero value deliberately. A PTW mcc file does not record this:
    /// it has CORRECTION_FACTOR, which is a scalar, and DETECTOR_IS_CALIBRATED, which is about
    /// calibration rather than geometry. So a reader that says nothing must leave the state unknown
    /// rather than assert a confident NotApplied.
    /// </para>
    /// </summary>
    public enum DetectorGeometryCorrection
    {
        Unknown = 0,
        /// <summary>Asserted by the caller, or by a format that does record it.</summary>
        NotApplied,
        /// <summary>The file states it, or the format guarantees it.</summary>
        AppliedBySource,
        /// <summary>This library applied it. The correction that was used is recorded alongside.</summary>
        AppliedByUs
    }
}
