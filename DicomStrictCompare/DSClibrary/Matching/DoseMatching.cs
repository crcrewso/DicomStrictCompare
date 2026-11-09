using System;
using System.Collections.Generic;
using System.Linq;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Matching
{
    /// <summary>
    /// How a source dose is matched to the target dose that should be compared against it.
    /// <para>
    /// Cody, 2026-11-08: "matching should either be with identifier or uid". Both are offered rather
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
        PlanAndBeamUid = 2
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
        public static DoseMatchKey? For(RTDoseWithPlan dose, DoseMatchStrategy strategy)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));

            return strategy switch
            {
                DoseMatchStrategy.Identifier =>
                    dose.Dose.PatientID is { Length: > 0 } patient
                    && dose.Plan?.RTPlanLabel is { Length: > 0 } label
                    && dose.Beam?.BeamName is { Length: > 0 } field
                        ? new DoseMatchKey(strategy, patientId: patient, planLabel: label, beamName: field)
                        : null,

                DoseMatchStrategy.PlanAndBeamUid =>
                    dose.Dose.ReferencedRTPlanSOPInstanceUID is { Length: > 0 } uid
                    && dose.Dose.ReferencedBeamNumber is { } number
                        ? new DoseMatchKey(strategy, planSopInstanceUid: PlanUids.WithoutSaveTime(uid), beamNumber: number)
                        : null,

                _ => throw new ArgumentOutOfRangeException(nameof(strategy))
            };
        }

        /// <summary>What this key needed and did not have, for a message a user can act on.</summary>
        public static string DescribeMissing(RTDoseWithPlan dose, DoseMatchStrategy strategy)
        {
            var missing = new List<string>();
            if (strategy == DoseMatchStrategy.Identifier)
            {
                if (dose.Dose.PatientID is not { Length: > 0 }) missing.Add("PatientID");
                if (dose.Plan is null) missing.Add("the plan was not found");
                else if (dose.Plan.RTPlanLabel is not { Length: > 0 }) missing.Add("RTPlanLabel");
                if (dose.Beam is null) missing.Add("the referenced beam was not in the plan");
                else if (dose.Beam.BeamName is not { Length: > 0 }) missing.Add("BeamName");
            }
            else
            {
                if (dose.Dose.ReferencedRTPlanSOPInstanceUID is not { Length: > 0 })
                    missing.Add("ReferencedRTPlanSOPInstanceUID");
                if (dose.Dose.ReferencedBeamNumber is null) missing.Add("ReferencedBeamNumber");
            }
            return missing.Count == 0 ? "nothing" : string.Join(", ", missing);
        }

        public override string ToString() => Strategy == DoseMatchStrategy.Identifier
            ? $"{PatientId} / {PlanLabel} / {BeamName}"
            : $"plan {PlanSopInstanceUid} beam {BeamNumber}";
    }

    /// <summary>A source dose and the target dose matched to it.</summary>
    public sealed record MatchedDosePair(DoseMatchKey Key, RTDoseWithPlan Source, RTDoseWithPlan Target)
    {
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
        /// Keys that more than one dose in a set claimed. Reported rather than resolved: picking one
        /// arbitrarily would compare a file the user did not choose and say nothing about it.
        /// </summary>
        public IReadOnlyList<string> AmbiguousKeys { get; }

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
            $"{Strategy}: {Pairs.Count} pair(s), {UnmatchedSource.Count} source and "
            + $"{UnmatchedTarget.Count} target unmatched, {AmbiguousKeys.Count} ambiguous";
    }

    /// <summary>
    /// Matches a set of source doses against a set of target doses.
    /// <para>
    /// This is the pairing the existing <c>DCSCore.DscDataHandler</c> performs, lifted so it can be
    /// tested without a Windows Forms project. Four things are different: the key is structured rather
    /// than a concatenated string, an unmatchable dose is reported rather than given a random
    /// identifier, a key claimed twice is reported as ambiguous rather than silently resolved, and the
    /// strategy is the caller's choice because identifier and UID matching fail in opposite
    /// situations.
    /// </para>
    /// </summary>
    public static class DoseMatcher
    {
        public static DoseMatchResult Match(
            IEnumerable<RTDoseWithPlan> source,
            IEnumerable<RTDoseWithPlan> target,
            DoseMatchStrategy strategy)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));

            var unmatchedSource = new List<UnmatchedDose>();
            var unmatchedTarget = new List<UnmatchedDose>();
            var ambiguous = new List<string>();

            var sourceByKey = Index(source, strategy, unmatchedSource, ambiguous, "source");
            var targetByKey = Index(target, strategy, unmatchedTarget, ambiguous, "target");

            var pairs = new List<MatchedDosePair>();
            foreach (var (key, dose) in sourceByKey)
            {
                if (targetByKey.TryGetValue(key, out var match))
                    pairs.Add(new MatchedDosePair(key, dose, match));
                else
                    unmatchedSource.Add(new UnmatchedDose(dose, $"no target has {key}"));
            }

            foreach (var (key, dose) in targetByKey)
                if (!sourceByKey.ContainsKey(key))
                    unmatchedTarget.Add(new UnmatchedDose(dose, $"no source has {key}"));

            return new DoseMatchResult(strategy, pairs, unmatchedSource, unmatchedTarget, ambiguous);
        }

        private static Dictionary<DoseMatchKey, RTDoseWithPlan> Index(
            IEnumerable<RTDoseWithPlan> doses,
            DoseMatchStrategy strategy,
            List<UnmatchedDose> unmatchable,
            List<string> ambiguous,
            string side)
        {
            var byKey = new Dictionary<DoseMatchKey, RTDoseWithPlan>();
            var seenTwice = new HashSet<DoseMatchKey>();

            foreach (var dose in doses)
            {
                DoseMatchKey? key = DoseMatchKey.For(dose, strategy);
                if (key is null)
                {
                    unmatchable.Add(new UnmatchedDose(dose,
                        $"cannot be matched by {strategy}: missing {DoseMatchKey.DescribeMissing(dose, strategy)}"));
                    continue;
                }

                if (byKey.ContainsKey(key))
                {
                    if (seenTwice.Add(key))
                        ambiguous.Add($"{side}: {key} is claimed by more than one dose");
                    continue;
                }

                byKey[key] = dose;
            }

            // A key claimed twice cannot be matched at all: remove it and say so on that side.
            foreach (var key in seenTwice)
            {
                unmatchable.Add(new UnmatchedDose(byKey[key], $"ambiguous: more than one {side} dose has {key}"));
                byKey.Remove(key);
            }

            return byKey;
        }
    }
}
