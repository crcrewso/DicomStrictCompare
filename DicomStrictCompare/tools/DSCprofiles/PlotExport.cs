using System.Collections.Concurrent;
using System.Globalization;
using DSClibrary.Comparing;
using DSClibrary.Profiles;
using DSCplots;

namespace DSCprofiles;

/// <summary>
/// One image per compared pair, the figure the Graphs tab shows (<see cref="ProfilePlot"/>), into
/// <c>&lt;stem&gt;-plots/</c>. Files are named by the results row, so row 12 of the CSV is <c>0012 ...png</c>.
/// </summary>
internal static class PlotExport
{
    /// <summary>Which pairs get an image.</summary>
    public enum Which { All, Failing }

    /// <summary>Writes the images and returns each row's file, relative to <paramref name="outputFolder"/>.</summary>
    public static IReadOnlyDictionary<int, string> Write(string outputFolder, string stem, string scansFolder, ProfileBatchResult result,
        Which which, string format, int width, int height, int? threads, Action<string> say)
    {
        string folder = Path.Combine(outputFolder, stem + "-plots");
        Directory.CreateDirectory(folder);
        var jobs = result.Rows
            .Select((row, i) => (Row: i + 1, Pair: row))
            .Where(j => j.Pair.Result is { } r && (which == Which.All || r.Passed < r.Analysed || r.Analysed == 0))
            .ToArray();
        say($"drawing {jobs.Length} plot(s) into {folder}");

        var written = new ConcurrentDictionary<int, string>();
        int done = 0;
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = threads is > 0 ? threads.Value : CpuTopology.PhysicalCores }, job =>
        {
            var pair = job.Pair;
            string scanName = $"{Path.GetRelativePath(scansFolder, pair.Scan.File)} scan {pair.Scan.ScanNumber}";
            var labels = ProfilePlot.LabelsFor(pair.Scan.Profile, scanName, pair.Dose);
            string name = FileName(job.Row, pair, format);
            try
            {
                ProfilePlot.Save(pair.Result!, labels, Path.Combine(folder, name), width, height);
                written[job.Row] = Path.Combine(stem + "-plots", name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                say($"row {job.Row}: the plot couldn't be written: {ex.Message}");
            }
            int n = Interlocked.Increment(ref done);
            if (n % 200 == 0 || n == jobs.Length) say($"{n} of {jobs.Length} plots drawn");
        });
        return written;
    }

    /// <summary>"0012 X06 OPEN 10X10 s3 crossplane 100mm - RD.QAtpsPTW.6x 10x10.png": the row, then the name the Graphs tab saves under.</summary>
    internal static string FileName(int row, ProfilePairRow pair, string format) =>
        row.ToString("0000", CultureInfo.InvariantCulture) + " "
        + ProfilePlot.FileName(pair.Scan.File, pair.Scan.ScanNumber, pair.Scan.Profile.Scan, pair.Dose.Dose.SourcePath, format);
}
