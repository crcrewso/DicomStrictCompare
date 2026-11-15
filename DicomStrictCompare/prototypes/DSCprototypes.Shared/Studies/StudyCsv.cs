using System.Globalization;
using System.Text;
using DSClibrary.Studies;
using DSClibrary.Comparing;

namespace DSCprototypes.Shared.Studies;

/// <summary>
/// The study's spreadsheets (F5), invariant culture throughout:
/// <list type="bullet">
/// <item><c>&lt;stem&gt;-results.csv</c>: long form, one row per comparison, field and criterion, so any other
/// pivot is a spreadsheet's job;</item>
/// <item><c>&lt;stem&gt;-pairing.csv</c>: what each comparison could not pair, and the near misses;</item>
/// <item><c>&lt;stem&gt;-index-&lt;n&gt;.csv</c>: one field size by energy table per comparison and criterion.</item>
/// </list>
/// </summary>
public static class StudyCsv
{
    public static readonly string[] ResultColumns =
    {
        "Comparison", "SourceSet", "TargetSet", "Patient", "SourcePlan", "TargetPlan", "Field",
        "Machine", "Energy", "FieldSize", "Collimation", "SSD", "Accessories",
        "TolerancePercent", "DistanceMm", "ThresholdPercent", "Scope", "Algorithm", "MaximumUsed", "MaximumDose", "EdgeCheck",
        "Compared", "Failed", "PercentFailed", "PassRate", "MaxGamma",
        "PlanCheck", "Warnings", "SourceFile", "TargetFile", "Failure",
    };

    public static string ResultsText(StudySession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Join(ResultColumns));
        foreach (var r in session.Rows)
        {
            var f = r.Facts;
            sb.AppendLine(Join(
                r.Comparison, r.Pair.Comparison.SourceSet, r.Pair.Comparison.TargetSet, f.Patient, f.SourcePlan, f.TargetPlan, f.Field,
                f.Machine, f.Energy, f.FieldSize, f.Collimation, f.Ssd, f.Accessories,
                N(r.Criterion.TolerancePercent), N(r.Criterion.DistanceMm), N(r.Criterion.ThresholdPercent),
                r.Criterion.Global ? "global" : "local", r.Criterion.Algorithm,
                r.MaximumUsed, r.Maximum is { } m ? m.Dose.ToString("G9", CultureInfo.InvariantCulture) : "", r.EdgeCheck,
                r.Compared.ToString(CultureInfo.InvariantCulture), r.Failed.ToString(CultureInfo.InvariantCulture),
                r.PercentFailed is { } p ? p.ToString("0.0000", CultureInfo.InvariantCulture) : "",
                r.PassRate is { } pr ? pr.ToString("0.0000", CultureInfo.InvariantCulture) : "",
                r.MaxGamma is { } g ? g.ToString("0.000", CultureInfo.InvariantCulture) : "",
                r.PlanCheck, r.Warnings, r.SourceFile, r.TargetFile, r.Failure ?? ""));
        }
        return sb.ToString();
    }

    public static string PairingText(StudySession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Join("Comparison", "Kind", "File", "Detail"));
        foreach (var p in session.Previews)
        {
            string name = p.Comparison.Name;
            foreach (var u in p.Match.UnmatchedSource) sb.AppendLine(Join(name, "source unmatched", Path.GetFileName(u.Dose.Dose.SourcePath), u.Reason));
            foreach (var u in p.Match.UnmatchedTarget) sb.AppendLine(Join(name, "target unmatched", Path.GetFileName(u.Dose.Dose.SourcePath), u.Reason));
            foreach (var a in p.Match.AmbiguousKeys) sb.AppendLine(Join(name, "ambiguous", "", a));
            foreach (var m in p.NearMisses) sb.AppendLine(Join(name, "near miss", "", m.ToString()));
            foreach (var w in p.SetWarnings) sb.AppendLine(Join(name, "set warning", "", w));
        }
        return sb.ToString();
    }

    public static string IndexText(IndexTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Join(table.Title));
        sb.AppendLine(Join(new[] { IndexTable.Say(table.RowAxis) + " \\ " + IndexTable.Say(table.ColumnAxis) }.Concat(table.ColumnLabels).ToArray()));
        for (int r = 0; r < table.RowLabels.Count; r++)
        {
            var cells = Enumerable.Range(0, table.ColumnLabels.Count)
                .Select(c => table[r, c]?.PooledPassRate is { } v ? v.ToString("0.000", CultureInfo.InvariantCulture) : "");
            sb.AppendLine(Join(new[] { table.RowLabels[r] }.Concat(cells).ToArray()));
        }
        sb.AppendLine();
        sb.AppendLine(Join("Fields per cell, worst field's pass rate"));
        for (int r = 0; r < table.RowLabels.Count; r++)
        {
            var cells = Enumerable.Range(0, table.ColumnLabels.Count).Select(c => table[r, c] is { } cell
                ? $"{cell.Fields}; worst {(cell.WorstPassRate is { } w ? w.ToString("0.000", CultureInfo.InvariantCulture) : "n/a")} ({cell.WorstField})"
                : "");
            sb.AppendLine(Join(new[] { table.RowLabels[r] }.Concat(cells).ToArray()));
        }
        return sb.ToString();
    }

    /// <summary>Writes every file and returns their paths. Nothing is overwritten: the stem carries the run's start time.</summary>
    public static IReadOnlyList<string> Write(StudySession session, string directory, string? stem = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        Directory.CreateDirectory(directory);
        stem ??= $"study-{session.StartedAt:yyyyMMdd-HHmmss}Z";
        var written = new List<string>();
        var utf8 = new UTF8Encoding(false);

        void Put(string name, string text)
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path)) throw new IOException($"{path} already exists; nothing was overwritten.");
            File.WriteAllText(path, text, utf8);
            written.Add(path);
        }

        if (!session.IsPreview) Put(stem + "-results.csv", ResultsText(session));
        Put(stem + "-pairing.csv", PairingText(session));
        int n = 0;
        foreach (var p in session.Previews)
            foreach (var spec in session.Criteria)
                Put($"{stem}-index-{++n}.csv", IndexText(IndexTable.Build(session.Rows, p.Comparison.Name, spec)));
        return written;
    }

    private static string N(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Join(params string[] fields) => string.Join(",", fields.Select(Quote));

    private static string Quote(string s) =>
        s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
