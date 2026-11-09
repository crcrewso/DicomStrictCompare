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
        /// The key under which an assumed SSD is recorded, so a front end can tell a read value from a
        /// substituted one. Deliberately not a DICOM tag spelling: nothing should mistake it for
        /// something the file said.
        /// </summary>
        public const string AssumedSsdHeaderKey = "(assumed) SourceToSurfaceDistance";

        /// <summary>
        /// The SSD used when the plan does not state one. <b>100 cm, on Cody's instruction of
        /// 2026-11-08, explicitly to be revisited.</b>
        /// <para>
        /// Why it is needed: <c>SourceToSurfaceDistance</c> (300A,0130) and <c>SurfaceEntryPoint</c>
        /// (300A,012E) occur zero times in all nine plans, so a beam key cannot be completed from
        /// DICOM alone and a mapped volume would never group with a measured profile. What the plans
        /// do contain, measured: <c>SourceAxisDistance</c> 1000 mm, <c>IsocenterPosition</c>
        /// (0, -100.009, ~0) identical across all nine, table top positions of 0 / 1000 / 0, a
        /// <c>BeamDose</c> with no <c>BeamDoseSpecificationPoint</c> to locate it, and
        /// <c>SetupTechnique</c> of ISOCENTRIC for every photon beam and FIXED_SSD for every electron
        /// beam with <c>PatientPosition</c> HFS throughout. So the plans name the setup regime and
        /// never the distance.
        /// </para>
        /// <para>
        /// 100 cm also equals the measured <c>SourceAxisDistance</c>, which would put the water surface
        /// at the isocentre plane and make the measured isocentre y the surface position. That is
        /// self-consistent and is the usual commissioning setup, but it is a <i>reading</i> resting on
        /// the assumption and not a measurement.
        /// </para>
        /// </summary>
        public static readonly Distance DefaultSsd = Distance.FromCentimetres(100);

        private readonly Distance? _assumedSsd;

        /// <param name="assumedSsd">
        /// The SSD to use when the plan does not state one. Defaults to <see cref="DefaultSsd"/>; pass
        /// null to leave the beam key incomplete instead, which a front end that would rather refuse
        /// than assume can do.
        /// </param>
        public RTDoseVolumeMapper(Distance? assumedSsd) => _assumedSsd = assumedSsd;

        public RTDoseVolumeMapper() : this(DefaultSsd) { }

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
                dose.ToGrid(values),
                scale,
                pairing.MonitorUnits,
                MapConditions(pairing),
                new Provenance(dose.SourcePath, Vendor, ReaderName, dose.ReferencedBeamNumber),
                dose.FrameOfReferenceUID,
                MapGeometry(pairing));
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
        /// The beam, from the plan. <b>SSD is absent from these plans</b> - measured:
        /// <c>SourceToSurfaceDistance</c> and <c>SurfaceEntryPoint</c> occur zero times in all nine -
        /// so the key will be incomplete and the volume will not group with a measured profile until
        /// an SSD is supplied from somewhere, most naturally the scan's own header.
        /// </summary>
        private BeamKey MapBeamKey(RTDoseWithPlan pairing)
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
                ssd: Distance.FromMillimetres(beam.SourceToSurfaceDistance) ?? _assumedSsd,
                gantryAngleDegrees: beam.GantryAngle,
                collimatorAngleDegrees: beam.BeamLimitingDeviceAngle,
                wedgeAngleDegrees: null);
        }

        /// <summary>
        /// How the phantom was positioned and where the gantry was, from the plan. Null when there is
        /// no plan, because neither is in the dose file.
        /// <para>
        /// Measured: every setup in all nine plans is HFS and every beam is at gantry 0, so every
        /// reference volume maps to a supported geometry. Cody, 2026-11-08, on why that is not a
        /// guarantee: HFS is normally chosen because it is the planning system default.
        /// </para>
        /// </summary>
        private static ScanGeometry? MapGeometry(RTDoseWithPlan pairing)
        {
            if (pairing.Plan is null || pairing.Beam is null) return null;

            var setup = pairing.Plan.SetupFor(pairing.Beam);
            return new ScanGeometry(
                PatientOrientations.Parse(setup?.PatientPosition),
                pairing.Beam.GantryAngle ?? double.NaN);
        }

        internal static Modality MapModality(string? radiationType) =>
            (radiationType ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "PHOTON" => Modality.Photon,
                "ELECTRON" => Modality.Electron,
                _ => Modality.Unknown
            };

        private MeasurementConditions MapConditions(RTDoseWithPlan pairing) => new(
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
            RawHeaders: BuildRawHeaders(pairing));

        /// <summary>
        /// Every element the dose file had, plus a note when the SSD was substituted rather than read.
        /// A complete beam key built on an assumption is exactly the thing that later prompts "why did
        /// these two match?", so the substitution travels with the data.
        /// </summary>
        private IReadOnlyDictionary<string, string> BuildRawHeaders(RTDoseWithPlan pairing)
        {
            var headers = pairing.Dose.Dataset.Tags
                .ToDictionary(t => t.ToString(), t => pairing.Dose.Dataset.GetString(t) ?? string.Empty);

            bool planStatedSsd = pairing.Beam?.SourceToSurfaceDistance is not null;
            if (!planStatedSsd && _assumedSsd.HasValue)
                headers[AssumedSsdHeaderKey] = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0} mm (assumed; the plan states none)", _assumedSsd.Value.Millimetres);

            return headers;
        }
    }
}
