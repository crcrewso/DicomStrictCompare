using System.Globalization;
using System.Text.RegularExpressions;
using DSClibrary.Profiles;
using DSClibrary.Results.PTW;
using DSCplots;
using DSCprototypes.Shared.ViewModels;

namespace DSCprototypes.Shared.Graphs;

/// <summary>How a scan-to-dose pair came out.</summary>
public enum ScanOutcome { Passed, Failed, NotCompared }

/// <summary>
/// One row of the Doses vs scans tab: a <see cref="ProfilePairRow"/> of the library's batch, with the labels the tab's
/// table and filters show. <see cref="Row"/> is the row's number in the results CSV, so a row on screen, its plot file
/// and its CSV line share a number.
/// </summary>
public sealed class ScanPairRow
{
    public required int Row { get; init; }
    public required ProfilePairRow Pair { get; init; }
    /// <summary>"Raw Data\X06 OPEN 10X10.mcc scan 3": the file relative to the scans folder, and the scan's number in it.</summary>
    public required string Scan { get; init; }
    public required string Energy { get; init; }
    public required string Field { get; init; }
    public required string Curve { get; init; }
    public required string Depth { get; init; }
    public required string DoseFile { get; init; }
    public required string Plan { get; init; }
    public required string Beam { get; init; }
    /// <summary>The pass rate, "97.31", or blank when not compared.</summary>
    public required string PassRate { get; init; }
    public required string Points { get; init; }
    public required string MaxGamma { get; init; }
    public required ScanOutcome Outcome { get; init; }
    /// <summary>"pass", "fail", or "not compared: " and why.</summary>
    public required string OutcomeText { get; init; }
    /// <summary>"check" when the scan's data disagrees with its header (<see cref="ScanHeaderCheck"/>), else blank.</summary>
    public string HeaderFlag => Pair.HeaderCheck.Agrees ? "" : "check";

    // Sort keys for the filter lists.
    internal double EnergyValue { get; init; }
    internal bool IsElectron { get; init; }
    internal bool IsFff { get; init; }
    internal double FieldArea { get; init; }
}

/// <summary>
/// The Doses vs scans tab's work, shared by both windows: the library's batch (<see cref="ProfileBatch"/>, the same one
/// <c>DSCprofiles</c> runs, so the window and the command line give the same rows), its rows labelled and filtered, the
/// figure (<see cref="ProfilePlot"/>) and the export (<see cref="ProfileBatchReport"/>, <see cref="ProfilePlotExport"/>:
/// the files <c>DSCprofiles</c> writes). Separate from the dose-versus-dose controls since 2026-11-17: a scan comparison
/// has its own doses folder, its own one-dimensional criterion and its own results.
/// </summary>
public static class ScanRows
{
    public const string AnyEnergy = PairCatalog.AnyEnergy;
    public const string AnyField = PairCatalog.AnyFieldSize;
    public const string AnyCurve = "Any curve";
    public const string AllPairs = "All pairs";
    public const string FailingPairs = "Failing";
    public const string NotComparedPairs = "Not compared";
    public const string HeaderDisagrees = "Header disagrees";
    /// <summary>The export's file stem, the same default as <c>DSCprofiles</c>.</summary>
    public const string Stem = "profiles";

    public static IReadOnlyList<ProfileTest> Tests { get; } = new[] { ProfileTest.Gamma, ProfileTest.Strict };
    public static IReadOnlyList<ProfileReference> References { get; } = new[] { ProfileReference.Measured, ProfileReference.Calculated };
    public static IReadOnlyList<string> PlotFormats { get; } = new[] { "png", "svg" };
    public static IReadOnlyList<PlotSelection> PlotSelections { get; } = new[] { PlotSelection.Failing, PlotSelection.All, PlotSelection.None };
    public static IReadOnlyList<string> Shows { get; } = new[] { AllPairs, FailingPairs, NotComparedPairs, HeaderDisagrees };

    public const string ConventionNote =
        "Prototype (2026-11-17): every scan pairs with every dose of the same field (ScanDoseMatching: energy, filter, field and its centre, "
        + "EDW, SSD when the plan has one); the water surface comes from the plan, else the scan's SSD, checked against the dose (PhantomFrame). "
        + "Depth doses are normalised to their maximum, profiles at the central axis. The same batch as DSCprofiles.";

    /// <summary>The criterion as the tab's controls describe it; the library's defaults for the rest.</summary>
    public static ProfileComparisonOptions Options(double dosePercent, double distanceMm, ProfileTest test, ProfileReference reference,
        bool local, double cutoffPercent, bool depthShift) =>
        new(dosePercent, distanceMm, test, reference, cutoffPercent, cutoffPercent, local,
            DepthShift: depthShift ? new DepthShiftSearch() : null);

    /// <summary>Why the inputs can't run, or null: missing folders, a pattern that isn't a regular expression, an invalid criterion.</summary>
    public static string? Refusal(string dosesFolder, string scansFolder, string? planFilter, string? excludeFolders, ProfileComparisonOptions options)
    {
        if (!Directory.Exists(dosesFolder)) return "Choose a doses folder of RT Dose and RT Plan files.";
        if (!Directory.Exists(scansFolder)) return "Choose a scans folder of PTW mcc files.";
        foreach (var (name, pattern) in new[] { ("Plan label filter", planFilter), ("Skip scan folders", excludeFolders) })
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;
            try { _ = new Regex(pattern); }
            catch (ArgumentException ex) { return $"{name} isn't a regular expression: {ex.Message}"; }
        }
        try { options.Validate(); }
        catch (ArgumentOutOfRangeException ex) { return $"Invalid criterion: {ex.Message}"; }
        return null;
    }

    /// <summary>
    /// The scans folder's header fixes (<see cref="ScanHeaderFixes.DefaultPath"/>; none when the file isn't there), or
    /// null with a sentence saying why the file can't be used.
    /// </summary>
    public static (ScanHeaderFixes? Fixes, string? Problem) LoadFixes(string scansFolder)
    {
        string path = ScanHeaderFixes.DefaultPath(scansFolder);
        try { return (ScanHeaderFixes.Load(path), null); }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        { return (null, $"The header fixes in {path} can't be read: {ex.Message}"); }
    }

    /// <summary>How many scans a run read from one file, paired or not: what "every scan in this file" covers.</summary>
    public static int ScansInFile(ProfileBatchResult result, string file)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Math.Max(1, result.Rows.Select(r => r.Scan).Concat(result.Unmatched.Select(u => u.Scan))
            .Where(s => s.File == file).Select(s => s.ScanNumber).Distinct().Count());
    }

    /// <summary>
    /// Carries out the header-fix form's answer: saves the fix (a whole-file fix replaces the scan's own) or removes the
    /// scan's and its file's, in the form's fixes file. Returns the status line to show, or null for Cancel.
    /// </summary>
    public static string? Answer(ScanHeaderFixes fixes, HeaderFixViewModel form, HeaderFixOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(fixes);
        ArgumentNullException.ThrowIfNull(form);
        string scanName = $"{form.File} scan {form.ScanNumber}";
        try
        {
            switch (outcome)
            {
                case HeaderFixOutcome.Save when form.ToFix() is { } fix:
                    if (fix.Scan is null) fixes.Remove(fix.File, form.ScanNumber);
                    fixes.Set(fix);
                    fixes.Save(form.FixesPath);
                    return $"Header fix saved for {(fix.Scan is null ? $"every scan in {fix.File}" : scanName)}: {fix.Describe()}, in {form.FixesPath}. "
                        + "Run again to apply it.";
                case HeaderFixOutcome.Save:
                    return $"The header fix wasn't saved: {form.Problem}";
                case HeaderFixOutcome.Remove:
                    bool removed = fixes.Remove(form.File, form.ScanNumber) | fixes.Remove(form.File, null);
                    fixes.Save(form.FixesPath);
                    return removed ? $"Header fix removed for {scanName} (and its file's). Run again to use the file's header." : $"{scanName} had no fix.";
                default:
                    return null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"The header fixes couldn't be saved in {form.FixesPath}: {ex.Message}";
        }
    }

    /// <summary>The batch's rows, numbered from 1 as the results CSV numbers them.</summary>
    public static IReadOnlyList<ScanPairRow> Build(ProfileBatchResult result, string scansFolder)
    {
        ArgumentNullException.ThrowIfNull(result);
        var ci = CultureInfo.InvariantCulture;
        return ProfilePlotExport.Numbered(result).Select(n =>
        {
            var (row, pair) = n;
            var profile = pair.Scan.Profile;
            var beam = profile.Beam;
            var setup = ScanDoseMatching.FromScan(profile).Setup;
            var x = pair.Result;
            var outcome = x is null ? ScanOutcome.NotCompared : ProfileBatchReport.IsFailing(x) ? ScanOutcome.Failed : ScanOutcome.Passed;
            double cross = beam.FieldCrossplane?.Millimetres ?? setup?.CrossplaneMm ?? 0;
            double inplane = beam.FieldInplane?.Millimetres ?? setup?.InplaneMm ?? 0;
            return new ScanPairRow
            {
                Row = row,
                Pair = pair,
                Scan = $"{Path.GetRelativePath(scansFolder, pair.Scan.File)} scan {pair.Scan.ScanNumber}",
                Energy = ScanCatalog.EnergyLabel(beam),
                Field = setup is not null ? ScanCatalog.FieldLabel(setup) : cross > 0 ? ScanCatalog.FieldSizeLabel(cross, inplane) : PairCatalog.Unknown,
                Curve = CurveName(profile.Scan.CurveType),
                Depth = profile.Scan.CurveType == CurveType.Pdd ? "" : profile.Scan.Depth is { } d ? d.Millimetres.ToString("0.#", ci) : "",
                DoseFile = Path.GetFileName(pair.Dose.Dose.SourcePath),
                Plan = pair.Dose.Plan?.RTPlanLabel ?? "",
                Beam = pair.Dose.Beam?.BeamName ?? "",
                PassRate = x is not null && double.IsFinite(x.PassRate) ? x.PassRate.ToString("0.00", ci) : "",
                Points = x is null ? "" : $"{x.Passed.ToString(ci)} / {x.Analysed.ToString(ci)}",
                MaxGamma = x is not null && x.MaxGammaFound is { } g && double.IsFinite(g) ? g.ToString("0.00", ci) : "",
                Outcome = outcome,
                OutcomeText = outcome switch
                {
                    ScanOutcome.Passed => "pass",
                    ScanOutcome.Failed => "fail",
                    _ => "not compared: " + (pair.Failure ?? "no result"),
                },
                EnergyValue = beam.Energy ?? double.MaxValue,
                IsElectron = beam.Modality == Modality.Electron,
                IsFff = beam.IsFff == true,
                FieldArea = cross * inplane,
            };
        }).ToArray();
    }

    /// <summary>"Depth dose", "Crossplane", "Inplane", or the curve type's own name.</summary>
    public static string CurveName(CurveType type) => type switch
    {
        CurveType.Pdd => "Depth dose",
        CurveType.Crossplane => "Crossplane",
        CurveType.Inplane => "Inplane",
        _ => type.ToString()
    };

    /// <summary>The scans no dose paired with and the files that couldn't be read, one line each.</summary>
    public static IReadOnlyList<string> Unpaired(ProfileBatchResult result, string scansFolder)
    {
        ArgumentNullException.ThrowIfNull(result);
        var lines = result.Unmatched
            .Select(u => $"{Path.GetRelativePath(scansFolder, u.Scan.File)} scan {u.Scan.ScanNumber}, {ProfilePlot.Describe(u.Scan.Profile.Scan)}: {u.Reason}"
                + (u.HeaderCheck.Summary is { } check ? "; check the header: " + check : ""))
            .ToList();
        lines.AddRange(result.UnreadableScans.Select(u => $"{u.Path}: unreadable scan: {u.Reason}"));
        lines.AddRange(result.UnreadableDoses.Select(u => $"{u.Path}: unreadable dose: {u.Reason}"));
        return lines;
    }

    public static IReadOnlyList<string> Energies(IEnumerable<ScanPairRow> rows) =>
        new[] { AnyEnergy }.Concat(rows.OrderBy(r => r.IsElectron).ThenBy(r => r.EnergyValue).ThenBy(r => r.IsFff)
            .Select(r => r.Energy).Distinct()).ToArray();

    public static IReadOnlyList<string> Fields(IEnumerable<ScanPairRow> rows) =>
        new[] { AnyField }.Concat(rows.OrderBy(r => r.FieldArea).ThenBy(r => r.Field, StringComparer.Ordinal)
            .Select(r => r.Field).Distinct()).ToArray();

    public static IReadOnlyList<string> Curves(IEnumerable<ScanPairRow> rows) =>
        new[] { AnyCurve }.Concat(rows.Select(r => r.Curve).Distinct().OrderBy(c => c, StringComparer.Ordinal)).ToArray();

    /// <summary>The rows the filters keep; null or an "any" choice keeps everything on that axis, and a blank search every row.</summary>
    public static IReadOnlyList<ScanPairRow> Filter(IEnumerable<ScanPairRow> rows, string? energy, string? field, string? curve, string? show,
        string? find = null) =>
        Filter(rows, energy is null or AnyEnergy ? null : new HashSet<string> { energy }, field is null or AnyField ? null : new HashSet<string> { field },
            curve, show, find);

    /// <summary>The rows the filters keep, with several energies and field sizes allowed at once; a null set keeps every one.</summary>
    public static IReadOnlyList<ScanPairRow> Filter(IEnumerable<ScanPairRow> rows, IReadOnlySet<string>? energies, IReadOnlySet<string>? fields,
        string? curve, string? show, string? find)
    {
        var words = Words(find);
        return rows.Where(r => (energies is null || energies.Contains(r.Energy))
                     && (fields is null || fields.Contains(r.Field))
                     && (curve is null or AnyCurve || r.Curve == curve)
                     && (words.Length == 0 || Matches(r, words))
                     && show switch
                     {
                         FailingPairs => r.Outcome == ScanOutcome.Failed,
                         NotComparedPairs => r.Outcome == ScanOutcome.NotCompared,
                         HeaderDisagrees => !r.Pair.HeaderCheck.Agrees,
                         _ => true
                     }).ToArray();
    }

    /// <summary>
    /// The search box's words, each lower-cased with its spaces gone, so "10FFF 6x6" is two words and "6 x 6" one.
    /// A word in quotes keeps its spaces out too: "6 x 6" (quoted) is the word 6x6.
    /// </summary>
    public static string[] Words(string? find)
    {
        if (string.IsNullOrWhiteSpace(find)) return Array.Empty<string>();
        var words = new List<string>();
        foreach (Match m in Regex.Matches(find, "\"([^\"]*)\"|(\\S+)"))
        {
            string w = Squash(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
            if (w.Length > 0) words.Add(w);
        }
        return words.ToArray();
    }

    /// <summary>
    /// Whether a row contains every word somewhere in what it shows or names: the scan's file and folder, energy, field,
    /// curve, depth, dose file, plan, beam and outcome, compared lower-case with spaces removed. So "10FFF 6x6" keeps
    /// the 10FFF folder's 6X6 files, and "10mvfff 6x6cm" the same rows by their energy and field columns.
    /// </summary>
    public static bool Matches(ScanPairRow row, IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(row);
        string text = Squash(string.Join("|", row.Scan, row.Energy, row.Field, row.Curve, row.Depth + "mm", row.DoseFile, row.Plan, row.Beam,
            row.OutcomeText, row.HeaderFlag));
        return words.All(w => Contains(text, w));
    }

    /// <summary>Whether text holds the word where no digit runs on from it, so 6x6 isn't found in 16x16 nor 50 in 150.</summary>
    private static bool Contains(string text, string word)
    {
        for (int at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.Ordinal))
        {
            int end = at + word.Length;
            bool before = at == 0 || !char.IsDigit(word[0]) || !char.IsDigit(text[at - 1]);
            bool after = end == text.Length || !char.IsDigit(word[^1]) || !char.IsDigit(text[end]);
            if (before && after) return true;
        }
        return false;
    }

    /// <summary>
    /// Lower-cased with spaces removed, except a space between two digits, which becomes a separator: "6 x 6 cm" is 6x6cm
    /// but "260616 14'16'17" stays two numbers (260616|14'16'17), so whole-number matching still finds 14'16'17.
    /// </summary>
    private static string Squash(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsWhiteSpace(s[i])) { sb.Append(char.ToLowerInvariant(s[i])); continue; }
            int next = i;
            while (next < s.Length && char.IsWhiteSpace(s[next])) next++;
            if (sb.Length > 0 && char.IsDigit(sb[^1]) && next < s.Length && char.IsDigit(s[next])) sb.Append('|');
            i = next - 1;
        }
        return sb.ToString();
    }

    /// <summary>The run in a line: pairs, how they came out, what didn't pair, how long it took.</summary>
    public static string Summary(ProfileBatchResult result, IReadOnlyList<ScanPairRow> rows, ProfileComparisonOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        int passed = rows.Count(r => r.Outcome == ScanOutcome.Passed), failed = rows.Count(r => r.Outcome == ScanOutcome.Failed);
        int none = rows.Count - passed - failed;
        int flagged = rows.Count(r => !r.Pair.HeaderCheck.Agrees);
        return $"{ProfilePlot.Criterion(options)}: {rows.Count} pair(s) of {result.Scans} scan(s) in {result.ScanFiles} file(s) with {result.Doses} dose(s); "
             + $"{passed} pass every point, {failed} fail somewhere, {none} not compared; {result.Unmatched.Count} scan(s) unpaired; "
             + (flagged > 0 ? $"{flagged} pair(s) whose scan disagrees with its header; " : "")
             + $"{result.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";
    }

    /// <summary>What a selected row says beyond its columns: the verdict, where the phantom was put, the pairing's notes.</summary>
    public static string Details(ScanPairRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var pair = row.Pair;
        var parts = new List<string> { $"Row {row.Row}: {row.Scan} against {row.DoseFile} ({row.Plan} / {row.Beam})" };
        if (pair.HeaderCheck.Summary is { } check) parts.Add("CHECK THE SCAN'S HEADER: " + check);
        if (pair.Result is { } x) parts.Add(ProfilePlot.Verdict(x));
        if (pair.Frame is { } frame) parts.Add(frame.Describe());
        parts.AddRange(pair.PairingNotes);
        if (pair.Result is { } r) parts.AddRange(r.Notes);
        if (pair.Failure is { } f) parts.Add("not compared: " + f);
        return string.Join("; ", parts);
    }

    /// <summary>The figure's labels for a row.</summary>
    public static ProfilePlotLabels Labels(ScanPairRow row, string scansFolder) => ProfilePlotExport.LabelsFor(row.Pair, scansFolder);

    /// <summary>The file a row's plot is saved under: its row number, then the pair's name, as <c>DSCprofiles --plots</c> names it.</summary>
    public static string PlotFileName(ScanPairRow row, string format) => ProfilePlotExport.FileName(row.Row, row.Pair, format);

    /// <summary>
    /// Writes what <c>DSCprofiles</c> writes for this run into one folder: the results, unpaired and settings files, and the
    /// chosen plots in <c>profiles-plots/</c>, named in the results' plot column. Returns a line saying what was written.
    /// </summary>
    public static string Export(string folder, string scansFolder, string dosesFolder, ProfileComparisonOptions options, string? planFilter,
        string? excludeFolders, ProfileBatchResult result, PlotSelection plots, string format, Action<string>? say = null, CancellationToken cancellationToken = default)
    {
        var plotFiles = ProfilePlotExport.Write(folder, Stem, scansFolder, result, plots, format, say: say, cancellationToken: cancellationToken);
        var written = ProfileBatchReport.Write(folder, Stem, scansFolder, dosesFolder, options, NullIfBlank(planFilter), NullIfBlank(excludeFolders),
            result, curves: false, plotFiles, tool: "DSC prototype window");
        return $"Wrote {string.Join(", ", written.Select(Path.GetFileName))}" + (plotFiles.Count > 0 ? $" and {plotFiles.Count} plot(s) in {Stem}-plots" : "") + $" into {folder}";
    }

    public static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Hands the batch's freed dose grids back to the system. A batch reads every paired dose once, some in parallel, and
    /// the rows keep none of them; but without memory pressure the runtime keeps the freed heap, which the window then
    /// holds for as long as it is open (measured 2026-11-17: 10 GB after the Equinox folders' 2,537 pairs). Once per run.
    /// </summary>
    public static void ReleaseGrids() => GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
}
