using System.Diagnostics;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>
/// Puts rows reported during a run into a grid's collection in batches, at most every <see cref="Interval"/>, so a long
/// run shows results as it goes without adding thousands of rows one at a time (D30). Rows still waiting are added
/// once the interval passes, even if no more arrive. Everything touching the collection runs through
/// <c>post</c>: the UI thread's synchronization context by default, or a front end's own dispatcher (Eto).
/// </summary>
public sealed class RowBatcher<T>
{
    private readonly ICollection<T> _target;
    private readonly Action<Action> _post;
    private readonly List<T> _pending = new();
    private readonly Stopwatch _sinceFlush = Stopwatch.StartNew();
    private bool _flushQueued;
    private bool _stopped;

    /// <param name="post">Runs an action on the UI thread. Null uses the creating thread's synchronization
    /// context, or runs inline when there is none.</param>
    public RowBatcher(ICollection<T> target, TimeSpan? interval = null, Action<Action>? post = null)
    {
        _target = target;
        Interval = interval ?? TimeSpan.FromMilliseconds(250);
        var context = SynchronizationContext.Current;
        _post = post ?? (context is null ? a => a() : a => context.Post(_ => a(), null));
        Progress = new Reporter(this);
    }

    public TimeSpan Interval { get; }

    /// <summary>Pass this to the runner; it may report from any thread.</summary>
    public IProgress<IReadOnlyList<T>> Progress { get; }

    /// <summary>Rows reported so far, shown or waiting.</summary>
    public int Received { get; private set; }

    /// <summary>Called after each batch is added, with <see cref="Received"/>.</summary>
    public Action<int>? Flushed { get; init; }

    /// <summary>Takes one report. Call it on the UI thread; <see cref="Progress"/> does.</summary>
    public void Add(IReadOnlyList<T> rows)
    {
        if (_stopped) return;
        _pending.AddRange(rows);
        Received += rows.Count;
        var wait = Interval - _sinceFlush.Elapsed;
        if (wait <= TimeSpan.Zero) Flush();
        else if (!_flushQueued)
        {
            _flushQueued = true;
            _ = Task.Delay(wait).ContinueWith(_ => _post(() => { _flushQueued = false; if (!_stopped) Flush(); }), TaskScheduler.Default);
        }
    }

    /// <summary>Ignores anything reported from now on, so the run's final rows can replace the grid's. UI thread.</summary>
    public void Stop() { _stopped = true; _pending.Clear(); }

    private void Flush()
    {
        foreach (var r in _pending) _target.Add(r);
        _pending.Clear();
        _sinceFlush.Restart();
        Flushed?.Invoke(Received);
    }

    private sealed class Reporter(RowBatcher<T> owner) : IProgress<IReadOnlyList<T>>
    {
        public void Report(IReadOnlyList<T> value) => owner._post(() => owner.Add(value));
    }
}
