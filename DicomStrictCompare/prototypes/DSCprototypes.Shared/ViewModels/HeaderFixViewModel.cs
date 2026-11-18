using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DSClibrary;
using DSClibrary.Profiles;
using DSCprototypes.Shared.Graphs;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>What the person chose in the header-fix form.</summary>
public enum HeaderFixOutcome { Cancel, Save, Remove }

/// <summary>
/// The form for fixing one scan's header (crcrewso, 2026-11-17: "lets users fix the metadata per profile"): filled from
/// the scan's fix if it has one, else from its file's header, so only what is wrong needs typing. Saved as a
/// <see cref="ScanHeaderFix"/> in the scans folder's fixes file; the scan file is never changed.
/// </summary>
public sealed partial class HeaderFixViewModel : ObservableObject
{
    public static IReadOnlyList<string> WedgeAngles { get; } = new[] { "open", "10", "15", "20", "25", "30", "45", "60" };
    public static IReadOnlyList<string> Directions { get; } = new[] { "IN", "OUT" };

    /// <summary>The scan file, relative to the scans folder, and the scan's number in it.</summary>
    public required string File { get; init; }
    public required int ScanNumber { get; init; }
    /// <summary>How many scans the file holds, so the form can offer "every scan in this file".</summary>
    public required int ScansInFile { get; init; }
    /// <summary>"The file says: Photon 10MV 100 mm x 100 mm, no wedge".</summary>
    public required string FileSays { get; init; }
    /// <summary>What the header check found, or blank.</summary>
    public required string CheckSays { get; init; }
    /// <summary>Whether a fix is saved for this scan (or its file) already: Remove is offered then.</summary>
    public required bool HasFix { get; init; }
    /// <summary>The fixes file the form saves to.</summary>
    public required string FixesPath { get; init; }

    [ObservableProperty] private bool _wholeFile;
    [ObservableProperty] private string _energy = "";
    [ObservableProperty] private bool _fff;
    [ObservableProperty] private string _fieldCrossplaneMm = "";
    [ObservableProperty] private string _fieldInplaneMm = "";
    [ObservableProperty] private string _wedgeAngle = "open";
    [ObservableProperty] private string _wedgeDirection = "IN";
    [ObservableProperty] private string _note = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasProblem))] private string _problem = "";

    public bool HasProblem => Problem.Length > 0;
    public bool HasCheck => CheckSays.Length > 0;
    public bool CanFixWholeFile => ScansInFile > 1;

    public bool IsElectron { get; init; }

    /// <summary>The form for a row's scan, filled from its fix if it has one, else from its header.</summary>
    public static HeaderFixViewModel For(ScanPairRow row, string scansFolder, ScanHeaderFixes fixes, int scansInFile)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(fixes);
        var scan = row.Pair.Scan;
        string key = ScanHeaderFixes.KeyOf(scansFolder, scan.File);
        var fix = fixes.For(key, scan.ScanNumber);
        var original = scan.OriginalBeam ?? scan.Profile.Beam;   // the file's own header
        var now = scan.Profile.Beam;                              // as the last run used it, fixed or not
        var ci = CultureInfo.InvariantCulture;
        scan.Profile.Conditions.RawHeaders.TryGetValue("WEDGE", out string? wedgeText);
        var (angle, direction) = ScanDoseMatching.ScanWedge(now.WedgeAngleDegrees, wedgeText);
        string? originalWedgeText = null;
        if (scan.OriginalBeam is null) originalWedgeText = wedgeText;
        return new HeaderFixViewModel
        {
            File = key,
            ScanNumber = scan.ScanNumber,
            ScansInFile = scansInFile,
            FileSays = $"The file says: {original}"
                + (original.WedgeAngleDegrees is > 0 || originalWedgeText is { Length: > 0 } ? $", wedge {originalWedgeText ?? original.WedgeAngleDegrees?.ToString("0.#", ci)}" : ", no wedge"),
            CheckSays = row.Pair.HeaderCheck.Summary is { } s ? "The header check says: " + s : "",
            HasFix = fix is not null,
            FixesPath = fixes.SourcePath ?? ScanHeaderFixes.DefaultPath(scansFolder),
            IsElectron = now.Modality == Modality.Electron,
            WholeFile = fix is { Scan: null } && scansInFile > 1,
            Energy = now.Energy?.ToString("0.##", ci) ?? "",
            Fff = now.IsFff == true,
            FieldCrossplaneMm = now.FieldCrossplane?.Millimetres.ToString("0.#", ci) ?? "",
            FieldInplaneMm = now.FieldInplane?.Millimetres.ToString("0.#", ci) ?? "",
            WedgeAngle = angle is > 0 ? angle.Value.ToString("0.#", ci) : "open",
            WedgeDirection = direction ?? "IN",
            Note = fix?.Note ?? "",
        };
    }

    /// <summary>The fix the form describes, or null with <see cref="Problem"/> set when a value can't be used.</summary>
    public ScanHeaderFix? ToFix()
    {
        var ci = CultureInfo.InvariantCulture;
        double? Number(string text, string what)
        {
            if (double.TryParse(text.Trim(), NumberStyles.Float, ci, out double v) && v > 0) return v;
            Problem = $"{what} must be a number above 0.";
            return null;
        }
        Problem = "";
        if (Number(Energy, "The energy") is not { } energy) return null;
        if (Number(FieldCrossplaneMm, "The crossplane field") is not { } cross) return null;
        if (Number(FieldInplaneMm, "The inplane field") is not { } inplane) return null;
        double wedge = WedgeAngle == "open" ? 0 : double.Parse(WedgeAngle, ci);
        var fix = new ScanHeaderFix(File, WholeFile ? null : ScanNumber, energy, IsElectron ? null : Fff, inplane, cross, wedge,
            wedge > 0 ? WedgeDirection : null, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(), DateTime.UtcNow);
        if (fix.Refusal() is { } why) { Problem = why; return null; }
        return fix;
    }
}
