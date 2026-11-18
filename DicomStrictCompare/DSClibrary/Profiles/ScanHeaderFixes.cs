using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A person's correction to a scan's header: the values that replace the file's, the rest left as the file has them
    /// (null). Wedge angle 0 means open. Kept beside the scans, never written into the scan file (crcrewso, 2026-11-17:
    /// "fix the metadata per profile").
    /// </summary>
    /// <param name="File">The scan file, relative to the scans folder, with forward slashes.</param>
    /// <param name="Scan">The scan's number within the file, or null for every scan in it.</param>
    /// <param name="Note">Why: "wedged, measured with the 17 June EDWs' order". Shown with every row the fix touches.</param>
    /// <param name="Changed">When the fix was made or last changed, UTC.</param>
    public sealed record ScanHeaderFix(
        string File,
        int? Scan = null,
        double? Energy = null,
        bool? Fff = null,
        double? FieldInplaneMm = null,
        double? FieldCrossplaneMm = null,
        double? WedgeAngle = null,
        string? WedgeDirection = null,
        string? Note = null,
        DateTime? Changed = null)
    {
        /// <summary>What it sets, in a line: "energy 10, field 200 x 200 mm, wedge 15 OUT".</summary>
        public string Describe()
        {
            var ci = CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (Energy is { } e) parts.Add($"energy {e.ToString("0.##", ci)}");
            if (Fff is { } f) parts.Add(f ? "FFF" : "flattened");
            if (FieldInplaneMm is { } i && FieldCrossplaneMm is { } c) parts.Add($"field {c.ToString("0.#", ci)} x {i.ToString("0.#", ci)} mm (crossplane x inplane)");
            else
            {
                if (FieldCrossplaneMm is { } c2) parts.Add($"field crossplane {c2.ToString("0.#", ci)} mm");
                if (FieldInplaneMm is { } i2) parts.Add($"field inplane {i2.ToString("0.#", ci)} mm");
            }
            if (WedgeAngle is { } w) parts.Add(w > 0 ? $"wedge {w.ToString("0.#", ci)} {WedgeDirection}".TrimEnd() : "open (no wedge)");
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }

        /// <summary>Why the fix can't be used, or null: a field or energy that isn't positive, a wedge direction other than IN or OUT.</summary>
        public string? Refusal()
        {
            if (string.IsNullOrWhiteSpace(File)) return "no scan file";
            if (Scan is <= 0) return "a scan number counts from 1";
            if (Energy is { } e && !(e > 0)) return "the energy must be above 0";
            if (FieldInplaneMm is { } i && !(i > 0)) return "the inplane field must be above 0 mm";
            if (FieldCrossplaneMm is { } c && !(c > 0)) return "the crossplane field must be above 0 mm";
            if (WedgeAngle is { } w && !(w >= 0 && w <= 90)) return "the wedge angle must be 0 (open) to 90 degrees";
            if (WedgeAngle is > 0 && WedgeDirection is not ("IN" or "OUT" or null)) return "the wedge direction must be IN or OUT";
            return null;
        }
    }

    /// <summary>
    /// The header fixes for a folder of scans, read from and written to one JSON file (by default
    /// <see cref="DefaultFileName"/> in the scans folder, so the fixes travel with the scans). A fix for one scan wins over
    /// one for its whole file. <see cref="Apply"/> gives the scan as if its file had said the fixed values: pairing and
    /// <see cref="ScanHeaderCheck"/> then see them, so a right fix clears the check's flag.
    /// </summary>
    public sealed class ScanHeaderFixes
    {
        public const string DefaultFileName = "dsc-header-fixes.json";

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly List<ScanHeaderFix> _fixes = new();

        public IReadOnlyList<ScanHeaderFix> Fixes => _fixes;

        /// <summary>The file these were read from or last saved to; null for fixes made only in memory.</summary>
        public string? SourcePath { get; private set; }

        public static ScanHeaderFixes Empty => new();

        /// <summary>The scans folder's default fixes file.</summary>
        public static string DefaultPath(string scansFolder) => Path.Combine(scansFolder, DefaultFileName);

        /// <summary>A scan file's key: its path relative to the scans folder, with forward slashes.</summary>
        public static string KeyOf(string scansFolder, string scanFile) => Path.GetRelativePath(scansFolder, scanFile).Replace('\\', '/');

        /// <summary>Reads a fixes file; a missing file is no fixes. Throws on a file that isn't one, or holds a fix that can't be used.</summary>
        public static ScanHeaderFixes Load(string path)
        {
            var fixes = new ScanHeaderFixes { SourcePath = path };
            if (!File.Exists(path)) return fixes;
            var file = JsonSerializer.Deserialize<FixesFile>(File.ReadAllText(path), Json)
                ?? throw new InvalidDataException($"{path} is empty");
            foreach (var f in file.Fixes ?? new List<ScanHeaderFix>())
            {
                if (f.Refusal() is { } why) throw new InvalidDataException($"{path}: the fix for {f.File} can't be used: {why}");
                fixes._fixes.Add(f with { File = f.File.Replace('\\', '/') });
            }
            return fixes;
        }

        public void Save(string path)
        {
            var file = new FixesFile(
                "Header fixes for the water-tank scans in this folder, made in DicomStrictCompare. Each replaces the named values of "
                + "a scan's header (scan null: every scan in the file); the scan files themselves are unchanged.",
                _fixes.OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Scan ?? 0).ToList());
            File.WriteAllText(path, JsonSerializer.Serialize(file, Json));
            SourcePath = path;
        }

        /// <summary>Adds a fix, replacing any for the same file and scan number.</summary>
        public void Set(ScanHeaderFix fix)
        {
            if (fix is null) throw new ArgumentNullException(nameof(fix));
            if (fix.Refusal() is { } why) throw new ArgumentException(why, nameof(fix));
            fix = fix with { File = fix.File.Replace('\\', '/') };
            Remove(fix.File, fix.Scan);
            _fixes.Add(fix);
        }

        /// <summary>Removes the fix for exactly this file and scan number (null: the whole-file fix); true when there was one.</summary>
        public bool Remove(string file, int? scan) =>
            _fixes.RemoveAll(f => string.Equals(f.File, file.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) && f.Scan == scan) > 0;

        /// <summary>The fix for a scan: its own if there is one, else its file's, else null.</summary>
        public ScanHeaderFix? For(string file, int scan)
        {
            file = file.Replace('\\', '/');
            var mine = _fixes.Where(f => string.Equals(f.File, file, StringComparison.OrdinalIgnoreCase)).ToList();
            return mine.FirstOrDefault(f => f.Scan == scan) ?? mine.FirstOrDefault(f => f.Scan is null);
        }

        /// <summary>
        /// The scan as if its header had said the fix's values: the beam's energy, filter, field and wedge, and the raw
        /// header keys they come from (ENERGY, FILTER, FIELD_INPLANE, FIELD_CROSSPLANE, WEDGE_ANGLE, WEDGE), so every
        /// reader sees the same. Everything else, the curve itself included, is the file's.
        /// </summary>
        public static Profile Apply(Profile scan, ScanHeaderFix fix)
        {
            if (scan is null) throw new ArgumentNullException(nameof(scan));
            if (fix is null) throw new ArgumentNullException(nameof(fix));
            var ci = CultureInfo.InvariantCulture;
            var b = scan.Beam;
            var headers = new Dictionary<string, string>(scan.Conditions.RawHeaders, StringComparer.Ordinal);
            if (fix.Energy is { } e) headers["ENERGY"] = e.ToString("0.00", ci);
            if (fix.Fff is { } fff) headers["FILTER"] = fff ? "FFF" : "FF";   // PTW's two values
            if (fix.FieldInplaneMm is { } i) headers["FIELD_INPLANE"] = i.ToString("0.00", ci);
            if (fix.FieldCrossplaneMm is { } c) headers["FIELD_CROSSPLANE"] = c.ToString("0.00", ci);
            double? wedge = b.WedgeAngleDegrees;
            if (fix.WedgeAngle is { } w)
            {
                wedge = w;
                headers["WEDGE_ANGLE"] = w.ToString("0.00", ci);
                if (w > 0) headers["WEDGE"] = $"{w.ToString("0.#", ci)} {fix.WedgeDirection?.ToLowerInvariant()}".TrimEnd();
                else headers.Remove("WEDGE");
            }
            var beam = new BeamKey(b.Modality, fix.Energy ?? b.Energy, fix.Fff ?? b.IsFff,
                fix.FieldInplaneMm is { } fi ? Distance.FromMillimetres(fi) : b.FieldInplane,
                fix.FieldCrossplaneMm is { } fc ? Distance.FromMillimetres(fc) : b.FieldCrossplane,
                b.Ssd, b.GantryAngleDegrees, b.CollimatorAngleDegrees, wedge);
            return new Profile(beam, scan.Scan, scan.Points, scan.Conditions with { RawHeaders = headers }, scan.Source,
                scan.Normalization, scan.Correction, scan.CorrectionDescription, scan.ValueScale);
        }

        private sealed record FixesFile(string? Comment, List<ScanHeaderFix>? Fixes);
    }
}
