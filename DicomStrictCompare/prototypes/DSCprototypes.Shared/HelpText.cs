using DSClibrary.Comparing;

namespace DSCprototypes.Shared;

/// <summary>
/// One engine in the Settings tab's list: the setting it stands for, a plain name, and what it does. The setting keeps
/// its code name (<see cref="ComparerKind"/>) in settings.json, so files saved before these names still load.
/// </summary>
public sealed record EngineChoice(ComparerKind Kind, string Label, string Description)
{
    public override string ToString() => Label;

    /// <summary>Every engine, in the list's order: the reference first.</summary>
    public static IReadOnlyList<EngineChoice> All { get; } = new[]
    {
        new EngineChoice(ComparerKind.HandWritten, "Reference: every point, one thread",
            "The original comparison, kept exactly as written: it tests every point of the grid, one after another, on one "
            + "CPU thread. The slowest engine, and the one every other engine is tested against. Several pairs can still run "
            + "at once (Threads)."),
        new EngineChoice(ComparerKind.SingleThread, "CPU, one thread",
            "The reference's rules on one CPU thread, skipping the points that can't reach the threshold: only the box "
            + "around the source dose above it is visited. Gives the reference's counts exactly."),
        new EngineChoice(ComparerKind.MultiThread, "CPU, several threads",
            "As CPU, one thread, with each pair split across several CPU threads (Threads). Gives the reference's counts exactly."),
        new EngineChoice(ComparerKind.Ilgpu, "Graphics card (ILGPU)",
            "The reference's rules on the graphics card, written in C# and turned into graphics-card code by ILGPU, a .NET "
            + "library, through the card's OpenCL driver. Gives the reference's counts exactly in double; GPU float may "
            + "differ on a few points in millions."),
        new EngineChoice(ComparerKind.OpenCl, "Graphics card (OpenCL)",
            "The reference's rules on the graphics card, written directly in OpenCL, the programming interface most graphics "
            + "cards' drivers provide. Gives the reference's counts exactly in double; GPU float may differ on a few points "
            + "in millions."),
    };

    public static EngineChoice For(ComparerKind kind) => All.First(c => c.Kind == kind);
}

/// <summary>
/// The windows' hover text, in one place so both say the same thing (crcrewso, 2026-11-17: hover and help text "that
/// makes sense for things like what 'HandWritten' or ILGPU mean"). Written for physicists, not developers.
/// </summary>
public static class HelpText
{
    // ---- criteria ----
    public const string Tolerance =
        "Dose difference allowed, in percent: of Max when Global is ticked, of the source dose at each point when it isn't.";
    public const string Distance =
        "Distance to agreement, in mm. A point outside the dose tolerance still passes if the target dose lies between "
        + "the lowest and highest source doses found this far away along each axis. For gamma, the distance criterion.";
    public const string Threshold =
        "Points where the source dose is below this percentage of Max aren't compared.";
    public const string Global =
        "Ticked: the dose tolerance is a percentage of Max (global). Not ticked: of the source dose at each point (local).";
    public const string Gamma =
        "Ticked: a gamma index at this dose and distance (the pymedphys method); a point passes when gamma is 1 or less. "
        + "Not ticked: the strict test, dose difference first, then distance to agreement.";
    public const string AppliesTo =
        "Which beams this criterion compares, by the source plan's radiation type. A pair with no source plan is compared only by Both.";
    public const string Maximum =
        "What the threshold and a global tolerance are percentages of. GlobalMax: the highest dose in the source grid. "
        + "CaxMax: the highest dose on the central axis (for an arc, the dose about the isocentre), averaged over 3 x 3 "
        + "voxels; used only when the source plan's jaws put the central axis inside a roughly centred field, at gantry and "
        + "couch 0, otherwise GlobalMax. Each result row says which was used.";
    public const string GpuFloat =
        "For the graphics-card engines only (Settings, Engine). Many graphics cards run double precision far slower than "
        + "float, for example NVIDIA GeForce RTX 20 and 30 series. Float may differ from the reference on a few points in "
        + "millions; double matches it exactly.";

    // ---- dose versus dose results ----
    public const string Key = "The source and target doses this row compares, by the key they were matched on.";
    public const string Criterion = "This row's criterion: dose / distance / threshold, global or local, and gamma.";
    public const string Ambiguous =
        "\"worst of N\" when N doses on the other side matched this key: every pairing was compared and this row is the "
        + "worst (a failure first, then the highest % failed).";
    public const string MaximumUsed = "What Max was for this row: the volume maximum, or the central-axis maximum and why.";
    public const string EdgeCheck =
        "Whether the dose's field agrees with its plan's jaws: at 10 cm depth the 50 % points should lie within 2 mm of "
        + "the jaws. Checked only for static open photon fields at gantry and couch 0, not FFF, wedged or MLC-shaped.";
    public const string Compared = "Points compared: those at or above the threshold.";
    public const string Failed = "Compared points that failed.";
    public const string PercentFailed = "Failed points as a percentage of compared points.";
    public const string PassRate = "Passed points as a percentage of compared points.";
    public const string MaxGammaDoses =
        "The largest gamma index found (gamma criteria only). To save time the search stops at 1.1, so 1.1 means 1.1 or "
        + "more, and stops at a point once it passes, so a value of 1 or less is an upper bound.";
    public const string SourceMu = "Monitor units of the source plan's beam.";
    public const string TargetMu = "Monitor units of the target plan's beam. A difference from the source suggests the doses weren't planned alike.";
    public const string Seconds = "Seconds this pair took. Each pair is loaded once for all its criteria, so its rows share the time.";
    public const string Failure = "Why the pair couldn't be compared, when it couldn't.";
    public const string PlanCheck =
        "Whether the two plans describe the same field (machine, energy, jaws, angles, MU, wedges and so on): \"agrees\", "
        + "\"not checked\" and why, or the differences.";
    public const string Warnings =
        "Things that don't stop the comparison but change what its number means, such as different grid spacing or dose reporting.";

    // ---- settings ----
    public const string Engine =
        "How the comparison is computed. Every engine applies the same rules and gives the reference's counts; they differ "
        + "in speed and in the hardware they use. The line below describes the one chosen.";
    public const string Threads =
        "CPU threads. The CPU, several threads engine splits each pair across them; the other engines compare that many "
        + "pairs at once. The recommended count is one per full-speed core, leaving out hyper-threads (SMT) and low-power E-cores.";
    public const string PairsAtOnce =
        "Pairs compared at the same time. 0 chooses from free memory: as many as half of it holds, up to the thread count "
        + "(2 for the CPU, several threads engine and the graphics-card engines).";
    public const string TargetBoxOnly =
        "Read each target dose only where the comparison and the field-edge check look, not the whole grid. A pair with a "
        + "gamma criterion is read whole. The counts are the same either way; it saves memory and time.";
    public const string Readers =
        "Dose files read ahead, at once, while others are being compared; fewer if memory is short. 0 has each comparison read its own pair.";
    public const string Held =
        "Pairs read ahead and waiting to be compared, when Readers is above 0. Each waiting pair holds two grids in memory.";

    // ---- doses versus scans ----
    public const string ScanTest =
        "Gamma: the gamma index along the scan (the pymedphys method); a point passes when gamma is 1 or less. "
        + "Strict: this program's dose comparison along the scan; a point passes on dose difference, else if the reference's "
        + "doses within the distance bracket the other curve's dose. With distance 0, a dose difference only.";
    public const string ScanReference =
        "The reference curve: its points are the ones tested, and the dose percentage and cutoff are of its maximum. "
        + "Measured: the scan. Calculated: the dose.";
    public const string ScanPoints = "Points passed / points tested.";
    public const string ScanMaxGamma = "The largest gamma index found (gamma test only).";
    public const string ScanOutcome =
        "pass: every tested point passed. fail: at least one point failed, or none could be tested. not compared: and why.";
}
