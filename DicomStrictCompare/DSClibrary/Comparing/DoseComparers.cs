namespace DSClibrary.Comparing
{
    public enum ComparerKind { HandWritten, SingleThread, MultiThread, Ilgpu, OpenCl }

    /// <summary>
    /// GPU arithmetic. <b>Double is the default</b> and reproduces <see cref="HandWrittenComparer"/> exactly. Float is
    /// for the GPU comparers only and only as a user's choice (crcrewso, 2026-11-15). It may differ from the reference
    /// on points within rounding of a tolerance or the threshold, a few in millions in the tests.
    /// <para>
    /// <b>Why offer float at all:</b> many consumer GPUs run double far slower than float. Roughly, from vendor
    /// specifications, so check your own card's:
    /// - NVIDIA GeForce RTX 20 series (Turing) at 1/32 of the float rate;
    /// - RTX 30 and 40 series (Ampere, Ada) at 1/64;
    /// - AMD Radeon RDNA cards at about 1/16 to 1/32;
    /// - Intel Arc A-series has no double-precision hardware at all.
    /// Data-centre cards (for example NVIDIA A100, H100) run double at half the float rate.
    /// </para>
    /// <para>
    /// <b>Measured here, 2026-11-14 to 15:</b> the kernel alone ran about 5-10x slower in double on an Intel Arc B390
    /// and 6-9x on an AMD RX 9070 XT. End to end it was only about 2x, because uploading the grids dominated.
    /// </para>
    /// </summary>
    public enum GpuPrecision { Double, Float }

    /// <summary>
    /// Which comparer to build. Front ends keep this as a setting and pass it to <see cref="DoseComparers.Create"/>,
    /// so they hold only a <see cref="DoseComparer"/>. Threads (null: <see cref="CpuTopology.PhysicalCores"/>) is the
    /// CPU thread budget: MultiThread's threads, and <see cref="BatchComparer.Threads"/>; Precision and Device to the
    /// GPU kinds (Device: part of the device name, or null for the first GPU); UseThresholdBox to all but HandWritten.
    /// </summary>
    public sealed record ComparerSettings(
        ComparerKind Kind = ComparerKind.HandWritten,
        int? Threads = null,
        GpuPrecision Precision = GpuPrecision.Double,
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
                    Threads = settings.Threads ?? CpuTopology.PhysicalCores,
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
                new(ComparerKind.MultiThread, true, $"{CpuTopology.PhysicalCores} threads, one per physical core{(CpuTopology.PinByDefault ? ", pinned" : "")}, threshold box"),
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
