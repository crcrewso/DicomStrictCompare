using System.Text.Json;
using System.Text.Json.Serialization;
using DSClibrary.Comparing;
using DSClibrary.Studies;

namespace DSCprototypes.Shared;

/// <summary>
/// Settings that belong to this machine, not to a study: how fast to run, never what a result is. None of them changes
/// a count. Kept in <see cref="DefaultPath"/>, one file per user and machine (crcrewso, 2026-11-15: "Build the settings
/// page and file"). A study keeps Max and GPU float, which can change results.
/// </summary>
public sealed record MachineSettings
{
    public int Version { get; init; } = 1;

    /// <summary>The comparison engine; HandWritten, the reference, unless chosen.</summary>
    public ComparerKind Engine { get; init; } = ComparerKind.HandWritten;

    /// <summary>CPU threads; null is the recommended count, <see cref="CpuTopology.PhysicalCores"/>, on whatever machine runs it.</summary>
    public int? Threads { get; init; }

    /// <summary>Pairs compared at once; null chooses from free memory.</summary>
    public int? PairsAtOnce { get; init; }

    /// <summary>Read each target only where the criteria and the edge check use it. On by default (agreed 2026-11-15).</summary>
    public bool PartialTargetReads { get; init; } = true;

    /// <summary>Files read at once ahead of the comparison; 0 has each worker read its own pair. 4 by default (agreed 2026-11-15).</summary>
    public int Readers { get; init; } = 4;

    /// <summary>Loaded pairs the read queue holds.</summary>
    public int PairsHeld { get; init; } = 2;

    /// <summary>Part of a GPU's name, or null for the first GPU.</summary>
    public string? Device { get; init; }

    public static MachineSettings Defaults { get; } = new();

    /// <summary>%APPDATA%\DicomStrictCompare\settings.json on Windows, ~/.config/DicomStrictCompare/settings.json on Linux.</summary>
    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DicomStrictCompare", "settings.json");

    /// <summary>The run settings for a comparison: these, plus the study's Max and GPU precision.</summary>
    public StudyRunSettings ToRun(MaximumKind maximum, GpuPrecision precision) => new(
        maximum,
        new ComparerSettings(Engine, Threads, precision, Device: Device),
        PairsAtOnce,
        PartialTargetReads,
        Readers > 0 ? Readers : null,
        Readers > 0 ? PairsHeld : null);

    /// <summary>The file's settings, or the defaults when there is no file. An unreadable file also gives the defaults, and says why.</summary>
    public static MachineSettings Load(string path, out string? problem)
    {
        problem = null;
        if (!File.Exists(path)) return Defaults;
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), MachineSettingsJson.Default.MachineSettings) ?? Defaults;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            problem = $"{path} could not be read ({ex.Message}); using the defaults.";
            return Defaults;
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, MachineSettingsJson.Default.MachineSettings));
    }

    /// <summary>
    /// The thread counts a front end lists: 1 to 4, then every 2 to 12, then every 4, up to <paramref name="max"/>; the
    /// recommended count and the maximum are always listed (crcrewso, 2026-11-15).
    /// </summary>
    public static IReadOnlyList<int> ThreadCounts(int max, int recommended)
    {
        max = Math.Max(1, max);
        var counts = new SortedSet<int> { Math.Clamp(recommended, 1, max), max };
        for (int n = 1; n <= max; n += n < 4 ? 1 : n < 12 ? 2 : 4) counts.Add(n);
        return counts.ToArray();
    }

    /// <summary>The listed count for a wanted one: the largest not above it, so a count between steps rounds down.</summary>
    public static int ListedThreads(IReadOnlyList<int> listed, int? wanted)
    {
        int w = wanted ?? CpuTopology.PhysicalCores;
        return listed.LastOrDefault(c => c <= w, listed[0]);
    }
}

// Source-generated, as the study file's is, so a trimmed or file-based program can read it too.
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(MachineSettings))]
internal sealed partial class MachineSettingsJson : JsonSerializerContext;
