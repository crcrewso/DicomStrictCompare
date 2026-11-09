namespace DSCprototypes.Shared;

/// <summary>
/// What a prototype can be told on its command line, so a run is repeatable from a script and
/// can be checked by its exported CSV rather than by watching the window:
/// <code>--source DIR --target DIR [--source-alias A] [--target-alias B] [--export DIR] [--run] [--show-graphs]</code>
/// Unknown arguments are ignored rather than fatal; a GUI should still open. <c>--show-graphs</c>
/// switches to the Graphs tab once a scripted run has finished, so a capture shows a curve.
/// </summary>
public sealed record LaunchOptions(
    string? SourceFolder,
    string? TargetFolder,
    string? SourceAlias,
    string? TargetAlias,
    string? ExportFolder,
    bool AutoRun,
    bool ShowGraphs = false)
{
    public static LaunchOptions None { get; } = new(null, null, null, null, null, false);

    /// <summary>Both folders given and --run present: the window runs on open, then exports if asked.</summary>
    public bool CanAutoRun => AutoRun && SourceFolder is not null && TargetFolder is not null;

    public static LaunchOptions FromCommandLine() => Parse(Environment.GetCommandLineArgs().Skip(1));

    public static LaunchOptions Parse(IEnumerable<string> args)
    {
        string? source = null, target = null, sourceAlias = null, targetAlias = null, export = null;
        bool run = false, showGraphs = false;
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            string a = list[i];
            string? Next() => i + 1 < list.Count ? list[++i] : null;
            switch (a.ToLowerInvariant())
            {
                case "--source": source = Next(); break;
                case "--target": target = Next(); break;
                case "--source-alias": sourceAlias = Next(); break;
                case "--target-alias": targetAlias = Next(); break;
                case "--export": export = Next(); break;
                case "--run": run = true; break;
                case "--show-graphs": showGraphs = true; break;
            }
        }
        return new LaunchOptions(source, target, sourceAlias, targetAlias, export, run, showGraphs);
    }
}
