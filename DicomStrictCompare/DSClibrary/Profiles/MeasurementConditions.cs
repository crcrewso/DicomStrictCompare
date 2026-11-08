using System;
using System.Collections.Generic;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>A single measured point of a profile.</summary>
    /// <param name="Position">
    /// Where along the axis of travel the point sits. A <see cref="Distance"/> rather than a number,
    /// so neither the reader that produced it nor the code that consumes it can be wrong about the
    /// unit: PTW reports these in millimetres and other formats will not.
    /// </param>
    /// <param name="Value">The field detector reading, in the file's own units.</param>
    /// <param name="ReferenceValue">
    /// The reference detector reading at the same point, where the format has one. Present for a
    /// sequential acquisition, which needs it to correct for output drift over the scan; null for a
    /// simultaneous one, which has no drift between points.
    /// </param>
    public readonly record struct ProfilePoint(Distance Position, double Value, double? ReferenceValue);

    /// <summary>The detector that took the measurement.</summary>
    public sealed record DetectorConditions(
        string Kind = "",
        string Name = "",
        string Subcode = "",
        string SerialNumber = "",
        Distance? Radius = null,
        double? HighVoltage = null,
        bool? IsCalibrated = null)
    {
        public static readonly DetectorConditions Unknown = new();
    }

    /// <summary>
    /// Conditions the readings depend on. Temperature and pressure are what an ion chamber reading
    /// must be corrected to, and both PTW mcc and Sun Nuclear prm record them.
    /// </summary>
    public sealed record EnvironmentConditions(
        double? TemperatureCelsius = null,
        double? PressureHectopascal = null,
        double? NormalisationTemperatureCelsius = null)
    {
        public static readonly EnvironmentConditions Unknown = new();
    }

    /// <summary>How the points were acquired, and by what.</summary>
    public sealed record AcquisitionConditions(
        AcquisitionMode Mode = AcquisitionMode.Unknown,
        string ScanDevice = "",
        string ScanDeviceSetup = "",
        double? CorrectionFactor = null)
    {
        public static readonly AcquisitionConditions Unknown = new();
    }

    /// <summary>The treatment machine and electrometer, as the file names them.</summary>
    public sealed record MachineIdentity(string LinacName = "", string Electrometer = "")
    {
        public static readonly MachineIdentity Unknown = new();
    }

    /// <summary>
    /// Everything of value a source declared about a measurement, beyond what addresses it.
    /// <para>
    /// Deliberately kept apart from <see cref="BeamKey"/> and <see cref="ScanKey"/>. Those are records
    /// whose structural equality does the matching, and the moment a measurement time, a temperature
    /// or a detector serial enters them no two profiles are ever equal again - a scan taken at 15:36
    /// and a dose calculated whenever would differ in a field nobody is matching on.
    /// </para>
    /// <para>
    /// <see cref="RawHeaders"/> carries every key the file had, verbatim and unparsed, so nothing is
    /// discarded by a reader that does not yet understand it. A PTW mcc scan header has 71 keys and a
    /// Sun Nuclear prm header 58; promoting one of them to a typed property above stays an additive
    /// change.
    /// </para>
    /// </summary>
    public sealed record MeasurementConditions(
        DetectorConditions Detector,
        DetectorConditions ReferenceDetector,
        EnvironmentConditions Environment,
        AcquisitionConditions Acquisition,
        MachineIdentity Machine,
        DateTime? MeasuredAt,
        IReadOnlyDictionary<string, string> RawHeaders)
    {
        public static MeasurementConditions Empty { get; } = new(
            DetectorConditions.Unknown,
            DetectorConditions.Unknown,
            EnvironmentConditions.Unknown,
            AcquisitionConditions.Unknown,
            MachineIdentity.Unknown,
            null,
            new Dictionary<string, string>());
    }

    /// <summary>
    /// Where a profile came from, so a number in a report can be traced back to a file and a scan.
    /// </summary>
    public sealed record Provenance(
        string SourcePath = "",
        string Vendor = "",
        string Reader = "",
        int? ScanNumber = null)
    {
        public static readonly Provenance Unknown = new();

        public override string ToString() =>
            ScanNumber.HasValue
                ? $"{System.IO.Path.GetFileName(SourcePath)} scan {ScanNumber.Value}"
                : System.IO.Path.GetFileName(SourcePath);
    }
}
