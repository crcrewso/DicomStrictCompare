using DSClibrary.Matching;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>A pairing strategy as the front ends name it.</summary>
public sealed record MatchMode(DoseMatchStrategy Strategy, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The pairing choices the front ends offer (crcrewso, 2026-11-16): plan UID and beam name in place of plan UID and
/// beam number, plan label and beam name, and patient, plan label and beam name. Plan UID and beam number stays for
/// studies saved with it.
/// </summary>
public static class MatchModes
{
    public static MatchMode PlanUidAndBeamName { get; } = new(DoseMatchStrategy.PlanUidAndBeamName, "plan UID and beam name");
    public static MatchMode PlanLabelAndBeamName { get; } = new(DoseMatchStrategy.PlanLabelAndBeamName, "plan label and beam name");
    public static MatchMode Identifier { get; } = new(DoseMatchStrategy.Identifier, "patient, plan label and beam name");
    public static MatchMode PlanAndBeamUid { get; } = new(DoseMatchStrategy.PlanAndBeamUid, "plan UID and beam number (older studies)");

    /// <summary>The comparison window's choices, the first being the default.</summary>
    public static IReadOnlyList<MatchMode> Window { get; } = new[] { PlanUidAndBeamName, PlanLabelAndBeamName, Identifier };

    /// <summary>A study comparison's choices: the window's, plus the one older study files may hold.</summary>
    public static IReadOnlyList<MatchMode> Study { get; } = new[] { Identifier, PlanUidAndBeamName, PlanLabelAndBeamName, PlanAndBeamUid };

    public static MatchMode For(DoseMatchStrategy strategy) => Study.Single(m => m.Strategy == strategy);

    /// <summary>Whether the key has a plan label, which label rules and overrides need.</summary>
    public static bool HasPlanLabel(DoseMatchStrategy s) => s is DoseMatchStrategy.Identifier or DoseMatchStrategy.PlanLabelAndBeamName;
}
