using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DSClibrary.Documents.PTW;
using DSClibrary.Mapping;
using DSClibrary.Profiles;
using DSClibrary.Units;

namespace DSClibrary.Mapping.PTW
{
    /// <summary>
    /// Maps a PTW <see cref="MCCDocument"/> onto the canonical model.
    /// <para>
    /// <b>Units.</b> PTW states lengths in millimetres and the canonical model uses millimetres, so
    /// nothing is scaled. Measured on the committed test file: SSD=1000.00 with FIELD_INPLANE=300.00
    /// and SCAN_DEPTH values of 16 to 300, and profile positions running to +-182.88 mm at 16 mm depth
    /// and +-234 mm at 300 mm - exactly 1.2 times the field half width at each depth, which is the
    /// divergence a 300 mm field at 1000 mm SSD must show. The 2024 ParsePTW divided SSD and both
    /// field sizes by ten into centimetres, which put centimetre values beside a millimetre dose grid.
    /// </para>
    /// <para>
    /// <b>Acquisition.</b> A PTW water tank moves one detector to each position with a three axis
    /// gantry, so the mode is <see cref="AcquisitionMode.Sequential"/>: positions are measured data,
    /// and the third data column is the reference chamber that such a scan needs in order to correct
    /// for output drift across its duration.
    /// </para>
    /// <para>
    /// <b>Detector geometry.</b> Left <see cref="DetectorGeometryCorrection.Unknown"/>. An mcc header
    /// has CORRECTION_FACTOR, which is a scalar, and DETECTOR_IS_CALIBRATED, which is about
    /// calibration - neither states whether the values were adjusted for the detector's volume.
    /// </para>
    /// </summary>
    public sealed class MCCMeasurementMapper : IMeasurementMapper<MCCDocument>
    {
        public string Vendor => "PTW";

        private const string ReaderName = nameof(MCCMeasurementMapper);

        public MeasurementSet Map(MCCDocument document)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            return MeasurementSet.Create(document.Scans.Select(s => MapScan(s, document)));
        }

        public Profile MapScan(MCCScan scan, MCCDocument document)
        {
            if (scan is null) throw new ArgumentNullException(nameof(scan));

            var beam = new BeamKey(
                modality: MapModality(scan.Modality),
                energy: scan.Energy,
                isFff: MapFilter(scan.Filter),
                // PTW states every length in millimetres. Saying so here, once, is the whole of the
                // unit handling for this format: Distance carries it from this point on.
                fieldInplane: Distance.FromMillimetres(scan.FieldInplane),
                fieldCrossplane: Distance.FromMillimetres(scan.FieldCrossplane),
                ssd: Distance.FromMillimetres(scan.Ssd),
                gantryAngleDegrees: scan.Gantry,
                collimatorAngleDegrees: scan.CollAngle,
                wedgeAngleDegrees: scan.WedgeAngle);

            CurveType curve = MapCurveType(scan.ScanCurvetype);

            var key = new ScanKey(
                curveType: curve,
                depth: Distance.FromMillimetres(scan.ScanDepth),
                offAxisInplane: Distance.FromMillimetres(scan.ScanOffaxisInplane),
                offAxisCrossplane: Distance.FromMillimetres(scan.ScanOffaxisCrossplane),
                scanAngleDegrees: scan.ScanAngle);

            var points = scan.Data
                .Select(d => new ProfilePoint(Distance.FromMillimetres(d.Position), d.Value, d.ReferenceValue))
                .ToArray();

            return new Profile(
                beam,
                key,
                points,
                MapConditions(scan),
                new Provenance(document.SourcePath, Vendor, ReaderName, scan.ScanNumber),
                NormalizationType.AsMeasured,
                DetectorGeometryCorrection.Unknown);
        }

        /// <summary>
        /// PTW writes X for photons and E for electrons. Anything else is left unknown rather than
        /// guessed, which makes the beam key incomplete and sends the profile to
        /// <see cref="MeasurementSet.Unresolved"/> where a caller can see it.
        /// </summary>
        internal static Modality MapModality(string modality) =>
            modality.Trim().ToUpperInvariant() switch
            {
                "X" => Modality.Photon,
                "P" => Modality.Photon,
                "E" => Modality.Electron,
                _ => Modality.Unknown
            };

        /// <summary>FILTER is FF for a flattened beam and FFF for a flattening-filter-free one.</summary>
        internal static bool? MapFilter(string filter) =>
            filter.Trim().ToUpperInvariant() switch
            {
                "FFF" => true,
                "FF" => false,
                "" => null,
                _ => null
            };

        internal static CurveType MapCurveType(string curvetype) =>
            curvetype.Trim().ToUpperInvariant() switch
            {
                "PDD" => CurveType.Pdd,
                "DEPTH_DOSE" => CurveType.Pdd,
                "INPLANE_PROFILE" => CurveType.Inplane,
                "CROSSPLANE_PROFILE" => CurveType.Crossplane,
                "DIAGONAL_PROFILE" => CurveType.PositiveDiagonal,
                _ => CurveType.Unknown
            };

        private static MeasurementConditions MapConditions(MCCScan scan) => new(
            Detector: new DetectorConditions(
                Kind: scan.Detector,
                Name: scan.DetectorName,
                Subcode: scan.DetectorSubcode,
                SerialNumber: scan.DetectorSn,
                Radius: Distance.FromMillimetres(scan.DetectorRadius),
                HighVoltage: scan.DetectorHv,
                IsCalibrated: scan.DetectorIsCalibrated),
            ReferenceDetector: new DetectorConditions(
                Kind: scan.DetectorReference,
                Name: scan.DetectorReferenceName,
                Subcode: scan.DetectorReferenceSubcode,
                SerialNumber: scan.DetectorReferenceSn,
                Radius: Distance.FromMillimetres(scan.DetectorReferenceRadius),
                HighVoltage: scan.DetectorReferenceHv,
                IsCalibrated: scan.DetectorReferenceIsCalibrated),
            Environment: new EnvironmentConditions(
                TemperatureCelsius: scan.Temperature,
                PressureHectopascal: scan.Pressure,
                NormalisationTemperatureCelsius: scan.NormTemperature),
            Acquisition: new AcquisitionConditions(
                Mode: AcquisitionMode.Sequential,
                ScanDevice: scan.ScanDevice,
                ScanDeviceSetup: scan.ScanDeviceSetup,
                CorrectionFactor: scan.CorrectionFactor),
            Machine: new MachineIdentity(
                LinacName: scan.Linac,
                Electrometer: scan.Electrometer),
            MeasuredAt: MapMeasuredAt(scan.MeasDate, scan.MeasTime),
            RawHeaders: scan.Headers);

        /// <summary>
        /// MEAS_DATE is written like 06-Apr-2018 and MEAS_TIME like 15:36:30. Both are parsed with
        /// invariant culture - the month abbreviation is English in the file whatever the machine's
        /// locale - and an unrecognised value gives null rather than an exception, because a date is
        /// not worth failing a parse over.
        /// </summary>
        internal static DateTime? MapMeasuredAt(string date, string time)
        {
            date = date.Trim();
            time = time.Trim();
            if (date.Length == 0) return null;

            string[] dateFormats = { "dd-MMM-yyyy", "d-MMM-yyyy", "yyyy-MM-dd", "dd.MM.yyyy" };
            if (!DateTime.TryParseExact(date, dateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime parsedDate))
                return null;

            if (time.Length > 0
                && TimeSpan.TryParseExact(time, new[] { @"hh\:mm\:ss", @"h\:mm\:ss", @"hh\:mm" },
                    CultureInfo.InvariantCulture, out TimeSpan parsedTime))
                return parsedDate.Add(parsedTime);

            return parsedDate;
        }
    }
}
