using System;
using System.Linq;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using DSClibrary.Units;

namespace DSClibrary.Mapping.DICOM
{
    /// <summary>
    /// Maps an RT Dose, with the plan beside it, onto a canonical <see cref="DoseVolume"/> in
    /// centigray.
    /// <para>
    /// Everything that is a decision lives here: the stored integers are multiplied by
    /// <c>DoseGridScaling</c> and by a hundred to reach centigray, in one traversal; the beam identity
    /// comes from the plan because a dose file does not carry it; and monitor units are recorded
    /// without being applied.
    /// </para>
    /// </summary>
    public sealed class RTDoseVolumeMapper
    {
        public string Vendor => "DICOM";

        private const string ReaderName = nameof(RTDoseVolumeMapper);

        /// <summary>
        /// Builds a volume in centigray. The dose is required; the plan may be absent, in which case
        /// the beam key will be incomplete and the volume belongs in
        /// <see cref="MeasurementSet.Unresolved"/>'s equivalent for volumes - the caller's decision.
        /// </summary>
        public DoseVolume Map(RTDoseWithPlan pairing)
        {
            if (pairing is null) throw new ArgumentNullException(nameof(pairing));

            RTDoseResult dose = pairing.Dose;
            ValueScale scale = MapValueScale(dose);

            double scaling = dose.DoseGridScaling
                ?? throw new DicomFormatException("DoseGridScaling is absent; the voxels cannot be interpreted.");

            // Gray to centigray in the same multiply that applies the grid scaling, so the voxels are
            // traversed once and the array is never held in the file's own units.
            double factor = scaling * 100.0;

            var values = new double[dose.VoxelCount];
            dose.CopyScaledTo(values, factor);

            return DoseVolume.FromCentigrayGrid(
                MapBeamKey(pairing),
                BuildGrid(dose, values),
                scale,
                pairing.MonitorUnits,
                MapConditions(pairing),
                new Provenance(dose.SourcePath, Vendor, ReaderName, dose.ReferencedBeamNumber));
        }

        /// <summary>
        /// <c>DoseUnits</c> of <c>GY</c> gives absolute centigray. <c>RELATIVE</c> is refused for now:
        /// no file in <c>resources/</c> uses it, so there is nothing to establish whether its stored
        /// values are a fraction or already a percentage, and the two differ by a factor of a hundred.
        /// Cody is producing a sample file; until then a clear refusal beats a guessed factor.
        /// </summary>
        internal static ValueScale MapValueScale(RTDoseResult dose) => MapValueScale(dose.DoseUnits);

        /// <summary>The same decision, taking the attribute value directly so it can be tested alone.</summary>
        internal static ValueScale MapValueScale(string? doseUnits)
        {
            return (doseUnits ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "GY" => ValueScale.AbsoluteCentigray,
                "RELATIVE" => throw new DicomFormatException(
                    "DoseUnits is RELATIVE, which this reader does not yet interpret: no sample file exists to "
                    + "establish whether the stored values are a fraction or a percentage, and those differ by a "
                    + "factor of one hundred. A sample is being produced; do not guess."),
                "" => throw new DicomFormatException("DoseUnits is absent; the voxels cannot be interpreted."),
                _ => throw new DicomFormatException($"DoseUnits of '{doseUnits}' is not recognised.")
            };
        }

        /// <summary>
        /// The grid geometry, in millimetres. <c>PixelSpacing</c> is row spacing then column spacing -
        /// y then x - and the z spacing comes from <c>GridFrameOffsetVector</c>.
        /// </summary>
        private static DoseMatrixOptimal BuildGrid(RTDoseResult dose, double[] centigray)
        {
            var origin = dose.ImagePositionPatient;
            var spacing = dose.PixelSpacing;
            var offsets = dose.GridFrameOffsetVector;

            if (origin.Count < 3)
                throw new DicomFormatException("ImagePositionPatient needs three values.");
            if (spacing.Count < 2)
                throw new DicomFormatException("PixelSpacing needs two values.");
            if (!dose.HasUniformFrameSpacing)
                throw new DicomFormatException(
                    "GridFrameOffsetVector is not uniformly spaced. decisions/2026-10-02-comparison-rules.md item 3 "
                    + "allows z spacing to differ from x and y but not to vary along z, and DoseMatrixOptimal holds a "
                    + "single ZRes. No file in resources/ is like this.");

            int dimX = dose.Columns ?? throw new DicomFormatException("Columns is absent.");
            int dimY = dose.Rows ?? throw new DicomFormatException("Rows is absent.");
            int dimZ = dose.NumberOfFrames ?? throw new DicomFormatException("NumberOfFrames is absent.");

            // The first frame offset is normally zero, but the standard does not require it, so it is
            // added rather than assumed away.
            double z0 = origin[2] + (offsets.Count > 0 ? offsets[0] : 0.0);
            double zRes = offsets.Count > 1 ? offsets[1] - offsets[0] : spacing[0];

            return new DoseMatrixOptimal(
                dimX, dimY, dimZ,
                origin[0], origin[1], z0,
                spacing[1], spacing[0], zRes,
                centigray);
        }

        /// <summary>
        /// The beam, from the plan. <b>SSD is absent from these plans</b> - measured:
        /// <c>SourceToSurfaceDistance</c> and <c>SurfaceEntryPoint</c> occur zero times in all nine -
        /// so the key will be incomplete and the volume will not group with a measured profile until
        /// an SSD is supplied from somewhere, most naturally the scan's own header.
        /// </summary>
        private static BeamKey MapBeamKey(RTDoseWithPlan pairing)
        {
            RTPlanBeam? beam = pairing.Beam;
            if (beam is null) return new BeamKey();

            var (crossplane, inplane) = beam.FieldSizeAtIsocentre;

            return new BeamKey(
                modality: MapModality(beam.RadiationType),
                energy: beam.NominalBeamEnergy,
                isFff: beam.IsFluenceModeFff,
                fieldInplane: Distance.FromMillimetres(inplane),
                fieldCrossplane: Distance.FromMillimetres(crossplane),
                ssd: Distance.FromMillimetres(beam.SourceToSurfaceDistance),
                gantryAngleDegrees: beam.GantryAngle,
                collimatorAngleDegrees: beam.BeamLimitingDeviceAngle,
                wedgeAngleDegrees: null);
        }

        internal static Modality MapModality(string? radiationType) =>
            (radiationType ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "PHOTON" => Modality.Photon,
                "ELECTRON" => Modality.Electron,
                _ => Modality.Unknown
            };

        private static MeasurementConditions MapConditions(RTDoseWithPlan pairing) => new(
            Detector: DetectorConditions.Unknown,
            ReferenceDetector: DetectorConditions.Unknown,
            Environment: EnvironmentConditions.Unknown,
            Acquisition: new AcquisitionConditions(
                Mode: AcquisitionMode.Unknown,          // a calculation, not an acquisition
                ScanDevice: pairing.Beam?.TreatmentMachineName ?? string.Empty,
                ScanDeviceSetup: pairing.Plan?.RTPlanLabel ?? string.Empty,
                CorrectionFactor: null),
            Machine: new MachineIdentity(
                LinacName: pairing.Beam?.TreatmentMachineName ?? string.Empty,
                Electrometer: string.Empty),
            MeasuredAt: null,
            RawHeaders: pairing.Dose.Dataset.Tags
                .ToDictionary(t => t.ToString(), t => pairing.Dose.Dataset.GetString(t) ?? string.Empty));
    }
}
