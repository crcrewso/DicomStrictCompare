using System;
using System.Collections.Generic;
using System.Linq;

namespace DSClibrary.Results.DICOM
{
    /// <summary>
    /// An RT Plan file, as the file has it.
    /// <para>
    /// It is read because a great deal of what identifies a dose is only in the plan beside it
    /// (Cody, 2026-11-08). An RT Dose carries its grid and its scaling, but the beam that produced it
    /// - energy, modality, gantry and collimator angles, field size, SSD, isocentre, monitor units -
    /// lives here. Measured: the 9 plans in <c>resources/</c> serve all 26 doses, so a handler reads
    /// plans first and then matches each dose to one.
    /// </para>
    /// </summary>
    public sealed class RTPlanResult
    {
        private readonly DicomDataset _dataset;
        private readonly Lazy<IReadOnlyList<RTPlanBeam>> _beams;
        private readonly Lazy<IReadOnlyDictionary<int, double>> _metersets;

        public string SourcePath { get; }

        internal RTPlanResult(DicomFile file)
        {
            if (!file.IsRTPlan)
                throw new DicomFormatException(
                    $"Not an RT Plan: SOPClassUID is {file.SOPClassUID ?? "absent"}, expected {DicomTags.RTPlanStorage}.");
            _dataset = file.Dataset;
            SourcePath = file.SourcePath;
            _beams = new Lazy<IReadOnlyList<RTPlanBeam>>(ReadBeams);
            _metersets = new Lazy<IReadOnlyDictionary<int, double>>(ReadMetersets);
        }

        public static RTPlanResult ReadFile(string path) => new(DicomFileReader.ReadFile(path));

        public DicomDataset Dataset => _dataset;

        public string? SOPInstanceUID => _dataset.GetString(DicomTags.SOPInstanceUID);
        public string? FrameOfReferenceUID => _dataset.GetString(DicomTags.FrameOfReferenceUID);

        /// <summary>RTPlanLabel (300A,0002). "P5 Varied MU" in the varied-meterset plan.</summary>
        public string? RTPlanLabel => _dataset.GetString(DicomTags.RTPlanLabel);

        /// <summary>RTPlanName (300A,0003). "Many different MU's" in that same plan.</summary>
        public string? RTPlanName => _dataset.GetString(DicomTags.RTPlanName);

        public IReadOnlyList<RTPlanBeam> Beams => _beams.Value;

        /// <summary>
        /// Monitor units per beam number, from FractionGroupSequence -> ReferencedBeamSequence ->
        /// BeamMeterset. Separate from <see cref="Beams"/> because that is where the standard puts it:
        /// a beam describes the geometry and a fraction group says how much of it is delivered.
        /// </summary>
        public IReadOnlyDictionary<int, double> MonitorUnitsByBeamNumber => _metersets.Value;

        public RTPlanBeam? Beam(int beamNumber) => Beams.FirstOrDefault(b => b.BeamNumber == beamNumber);

        public double? MonitorUnitsFor(int beamNumber) =>
            MonitorUnitsByBeamNumber.TryGetValue(beamNumber, out double mu) ? mu : null;

        private IReadOnlyList<RTPlanBeam> ReadBeams() =>
            _dataset.GetSequence(DicomTags.BeamSequence).Select(item => new RTPlanBeam(item)).ToArray();

        private IReadOnlyDictionary<int, double> ReadMetersets()
        {
            var byBeam = new Dictionary<int, double>();
            foreach (var group in _dataset.GetSequence(DicomTags.FractionGroupSequence))
                foreach (var reference in group.GetSequence(DicomTags.ReferencedBeamSequence))
                {
                    int? number = reference.GetInteger(DicomTags.ReferencedBeamNumber);
                    double? meterset = reference.GetDecimal(DicomTags.BeamMeterset);
                    if (number.HasValue && meterset.HasValue)
                        byBeam[number.Value] = meterset.Value;
                }
            return byBeam;
        }

        public override string ToString() =>
            $"{System.IO.Path.GetFileName(SourcePath)}: {RTPlanLabel}, {Beams.Count} beam(s)";
    }

    /// <summary>
    /// One item of an RT Plan's BeamSequence, in DICOM's own names and units.
    /// <para>
    /// Angles, energy and the jaw positions that give field size live on the first control point, not
    /// on the beam, because the standard allows them to vary along the delivery. For the static beams
    /// this project compares they are constant, so the first control point is the beam's geometry -
    /// <i>which is a reading, and would be wrong for an arc.</i>
    /// </para>
    /// </summary>
    public sealed class RTPlanBeam
    {
        private readonly DicomDataset _beam;
        private readonly DicomDataset? _firstControlPoint;

        internal RTPlanBeam(DicomDataset beam)
        {
            _beam = beam;
            _firstControlPoint = beam.GetSequence(DicomTags.ControlPointSequence).FirstOrDefault();
        }

        public int? BeamNumber => _beam.GetInteger(DicomTags.BeamNumber);

        /// <summary>BeamName (300A,00C2). "X-095A-10.0-1" in the varied-meterset plan.</summary>
        public string? BeamName => _beam.GetString(DicomTags.BeamName);

        public string? BeamType => _beam.GetString(DicomTags.BeamType);

        /// <summary>RadiationType (300A,00C6): PHOTON or ELECTRON in the reference plans.</summary>
        public string? RadiationType => _beam.GetString(DicomTags.RadiationType);

        public string? TreatmentMachineName => _beam.GetString(DicomTags.TreatmentMachineName);

        /// <summary>SourceAxisDistance (300A,00B4), millimetres. 1000 in the reference plans.</summary>
        public double? SourceAxisDistance => _beam.GetDecimal(DicomTags.SourceAxisDistance);

        public int? NumberOfControlPoints => _beam.GetInteger(DicomTags.NumberOfControlPoints);

        /// <summary>True when the beam has more than two control points, so its geometry may vary.</summary>
        public bool MayBeDynamic => (NumberOfControlPoints ?? 0) > 2;

        // ---- from the first control point ----

        public double? NominalBeamEnergy => _firstControlPoint?.GetDecimal(DicomTags.NominalBeamEnergy);
        public double? GantryAngle => _firstControlPoint?.GetDecimal(DicomTags.GantryAngle);
        public double? BeamLimitingDeviceAngle => _firstControlPoint?.GetDecimal(DicomTags.BeamLimitingDeviceAngle);
        public double? PatientSupportAngle => _firstControlPoint?.GetDecimal(DicomTags.PatientSupportAngle);

        /// <summary>SourceToSurfaceDistance (300A,0130), millimetres. The SSD a scan's header also states.</summary>
        public double? SourceToSurfaceDistance => _firstControlPoint?.GetDecimal(DicomTags.SourceToSurfaceDistance);

        /// <summary>IsocenterPosition (300A,012C): x, y, z in the patient coordinate system.</summary>
        public IReadOnlyList<double> IsocenterPosition =>
            _firstControlPoint?.GetDecimals(DicomTags.IsocenterPosition) ?? Array.Empty<double>();

        /// <summary>
        /// SurfaceEntryPoint (300A,012E), where present. This is what pymedphys's geometry convention
        /// uses to locate the phantom surface, so it is the preferred origin for sampling a scan line
        /// out of a dose - see swift's addendum of 2026-10-02.
        /// </summary>
        public IReadOnlyList<double> SurfaceEntryPoint =>
            _firstControlPoint?.GetDecimals(DicomTags.SurfaceEntryPoint) ?? Array.Empty<double>();

        /// <summary>
        /// Whether the beam is flattening-filter-free, from PrimaryFluenceModeSequence. Null when the
        /// sequence is absent, which is not the same as flattened - an older plan simply does not say.
        /// </summary>
        public bool? IsFluenceModeFff
        {
            get
            {
                var fluence = _beam.GetFirstItem(DicomTags.PrimaryFluenceModeSequence);
                if (fluence is null) return null;
                string? mode = fluence.GetString(DicomTags.FluenceMode);
                if (string.Equals(mode, "STANDARD", StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.Equals(mode, "NON_STANDARD", StringComparison.OrdinalIgnoreCase)) return null;
                string? id = fluence.GetString(DicomTags.FluenceModeID);
                return id is not null && id.Contains("FFF", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Jaw positions by device type - ASYMX, ASYMY, X, Y, MLCX, MLCY - from the first control
        /// point's BeamLimitingDevicePositionSequence. Millimetres, and for a jaw pair two values.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<double>> LeafJawPositions
        {
            get
            {
                var positions = new Dictionary<string, IReadOnlyList<double>>(StringComparer.OrdinalIgnoreCase);
                if (_firstControlPoint is null) return positions;
                foreach (var device in _firstControlPoint.GetSequence(DicomTags.BeamLimitingDevicePositionSequence))
                {
                    string? type = device.GetString(DicomTags.RTBeamLimitingDeviceType);
                    if (!string.IsNullOrEmpty(type))
                        positions[type] = device.GetDecimals(DicomTags.LeafJawPositions);
                }
                return positions;
            }
        }

        /// <summary>
        /// Field size at the isocentre from the jaw positions: crossplane from the X jaws, inplane from
        /// the Y jaws, in millimetres. Null for either axis whose jaws are absent.
        /// <para>
        /// <i>A reading, not a standard definition:</i> field size is taken as the separation of the
        /// jaw pair, which is right for an open rectangular field and is what the mcc header's
        /// FIELD_CROSSPLANE and FIELD_INPLANE describe. An MLC-shaped field has no single field size
        /// and this will report the jaw opening around it.
        /// </para>
        /// </summary>
        public (double? CrossplaneMm, double? InplaneMm) FieldSizeAtIsocentre
        {
            get
            {
                var jaws = LeafJawPositions;
                return (Separation(jaws, "ASYMX") ?? Separation(jaws, "X"),
                        Separation(jaws, "ASYMY") ?? Separation(jaws, "Y"));

                static double? Separation(IReadOnlyDictionary<string, IReadOnlyList<double>> all, string type) =>
                    all.TryGetValue(type, out var p) && p.Count == 2 ? Math.Abs(p[1] - p[0]) : null;
            }
        }

        public override string ToString() => $"#{BeamNumber} {BeamName} ({RadiationType})";
    }
}
