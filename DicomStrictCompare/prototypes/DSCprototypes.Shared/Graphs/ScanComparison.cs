using DSClibrary;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using DSCplots;

namespace DSCprototypes.Shared.Graphs;

/// <summary>What comparing one scan with one dose gave: the result and its plot labels, or why there is none.</summary>
public sealed record ScanComparisonOutcome(ProfileComparisonResult? Result, ProfilePlotLabels? Labels, PhantomFrame? Frame, string Status);

/// <summary>
/// A scan against a calculated dose for the Graphs tab, through the library's comparison
/// (<see cref="ProfileComparison"/>, with the phantom placed by <see cref="PhantomFrame"/>): the same numbers and the
/// same figure as <c>DSCprofiles</c>. Both windows call this, so they differ only in their controls.
/// </summary>
public static class ScanComparison
{
    public static IReadOnlyList<ProfileTest> Tests { get; } = new[] { ProfileTest.Gamma, ProfileTest.Strict };
    public static IReadOnlyList<ProfileReference> References { get; } = new[] { ProfileReference.Measured, ProfileReference.Calculated };
    public static IReadOnlyList<string> PlotFormats { get; } = new[] { "png", "svg" };

    /// <summary>Compares one scan with one dose whose grid is loaded (values in Gy, as <see cref="ComparisonRunner.LoadGrid"/> gives).</summary>
    public static ScanComparisonOutcome Run(ScanDescriptor scan, DoseCandidate candidate, DoseMatrixOptimal grid, ProfileComparisonOptions options)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(options);
        try { options.Validate(); }
        catch (ArgumentOutOfRangeException ex) { return new(null, null, null, $"Invalid criterion: {ex.Message}"); }

        var dose = candidate.Dose;
        if (dose.Beam is null) return new(null, null, null, "The dose has no plan beam, so the phantom can't be placed in it.");
        var (frame, refusal) = PhantomFrame.For(dose.Beam, dose.Plan?.SetupFor(dose.Beam), grid, scan.Profile.Beam.Ssd);
        if (frame is null) return new(null, null, null, $"The phantom can't be placed in this dose: {refusal}");

        try
        {
            var result = ProfileComparison.Compare(scan.Profile, grid, frame, options, dose.Dose.SourcePath);
            var labels = ProfilePlot.LabelsFor(scan.Profile, $"{Path.GetFileName(scan.File)} scan {scan.ScanNumber}", dose);
            var status = new List<string> { ProfilePlot.Verdict(result), frame.Describe() };
            if (candidate.Note.Length > 0) status.Add(candidate.Note);
            status.AddRange(result.Notes);
            return new(result, labels, frame, string.Join("; ", status));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return new(null, null, frame, $"Not compared: {ex.Message}");
        }
    }

    /// <summary>The file a plot of this pair is saved under, the same name <c>DSCprofiles --plots</c> uses after its row number.</summary>
    public static string FileName(ScanDescriptor scan, DoseCandidate candidate, string format) =>
        ProfilePlot.FileName(scan.File, scan.ScanNumber, scan.Profile.Scan, candidate.Dose.Dose.SourcePath, format);

    /// <summary>
    /// Saves a plot of every listed scan against every dose it pairs with, into one folder. Each dose's grid is loaded
    /// once. Returns how many plots were written and the pairs that couldn't be, with the reason.
    /// </summary>
    public static (int Written, IReadOnlyList<string> Failed) SaveAll(IReadOnlyList<ScanDescriptor> scans, IReadOnlyList<RTDoseWithPlan> doses,
        Func<string, DoseMatrixOptimal> loader, ProfileComparisonOptions options, string folder, string format,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scans);
        ArgumentNullException.ThrowIfNull(loader);
        Directory.CreateDirectory(folder);
        var byDose = scans
            .SelectMany(s => ScanCatalog.MatchingDoses(s, doses).Select(c => (Scan: s, Candidate: c)))
            .GroupBy(p => p.Candidate.Dose.Dose.SourcePath, StringComparer.Ordinal)
            .ToArray();
        int total = byDose.Sum(g => g.Count()), written = 0;
        var failed = new List<string>();
        foreach (var group in byDose)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DoseMatrixOptimal grid;
            try { grid = loader(group.Key); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or FormatException or DicomFormatException)
            {
                failed.AddRange(group.Select(p => $"{FileName(p.Scan, p.Candidate, format)}: the dose couldn't be read: {ex.Message}"));
                continue;
            }
            foreach (var (scan, candidate) in group)
            {
                var outcome = Run(scan, candidate, grid, options);
                string name = FileName(scan, candidate, format);
                if (outcome.Result is null) { failed.Add($"{name}: {outcome.Status}"); continue; }
                ProfilePlot.Save(outcome.Result, outcome.Labels!, Path.Combine(folder, name));
                written++;
            }
            progress?.Report($"{written} of {total} plots saved");
        }
        return (written, failed);
    }
}
