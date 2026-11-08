using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DSClibrary.Results.PTW
{
    /// <summary>
    /// A PTW mcc file, as the file has it. Vendor names, vendor units, no interpretation.
    /// <para>
    /// This layer exists so that fidelity and interpretation can be checked separately. "The file says
    /// SSD=1000.00" is verifiable against the file by someone who knows PTW and holds no opinion about
    /// this project's conventions; "that means 1000 mm in our model" is a decision of ours. Fused into
    /// one step, a unit mistake and a parsing mistake look identical in a failing test. Converting to
    /// the canonical model is the mapper's job, not this type's.
    /// </para>
    /// <para>
    /// So <see cref="MCCScan.Ssd"/> returns 1000.00 for a file that says SSD=1000.00, and
    /// <see cref="MCCScan.ScanCurvetype"/> returns the string "CROSSPLANE_PROFILE". The vendor spelling
    /// is kept on purpose: it signals that you are holding vendor data.
    /// </para>
    /// </summary>
    public sealed class MCCResult
    {
        public string Format { get; }
        public string FileCreationDate { get; }
        public string LastModified { get; }
        public IReadOnlyList<MCCScan> Scans { get; }
        public IReadOnlyDictionary<string, string> FileHeaders { get; }
        public string SourcePath { get; }

        internal MCCResult(
            string sourcePath,
            IReadOnlyDictionary<string, string> fileHeaders,
            IReadOnlyList<MCCScan> scans)
        {
            SourcePath = sourcePath;
            FileHeaders = fileHeaders;
            Scans = scans;
            Format = fileHeaders.TryGetValue("FORMAT", out var f) ? f : string.Empty;
            FileCreationDate = fileHeaders.TryGetValue("FILE_CREATION_DATE", out var c) ? c : string.Empty;
            LastModified = fileHeaders.TryGetValue("LAST_MODIFIED", out var m) ? m : string.Empty;
        }

        public override string ToString() =>
            $"{System.IO.Path.GetFileName(SourcePath)}: {Scans.Count} scan(s), format '{Format}'";
    }

    /// <summary>One point of an mcc scan's data block, exactly as the three columns appear.</summary>
    /// <param name="Position">First column. The axis of travel, in the file's own units.</param>
    /// <param name="Value">Second column. The field detector reading.</param>
    /// <param name="ReferenceValue">
    /// Third column. The reference detector reading. An mcc scan declares a whole second chamber -
    /// DETECTOR_REFERENCE, DETECTOR_REFERENCE_SN, with its own HV and calibration - which is what a
    /// sequential acquisition needs in order to correct for output drift across the scan.
    /// </param>
    public readonly record struct MCCDataPoint(double Position, double Value, double? ReferenceValue);

    /// <summary>
    /// One BEGIN_SCAN..END_SCAN block. <see cref="Headers"/> is the truth and holds every key the
    /// block had; the typed properties are views over it, named as PTW names them and returning the
    /// file's own units unconverted.
    /// </summary>
    public sealed class MCCScan
    {
        /// <summary>Every header key in this scan, verbatim and unparsed. Measured: 71 of them in the
        /// committed test file. The typed properties below read from this.</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        public int ScanNumber { get; }

        public IReadOnlyList<MCCDataPoint> Data { get; }

        internal MCCScan(int scanNumber, IReadOnlyDictionary<string, string> headers, IReadOnlyList<MCCDataPoint> data)
        {
            ScanNumber = scanNumber;
            Headers = headers;
            Data = data;
        }

        /// <summary>A header as written, or empty if the scan did not have it.</summary>
        public string Text(string key) => Headers.TryGetValue(key, out var v) ? v : string.Empty;

        /// <summary>
        /// A header parsed as a number, or null if absent or unparseable. Always invariant culture:
        /// these files write 1.0570E+00 regardless of the machine's locale, so a bare double.Parse
        /// would throw or misread under a decimal-comma locale.
        /// </summary>
        public double? Number(string key) =>
            Headers.TryGetValue(key, out var v)
            && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d
                : null;

        /// <summary>A header parsed as a 0/1 flag, or null if absent or not 0/1.</summary>
        public bool? Flag(string key) => Number(key) switch
        {
            0 => false,
            1 => true,
            _ => null
        };

        // ---- Task and machine, as PTW names them ----
        public string TaskName => Text("TASK_NAME");
        public string Program => Text("PROGRAM");
        public string MeasDate => Text("MEAS_DATE");
        public string MeasTime => Text("MEAS_TIME");
        public string Linac => Text("LINAC");
        public string Electrometer => Text("ELECTROMETER");

        // ---- Beam ----
        public string Modality => Text("MODALITY");
        public double? Energy => Number("ENERGY");
        public string Filter => Text("FILTER");
        public double? Ssd => Number("SSD");
        public double? Scd => Number("SCD");
        public double? Isocenter => Number("ISOCENTER");
        public double? FieldInplane => Number("FIELD_INPLANE");
        public double? FieldCrossplane => Number("FIELD_CROSSPLANE");
        public string FieldType => Text("FIELD_TYPE");
        public double? Gantry => Number("GANTRY");
        public double? CollAngle => Number("COLL_ANGLE");
        public double? WedgeAngle => Number("WEDGE_ANGLE");
        public string Block => Text("BLOCK");

        // ---- Scan geometry ----
        public string ScanCurvetype => Text("SCAN_CURVETYPE");
        public double? ScanDepth => Number("SCAN_DEPTH");
        public double? ScanOffaxisInplane => Number("SCAN_OFFAXIS_INPLANE");
        public double? ScanOffaxisCrossplane => Number("SCAN_OFFAXIS_CROSSPLANE");
        public double? ScanAngle => Number("SCAN_ANGLE");
        public string ScanDiagonal => Text("SCAN_DIAGONAL");
        public string ScanDirection => Text("SCAN_DIRECTION");
        public string ScanDevice => Text("SCAN_DEVICE");
        public string ScanDeviceSetup => Text("SCAN_DEVICE_SETUP");
        public string InplaneAxis => Text("INPLANE_AXIS");
        public string InplaneAxisDir => Text("INPLANE_AXIS_DIR");
        public string CrossplaneAxis => Text("CROSSPLANE_AXIS");
        public string CrossplaneAxisDir => Text("CROSSPLANE_AXIS_DIR");

        /// <summary>
        /// DEPTH_AXIS, not CROSSPLANE_AXIS_DIR. The 2024 ParsePTW read the crossplane direction key
        /// into both of its depth fields, which is the sort of mistake a faithful result layer makes
        /// visible: here the property name and the key it reads are checkable side by side.
        /// </summary>
        public string DepthAxis => Text("DEPTH_AXIS");

        public string DepthAxisDir => Text("DEPTH_AXIS_DIR");

        // ---- Detector ----
        public string Detector => Text("DETECTOR");
        public string DetectorName => Text("DETECTOR_NAME");
        public string DetectorSubcode => Text("DETECTOR_SUBCODE");
        public string DetectorSn => Text("DETECTOR_SN");
        public double? DetectorRadius => Number("DETECTOR_RADIUS");
        public double? DetectorHv => Number("DETECTOR_HV");
        public bool? DetectorIsCalibrated => Flag("DETECTOR_IS_CALIBRATED");
        public double? DetectorCalibration => Number("DETECTOR_CALIBRATION");

        public string DetectorReference => Text("DETECTOR_REFERENCE");
        public string DetectorReferenceName => Text("DETECTOR_REFERENCE_NAME");
        public string DetectorReferenceSubcode => Text("DETECTOR_REFERENCE_SUBCODE");
        public string DetectorReferenceSn => Text("DETECTOR_REFERENCE_SN");
        public double? DetectorReferenceRadius => Number("DETECTOR_REFERENCE_RADIUS");
        public double? DetectorReferenceHv => Number("DETECTOR_REFERENCE_HV");
        public bool? DetectorReferenceIsCalibrated => Flag("DETECTOR_REFERENCE_IS_CALIBRATED");

        // ---- Environment and corrections ----
        public double? Temperature => Number("TEMPERATURE");
        public double? Pressure => Number("PRESSURE");
        public double? NormTemperature => Number("NORM_TEMPERATURE");

        /// <summary>
        /// CORRECTION_FACTOR. A scalar, and not a statement about detector geometry - which is why the
        /// canonical model's detector correction state defaults to unknown for an mcc file.
        /// </summary>
        public double? CorrectionFactor => Number("CORRECTION_FACTOR");

        public string MeasMedium => Text("MEAS_MEDIUM");
        public string MeasPreset => Text("MEAS_PRESET");
        public string MeasUnit => Text("MEAS_UNIT");
        public double? NominalDmax => Number("NOMINAL_DMAX");

        public override string ToString() =>
            $"scan {ScanNumber}: {ScanCurvetype}, {Data.Count} point(s), {Headers.Count} header(s)";
    }

    /// <summary>Thrown when an mcc file does not have the structure the format requires.</summary>
    public sealed class MCCFormatException : Exception
    {
        public MCCFormatException(string message) : base(message) { }
    }
}
