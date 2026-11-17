using System.Collections.Concurrent;
using System.Globalization;
using DSClibrary.Comparing;
using DSClibrary.Profiles;

namespace DSCplots;

/// <summary>Which compared pairs of a batch get an image.</summary>
public enum PlotSelection
{
    None,
    /// <summary>Pairs with a point that failed, or none that could be analysed (<see cref="ProfileBatchReport.IsFailing"/>).</summary>
    Failing,
    All
}

/// <summary>
/// One image per compared pair of a <see cref="ProfileBatch"/> run, the figure the windows show (<see cref="ProfilePlot"/>).
/// Files are named by the results row, so row 12 of the CSV is <c>0012 ...png</c>. Used by <c>DSCprofiles --plots</c> and
/// by the windows' Doses vs scans tab (moved here from <c>tools/DSCprofiles</c>, crane, 2026-11-17).
/// </summary>
public static class ProfilePlotExport
{
    /// <summary>
    /// Writes the images the selection asks for into <c>&lt;outputFolder&gt;/&lt;stem&gt;-plots/</c> and returns each row's file,
    /// relative to <paramref name="outputFolder"/>, for the results' <c>plot</c> column.
    /// </summary>
    public static IReadOnlyDictionary<int, string> Write(string outputFolder, string stem, string scansFolder, ProfileBatchResult result,
        PlotSelection which, string format, int width = ProfilePlot.DefaultWidth, int height = ProfilePlot.DefaultHeight,
        int? threads = null, Action<string>? say = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (which == PlotSelection.None) return new Dictionary<int, string>();
        var jobs = Numbered(result)
            .Where(j => j.Pair.Result is { } r && (which == PlotSelection.All || ProfileBatchReport.IsFailing(r)))
            .ToArray();
        string folder = Path.Combine(outputFolder, stem + "-plots");
        var names = WriteInto(folder, scansFolder, jobs, format, width, height, threads, say, cancellationToken);
        return names.ToDictionary(kv => kv.Key, kv => Path.Combine(stem + "-plots", kv.Value));
    }

    /// <summary>
    /// Writes one image per given row into <paramref name="folder"/> and returns each row's file name. Rows without a
    /// result are skipped. In parallel, physical cores by default.
    /// </summary>
    public static IReadOnlyDictionary<int, string> WriteInto(string folder, string scansFolder, IReadOnlyList<(int Row, ProfilePairRow Pair)> rows,
        string format, int width = ProfilePlot.DefaultWidth, int height = ProfilePlot.DefaultHeight,
        int? threads = null, Action<string>? say = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Directory.CreateDirectory(folder);
        var jobs = rows.Where(j => j.Pair.Result is not null).ToArray();
        say?.Invoke($"drawing {jobs.Length} plot(s) into {folder}");

        var written = new ConcurrentDictionary<int, string>();
        int done = 0;
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = threads is > 0 ? threads.Value : CpuTopology.PhysicalCores,
            CancellationToken = cancellationToken,
        };
        Parallel.ForEach(jobs, options, job =>
        {
            string name = FileName(job.Row, job.Pair, format);
            try
            {
                ProfilePlot.Save(job.Pair.Result!, LabelsFor(job.Pair, scansFolder), Path.Combine(folder, name), width, height);
                written[job.Row] = name;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                say?.Invoke($"row {job.Row}: the plot couldn't be written: {ex.Message}");
            }
            int n = Interlocked.Increment(ref done);
            if (n % 200 == 0 || n == jobs.Length) say?.Invoke($"{n} of {jobs.Length} plots drawn");
        });
        return written;
    }

    /// <summary>The batch's rows numbered from 1, as the results CSV numbers them.</summary>
    public static IEnumerable<(int Row, ProfilePairRow Pair)> Numbered(ProfileBatchResult result) =>
        result.Rows.Select((row, i) => (i + 1, row));

    /// <summary>The figure's labels for a pair: the scan named relative to the scans folder.</summary>
    public static ProfilePlotLabels LabelsFor(ProfilePairRow pair, string scansFolder)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return ProfilePlot.LabelsFor(pair.Scan.Profile, $"{Path.GetRelativePath(scansFolder, pair.Scan.File)} scan {pair.Scan.ScanNumber}", pair.Dose);
    }

    /// <summary>"0012 X06 OPEN 10X10 s3 crossplane 100mm - RD.QAtpsPTW.6x 10x10.png": the row, then the pair's own name.</summary>
    public static string FileName(int row, ProfilePairRow pair, string format)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return row.ToString("0000", CultureInfo.InvariantCulture) + " "
            + ProfilePlot.FileName(pair.Scan.File, pair.Scan.ScanNumber, pair.Scan.Profile.Scan, pair.Dose.Dose.SourcePath, format);
    }
}
