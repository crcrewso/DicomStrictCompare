using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Matching
{
    /// <summary>
    /// A rewrite of a plan label before it is used as a key: a regular expression and its replacement,
    /// applied with <see cref="Regex.Replace(string, string)"/>.
    /// <para>
    /// The case it exists for (design/comparison-studies.md, F2): AAA and AXB copies of one plan differ
    /// only in their label, <c>STBjawAAA</c> and <c>STBjawAXB</c>, so <c>(AAA|AXB)</c> to
    /// <c>{algorithm}</c> gives both the key <c>STBjaw{algorithm}</c>.
    /// </para>
    /// </summary>
    public sealed record LabelRule
    {
        public string Pattern { get; }
        public string Replacement { get; }
        private readonly Regex _regex;

        public LabelRule(string pattern, string replacement)
        {
            if (string.IsNullOrEmpty(pattern)) throw new ArgumentException("A label rule needs a pattern.", nameof(pattern));
            Pattern = pattern;
            Replacement = replacement ?? string.Empty;
            try
            {
                _regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"The label rule pattern '{pattern}' is not a valid regular expression: {ex.Message}", nameof(pattern), ex);
            }
        }

        public string Apply(string label) => _regex.Replace(label, Replacement);

        public override string ToString() => $"{Pattern} -> {Replacement}";
    }

    /// <summary>
    /// How one comparison pairs its doses: a strategy, plus label rules, field rules and exact label
    /// overrides for <see cref="DoseMatchStrategy.Identifier"/> matching.
    /// <para>
    /// Field rules rewrite beam names the same way label rules rewrite plan labels. They exist because
    /// some sites put the algorithm in the field name too: in <c>resources/</c> the AAA field is
    /// <c>X-100A-10.0-1</c> and its Acuros copy <c>X-100X-10.0-1</c>.
    /// </para>
    /// <para>
    /// Rules apply to both sides, so they cannot make a source label match a target label that a
    /// rule does not also reach. An override maps one exact source label to one exact target label
    /// and wins over the rules on both sides. Rules need Identifier matching: a UID key has no label,
    /// and across algorithms the beam numbers differ (the AXB plans are reordered), so a UID pairing
    /// would compare the wrong fields.
    /// </para>
    /// </summary>
    public sealed record PairingRules
    {
        public DoseMatchStrategy Strategy { get; }
        public IReadOnlyList<LabelRule> LabelRules { get; }

        /// <summary>Rewrites applied to beam names on both sides.</summary>
        public IReadOnlyList<LabelRule> FieldRules { get; }

        /// <summary>Exact source plan label to exact target plan label.</summary>
        public IReadOnlyDictionary<string, string> LabelOverrides { get; }

        public PairingRules(
            DoseMatchStrategy strategy = DoseMatchStrategy.Identifier,
            IEnumerable<LabelRule>? labelRules = null,
            IReadOnlyDictionary<string, string>? labelOverrides = null,
            IEnumerable<LabelRule>? fieldRules = null)
        {
            Strategy = strategy;
            LabelRules = labelRules?.ToArray() ?? Array.Empty<LabelRule>();
            FieldRules = fieldRules?.ToArray() ?? Array.Empty<LabelRule>();
            LabelOverrides = labelOverrides is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(labelOverrides, StringComparer.Ordinal);

            if ((LabelRules.Count > 0 || LabelOverrides.Count > 0)
                && strategy is not (DoseMatchStrategy.Identifier or DoseMatchStrategy.PlanLabelAndBeamName))
                throw new ArgumentException(
                    "Label rules and overrides need a key with the plan label: patient, plan label and beam name, or plan label and beam name.");
            if (FieldRules.Count > 0 && strategy == DoseMatchStrategy.PlanAndBeamUid)
                throw new ArgumentException("Field rules need a key with the beam name; plan UID and beam number has none.");
        }

        /// <summary>Exact keys, no rewriting: what <see cref="DoseMatcher.Match(IEnumerable{RTDoseWithPlan}, IEnumerable{RTDoseWithPlan}, DoseMatchStrategy)"/> does.</summary>
        public static PairingRules Exact(DoseMatchStrategy strategy) => new(strategy);

        /// <summary>The rule that pairs an AAA plan with its AXB copy: either token becomes <c>{algorithm}</c>.</summary>
        public static PairingRules AcrossAlgorithms(params string[] algorithmTokens)
        {
            var tokens = algorithmTokens is { Length: > 0 } ? algorithmTokens : new[] { "AAA", "AXB" };
            string pattern = "(" + string.Join("|", tokens.Select(Regex.Escape)) + ")";
            return new PairingRules(DoseMatchStrategy.Identifier, new[] { new LabelRule(pattern, "{algorithm}") });
        }

        public bool RewritesLabels => LabelRules.Count > 0 || LabelOverrides.Count > 0 || FieldRules.Count > 0;

        /// <summary>The field name a dose is keyed by, on either side.</summary>
        public string KeyFieldName(string beamName)
        {
            foreach (var rule in FieldRules) beamName = rule.Apply(beamName);
            return beamName;
        }

        /// <summary>The label a source dose is keyed by.</summary>
        public string SourceKeyLabel(string label) =>
            LabelOverrides.TryGetValue(label, out var target) ? target : ApplyRules(label);

        /// <summary>The label a target dose is keyed by: verbatim when an override names it, else rewritten.</summary>
        public string TargetKeyLabel(string label) =>
            LabelOverrides.Values.Contains(label, StringComparer.Ordinal) ? label : ApplyRules(label);

        private string ApplyRules(string label)
        {
            foreach (var rule in LabelRules) label = rule.Apply(label);
            return label;
        }

        public override string ToString()
        {
            if (!RewritesLabels) return Strategy.ToString();
            var parts = LabelRules.Select(r => "label " + r)
                .Concat(FieldRules.Select(r => "field " + r))
                .Concat(LabelOverrides.Select(o => $"{o.Key} = {o.Value}"));
            return $"{Strategy} with {string.Join("; ", parts)}";
        }
    }

    /// <summary>
    /// A source dose and target dose with the same patient and field name but different plan labels:
    /// the pairs a label rule usually exists to make. Grouped by label pair, so one line says
    /// "SquareFieldsS against SquFldsSAXB, 40 fields".
    /// </summary>
    public sealed record NearMiss(string PatientId, string SourcePlanLabel, string TargetPlanLabel, int FieldCount)
    {
        public override string ToString() =>
            $"{PatientId}: {SourcePlanLabel} and {TargetPlanLabel} share {FieldCount} field name(s)";
    }

    public static class PairingPreview
    {
        /// <summary>
        /// Unmatched source and target doses that share a patient and a field name (after any field
        /// rules). Only meaningful for Identifier matching; for UID matching the result is empty.
        /// </summary>
        public static IReadOnlyList<NearMiss> NearMisses(DoseMatchResult result, PairingRules? rules = null)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));
            if (result.Strategy != DoseMatchStrategy.Identifier) return Array.Empty<NearMiss>();

            (string Patient, string Field)? Id(RTDoseWithPlan d) =>
                d.Dose.PatientID is { Length: > 0 } p && d.Beam?.BeamName is { Length: > 0 } f
                    ? (p, rules is null ? f : rules.KeyFieldName(f))
                    : null;

            var targets = result.UnmatchedTarget
                .Where(u => Id(u.Dose) is not null && u.Dose.Plan?.RTPlanLabel is { Length: > 0 })
                .ToLookup(u => Id(u.Dose)!.Value);

            var counts = new Dictionary<(string, string, string), int>();
            foreach (var s in result.UnmatchedSource)
            {
                if (Id(s.Dose) is not { } id || s.Dose.Plan?.RTPlanLabel is not { Length: > 0 } sourceLabel) continue;
                foreach (var t in targets[id])
                {
                    string targetLabel = t.Dose.Plan!.RTPlanLabel!;
                    if (targetLabel == sourceLabel) continue;
                    var k = (id.Patient, sourceLabel, targetLabel);
                    counts[k] = counts.TryGetValue(k, out int n) ? n + 1 : 1;
                }
            }

            return counts
                .Select(kv => new NearMiss(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value))
                .OrderByDescending(m => m.FieldCount)
                .ThenBy(m => m.PatientId, StringComparer.Ordinal)
                .ThenBy(m => m.SourcePlanLabel, StringComparer.Ordinal)
                .ThenBy(m => m.TargetPlanLabel, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
