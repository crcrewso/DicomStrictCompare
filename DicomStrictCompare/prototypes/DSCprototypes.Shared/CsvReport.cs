using System.Globalization;
using System.Text;

namespace DSCprototypes.Shared;

/// <summary>
/// The spreadsheet. CSV, as DSCcore wrote, because every spreadsheet program opens it and it diffs;
/// a native .xlsx writer is a later choice, not a prototype's. Invariant culture throughout, so a
/// file written on a machine with a comma decimal separator still parses.
/// </summary>
public static class CsvReport
{
    public static readonly string[] ResultColumns =
    {
        "SourceAlias", "TargetAlias", "Key", "SourceFile", "TargetFile",
        "TolerancePercent", "DistanceMm", "ThresholdPercent", "Scope",
        "TotalVoxels", "Compared", "Failed", "PercentFailed",
        "SourceMU", "TargetMU", "ElapsedSeconds", "Failure",
    };

    public static readonly string[] UnmatchedColumns = { "Side", "File", "Reason" };

    public static string ResultsText(ComparisonSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Join(ResultColumns));
        foreach (var r in session.Rows)
        {
            sb.AppendLine(Join(
                session.Request.SourceAlias, session.Request.TargetAlias, r.Key, r.SourceFile, r.TargetFile,
                F(r.Criterion.TolerancePercent), F(r.Criterion.DistanceMm), F(r.Criterion.ThresholdPercent),
                r.Criterion.Global ? "global" : "local",
                r.TotalVoxels.ToString(CultureInfo.InvariantCulture),
                r.Compared.ToString(CultureInfo.InvariantCulture),
                r.Failed.ToString(CultureInfo.InvariantCulture),
                r.PercentFailed is { } p ? p.ToString("0.0000", CultureInfo.InvariantCulture) : "",
                r.SourceMonitorUnits is { } s ? F(s) : "",
                r.TargetMonitorUnits is { } t ? F(t) : "",
                r.ElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                r.Failure ?? ""));
        }
        return sb.ToString();
    }

    public static string UnmatchedText(ComparisonSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Join(UnmatchedColumns));
        foreach (var u in session.Match.UnmatchedSource)
            sb.AppendLine(Join("source", Path.GetFileName(u.Dose.Dose.SourcePath), u.Reason));
        foreach (var u in session.Match.UnmatchedTarget)
            sb.AppendLine(Join("target", Path.GetFileName(u.Dose.Dose.SourcePath), u.Reason));
        foreach (var a in session.Match.AmbiguousKeys)
            sb.AppendLine(Join("ambiguous", "", a));
        return sb.ToString();
    }

    /// <summary>
    /// Writes <c>&lt;stem&gt;.csv</c> and, when anything went unmatched, <c>&lt;stem&gt;-unmatched.csv</c>
    /// beside it. Returns the paths written.
    /// </summary>
    public static IReadOnlyList<string> Write(ComparisonSession session, string directory, string? stem = null)
    {
        Directory.CreateDirectory(directory);
        stem ??= $"{Safe(session.Request.SourceAlias)}-vs-{Safe(session.Request.TargetAlias)}-{session.StartedAt:yyyyMMdd-HHmmss}Z";

        var written = new List<string>();
        string results = Path.Combine(directory, stem + ".csv");
        File.WriteAllText(results, ResultsText(session), new UTF8Encoding(false));
        written.Add(results);

        if (session.Unmatched.Any() || session.Match.AmbiguousKeys.Count > 0)
        {
            string unmatched = Path.Combine(directory, stem + "-unmatched.csv");
            File.WriteAllText(unmatched, UnmatchedText(session), new UTF8Encoding(false));
            written.Add(unmatched);
        }
        return written;
    }

    private static string F(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Safe(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        var chars = s.Trim().Select(c => bad.Contains(c) || c == ' ' ? '_' : c).ToArray();
        return chars.Length == 0 ? "unnamed" : new string(chars);
    }

    private static string Join(params string[] fields) => string.Join(",", fields.Select(Quote));

    private static string Quote(string s) =>
        s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
