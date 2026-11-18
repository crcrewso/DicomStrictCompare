using System;
using System.Collections.Generic;
using System.Threading;

namespace DSClibrary.Comparing
{
    /// <summary>
    /// Grid arrays kept for reuse, by exact length. The cap covers arrays lent out as well as those kept, so a batch of
    /// mixed grid sizes can't hold idle arrays on top of the ones in use; the oldest kept go first. A batch's grids are
    /// large-object-heap arrays the GC reclaims lazily, so many pairs at once used to overrun memory (WC2, crane
    /// 2026-11-15); reusing them keeps the garbage from building up. Thread-safe.
    /// </summary>
    public sealed class GridPool
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, Stack<LinkedListNode<double[]>>> _byLength = new();
        private readonly LinkedList<double[]> _age = new();
        private long _bytes, _lent, _hits, _misses;

        public GridPool(long maxBytes) => MaxBytes = Math.Max(0, maxBytes);

        public long MaxBytes { get; }
        /// <summary>Bytes kept for reuse, and bytes lent out and not yet returned.</summary>
        public long Bytes { get { lock (_gate) return _bytes; } }
        public long Lent { get { lock (_gate) return _lent; } }
        /// <summary>Rents served from the pool, and rents that had to allocate.</summary>
        public long Hits => Interlocked.Read(ref _hits);
        public long Misses => Interlocked.Read(ref _misses);

        /// <summary>An array of exactly <paramref name="length"/>; its contents are whatever the last user left.</summary>
        public double[] Rent(int length)
        {
            long size = (long)length * sizeof(double);
            lock (_gate)
            {
                _lent += size;
                if (_byLength.TryGetValue(length, out var stack) && stack.Count > 0)
                {
                    var node = stack.Pop();
                    _age.Remove(node);
                    _bytes -= size;
                    Interlocked.Increment(ref _hits);
                    return node.Value;
                }
                // A new array: let go of kept ones of other lengths first, so kept and lent stay within the cap.
                EvictUntil(MaxBytes);
            }
            Interlocked.Increment(ref _misses);
            return GC.AllocateUninitializedArray<double>(length);
        }

        /// <summary>Hands an array back. The caller must hold no other reference to it.</summary>
        public void Return(double[]? array)
        {
            if (array is null || array.Length == 0) return;
            long size = (long)array.Length * sizeof(double);
            lock (_gate)
            {
                _lent = Math.Max(0, _lent - size);
                if (size > MaxBytes) return;
                EvictUntil(MaxBytes - size);
                var node = _age.AddLast(array);
                if (!_byLength.TryGetValue(array.Length, out var s)) _byLength[array.Length] = s = new();
                s.Push(node);
                _bytes += size;
            }
        }

        // Drops the oldest kept arrays until kept plus lent is at most limit, or nothing is kept. Caller holds _gate.
        private void EvictUntil(long limit)
        {
            while (_bytes + _lent > limit && _age.First is { } oldest)
            {
                _age.RemoveFirst();
                var stack = _byLength[oldest.Value.Length];
                // The oldest of a length is at the bottom of its stack.
                var kept = new Stack<LinkedListNode<double[]>>();
                while (stack.Count > 0) { var n = stack.Pop(); if (n != oldest) kept.Push(n); }
                while (kept.Count > 0) stack.Push(kept.Pop());
                _bytes -= (long)oldest.Value.Length * sizeof(double);
            }
        }
    }
}
