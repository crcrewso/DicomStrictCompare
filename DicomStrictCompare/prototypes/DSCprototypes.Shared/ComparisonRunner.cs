using System.Diagnostics;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Gamma;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;

namespace DSCprototypes.Shared;

/// <summary>
/// One comparison criterion as a user states it: percentages and millimetres, not fractions.
/// <see cref="ToDta"/> converts to the library's <see cref="Dta"/>, whose constructor takes fractions.
/// <para>
/// <see cref="Gamma"/> selects the gamma index (<see cref="DSClibrary.Gamma.GammaShell"/>, the pymedphys
/// port) in place of the strict engine: the same four numbers read as dose criterion, distance
/// criterion, lower dose cutoff, and global or local normalisation (Cody, 2026-11-09: "wire it in").
/// </para>
/// </summary>
public sealed record DtaSpec(double TolerancePercent, double DistanceMm, double ThresholdPercent, bool Global, bool Gamma = false)
{
    public Dta ToDta() => new(useMM: true,
        threshhold: ThresholdPercent / 100.0,
        tolerance: TolerancePercent / 100.0,
        distance: DistanceMm,
        relative: Global,
        gamma: Gamma);

    /// <summary>
    /// The gamma parameters for this criterion. Max gamma 1.1 and stop-once-passed, because the row
    /// reports pass or fail: both leave every pass/fail decision as it is and spare the long search
    /// for points that fail badly. The full gamma, uncapped, is a call to <c>GammaShell</c> directly.
    /// </summary>
    public GammaOptions ToGammaOptions() => new(
        DosePercentThreshold: TolerancePercent,
        DistanceThreshold: DistanceMm,
        LowerPercentDoseCutoff: ThresholdPercent,
        LocalGamma: !Global,
        MaxGamma: 1.1,
        SkipOncePassed: true);

    public string Label =>
        $"{TolerancePercent:0.#}% / {DistanceMm:0.#} mm / {ThresholdPercent:0.#}% {(Global ? "global" : "local")}{(Gamma ? " gamma" : "")}";

    public string Algorithm => Gamma ? "gamma" : "dta";

    /// <summary>The set DSCcore ships with by default, so the prototypes start where the GUI does.</summary>
    public static IReadOnlyList<DtaSpec> Defaults { get; } = new[]
    {
        new DtaSpec(1.0, 1.0, 10.0, Global: true),
        new DtaSpec(2.0, 2.0, 10.0, Global: true),
        new DtaSpec(3.0, 3.0, 10.0, Global: true),
    };

    /// <summary>The gamma criterion a window adds on <c>--gamma</c>: 3 %/3 mm, 10 % cutoff, global.</summary>
    public static DtaSpec DefaultGamma { get; } = new(3.0, 3.0, 10.0, Global: true, Gamma: true);

    public override string ToString() => Label;
}

/// <summary>What a run needs. Immutable once built so a result can say exactly what it answered.</summary>
public sealed record ComparisonRequest(
    string SourceFolder,
    string TargetFolder,
    string SourceAlias,
    string TargetAlias,
    IReadOnlyList<DtaSpec> Dtas,
    DoseMatchStrategy Strategy = DoseMatchStrategy.PlanAndBeamUid,
    int? DegreeOfParallelism = null);

/// <summary>A result as a grid row: one matched pair under one criterion, flattened for display and CSV.</summary>
public sealed record ResultRow(
    string Key,
    string SourceFile,
    string TargetFile,
    DtaSpec Criterion,
    long TotalVoxels,
    long Compared,
    long Failed,
    double? PercentFailed,
    double? SourceMonitorUnits,
    double? TargetMonitorUnits,
    double ElapsedSeconds,
    string? Failure,
    double? MaxGamma = null)
{
    public bool Succeeded => Failure is null;

    /// <summary>For a gamma row, the pass rate the way gamma users quote it; null for a DTA row.</summary>
    public double? GammaPassRate => Criterion.Gamma && Compared > 0 ? 100.0 * (Compared - Failed) / Compared : null;

    internal static ResultRow From(ComparisonOutcome o, DtaSpec spec) => new(
        o.Key.ToString(),
        Path.GetFileName(o.SourcePath),
        Path.GetFileName(o.TargetPath),
        spec,
        o.Comparison?.TotalCount ?? 0,
        o.Comparison?.TotalCompared ?? 0,
        o.Comparison?.TotalFailed ?? 0,
        o.PercentFailed,
        o.SourceMonitorUnits,
        o.TargetMonitorUnits,
        o.Elapsed.TotalSeconds,
        o.Failure,
        o.Gamma is { Analysed: > 0 } g ? g.MaxGammaFound : null);
}

/// <summary>Everything a run produced, retained (backlog D15) rather than rendered and thrown away.</summary>
public sealed class ComparisonSession
{
    public required ComparisonRequest Request { get; init; }
    public required RTFolderContents Source { get; init; }
    public required RTFolderContents Target { get; init; }
    public required DoseMatchResult Match { get; init; }
    public required IReadOnlyList<ComparisonOutcome> Outcomes { get; init; }
    public required IReadOnlyList<ResultRow> Rows { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required DateTimeOffset StartedAt { get; init; }

    public IEnumerable<UnmatchedDose> Unmatched => Match.UnmatchedSource.Concat(Match.UnmatchedTarget);

    public string Summary =>
        $"{Match.Pairs.Count} pair(s) by {Match.Strategy}, {Request.Dtas.Count} criteria, "
        + $"{Rows.Count(r => r.Succeeded)} of {Rows.Count} comparisons succeeded in {Elapsed.TotalSeconds:0.0} s; "
        + $"{Match.UnmatchedSource.Count} source and {Match.UnmatchedTarget.Count} target doses unmatched";
}

/// <summary>
/// The whole workflow DSCcore's <c>DscDataHandler.Run</c> performs, on DSClibrary types only:
/// read plans-first, match, compare each criterion over the matched pairs in parallel, keep the results.
/// </summary>
public static class ComparisonRunner
{
    /// <summary>
    /// Grid loader for <see cref="BatchComparer"/>. EvilDICOM is what the library's own tests use; the
    /// in-house reader (<c>RTDoseResult</c>) does not yet produce a <see cref="DoseMatrixOptimal"/>.
    /// </summary>
    public static DoseMatrixOptimal LoadWithEvilDicom(string path) =>
        new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path)));

    public static ComparisonSession Run(
        ComparisonRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Dtas.Count == 0) throw new ArgumentException("At least one criterion is required.", nameof(request));

        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();

        // Subfolders included (Cody, 2026-11-08: "all guis should scan subfolders"). A TPS export is
        // usually one folder per plan or per patient, and the matching is by UID, so where a file
        // sits does not matter to it.
        progress?.Report($"Reading source folder {request.SourceFolder} and its subfolders");
        var source = RTFolderReader.Read(request.SourceFolder, recursive: true);
        progress?.Report(source.ToString());
        cancellation.ThrowIfCancellationRequested();

        progress?.Report($"Reading target folder {request.TargetFolder} and its subfolders");
        var target = RTFolderReader.Read(request.TargetFolder, recursive: true);
        progress?.Report(target.ToString());
        cancellation.ThrowIfCancellationRequested();

        var match = DoseMatcher.Match(source.Doses, target.Doses, request.Strategy);
        progress?.Report(match.ToString());

        var comparer = new BatchComparer(LoadWithEvilDicom);
        var outcomes = new List<ComparisonOutcome>();
        var rows = new List<ResultRow>();

        foreach (var spec in request.Dtas)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report($"Comparing {match.Pairs.Count} pair(s) at {spec.Label}");
            var batch = spec.Gamma
                ? comparer.CompareGamma(match.Pairs, spec.ToGammaOptions(), request.DegreeOfParallelism, cancellation)
                : comparer.Compare(match.Pairs, spec.ToDta(), spec.Global, request.DegreeOfParallelism, cancellation);
            outcomes.AddRange(batch.Outcomes);
            rows.AddRange(batch.Outcomes.Select(o => ResultRow.From(o, spec)));
            progress?.Report(batch.ToString());
        }

        clock.Stop();
        return new ComparisonSession
        {
            Request = request,
            Source = source,
            Target = target,
            Match = match,
            Outcomes = outcomes,
            Rows = rows,
            Elapsed = clock.Elapsed,
            StartedAt = started,
        };
    }

    /// <summary>The same run, off the calling thread, for a UI that must stay responsive.</summary>
    public static Task<ComparisonSession> RunAsync(
        ComparisonRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default) =>
        Task.Run(() => Run(request, progress, cancellation), cancellation);
}
