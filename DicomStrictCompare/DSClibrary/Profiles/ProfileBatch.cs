using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSClibrary.Comparing;
using DSClibrary.Mapping.PTW;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using DSClibrary.Results.PTW;

namespace DSClibrary.Profiles
{
    /// <summary>The settings of a batch of scan-to-dose comparisons.</summary>
    /// <param name="Comparison">The criterion every pair is compared at.</param>
    /// <param name="PlanLabelPattern">A regular expression on the plan label; doses whose plan doesn't match are left out. Null for all.</param>
    /// <param name="ExcludeFolderPattern">A regular expression on each scan folder's name, relative to the scans folder; a
    /// file under a matching folder is skipped. For example <c>^zz</c> leaves out folders kept for reference.</param>
    /// <param name="Threads">Doses loaded at once; by default the physical cores, fewer when half the free memory won't
    /// hold that many of the largest grid.</param>
    /// <param name="HeaderFixes">People's corrections to scan headers (<see cref="ScanHeaderFixes"/>), applied as each
    /// scan is read; null for none.</param>
    public sealed record ProfileBatchOptions(
        ProfileComparisonOptions Comparison,
        string? PlanLabelPattern = null,
        string? ExcludeFolderPattern = null,
        int? Threads = null,
        ScanHeaderFixes? HeaderFixes = null);

    /// <summary>One water-tank scan: its file, its number within the file and its profile as measured.</summary>
    public sealed record ScanRef(string File, string RelativeFolder, int ScanNumber, Profile Profile)
    {
        /// <summary>The header fix applied to <see cref="Profile"/>, if any; <see cref="OriginalBeam"/> is then the file's own.</summary>
        public ScanHeaderFix? HeaderFix { get; init; }
        public BeamKey? OriginalBeam { get; init; }

        /// <summary>"header fixed by hand: wedge 15 OUT (the file says open 10 x 10 cm); why", or null.</summary>
        public string? HeaderFixNote => HeaderFix is { } f
            ? $"header fixed by hand: {f.Describe()} (the file says {OriginalBeam})" + (string.IsNullOrWhiteSpace(f.Note) ? "" : $"; {f.Note}")
            : null;
    }

    /// <summary>One scan compared with one dose. <see cref="Failure"/> says why there's no <see cref="Result"/>.</summary>
    public sealed record ProfilePairRow(
        ScanRef Scan,
        RTDoseWithPlan Dose,
        string? Algorithm,
        BeamSetup Plan,
        IReadOnlyList<string> PairingNotes,
        PhantomFrame? Frame,
        ProfileComparisonResult? Result,
        string? Failure)
    {
        /// <summary>The scan checked against its own header (<see cref="ScanHeaderCheck"/>); a flag, never a change to the result.</summary>
        public ScanHeaderCheckResult HeaderCheck { get; init; } = ScanHeaderCheckResult.NotChecked;
    }

    /// <summary>A scan that no dose pairs with, and the reason.</summary>
    public sealed record UnmatchedScan(ScanRef Scan, string Reason)
    {
        /// <summary>The scan checked against its own header: a wrong header is often why nothing pairs.</summary>
        public ScanHeaderCheckResult HeaderCheck { get; init; } = ScanHeaderCheckResult.NotChecked;
    }

    /// <summary>What a batch found.</summary>
    public sealed class ProfileBatchResult
    {
        public required IReadOnlyList<ProfilePairRow> Rows { get; init; }
        public required IReadOnlyList<UnmatchedScan> Unmatched { get; init; }
        public required IReadOnlyList<(string Path, string Reason)> UnreadableScans { get; init; }
        public required IReadOnlyList<(string Path, string Reason)> UnreadableDoses { get; init; }
        public required int ScanFiles { get; init; }
        public required int Scans { get; init; }
        public required int Doses { get; init; }
        public required int Threads { get; init; }
        public required TimeSpan Elapsed { get; init; }
        /// <summary>The header fixes file the run used, or null.</summary>
        public string? HeaderFixesPath { get; init; }
    }

    /// <summary>
    /// Compares every water-tank scan under one folder with every calculated dose under another that pairs with it
    /// (<see cref="ScanDoseMatching"/>). Doses are read headers only to pair; each paired dose's grid is then loaded once,
    /// streamed (<see cref="DoseGridReader"/>), every scan paired with it is compared, and the grid is let go.
    /// Rows come back sorted by scan file, scan number and dose path, so a run's output is stable.
    /// </summary>
    public static class ProfileBatch
    {
        public static ProfileBatchResult Run(string scansFolder, string dosesFolder, ProfileBatchOptions options,
            Action<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (!Directory.Exists(scansFolder)) throw new DirectoryNotFoundException(scansFolder);
            if (!Directory.Exists(dosesFolder)) throw new DirectoryNotFoundException(dosesFolder);
            options.Comparison.Validate();
            var clock = Stopwatch.StartNew();

            var (scans, scanFiles, unreadableScans) = ReadScans(scansFolder, options.ExcludeFolderPattern, progress, options.HeaderFixes);
            progress?.Invoke($"{scans.Count} scans in {scanFiles} files; reading dose headers");

            var folder = RTFolderReader.Read(dosesFolder, recursive: true, headersOnly: true);
            var labelFilter = options.PlanLabelPattern is { Length: > 0 } pattern ? new Regex(pattern, RegexOptions.IgnoreCase) : null;
            var doses = folder.Doses.Where(d => d.Beam is not null
                && (labelFilter is null || labelFilter.IsMatch(d.Plan?.RTPlanLabel ?? ""))).ToArray();
            progress?.Invoke($"{doses.Length} doses with a plan beam{(labelFilter is null ? "" : " and a matching plan label")}");

            // Pair, then group by dose so each grid is loaded once.
            var pairs = new List<(ScanRef Scan, ScanDoseCandidate Candidate)>();
            var unmatched = new List<UnmatchedScan>();
            var checks = new Dictionary<ScanRef, ScanHeaderCheckResult>(ReferenceEqualityComparer.Instance);
            foreach (var scan in scans)
            {
                var check = checks[scan] = ScanHeaderCheck.Check(scan.Profile);
                if (scan.Profile.Scan.CurveType is not (CurveType.Pdd or CurveType.Crossplane or CurveType.Inplane))
                {
                    unmatched.Add(new UnmatchedScan(scan, $"{scan.Profile.Scan.CurveType} scans aren't compared yet") { HeaderCheck = check });
                    continue;
                }
                var (candidates, reason) = ScanDoseMatching.Match(scan.Profile, doses);
                if (candidates.Count == 0)
                    unmatched.Add(new UnmatchedScan(scan, (reason ?? "no dose pairs with it") + (scan.HeaderFixNote is { } n ? "; " + n : "")) { HeaderCheck = check });
                foreach (var c in candidates) pairs.Add((scan, c));
            }
            var byDose = pairs.GroupBy(p => p.Candidate.Dose.Dose.SourcePath, StringComparer.Ordinal).ToArray();
            progress?.Invoke($"{pairs.Count} scan-dose pairs over {byDose.Length} doses; {unmatched.Count} scans unpaired");

            int threads = Threads(options.Threads, byDose.Select(g => g.First().Candidate.Dose.Dose));
            var rows = new ConcurrentBag<ProfilePairRow>();
            int done = 0;
            Parallel.ForEach(byDose, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = cancellationToken }, group =>
            {
                var dose = group.First().Candidate.Dose;
                string? algorithm = AlgorithmTokens.Default.Of(dose);
                DoseMatrixOptimal? grid = null;
                string? loadFailure = null;
                try { grid = DoseGridReader.Read(dose.Dose.SourcePath); }
                catch (Exception ex) when (ex is IOException or DicomFormatException or InvalidOperationException or OutOfMemoryException)
                { loadFailure = "the dose couldn't be read: " + ex.Message; }

                foreach (var (scan, candidate) in group)
                {
                    if (grid is null)
                    {
                        rows.Add(new ProfilePairRow(scan, dose, algorithm, candidate.Plan, candidate.Notes, null, null, loadFailure));
                        continue;
                    }
                    var (frame, refusal) = PhantomFrame.For(dose.Beam, dose.Plan?.SetupFor(dose.Beam!), grid, scan.Profile.Beam.Ssd);
                    if (frame is null)
                    {
                        rows.Add(new ProfilePairRow(scan, dose, algorithm, candidate.Plan, candidate.Notes, null, null, refusal));
                        continue;
                    }
                    try
                    {
                        var result = ProfileComparison.Compare(scan.Profile, grid, frame, options.Comparison, dose.Dose.SourcePath);
                        rows.Add(new ProfilePairRow(scan, dose, algorithm, candidate.Plan, candidate.Notes, frame, result, null));
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
                    {
                        rows.Add(new ProfilePairRow(scan, dose, algorithm, candidate.Plan, candidate.Notes, frame, null, ex.Message));
                    }
                }
                int n = Interlocked.Increment(ref done);
                if (n % 10 == 0 || n == byDose.Length) progress?.Invoke($"{n} of {byDose.Length} doses compared");
            });

            return new ProfileBatchResult
            {
                Rows = rows.OrderBy(r => r.Scan.File, StringComparer.Ordinal).ThenBy(r => r.Scan.ScanNumber)
                    .ThenBy(r => r.Dose.Dose.SourcePath, StringComparer.Ordinal)
                    .Select(r => r with
                    {
                        HeaderCheck = checks[r.Scan],
                        PairingNotes = r.Scan.HeaderFixNote is { } fixNote ? r.PairingNotes.Prepend(fixNote).ToArray() : r.PairingNotes,
                    }).ToArray(),
                Unmatched = unmatched,
                UnreadableScans = unreadableScans,
                UnreadableDoses = folder.Unreadable,
                ScanFiles = scanFiles,
                Scans = scans.Count,
                Doses = doses.Length,
                Threads = threads,
                Elapsed = clock.Elapsed,
                HeaderFixesPath = options.HeaderFixes is { Fixes.Count: > 0 } f ? f.SourcePath : null,
            };
        }

        /// <summary>
        /// Every scan of every PTW mcc file under a folder, mapped, with any header fix applied; files that can't be read
        /// are listed.
        /// </summary>
        public static (IReadOnlyList<ScanRef> Scans, int Files, IReadOnlyList<(string Path, string Reason)> Unreadable) ReadScans(
            string folder, string? excludeFolderPattern = null, Action<string>? progress = null, ScanHeaderFixes? fixes = null)
        {
            var exclude = excludeFolderPattern is { Length: > 0 } p ? new Regex(p, RegexOptions.IgnoreCase) : null;
            var mapper = new MCCMeasurementMapper();
            var scans = new List<ScanRef>();
            var unreadable = new List<(string, string)>();
            int files = 0;
            foreach (string file in Directory.EnumerateFiles(folder, "*.mcc", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(folder, Path.GetDirectoryName(file)!);
                if (exclude is not null && relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(exclude.IsMatch)) continue;
                MCCResult result;
                try { result = MCCParser.ParseFile(file); files++; }
                catch (Exception ex) when (ex is IOException or FormatException or InvalidOperationException or ArgumentException)
                { unreadable.Add((file, ex.Message)); continue; }
                foreach (var scan in result.Scans)
                {
                    try
                    {
                        var profile = mapper.MapScan(scan, result);
                        var fix = fixes?.For(ScanHeaderFixes.KeyOf(folder, file), scan.ScanNumber);
                        scans.Add(fix is null
                            ? new ScanRef(file, relative, scan.ScanNumber, profile)
                            : new ScanRef(file, relative, scan.ScanNumber, ScanHeaderFixes.Apply(profile, fix)) { HeaderFix = fix, OriginalBeam = profile.Beam });
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
                    { unreadable.Add(($"{file} scan {scan.ScanNumber}", ex.Message)); }
                }
            }
            return (scans, files, unreadable);
        }

        /// <summary>The physical cores, or the caller's count, capped so the largest grid times the count fits half the free memory.</summary>
        private static int Threads(int? requested, IEnumerable<RTDoseResult> doses)
        {
            int cores = requested is > 0 ? requested.Value : CpuTopology.PhysicalCores;
            long largest = doses.Select(d => (long)(d.DeclaredVoxelCount ?? 0) * sizeof(double)).DefaultIfEmpty(1).Max();   // headers only: the declared count
            long budget = FreeMemory.Bytes() / 2;
            int fits = (int)Math.Max(1, Math.Min(int.MaxValue, budget / Math.Max(1, largest)));
            return Math.Max(1, Math.Min(cores, fits));
        }
    }
}
