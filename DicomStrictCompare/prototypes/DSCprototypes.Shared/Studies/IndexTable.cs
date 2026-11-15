using System.Globalization;

namespace DSCprototypes.Shared.Studies;

/// <summary>A beam attribute an index table can put on an axis.</summary>
public enum BeamAxis
{
    FieldSize,
    Energy,
    Machine,
    Ssd,
    Collimation,
    Accessories,
    Patient,
    Plan
}

/// <summary>
/// One cell of an index table: the fields in it, pooled.
/// <para>
/// <see cref="PooledPassRate"/> is 100 x (voxels passed) / (voxels compared) over every field in the
/// cell, which weights a field by its voxels the way one comparison already pools them.
/// <see cref="WorstPassRate"/> and <see cref="WorstField"/> are there because a pooled 99.8 % can hide
/// one field at 80 %. Comparisons that failed to run are counted, never pooled.
/// </para>
/// </summary>
public sealed record IndexCell(
    int Fields,
    long Compared,
    long Passed,
    double? WorstPassRate,
    string? WorstField,
    int NotRun)
{
    public double? PooledPassRate => Compared > 0 ? 100.0 * Passed / Compared : null;

    /// <summary>"99.82 % (12 fields; worst 97.10 %)" for a grid cell.</summary>
    public string Text => PooledPassRate is not { } p
        ? (NotRun > 0 ? $"not run ({NotRun})" : "")
        : $"{p.ToString("0.00", CultureInfo.InvariantCulture)} % ({Fields} field{(Fields == 1 ? "" : "s")}"
          + (Fields > 1 && WorstPassRate is { } w ? $"; worst {w.ToString("0.00", CultureInfo.InvariantCulture)} %" : "")
          + (NotRun > 0 ? $"; {NotRun} not run" : "") + ")";
}

/// <summary>
/// A pivot of the result rows (F5): one comparison under one criterion, with a beam attribute on each
/// axis. crcrewso's first case, 2026-11-14: "Field size vs Energy global pass rate". "Global" there means
/// over all fields in the cell; the criterion still decides global or local dose difference.
/// </summary>
public sealed class IndexTable
{
    public required string Comparison { get; init; }
    public required DtaSpec Criterion { get; init; }
    public required BeamAxis RowAxis { get; init; }
    public required BeamAxis ColumnAxis { get; init; }
    public required IReadOnlyList<string> RowLabels { get; init; }
    public required IReadOnlyList<string> ColumnLabels { get; init; }

    /// <summary>[row, column]; null where no field falls.</summary>
    public required IndexCell?[,] Cells { get; init; }

    public IndexCell? this[int row, int column] => Cells[row, column];

    public string Title =>
        $"{Comparison}, {Criterion.Label}: pass rate by {Say(RowAxis)} and {Say(ColumnAxis)}";

    public static IndexTable Build(IEnumerable<StudyRow> rows, string comparison, DtaSpec criterion,
        BeamAxis rowAxis = BeamAxis.FieldSize, BeamAxis columnAxis = BeamAxis.Energy)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var mine = rows.Where(r => r.Comparison == comparison && r.Criterion == criterion).ToArray();

        var rowLabels = Ordered(mine, rowAxis);
        var colLabels = Ordered(mine, columnAxis);
        var cells = new IndexCell?[rowLabels.Count, colLabels.Count];
        var rIndex = rowLabels.Select((l, i) => (l, i)).ToDictionary(x => x.l, x => x.i, StringComparer.Ordinal);
        var cIndex = colLabels.Select((l, i) => (l, i)).ToDictionary(x => x.l, x => x.i, StringComparer.Ordinal);

        foreach (var group in mine.GroupBy(r => (Label(r.Facts, rowAxis), Label(r.Facts, columnAxis))))
        {
            var ran = group.Where(r => r.PassRate is not null).ToArray();
            var worst = ran.OrderBy(r => r.PassRate).FirstOrDefault();
            cells[rIndex[group.Key.Item1], cIndex[group.Key.Item2]] = new IndexCell(
                group.Count(),
                ran.Sum(r => r.Compared),
                ran.Sum(r => r.Compared - r.Failed),
                worst?.PassRate,
                worst is null ? null : $"{worst.Facts.SourcePlan} / {worst.Facts.Field}",
                group.Count() - ran.Length);
        }

        return new IndexTable
        {
            Comparison = comparison,
            Criterion = criterion,
            RowAxis = rowAxis,
            ColumnAxis = columnAxis,
            RowLabels = rowLabels,
            ColumnLabels = colLabels,
            Cells = cells,
        };
    }

    public static string Label(FieldFacts f, BeamAxis axis) => axis switch
    {
        BeamAxis.FieldSize => f.FieldSize,
        BeamAxis.Energy => f.Energy,
        BeamAxis.Machine => f.Machine,
        BeamAxis.Ssd => f.Ssd,
        BeamAxis.Collimation => f.Collimation,
        BeamAxis.Accessories => f.Accessories,
        BeamAxis.Patient => f.Patient,
        BeamAxis.Plan => f.SourcePlan,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    public static string Say(BeamAxis axis) => axis switch
    {
        BeamAxis.FieldSize => "field size",
        BeamAxis.Energy => "energy",
        BeamAxis.Machine => "machine",
        BeamAxis.Ssd => "SSD",
        BeamAxis.Collimation => "collimation",
        BeamAxis.Accessories => "accessories",
        BeamAxis.Patient => "patient",
        BeamAxis.Plan => "plan",
        _ => axis.ToString()
    };

    /// <summary>Field sizes by area, energies in physical order (photons, flattened before FFF, then electrons), the rest by name.</summary>
    private static IReadOnlyList<string> Ordered(IEnumerable<StudyRow> rows, BeamAxis axis)
    {
        var facts = rows.Select(r => r.Facts);
        IEnumerable<FieldFacts> ordered = axis switch
        {
            BeamAxis.FieldSize => facts.OrderBy(f => double.IsNaN(f.FieldAreaMm2) ? double.MaxValue : f.FieldAreaMm2).ThenBy(f => f.FieldCrossplaneMm),
            BeamAxis.Energy => facts.OrderBy(f => f.IsElectron).ThenBy(f => double.IsNaN(f.EnergyValue) ? double.MaxValue : f.EnergyValue).ThenBy(f => f.IsFff),
            _ => facts.OrderBy(f => Label(f, axis), StringComparer.Ordinal)
        };
        return ordered.Select(f => Label(f, axis)).Distinct(StringComparer.Ordinal).ToArray();
    }
}
