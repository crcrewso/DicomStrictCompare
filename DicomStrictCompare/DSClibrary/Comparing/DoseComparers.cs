namespace DSClibrary.Comparing
{
    public enum ComparerKind { HandWritten, SingleThread, MultiThread, Ilgpu, OpenCl }

    /// <summary>
    /// GPU arithmetic. Float is the default (Cody, 2026-11-15) so GPUs with weak double rates are not crushed: the
    /// stored data is coarser than float's rounding, and cells and weights are chosen in double on the host either
    /// way. Double reproduces <see cref="HandWrittenComparer"/> exactly; float may differ on points within rounding
    /// of a tolerance.
    /// </summary>
    public enum GpuPrecision { Float, Double }

    /// <summary>
    /// Which comparer to build. Front ends keep this as a setting and pass it to <see cref="DoseComparers.Create"/>,
    /// so they hold only a <see cref="DoseComparer"/>. Threads applies to MultiThread; Precision and Device to the
    /// GPU kinds (Device: part of the device name, or null for the first GPU); UseThresholdBox to all but HandWritten.
    /// </summary>
    public sealed record ComparerSettings(
        ComparerKind Kind = ComparerKind.HandWritten,
        int? Threads = null,
        GpuPrecision Precision = GpuPrecision.Float,
        bool UseThresholdBox = true,
        string? Device = null);

    /// <summary>One kind of comparer, whether this machine can run it, and on what.</summary>
    public sealed record ComparerAvailability(ComparerKind Kind, bool Available, string Detail);

    /// <summary>Thrown when the chosen comparer cannot run here. There is no silent fallback to another one.</summary>
    public sealed class ComparerUnavailableException : Exception
    {
        public ComparerUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>Builds comparers from settings, and says which ones this machine can run.</summary>
    public static class DoseComparers
    {
        public static DoseComparer Create(ComparerSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            return settings.Kind switch
            {
                ComparerKind.HandWritten => new HandWrittenComparer(),
                ComparerKind.SingleThread => new SingleThreadComparer { UseThresholdBox = settings.UseThresholdBox },
                ComparerKind.MultiThread => new MultiThreadComparer
                {
                    Threads = settings.Threads ?? Environment.ProcessorCount,
                    UseThresholdBox = settings.UseThresholdBox,
                },
                ComparerKind.Ilgpu => Gpu.IlgpuComparer.Create(settings),
                ComparerKind.OpenCl => Gpu.OpenClComparer.Create(settings),
                _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Kind, "Unknown comparer kind."),
            };
        }

        /// <summary>Every kind, with whether it can run here; the GPU kinds are tried, so this can take a moment.</summary>
        public static IReadOnlyList<ComparerAvailability> Available()
        {
            var list = new List<ComparerAvailability>
            {
                new(ComparerKind.HandWritten, true, "one thread, every point"),
                new(ComparerKind.SingleThread, true, "one thread, threshold box"),
                new(ComparerKind.MultiThread, true, $"{Environment.ProcessorCount} threads, threshold box"),
            };
            foreach (var kind in new[] { ComparerKind.Ilgpu, ComparerKind.OpenCl })
            {
                try
                {
                    var c = Create(new ComparerSettings(kind));
                    list.Add(new(kind, true, c.Description));
                    (c as IDisposable)?.Dispose();
                }
                catch (ComparerUnavailableException ex) { list.Add(new(kind, false, ex.Message)); }
            }
            return list;
        }
    }
}
