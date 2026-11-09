using System;

namespace DSClibrary.Gamma
{
    /// <summary>
    /// The parameters of a gamma comparison, named as pymedphys names them so that a result here can
    /// be checked against one there.
    /// </summary>
    /// <param name="DosePercentThreshold">The dose criterion, in percent of the normalisation (global) or of the reference point (local).</param>
    /// <param name="DistanceThreshold">The distance criterion, in the units of the grid axes (mm for RT Dose).</param>
    /// <param name="LowerPercentDoseCutoff">Reference points below this percent of the normalisation are not analysed. Applied to the reference only.</param>
    /// <param name="InterpFraction">The distance criterion divided by this is the search step; 10 as recommended by Wendling et al. (2007).</param>
    /// <param name="MaxGamma">
    /// The largest gamma reported; the search stops once further shells could only give larger
    /// values. Must be greater than 1, so it never changes whether a point passes. Infinity, the
    /// default, searches until a value is found, which on a large 3D grid can be very slow for points
    /// that fail badly: pass 1.1 to 2 when only pass or fail is wanted.
    /// </param>
    /// <param name="LocalGamma">Normalise the dose difference to the reference point's own dose rather than the global normalisation.</param>
    /// <param name="GlobalNormalisation">The dose the percent inputs refer to; the maximum reference dose when null.</param>
    /// <param name="SkipOncePassed">Stop searching a point as soon as its gamma is below 1; its value is then an upper bound.</param>
    /// <param name="RandomSubset">Analyse only this many of the eligible reference points, chosen at random.</param>
    /// <param name="RandomSeed">The seed for <paramref name="RandomSubset"/>, so a subset can be repeated. The sequence is .NET's, not NumPy's.</param>
    /// <param name="ExcludeNanReference">Leave NaN reference points out of the analysis instead of refusing the grid.</param>
    /// <param name="DegreeOfParallelism">Threads for the per-point search; the processor count when null.</param>
    public sealed record GammaOptions(
        double DosePercentThreshold,
        double DistanceThreshold,
        double LowerPercentDoseCutoff = 20,
        double InterpFraction = 10,
        double MaxGamma = double.PositiveInfinity,
        bool LocalGamma = false,
        double? GlobalNormalisation = null,
        bool SkipOncePassed = false,
        int? RandomSubset = null,
        int? RandomSeed = null,
        bool ExcludeNanReference = false,
        int? DegreeOfParallelism = null)
    {
        public void Validate()
        {
            if (!(DosePercentThreshold > 0)) throw new ArgumentOutOfRangeException(nameof(DosePercentThreshold), "must be positive");
            if (!(DistanceThreshold > 0)) throw new ArgumentOutOfRangeException(nameof(DistanceThreshold), "must be positive");
            if (LowerPercentDoseCutoff < 0) throw new ArgumentOutOfRangeException(nameof(LowerPercentDoseCutoff));
            if (!(InterpFraction > 0)) throw new ArgumentOutOfRangeException(nameof(InterpFraction));
            if (double.IsNaN(MaxGamma) || MaxGamma <= 1)
                throw new ArgumentOutOfRangeException(nameof(MaxGamma),
                    "max gamma must be greater than 1: values above it are reported as max gamma, so a smaller cap would report failing points as passing");
            if (GlobalNormalisation is { } g && !(double.IsFinite(g) && g > 0))
                throw new ArgumentOutOfRangeException(nameof(GlobalNormalisation), "must be finite and positive");
            if (RandomSubset is { } n && n < 0) throw new ArgumentOutOfRangeException(nameof(RandomSubset));
            if (DegreeOfParallelism is { } p && p < 1) throw new ArgumentOutOfRangeException(nameof(DegreeOfParallelism));
        }

        public override string ToString() =>
            $"{DosePercentThreshold:0.##}% / {DistanceThreshold:0.##} mm, {(LocalGamma ? "local" : "global")}, "
            + $"cutoff {LowerPercentDoseCutoff:0.#}%, step 1/{InterpFraction:0.#}"
            + (double.IsPositiveInfinity(MaxGamma) ? "" : $", max gamma {MaxGamma:0.##}")
            + (SkipOncePassed ? ", skip once passed" : "");
    }

    /// <summary>What a gamma comparison produced.</summary>
    public sealed class GammaResult
    {
        /// <summary>
        /// One gamma per reference point, in the reference grid's C order. NaN marks the points that
        /// were not analysed (below the cutoff, outside the random subset, or NaN reference dose);
        /// every analysed point has a finite value, capped at <see cref="GammaOptions.MaxGamma"/>.
        /// </summary>
        public double[] Gamma { get; }
        public int[] Shape { get; }
        public GammaOptions Options { get; }

        /// <summary>The dose the percent criteria were taken from.</summary>
        public double GlobalNormalisation { get; }
        public double LowerDoseCutoff { get; }

        /// <summary>Analysed reference points that lie more than one search step outside the evaluation grid; they are compared with the grid's edge and may fail for that reason.</summary>
        public int OutsideEvaluationGrid { get; }

        public int Analysed { get; }
        public int Passed { get; }

        /// <summary>Percent of analysed points with gamma at most 1.</summary>
        public double PassRate => Analysed == 0 ? double.NaN : 100.0 * Passed / Analysed;

        /// <summary>The largest gamma found, or NaN when nothing was analysed.</summary>
        public double MaxGammaFound { get; }

        internal GammaResult(double[] gamma, int[] shape, GammaOptions options, double normalisation,
            double lowerDoseCutoff, int outsideEvaluationGrid)
        {
            Gamma = gamma;
            Shape = shape;
            Options = options;
            GlobalNormalisation = normalisation;
            LowerDoseCutoff = lowerDoseCutoff;
            OutsideEvaluationGrid = outsideEvaluationGrid;
            int analysed = 0, passed = 0;
            double max = double.NaN;
            foreach (double g in gamma)
            {
                if (double.IsNaN(g)) continue;
                analysed++;
                if (g <= 1) passed++;
                if (double.IsNaN(max) || g > max) max = g;
            }
            Analysed = analysed;
            Passed = passed;
            MaxGammaFound = max;
        }

        public override string ToString() =>
            $"{Options}: {Passed} of {Analysed} analysed points pass ({PassRate:0.0}%), max gamma {MaxGammaFound:0.00}";
    }
}
