using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;

namespace DSCresample;

/// <summary>
/// Resamples every RT Dose under a source folder onto a coarser grid, mirroring the folder layout
/// under a destination, and copies the RT Plans (and any .toml beside them) so the sets stay
/// matchable by plan UID. One progress line per file, timestamped in UTC, so a long run can be
/// followed from its log.
/// </summary>
internal static class Resample
{
    public sealed record Options(string Source, string Destination, double SpacingMm, int Parallelism, bool Overwrite);

    public static int Run(Options o)
    {
        var started = Stopwatch.StartNew();
        var doses = Directory.EnumerateFiles(o.Source, "RD*.dcm", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var sidecars = Directory.EnumerateFiles(o.Source, "*", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).StartsWith("RP", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Log($"{doses.Length} dose files and {sidecars.Length} plan/toml files under {o.Source}");
        Log($"-> {o.Destination} at {o.SpacingMm} mm, parallelism {o.Parallelism}{(o.Overwrite ? ", overwriting" : ", skipping existing")}");

        foreach (string side in sidecars)
        {
            string dst = Destination(o, side);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            if (o.Overwrite || !File.Exists(dst)) File.Copy(side, dst, overwrite: true);
        }
        Log($"copied {sidecars.Length} plan/toml files");

        int done = 0, skipped = 0;
        long bytesIn = 0, bytesOut = 0;
        var failures = new ConcurrentBag<(string File, string Error)>();

        Parallel.ForEach(doses, new ParallelOptions { MaxDegreeOfParallelism = o.Parallelism }, path =>
        {
            string dst = Destination(o, path);
            string rel = Path.GetRelativePath(o.Source, path);
            if (!o.Overwrite && File.Exists(dst))
            {
                Interlocked.Increment(ref skipped);
                return;
            }
            var timer = Stopwatch.StartNew();
            try
            {
                var file = DicomFileReader.ReadFile(path);
                var dose = RTDoseResult.From(file);
                var fine = RTDoseDerivation.RawGrid(dose);
                var coarse = GridResampler.Resample(fine, o.SpacingMm);
                var dataset = RTDoseDerivation.WithGrid(dose, coarse, RTDoseDerivation.ResampleDescription(coarse));

                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                string tmp = dst + ".partial";
                DicomFileWriter.WriteFile(tmp, dataset);
                File.Move(tmp, dst, overwrite: true);

                long inLen = new FileInfo(path).Length, outLen = new FileInfo(dst).Length;
                Interlocked.Add(ref bytesIn, inLen);
                Interlocked.Add(ref bytesOut, outLen);
                int n = Interlocked.Increment(ref done);
                Log(string.Format(CultureInfo.InvariantCulture,
                    "[{0}/{1}] {2}  {3}x{4}x{5} @ {6}/{7}/{8} mm -> {9}x{10}x{11}  {12:0.0} MB -> {13:0.00} MB  {14:0.0} s",
                    n + skipped, doses.Length, rel,
                    fine.DimensionX, fine.DimensionY, fine.DimensionZ, fine.XRes, fine.YRes, fine.ZRes,
                    coarse.DimensionX, coarse.DimensionY, coarse.DimensionZ,
                    inLen / 1048576.0, outLen / 1048576.0, timer.Elapsed.TotalSeconds));
            }
            catch (Exception ex)
            {
                failures.Add((rel, $"{ex.GetType().Name}: {ex.Message}"));
                Log($"FAILED {rel}: {ex.GetType().Name}: {ex.Message}");
            }
        });

        Log(string.Format(CultureInfo.InvariantCulture,
            "done: {0} written, {1} skipped (already present), {2} failed; {3:0.0} GB in -> {4:0.0} MB out; {5:0.0} min",
            done, skipped, failures.Count, bytesIn / 1073741824.0, bytesOut / 1048576.0, started.Elapsed.TotalMinutes));
        foreach (var (file, error) in failures.OrderBy(f => f.File, StringComparer.Ordinal))
            Console.WriteLine($"  {file}: {error}");
        return failures.IsEmpty ? 0 : 1;
    }

    private static string Destination(Options o, string sourceFile) =>
        Path.Combine(o.Destination, Path.GetRelativePath(o.Source, sourceFile));

    private static void Log(string message)
    {
        Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss}Z {message}");
        Console.Out.Flush();
    }
}
