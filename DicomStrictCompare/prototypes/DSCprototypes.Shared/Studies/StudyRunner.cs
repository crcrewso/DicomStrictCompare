using System.Diagnostics;
using System.Globalization;
using DSClibrary.Comparing;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using DSClibrary.Studies;
using DSCprototypes.Shared.Graphs;

namespace DSCprototypes.Shared.Studies;

/// <summary>
/// The beam a result row is about, in the terms a person filters and pivots on (F5). Read from the
/// source dose's plan, or the target's when the source has none. Sort keys travel with the labels so
/// an index table can order field sizes by area and energies physically.
/// </summary>
public sealed record FieldFacts(
    string Patient,
    string SourcePlan,
    string TargetPlan,
    string Field,
    string Machine,
    string Energy,
    string FieldSize,
    string Collimation,
    string Ssd,
    string Accessories,
    bool IsElectron,
    double EnergyValue,
    bool IsFff,
    double FieldAreaMm2,
    double FieldCrossplaneMm)
{
    public const string NotStated = "not stated";

    public static FieldFacts Of(MatchedDosePair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        var beam = pair.Source.Beam ?? pair.Target.Beam;
        var (cross, inplane) = beam is null ? (null, null) : PairCatalog.EffectiveFieldSizeMm(beam);
        bool electron = string.Equals(beam?.RadiationType, "ELECTRON", StringComparison.OrdinalIgnoreCase);

        string collimation = beam is null ? NotStated
            : beam.ApplicatorID is { Length: > 0 } ? "applicator"
            : beam.LeafJawPositions.Keys.Any(k => k.StartsWith("MLC", StringComparison.OrdinalIgnoreCase)) ? "MLC"
            : "jaws";

        var accessories = new List<string>();
        if (beam is not null)
        {
            if (beam.ApplicatorID is { Length: > 0 } a) accessories.Add(a);
            accessories.AddRange(beam.WedgeIDs);
            if (beam.NumberOfBlocks is > 0) accessories.Add("cutout");
        }

        return new FieldFacts(
            pair.Source.Dose.PatientID ?? pair.Target.Dose.PatientID ?? NotStated,
            pair.Source.Plan?.RTPlanLabel ?? NotStated,
            pair.Target.Plan?.RTPlanLabel ?? NotStated,
            pair.Source.Beam?.BeamName ?? pair.Target.Beam?.BeamName ?? NotStated,
            beam?.TreatmentMachineName ?? NotStated,
            PairCatalog.EnergyLabel(beam),
            PairCatalog.FieldSizeLabel(beam),
            collimation,
            beam?.SourceToSurfaceDistance is { } ssd ? (ssd / 10).ToString("0.#", CultureInfo.InvariantCulture) + " cm" : NotStated,
            accessories.Count == 0 ? "none" : string.Join(" ", accessories),
            electron,
            beam?.NominalBeamEnergy ?? double.NaN,
            beam?.IsFluenceModeFff == true,
            cross is { } c && inplane is { } i ? c * i : double.NaN,
            cross ?? double.NaN);
    }
}

/// <summary>A matched pair of one comparison, with what was checked about it before any voxel (F3, F4).</summary>
public sealed record StudyPair(SetComparison Comparison, MatchedDosePair Pair, FieldFacts Facts, PlanCheckResult PlanCheck, IReadOnlyList<string> Warnings)
{
    public string WarningText => Warnings.Count == 0 ? "" : string.Join("; ", Warnings);
}

/// <summary>One result row: one matched field of one comparison under one criterion (F5).</summary>
public sealed record StudyRow(
    StudyPair Pair,
    DtaSpec Criterion,
    long Compared,
    long Failed,
    double? PercentFailed,
    double? MaxGamma,
    string SourceFile,
    string TargetFile,
    string? Failure,
    ReferenceMaximum? Maximum = null,
    string EdgeCheck = "")
{
    public string Comparison => Pair.Comparison.Name;
    public string MaximumUsed => Maximum?.Note ?? "";
    public FieldFacts Facts => Pair.Facts;

    /// <summary>100 x passed / compared; null when nothing was compared or the comparison failed.</summary>
    public double? PassRate => Failure is null && Compared > 0 ? 100.0 * (Compared - Failed) / Compared : null;

    public string PlanCheck => Pair.PlanCheck.Summary;
    public string Warnings => Pair.WarningText;
}

/// <summary>What one comparison's pairing found before comparing (the F2 preview).</summary>
public sealed record ComparisonPreview(
    SetComparison Comparison,
    DoseMatchResult Match,
    IReadOnlyList<NearMiss> NearMisses,
    IReadOnlyList<StudyPair> Pairs,
    IReadOnlyList<string> SetWarnings)
{
    public string Summary =>
        $"{Comparison.Name}: {Match.Pairs.Count} pair(s), {Match.UnmatchedSource.Count} source and "
        + $"{Match.UnmatchedTarget.Count} target unmatched, {Match.AmbiguousKeys.Count} ambiguous"
        + (NearMisses.Count > 0 ? $", {NearMisses.Count} near-miss label pair(s)" : "")
        + (Pairs.Count(p => p.PlanCheck.Checked && !p.PlanCheck.Agrees) is > 0 and var n ? $", {n} with plan differences" : "");
}

/// <summary>Everything a study run produced. A preview is a session with no rows.</summary>
public sealed class StudySession
{
    public required Study Study { get; init; }
    public required IReadOnlyList<DtaSpec> Criteria { get; init; }
    public required IReadOnlyDictionary<string, RTFolderContents> Contents { get; init; }
    public required IReadOnlyList<ComparisonPreview> Previews { get; init; }
    public required IReadOnlyList<StudyRow> Rows { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required DateTimeOffset StartedAt { get; init; }

    public bool IsPreview => Rows.Count == 0;

    public string Summary =>
        $"{Study.Sets.Count} set(s), {Previews.Count} comparison(s), {Previews.Sum(p => p.Match.Pairs.Count)} pair(s)"
        + (IsPreview ? " previewed" : $", {Rows.Count(r => r.Failure is null)} of {Rows.Count} result(s) succeeded")
        + $" in {Elapsed.TotalSeconds:0.0} s";
}

/// <summary>
/// Runs a study (F1): reads each set once, pairs every comparison under its rules (F2), checks every
/// pair's plans (F3) and doses (F4), then compares each comparison under each criterion with the
/// same engine the two-folder run uses (F5).
/// </summary>
public static class StudyRunner
{
    /// <summary>Reads and pairs only: the preview a person checks before committing to a long run.</summary>
    public static StudySession Preview(Study study, IProgress<string>? progress = null, CancellationToken cancellation = default) =>
        Execute(study, Array.Empty<DtaSpec>(), null, progress, cancellation, compare: false, StudyRunSettings.Default);

    public static StudySession Run(Study study, IReadOnlyList<DtaSpec> criteria, int? degreeOfParallelism = null,
        IProgress<string>? progress = null, CancellationToken cancellation = default, StudyRunSettings? run = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0) throw new ArgumentException("At least one criterion is required.", nameof(criteria));
        run ??= study.Run;
        return Execute(study, criteria, degreeOfParallelism ?? run.PairsAtOnce, progress, cancellation, compare: true, run);
    }

    public static Task<StudySession> RunAsync(Study study, IReadOnlyList<DtaSpec> criteria, IProgress<string>? progress = null,
        CancellationToken cancellation = default, StudyRunSettings? run = null) =>
        Task.Run(() => Run(study, criteria, null, progress, cancellation, run), cancellation);

    public static Task<StudySession> PreviewAsync(Study study, IProgress<string>? progress = null, CancellationToken cancellation = default) =>
        Task.Run(() => Preview(study, progress, cancellation), cancellation);

    private static StudySession Execute(Study study, IReadOnlyList<DtaSpec> criteria, int? parallelism,
        IProgress<string>? progress, CancellationToken cancellation, bool compare, StudyRunSettings run)
    {
        ArgumentNullException.ThrowIfNull(study);
        var problems = study.Problems();
        if (problems.Count > 0) throw new InvalidOperationException("The study cannot run: " + string.Join(" ", problems));

        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();

        // Each folder once, however many sets and comparisons use it; each set then keeps the doses its plan filter admits.
        var folders = new Dictionary<string, RTFolderContents>(StringComparer.Ordinal);
        var contents = new Dictionary<string, RTFolderContents>(StringComparer.Ordinal);
        var doses = new Dictionary<string, IReadOnlyList<RTDoseWithPlan>>(StringComparer.Ordinal);
        foreach (var set in study.Sets.Where(s => study.Comparisons.Any(c => c.SourceSet == s.Name || c.TargetSet == s.Name)))
        {
            cancellation.ThrowIfCancellationRequested();
            string folder = Path.GetFullPath(set.Folder);
            if (!folders.TryGetValue(folder, out var read))
            {
                progress?.Report($"Reading {set.Folder} and its subfolders");
                // Headers only: the comparison loads each pair's grids itself, so the voxels need not stay in memory.
                read = folders[folder] = RTFolderReader.Read(set.Folder, recursive: true, headersOnly: true);
                progress?.Report(read.ToString());
            }
            contents[set.Name] = read;
            doses[set.Name] = read.Doses.Where(set.Includes).ToArray();
            if (!string.IsNullOrEmpty(set.PlanFilter))
                progress?.Report($"{set.Name}: {doses[set.Name].Count} of {read.Doses.Count} dose(s) pass the plan filter '{set.PlanFilter}'");
        }

        var previews = new List<ComparisonPreview>();
        foreach (var c in study.Comparisons)
        {
            cancellation.ThrowIfCancellationRequested();
            var sourceSet = study.Set(c.SourceSet);
            var targetSet = study.Set(c.TargetSet);
            var match = DoseMatcher.Match(doses[c.SourceSet], doses[c.TargetSet], c.Rules);
            var pairs = match.Pairs.Select(p => new StudyPair(
                c, p, FieldFacts.Of(p), PlanCheck.Compare(p), DoseWarnings.For(sourceSet, targetSet, p.Source, p.Target))).ToArray();
            var preview = new ComparisonPreview(c, match, PairingPreview.NearMisses(match, c.Rules), pairs, DoseWarnings.ForSets(sourceSet, targetSet));
            previews.Add(preview);
            progress?.Report(preview.Summary);
        }

        var rows = new List<StudyRow>();
        if (compare)
        {
            var maximum = run.Maximum;
            var engine = DoseComparers.Create(run.ComparerOrDefault);
            using var release = engine as IDisposable;
            progress?.Report($"Engine: {engine.Description}");
            var comparer = new BatchComparer(ComparisonRunner.LoadGrid, engine);
            foreach (var preview in previews)
            {
                var byFiles = preview.Pairs.ToDictionary(p => (p.Pair.Source.Dose.SourcePath, p.Pair.Target.Dose.SourcePath));
                // Each pair is loaded once for every criterion (and uploaded once, on a GPU).
                cancellation.ThrowIfCancellationRequested();
                progress?.Report($"{preview.Comparison.Name}: comparing {preview.Match.Pairs.Count} pair(s) at {criteria.Count} criteria");
                var batch = comparer.CompareMany(preview.Match.Pairs, criteria.Select(c => c.ToCriterion()).ToArray(), parallelism, cancellation, maximum);
                // Rows criterion by criterion, as before, each in key order.
                foreach (var o in batch.Outcomes.OrderBy(o => o.CriterionIndex))
                {
                    var pair = byFiles[(o.SourcePath, o.TargetPath)];
                    rows.Add(new StudyRow(pair, criteria[o.CriterionIndex],
                        o.Comparison?.TotalCompared ?? 0,
                        o.Comparison?.TotalFailed ?? 0,
                        o.PercentFailed,
                        o.Gamma is { Analysed: > 0 } g ? g.MaxGammaFound : null,
                        Path.GetFileName(o.SourcePath),
                        Path.GetFileName(o.TargetPath),
                        o.Failure,
                        o.Maximum,
                        o.EdgeCheck));
                }
                progress?.Report(batch.ToString());
            }
        }

        clock.Stop();
        return new StudySession
        {
            Study = study,
            Criteria = criteria,
            Contents = contents,
            Previews = previews,
            Rows = rows,
            Elapsed = clock.Elapsed,
            StartedAt = started,
        };
    }
}
