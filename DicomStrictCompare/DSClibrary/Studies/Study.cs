using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSClibrary.Matching;

namespace DSClibrary.Studies
{
    /// <summary>
    /// What a person declares about a set's doses that the RT Dose file has no standard place for. An
    /// AXB calculation can report dose to water or dose to medium; comparing one against the other
    /// without knowing is a silent error (design/comparison-studies.md, F4).
    /// </summary>
    public enum DoseReporting
    {
        NotStated = 0,
        DoseToWater = 1,
        DoseToMedium = 2
    }

    /// <summary>
    /// A folder of RT Dose and RT Plan files under a name a person chose, with what they know about it
    /// (F1). Name, folder and the rest are as typed: nothing is inferred from the folder name.
    /// </summary>
    public sealed record DoseSet
    {
        public string Name { get; init; }
        public string Folder { get; init; }

        /// <summary>For example "AAA 15.6.06" or "AXB 18.0". Free text.</summary>
        public string? Algorithm { get; init; }

        /// <summary>For example "Eclipse 15.6". Free text.</summary>
        public string? TpsVersion { get; init; }

        public DoseReporting Reporting { get; init; } = DoseReporting.NotStated;
        public string? Notes { get; init; }

        /// <summary>
        /// A regular expression a dose's plan label must match to belong to the set, or null for every
        /// dose. For an export that holds both algorithms in one folder, as QAtpsPTW does: "AXB$" for
        /// the Acuros plans, "^(?!.*AXB)" for the rest. A dose whose plan was not found never matches
        /// a filter. Measured 2026-11-14: without one, a folder compared with itself paired every AAA
        /// plan with itself.
        /// </summary>
        public string? PlanFilter { get; init; }

        /// <summary>True when the dose belongs to this set under <see cref="PlanFilter"/>.</summary>
        public bool Includes(DSClibrary.Results.DICOM.RTDoseWithPlan dose)
        {
            if (string.IsNullOrEmpty(PlanFilter)) return true;
            return dose.Plan?.RTPlanLabel is { } label
                   && System.Text.RegularExpressions.Regex.IsMatch(label, PlanFilter,
                       System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }

        public DoseSet(string name, string folder)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A dose set needs a name.", nameof(name));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("A dose set needs a folder.", nameof(folder));
            Name = name.Trim();
            Folder = folder.Trim();
        }

        /// <summary>
        /// True when the declared algorithm, or failing that the set's name, names Acuros (AXB). Used
        /// only to decide whether a missing dose-reporting declaration deserves a warning.
        /// </summary>
        [JsonIgnore]
        public bool LooksLikeAcuros
        {
            get
            {
                string text = (Algorithm ?? "") + " " + Name;
                return text.Contains("AXB", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("Acuros", StringComparison.OrdinalIgnoreCase);
            }
        }

        public override string ToString() => $"{Name} ({Folder})";
    }

    /// <summary>One comparison of a study: a source set, a target set, and how their doses pair (F2).</summary>
    public sealed record SetComparison
    {
        public string SourceSet { get; init; }
        public string TargetSet { get; init; }

        [JsonIgnore]
        public PairingRules Rules { get; init; }

        public SetComparison(string sourceSet, string targetSet, PairingRules? rules = null)
        {
            if (string.IsNullOrWhiteSpace(sourceSet)) throw new ArgumentException("A comparison needs a source set.", nameof(sourceSet));
            if (string.IsNullOrWhiteSpace(targetSet)) throw new ArgumentException("A comparison needs a target set.", nameof(targetSet));
            SourceSet = sourceSet.Trim();
            TargetSet = targetSet.Trim();
            Rules = rules ?? PairingRules.Exact(DoseMatchStrategy.Identifier);
        }

        /// <summary>"AAA 15.6 vs AXB 15.6": the name every result row of this comparison carries.</summary>
        [JsonIgnore]
        public string Name => $"{SourceSet} vs {TargetSet}";

        public override string ToString() => $"{Name} ({Rules})";
    }

    /// <summary>
    /// The depths profiles are drawn at (F7), in millimetres below the surface, plus whether dmax is
    /// one of them. The default is the list the Graphs tab has used since 2026-11-09: dmax, 50, 100 and
    /// 200 mm.
    /// </summary>
    public sealed record DepthSettings
    {
        public bool IncludeDmax { get; init; } = true;
        public IReadOnlyList<double> DepthsMm { get; init; }

        public DepthSettings(IEnumerable<double>? depthsMm = null, bool includeDmax = true)
        {
            var depths = (depthsMm ?? new double[] { 50, 100, 200 }).ToArray();
            if (depths.Any(d => double.IsNaN(d) || double.IsInfinity(d) || d < 0))
                throw new ArgumentException("Depths must be finite and not negative (millimetres below the surface).", nameof(depthsMm));
            DepthsMm = depths.Distinct().OrderBy(d => d).ToArray();
            IncludeDmax = includeDmax;
        }

        public static DepthSettings Default { get; } = new();

        /// <summary>
        /// Reads "dmax, 15, 50, 100 mm" or "50;100": numbers in millimetres, the word dmax anywhere,
        /// separated by commas, semicolons or spaces. A unit of "mm" is allowed; "cm" multiplies by ten.
        /// </summary>
        public static DepthSettings Parse(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            bool dmax = false;
            var depths = new List<double>();
            foreach (string raw in text.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = raw.Trim().ToLowerInvariant();
                if (token == "mm") continue;
                // A unit written as its own word ("5 cm") applies to the number before it.
                if (token == "cm" && depths.Count > 0) { depths[^1] *= 10; continue; }
                if (token == "dmax") { dmax = true; continue; }
                double scale = 1;
                if (token.EndsWith("cm", StringComparison.Ordinal)) { scale = 10; token = token[..^2]; }
                else if (token.EndsWith("mm", StringComparison.Ordinal)) token = token[..^2];
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw new FormatException($"'{raw}' is not a depth: give millimetres, or 'dmax'.");
                depths.Add(value * scale);
            }
            if (!dmax && depths.Count == 0) throw new FormatException("Give at least one depth, or 'dmax'.");
            return new DepthSettings(depths, dmax);
        }

        public override string ToString() =>
            string.Join(", ", (IncludeDmax ? new[] { "dmax" } : Array.Empty<string>())
                .Concat(DepthsMm.Select(d => d.ToString("0.#", CultureInfo.InvariantCulture)))) + " mm";
    }

    /// <summary>
    /// Dose sets and the comparisons between them (F1). One run reads each set once, however many
    /// comparisons use it.
    /// </summary>
    public sealed class Study
    {
        public IReadOnlyList<DoseSet> Sets { get; }
        public IReadOnlyList<SetComparison> Comparisons { get; }
        public DepthSettings Depths { get; }

        public Study(IEnumerable<DoseSet> sets, IEnumerable<SetComparison> comparisons, DepthSettings? depths = null)
        {
            Sets = sets?.ToArray() ?? throw new ArgumentNullException(nameof(sets));
            Comparisons = comparisons?.ToArray() ?? throw new ArgumentNullException(nameof(comparisons));
            Depths = depths ?? DepthSettings.Default;
        }

        public DoseSet Set(string name) =>
            Sets.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"No dose set is named '{name}'.");

        /// <summary>Everything wrong with the study as written, for a message a user can act on. Empty when it can run.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            if (Sets.Count == 0) problems.Add("The study has no dose sets.");
            if (Comparisons.Count == 0) problems.Add("The study has no comparisons.");
            foreach (var dup in Sets.GroupBy(s => s.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
                problems.Add($"More than one dose set is named '{dup.Key}'.");
            foreach (var s in Sets.Where(s => !string.IsNullOrEmpty(s.PlanFilter)))
            {
                try { _ = new System.Text.RegularExpressions.Regex(s.PlanFilter!); }
                catch (ArgumentException ex) { problems.Add($"{s.Name}: the plan filter '{s.PlanFilter}' is not a valid regular expression: {ex.Message}"); }
            }
            var names = new HashSet<string>(Sets.Select(s => s.Name), StringComparer.Ordinal);
            foreach (var c in Comparisons)
            {
                if (!names.Contains(c.SourceSet)) problems.Add($"{c.Name}: no dose set is named '{c.SourceSet}'.");
                if (!names.Contains(c.TargetSet)) problems.Add($"{c.Name}: no dose set is named '{c.TargetSet}'.");
                if (string.Equals(c.SourceSet, c.TargetSet, StringComparison.Ordinal))
                    problems.Add($"{c.Name}: a set compared with itself.");
            }
            foreach (var dup in Comparisons.GroupBy(c => c.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
                problems.Add($"The comparison '{dup.Key}' is listed more than once.");
            return problems;
        }

        // ---- saving and reopening (design note CS-Q1: assumed wanted, format open) ----

        internal sealed record RuleFile(string Pattern, string Replacement);
        internal sealed record ComparisonFile(string SourceSet, string TargetSet, DoseMatchStrategy Strategy,
            RuleFile[]? LabelRules, Dictionary<string, string>? LabelOverrides, RuleFile[]? FieldRules = null);
        internal sealed record DepthFile(bool IncludeDmax, double[] DepthsMm);
        internal sealed record StudyFile(int Version, DoseSet[] Sets, ComparisonFile[] Comparisons, DepthFile Depths);

        // Source-generated, not reflection: a trimmed, AOT or file-based (`dotnet run x.cs`) program has
        // reflection-based JSON switched off, and Load failed there (measured 2026-11-14).
        private static readonly System.Text.Json.Serialization.Metadata.JsonTypeInfo<StudyFile> JsonInfo =
            StudyJsonContext.Default.StudyFile;

        public string ToJson() => JsonSerializer.Serialize(new StudyFile(
            1,
            Sets.ToArray(),
            Comparisons.Select(c => new ComparisonFile(
                c.SourceSet, c.TargetSet, c.Rules.Strategy,
                c.Rules.LabelRules.Select(r => new RuleFile(r.Pattern, r.Replacement)).ToArray(),
                new Dictionary<string, string>(c.Rules.LabelOverrides),
                c.Rules.FieldRules.Select(r => new RuleFile(r.Pattern, r.Replacement)).ToArray())).ToArray(),
            new DepthFile(Depths.IncludeDmax, Depths.DepthsMm.ToArray())), JsonInfo);

        public static Study FromJson(string json)
        {
            var file = JsonSerializer.Deserialize(json, JsonInfo)
                       ?? throw new FormatException("The study file is empty.");
            if (file.Version != 1) throw new FormatException($"Study file version {file.Version} is not one this program reads (it reads 1).");
            return new Study(
                file.Sets ?? Array.Empty<DoseSet>(),
                (file.Comparisons ?? Array.Empty<ComparisonFile>()).Select(c => new SetComparison(
                    c.SourceSet, c.TargetSet,
                    new PairingRules(c.Strategy,
                        (c.LabelRules ?? Array.Empty<RuleFile>()).Select(r => new LabelRule(r.Pattern, r.Replacement)),
                        c.LabelOverrides,
                        (c.FieldRules ?? Array.Empty<RuleFile>()).Select(r => new LabelRule(r.Pattern, r.Replacement))))),
                file.Depths is null ? null : new DepthSettings(file.Depths.DepthsMm, file.Depths.IncludeDmax));
        }

        public void Save(string path) => File.WriteAllText(path, ToJson());
        public static Study Load(string path) => FromJson(File.ReadAllText(path));
    }

    /// <summary>The study file's serializer, generated at compile time (see <see cref="Study.ToJson"/>).</summary>
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(Study.StudyFile))]
    internal sealed partial class StudyJsonContext : JsonSerializerContext
    {
    }
}
