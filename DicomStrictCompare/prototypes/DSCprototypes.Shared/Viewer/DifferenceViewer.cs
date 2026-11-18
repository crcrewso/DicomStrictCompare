using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DSClibrary;
using DSClibrary.Gamma;
using DSClibrary.Maps;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;

namespace DSCprototypes.Shared.Viewer;

/// <summary>What the viewer page shows: the map files, the frame and the counts (the page reads it as info.json).</summary>
public sealed record ViewerMaps(
    string Folder, string Title, string SourceFile, string TargetFile, string FrameNote, string Criterion,
    double SourceMaximumGy, double CutoffPercent, int GammaEvaluated, int GammaFailed, int PercentEvaluated,
    double[] GridSpacingMm, int[] GridShape, string[] Volumes);

/// <summary>
/// Turns a pair of doses into the files NiiVue opens (crcrewso, 2026-11-18): the source dose, the gamma map and the
/// signed % difference, as NIfTI in the beam frame (<see cref="BeamFrame"/>), on the source grid. Front-end free, so
/// both GUIs can host the page.
/// </summary>
public static class DifferenceViewer
{
    public const string DoseFile = "dose.nii", GammaFile = "gamma.nii", PercentFile = "percent.nii", InfoFile = "info.json";

    /// <summary>Reads both doses, finds the source's plan beam for the frame, computes the maps and writes them to <paramref name="folder"/>.</summary>
    public static ViewerMaps Prepare(string sourcePath, string targetPath, GammaOptions gamma, double cutoffPercent, string folder,
        IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        Directory.CreateDirectory(folder);
        progress?.Report($"Reading {Path.GetFileName(sourcePath)}");
        var source = DoseGridReader.Read(sourcePath);
        var withPlan = FindPlan(sourcePath);
        var (frame, refusal) = BeamFrame.For(withPlan?.Beam, withPlan?.Beam is { } b ? withPlan.Plan?.SetupFor(b) : null, source);
        if (frame is null) throw new InvalidOperationException("Can't place the beam frame: " + refusal);
        progress?.Report($"Frame: {frame.Note}");
        progress?.Report($"Reading {Path.GetFileName(targetPath)}");
        var target = DoseGridReader.Read(targetPath);
        cancellation.ThrowIfCancellationRequested();
        progress?.Report($"Computing gamma {gamma.DosePercentThreshold:0.##} %/{gamma.DistanceThreshold:0.##} mm and the % difference");
        var map = DifferenceMap.Compute(source, target, gamma, cutoffPercent, cancellation);
        return Write(map, frame, sourcePath, targetPath, folder, withPlan, progress);
    }

    /// <summary>Writes already computed maps, so a caller holding the grids can skip the reads.</summary>
    public static ViewerMaps Write(DifferenceMap map, BeamFrame frame, string sourcePath, string targetPath, string folder,
        RTDoseWithPlan? source = null, IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(folder);
        var volumes = new List<string> { DoseFile };
        NiftiWriter.Write(Path.Combine(folder, DoseFile), map.SourceDose, map.Grid, frame, "source dose, Gy");
        if (map.Gamma is { } g) { NiftiWriter.Write(Path.Combine(folder, GammaFile), g, map.Grid, frame, "gamma, capped at 3"); volumes.Add(GammaFile); }
        NiftiWriter.Write(Path.Combine(folder, PercentFile), map.PercentDifference, map.Grid, frame, "target - source, % of source max");
        volumes.Add(PercentFile);
        var o = map.GammaOptions;
        string criterion = o is null ? "no gamma" : string.Create(CultureInfo.InvariantCulture,
            $"gamma {o.DosePercentThreshold:0.##} %/{o.DistanceThreshold:0.##} mm {(o.LocalGamma ? "local" : "global")}, cutoff {map.CutoffPercent:0.#} %, capped at {DifferenceMap.GammaCap:0}");
        string title = source?.Beam is { } beam
            ? $"{source.Plan?.RTPlanLabel ?? "plan"} / {beam.BeamName ?? beam.BeamNumber?.ToString(CultureInfo.InvariantCulture) ?? "beam"}"
            : Path.GetFileNameWithoutExtension(sourcePath);
        var maps = new ViewerMaps(folder, title, Path.GetFileName(sourcePath), Path.GetFileName(targetPath), frame.Note, criterion,
            map.SourceMaximum, map.CutoffPercent, map.Evaluated, map.GammaFailed, map.PercentEvaluated,
            new[] { map.Grid.XRes, map.Grid.YRes, map.Grid.ZRes }, new[] { map.Grid.DimensionX, map.Grid.DimensionY, map.Grid.DimensionZ },
            volumes.ToArray());
        File.WriteAllText(Path.Combine(folder, InfoFile), JsonSerializer.Serialize(maps, ViewerJson.Default.ViewerMaps));
        progress?.Report($"Maps written to {folder}");
        return maps;
    }

    /// <summary>The dose with its plan beam, from the dose's own folder (its plan is exported beside it), else its parent's tree.</summary>
    public static RTDoseWithPlan? FindPlan(string dosePath)
    {
        string full = Path.GetFullPath(dosePath);
        string? folder = Path.GetDirectoryName(full);
        foreach (bool recursive in new[] { false, true })
        {
            if (folder is null) break;
            var root = recursive ? Directory.GetParent(folder)?.FullName ?? folder : folder;
            var found = RTFolderReader.Read(root, recursive: recursive, headersOnly: true).Doses
                .FirstOrDefault(d => string.Equals(Path.GetFullPath(d.Dose.SourcePath), full, StringComparison.Ordinal));
            if (found?.Beam is not null) return found;
        }
        return null;
    }
}

/// <summary>Source-generated, so it also works where reflection-based JSON is off (file-based apps, trimming).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ViewerMaps))]
internal sealed partial class ViewerJson : JsonSerializerContext;
