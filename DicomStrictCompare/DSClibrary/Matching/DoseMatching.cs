using System;
using System.Collections.Generic;
using System.Linq;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Matching
{
    /// <summary>
    /// How a source dose is matched to the target dose that should be compared against it.
    /// <para>
    /// crcrewso, 2026-11-08: "matching should either be with identifier or uid". Both are offered rather
    /// than one chosen, because they fail in opposite situations.
    /// </para>
    /// </summary>
    public enum DoseMatchStrategy
    {
        /// <summary>
        /// Patient, plan label and beam name. Survives recalculation and re-planning, because those
        /// labels are what a person keeps stable while the objects underneath are replaced - which is
        /// the usual shape of a TPS-upgrade comparison, where the two sides are different plans
        /// entirely. It is string matching on metadata, so it is only as reliable as the naming.
        /// </summary>
        Identifier = 1,

        /// <summary>
        /// The referenced plan's SOPInstanceUID, less its save time, with the referenced beam number.
        /// Needs no plan file and no naming, but only works when both doses reference <b>the same
        /// plan</b> - two algorithms, two grid resolutions, or the same plan recalculated at two
        /// times. Across a re-plan the UIDs differ by definition and nothing will match.
        /// <para>
        /// Eclipse writes the save time as the UID's last component and changes it on every save,
        /// so an exact UID pairs nothing between a plan's two recalculations; see
        /// <see cref="PlanUids.WithoutSaveTime"/> for the measurement (2026-11-09).
        /// </para>
        /// </summary>
        PlanAndBeamUid = 2,

        /// <summary>
        /// The referenced plan's UID, less its save time, with the beam's <b>name</b> from that plan. The same plan as
        /// <see cref="PlanAndBeamUid"/>, but a renumbered beam still pairs (crcrewso, 2026-11-16: "the match by plan and
        /// beam number should become plan and beam name"). Needs the plan, for the name.
        /// </summary>
        PlanUidAndBeamName = 3,

        /// <summary>
        /// Plan label and beam name, without the patient: pairs the same plans kept under different test patients.
        /// </summary>
        PlanLabelAndBeamName = 4,
    }

    /// <summary>
    /// What identifies a dose for matching. A record rather than a concatenated string so that a
    /// failure to match can say which part differed.
    /// <para>
    /// The existing <c>DCSCore.DoseFile.MatchIdentifier</c> is <c>PatientId + PlanID + FieldName</c>
    /// concatenated, which cannot distinguish a patient of "AB" with plan "C" from a patient of "A"
    /// with plan "BC", and cannot report what went wrong.
    /// </para>
    /// </summary>
    public sealed record DoseMatchKey
    {
        public DoseMatchStrategy Strategy { get; }
        public string? PatientId { get; }
        public string? PlanLabel { get; }
        public string? BeamName { get; }

        /// <summary>The referenced plan's UID as compared: with its save time removed, see <see cref="PlanUids"/>.</summary>
        public string? PlanSopInstanceUid { get; }
        public int? BeamNumber { get; }

        private DoseMatchKey(
            DoseMatchStrategy strategy,
            string? patientId = null,
            string? planLabel = null,
            string? beamName = null,
            string? planSopInstanceUid = null,
            int? beamNumber = null)
        {
            Strategy = strategy;
            PatientId = patientId;
            PlanLabel = planLabel;
            BeamName = beamName;
            PlanSopInstanceUid = planSopInstanceUid;
            BeamNumber = beamNumber;
        }

        /// <summary>
        /// Builds the key for a dose under the given strategy, or null when the dose lacks what the
        /// strategy needs. Null is deliberate: an unmatchable dose is reported as unmatched rather
        /// than given a fabricated key.
        /// <para>
        /// The existing code fabricates one - <c>"FieldNotFound" + random.Next()</c> - so that
        /// unmatched files cannot accidentally pair. That works, but it is non-deterministic, so the
        /// same folder produces different identifiers on every run and a log cannot be compared with
        /// yesterday's.
        /// </para>
        /// </summary>
        public static DoseMatchKey? For(RTDoseWithPlan dose, DoseMatchStrategy strategy) => For(dose, strategy, null, null);

        /// <summary>
        /// The same, with the plan label and beam name passed through the given rewrites first (F2).
        /// The rewrites apply to Identifier keys only.
        /// </summary>
        public static DoseMatchKey? For(RTDoseWithPlan dose, DoseMatchStrategy strategy,
            Func<string, string>? rewriteLabel, Func<string, string>? rewriteField = null)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));

            return strategy switch
            {
                DoseMatchStrategy.Identifier =>
                    dose.Dose.PatientID is { Length: > 0 } patient
                    && dose.Plan?.RTPlanLabel is { Length: > 0 } label
                    && dose.Beam?.BeamName is { Length: > 0 } field
                        ? new DoseMatchKey(strategy, patientId: patient,
                            planLabel: rewriteLabel is null ? label : rewriteLabel(label),
                            beamName: rewriteField is null ? field : rewriteField(field))
                        : null,

                DoseMatchStrategy.PlanAndBeamUid =>
                    dose.Dose.ReferencedRTPlanSOPInstanceUID is { Length: > 0 } uid
                    && dose.Dose.ReferencedBeamNumber is { } number
                        ? new DoseMatchKey(strategy, planSopInstanceUid: PlanUids.WithoutSaveTime(uid), beamNumber: number)
                        : null,
                DoseMatchStrategy.PlanUidAndBeamName =>
                    dose.Dose.ReferencedRTPlanSOPInstanceUID is { Length: > 0 } planUid
                    && dose.Beam?.BeamName is { Length: > 0 } name
                        ? new DoseMatchKey(strategy, planSopInstanceUid: PlanUids.WithoutSaveTime(planUid),
                            beamName: rewriteField is null ? name : rewriteField(name))
                        : null,
                DoseMatchStrategy.PlanLabelAndBeamName =>
                    dose.Plan?.RTPlanLabel is { Length: > 0 } planLabel
                    && dose.Beam?.BeamName is { Length: > 0 } beam
                        ? new DoseMatchKey(strategy,
                            planLabel: rewriteLabel is null ? planLabel : rewriteLabel(planLabel),
                            beamName: rewriteField is null ? beam : rewriteField(beam))
                        : null,

                _ => throw new ArgumentOutOfRangeException(nameof(strategy))
            };
        }

        /// <summary>What this key needed and did not have, for a message a user can act on.</summary>
        public static string DescribeMissing(RTDoseWithPlan dose, DoseMatchStrategy strategy)
        {
            var missing = new List<string>();
            bool patient = strategy == DoseMatchStrategy.Identifier;
            bool label = strategy is DoseMatchStrategy.Identifier or DoseMatchStrategy.PlanLabelAndBeamName;
            bool uid = strategy is DoseMatchStrategy.PlanAndBeamUid or DoseMatchStrategy.PlanUidAndBeamName;
            bool name = strategy != DoseMatchStrategy.PlanAndBeamUid;
            if (patient && dose.Dose.PatientID is not { Length: > 0 }) missing.Add("PatientID");
            if (uid && dose.Dose.ReferencedRTPlanSOPInstanceUID is not { Length: > 0 }) missing.Add("ReferencedRTPlanSOPInstanceUID");
            if (strategy == DoseMatchStrategy.PlanAndBeamUid && dose.Dose.ReferencedBeamNumber is null) missing.Add("ReferencedBeamNumber");
            if ((label || name) && dose.Plan is null) missing.Add("the plan was not found");
            else
            {
                if (label && dose.Plan!.RTPlanLabel is not { Length: > 0 }) missing.Add("RTPlanLabel");
                if (name && dose.Beam is null) missing.Add("the referenced beam was not in the plan");
                else if (name && dose.Beam!.BeamName is not { Length: > 0 }) missing.Add("BeamName");
            }
            return missing.Count == 0 ? "nothing" : string.Join(", ", missing);
        }

        public override string ToString() => Strategy switch
        {
            DoseMatchStrategy.Identifier => $"{PatientId} / {PlanLabel} / {BeamName}",
            DoseMatchStrategy.PlanUidAndBeamName => $"plan {PlanSopInstanceUid} / {BeamName}",
            DoseMatchStrategy.PlanLabelAndBeamName => $"{PlanLabel} / {BeamName}",
            _ => $"plan {PlanSopInstanceUid} beam {BeamNumber}",
        };
    }

    /// <summary>A source dose and the target dose matched to it.</summary>
    public sealed record MatchedDosePair(DoseMatchKey Key, RTDoseWithPlan Source, RTDoseWithPlan Target)
    {
        /// <summary>
        /// How many pairs share this key: 1, or every source x target pairing of an ambiguous key, all of which are
        /// compared and the worst kept per criterion (crcrewso, 2026-11-16).
        /// </summary>
        public int Candidates { get; init; } = 1;

        public override string ToString() =>
            $"{Key}: {System.IO.Path.GetFileName(Source.Dose.SourcePath)} vs "
            + $"{System.IO.Path.GetFileName(Target.Dose.SourcePath)}";
    }

    /// <summary>A dose that could not be matched, and why.</summary>
    public sealed record UnmatchedDose(RTDoseWithPlan Dose, string Reason)
    {
        public override string ToString() => $"{System.IO.Path.GetFileName(Dose.Dose.SourcePath)}: {Reason}";
    }

    /// <summary>The outcome of matching one set of doses against another.</summary>
    public sealed class DoseMatchResult
    {
        public DoseMatchStrategy Strategy { get; }
        public IReadOnlyList<MatchedDosePair> Pairs { get; }
        public IReadOnlyList<UnmatchedDose> UnmatchedSource { get; }
        public IReadOnlyList<UnmatchedDose> UnmatchedTarget { get; }

        /// <summary>
        /// Keys that more than one dose in a set claimed. None is picked: when the other set has the key, every
        /// pairing is compared and the worst kept per criterion (<see cref="MatchedDosePair.Candidates"/>).
        /// </summary>
        public IReadOnlyList<string> AmbiguousKeys { get; }

        /// <summary>Distinct keys paired; less than <see cref="Pairs"/>' count when a key is ambiguous.</summary>
        public int PairedKeys => Pairs.Select(p => p.Key).Distinct().Count();

        internal DoseMatchResult(
            DoseMatchStrategy strategy,
            IReadOnlyList<MatchedDosePair> pairs,
            IReadOnlyList<UnmatchedDose> unmatchedSource,
            IReadOnlyList<UnmatchedDose> unmatchedTarget,
            IReadOnlyList<string> ambiguousKeys)
        {
            Strategy = strategy;
            Pairs = pairs;
            UnmatchedSource = unmatchedSource;
            UnmatchedTarget = unmatchedTarget;
            AmbiguousKeys = ambiguousKeys;
        }

        public override string ToString() =>
            $"{Strategy}: {Pairs.Count} pair(s)"
            + (PairedKeys < Pairs.Count ? $" for {PairedKeys} key(s), every pairing of an ambiguous key compared and the worst kept" : "")
            + $", {UnmatchedSource.Count} source and {UnmatchedTarget.Count} target unmatched, {AmbiguousKeys.Count} ambiguous";
    }

    /// <summary>
    /// Matches a set of source doses against a set of target doses.
    /// <para>
    /// This is the pairing the existing <c>DCSCore.DscDataHandler</c> performs, lifted so it can be
    /// tested without a Windows Forms project. Four things are different: the key is structured rather
    /// than a concatenated string, an unmatchable dose is reported rather than given a random
    /// identifier, a key claimed twice is reported as ambiguous and every pairing compared (the worst kept), and the
    /// strategy is the caller's choice because identifier and UID matching fail in opposite
    /// situations.
    /// </para>
    /// </summary>
    public static class DoseMatcher
    {
        public static DoseMatchResult Match(
            IEnumerable<RTDoseWithPlan> source,
            IEnumerable<RTDoseWithPlan> target,
            DoseMatchStrategy strategy) =>
            Match(source, target, PairingRules.Exact(strategy));

        /// <summary>
        /// Matches with label rules and overrides applied before keying (F2). With no rules this is
        /// exactly the strategy's own match.
        /// </summary>
        public static DoseMatchResult Match(
            IEnumerable<RTDoseWithPlan> source,
            IEnumerable<RTDoseWithPlan> target,
            PairingRules rules)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (rules is null) throw new ArgumentNullException(nameof(rules));

            var strategy = rules.Strategy;
            var unmatchedSource = new List<UnmatchedDose>();
            var unmatchedTarget = new List<UnmatchedDose>();
            var ambiguous = new List<string>();

            Func<string, string>? sourceLabel = rules.RewritesLabels ? rules.SourceKeyLabel : null;
            Func<string, string>? targetLabel = rules.RewritesLabels ? rules.TargetKeyLabel : null;
            Func<string, string>? field = rules.FieldRules.Count > 0 ? rules.KeyFieldName : null;
            var sourceByKey = Index(source, strategy, sourceLabel, field, unmatchedSource, ambiguous, "source");
            var targetByKey = Index(target, strategy, targetLabel, field, unmatchedTarget, ambiguous, "target");

            var pairs = new List<MatchedDosePair>();
            foreach (var (key, sources) in sourceByKey)
            {
                if (targetByKey.TryGetValue(key, out var targets))
                {
                    // An ambiguous key pairs every way; the comparison keeps the worst per criterion.
                    foreach (var s in sources)
                        foreach (var t in targets)
                            pairs.Add(new MatchedDosePair(key, s, t) { Candidates = sources.Count * targets.Count });
                }
                else
                    foreach (var s in sources) unmatchedSource.Add(new UnmatchedDose(s, $"no target has {key}"));
            }

            foreach (var (key, targets) in targetByKey)
                if (!sourceByKey.ContainsKey(key))
                    foreach (var t in targets) unmatchedTarget.Add(new UnmatchedDose(t, $"no source has {key}"));

            return new DoseMatchResult(strategy, pairs, unmatchedSource, unmatchedTarget, ambiguous);
        }

        private static Dictionary<DoseMatchKey, List<RTDoseWithPlan>> Index(
            IEnumerable<RTDoseWithPlan> doses,
            DoseMatchStrategy strategy,
            Func<string, string>? rewriteLabel,
            Func<string, string>? rewriteField,
            List<UnmatchedDose> unmatchable,
            List<string> ambiguous,
            string side)
        {
            var byKey = new Dictionary<DoseMatchKey, List<RTDoseWithPlan>>();

            foreach (var dose in doses)
            {
                DoseMatchKey? key = DoseMatchKey.For(dose, strategy, rewriteLabel, rewriteField);
                if (key is null)
                {
                    unmatchable.Add(new UnmatchedDose(dose,
                        $"cannot be matched by {strategy}: missing {DoseMatchKey.DescribeMissing(dose, strategy)}"));
                    continue;
                }

                if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = new List<RTDoseWithPlan>();
                list.Add(dose);
            }

            foreach (var (key, list) in byKey)
                if (list.Count > 1)
                    ambiguous.Add($"{side}: {key} is claimed by {list.Count} doses ({string.Join(", ", list.Select(d => System.IO.Path.GetFileName(d.Dose.SourcePath)))})");

            return byKey;
        }
    }
}
