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
    /// What a set of values means. Shared by <see cref="Profile"/> and <see cref="DoseVolume"/> so
    /// there is one vocabulary rather than two overlapping ones.
    /// <para>
    /// Cody, 2026-11-08: a dose volume holds either absolute dose in centigray or relative dose in
    /// percent, both being the stored voxel value times DoseGridScaling. And: "for the most part
    /// profiles are treated as relative, and only RD to RD comparisons will be looking for absolute
    /// dose comparisons."
    /// </para>
    /// <para>
    /// <see cref="ArbitraryUnits"/> is why that split is forced rather than chosen. Measured in the
    /// committed PTW scan: <c>MEAS_UNIT=A.U.</c> and <c>DETECTOR_IS_CALIBRATED=0</c>, with values
    /// running 0.4765 to 1.7474. An uncalibrated detector reading is not a dose and cannot be made
    /// one without a calibration factor the file does not carry, so a measured profile is physically
    /// incapable of an absolute comparison.
    /// </para>
    /// <para>
    /// There is no default and no Unknown: values whose meaning is not known cannot be compared with
    /// anything, and silently reading a percentage or an arbitrary unit as centigray would be wrong
    /// by whatever the normalisation or the calibration was.
    /// </para>
    /// </summary>
    public enum ValueScale
    {
        /// <summary>
        /// Detector units, uncalibrated. Comparable only after normalisation, and never as an
        /// absolute dose. What a water tank scan arrives as.
        /// </summary>
        ArbitraryUnits = 1,

        /// <summary>Absolute dose in centigray. DICOM's DoseUnits of GY, converted.</summary>
        AbsoluteCentigray = 2,

        /// <summary>Relative dose as a percentage of some reference. DICOM's DoseUnits of RELATIVE.</summary>
        RelativePercent = 3
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
