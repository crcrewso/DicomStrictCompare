// Ported from pymedphys (lib/pymedphys/_gamma/implementation/shell.py and _gamma/utilities/core.py),
// Copyright (C) 2022-2026 Matthew Jennings, Copyright (C) 2015-2018 Simon Biggs, Apache License 2.0 -
// see THIRD-PARTY-NOTICES.md at the repository root. Ported to C# for DicomStrictCompare by crane-dsc
// on 2026-11-09 at Cody Crewson's request.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DSClibrary.Gamma
{
    /// <summary>
    /// The gamma index of a reference dose grid against an evaluation dose grid, in one, two or three
    /// dimensions, with the evaluation interpolated on the fly: a port of pymedphys' <c>gamma</c>
    /// (the "shell" implementation of Wendling et al. 2007, <see href="http://dx.doi.org/10.1118/1.2721657"/>).
    /// <para>
    /// Each reference point at or above the lower dose cutoff is the centre of a search. Shells of
    /// increasing radius are laid round it, the evaluation dose is interpolated at every shell point,
    /// and the smallest dose difference on a shell gives the gamma at that radius. The search for a
    /// point stops once the radius alone, over the distance criterion, exceeds the best gamma so far,
    /// since no farther shell can improve it. Points outside the evaluation grid are first bounded by
    /// the nearest point of the grid's extent, so every analysed point gets a value.
    /// </para>
    /// <para>
    /// Where this differs from pymedphys: one dose and one distance criterion per call; the search is
    /// per reference point in parallel rather than vectorised over all points per shell (the shells
    /// and step sizes are the same, so the values are the same); evaluation axes must be evenly
    /// spaced (pymedphys falls back to SciPy for uneven ones); and the random subset uses .NET's
    /// generator, so a seeded subset is repeatable here but not the same subset pymedphys would pick.
    /// </para>
    /// </summary>
    public static class GammaShell
    {
        /// <summary>Gamma of one dose grid read by the library against another, over x, y and z.</summary>
        public static GammaResult Compare(DoseMatrixOptimal reference, DoseMatrixOptimal evaluation,
            GammaOptions options, CancellationToken cancellationToken = default) =>
            Compute(GammaGrid.FromDose(reference), GammaGrid.FromDose(evaluation), options, cancellationToken);

        /// <summary>
        /// Gamma of every analysed reference point. The result has the reference grid's shape and
        /// order; NaN marks points that were not analysed.
        /// </summary>
        public static GammaResult Compute(GammaGrid reference, GammaGrid evaluation, GammaOptions options,
            CancellationToken cancellationToken = default)
        {
            if (reference is null) throw new ArgumentNullException(nameof(reference));
            if (evaluation is null) throw new ArgumentNullException(nameof(evaluation));
            if (options is null) throw new ArgumentNullException(nameof(options));
            options.Validate();
            if (reference.Dimensions != evaluation.Dimensions)
                throw new ArgumentException(
                    $"The reference has {reference.Dimensions} dimension(s) and the evaluation {evaluation.Dimensions}; they must match");

            int dims = reference.Dimensions;
            double[] refDose = reference.Values;
            CheckFiniteDoses(refDose, evaluation.Values, options.ExcludeNanReference);

            // The evaluation grid as the interpolator needs it: ascending axes, dose flipped to match.
            var (evalAxes, evalDose) = PrepareEvaluation(evaluation);

            double normalisation = options.GlobalNormalisation ?? NanMax(refDose);
            if (!(double.IsFinite(normalisation) && normalisation > 0))
                throw new ArgumentException("The global normalisation (by default the maximum reference dose) must be finite and positive");
            double lowerDoseCutoff = options.LowerPercentDoseCutoff / 100 * normalisation;
            double doseFraction = options.DosePercentThreshold / 100;
            double distanceThreshold = options.DistanceThreshold;

            // Axes with more than one evaluation value are searched over; a singleton axis fixes a
            // coordinate of a plane, line or point, and the reference's distance from it is added.
            int[] freeAxes = Enumerable.Range(0, dims).Where(d => evalAxes[d].Length > 1).ToArray();
            var (minimumTestDistance, spatialLimit) = GridDistanceBounds(
                freeAxes.Select(d => reference.Axes[d]).ToArray(), freeAxes.Select(d => evalAxes[d]).ToArray());
            double maximumTestDistance = Math.Min(distanceThreshold * options.MaxGamma, spatialLimit);

            int[] refStrides = Strides(reference.Shape);
            int n = refDose.Length;

            // Which reference points are analysed.
            var toCalc = new bool[n];
            for (int i = 0; i < n; i++) toCalc[i] = refDose[i] >= lowerDoseCutoff;
            if (options.RandomSubset is { } subset)
            {
                int[] eligible = Enumerable.Range(0, n).Where(i => toCalc[i]).ToArray();
                var random = options.RandomSeed is { } seed ? new Random(seed) : new Random();
                for (int i = eligible.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
                }
                Array.Clear(toCalc);
                foreach (int i in eligible.Take(subset)) toCalc[i] = true;
            }
            int[] analysed = Enumerable.Range(0, n).Where(i => toCalc[i]).ToArray();

            if (options.LocalGamma && analysed.Any(i => refDose[i] == 0))
                throw new ArgumentException(
                    "Local gamma is undefined where the reference dose is zero, and some analysed reference points have zero dose. Raise the lower dose cutoff above zero to exclude them.");

            // Coordinates of a reference point on every axis, and on the free axes only.
            double RefCoordinate(int index, int axis) => reference.Axes[axis][(index / refStrides[axis]) % reference.Shape[axis]];

            double[]? perpendicular = null;
            if (freeAxes.Length < dims)
            {
                perpendicular = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double sum = 0;
                    for (int d = 0; d < dims; d++)
                    {
                        if (Array.IndexOf(freeAxes, d) >= 0) continue;
                        double delta = RefCoordinate(i, d) - evalAxes[d][0];
                        sum += delta * delta;
                    }
                    perpendicular[i] = Math.Sqrt(sum);
                }
            }

            double DistanceOutsideGrid(int index)
            {
                double sum = 0;
                for (int d = 0; d < dims; d++)
                {
                    double c = RefCoordinate(index, d);
                    double clipped = Math.Clamp(c, evalAxes[d][0], evalAxes[d][^1]);
                    sum += (c - clipped) * (c - clipped);
                }
                return Math.Sqrt(sum);
            }

            double searchStep = distanceThreshold / options.InterpFraction;
            int outsideGrid = analysed.Count(i => DistanceOutsideGrid(i) > searchStep);

            RegularGridInterpolator? interpolator = freeAxes.Length == 0
                ? null
                : new RegularGridInterpolator(freeAxes.Select(d => evalAxes[d]).ToArray(), evalDose, double.PositiveInfinity);
            double singleEvaluationValue = freeAxes.Length == 0 ? evalDose[0] : double.NaN;

            var gamma = new double[n];
            Array.Fill(gamma, double.PositiveInfinity);

            // Bound every analysed point outside the grid by the grid's nearest point, so a grid
            // narrower than a step, a plane, or a far-off grid still gives each point a value.
            Parallel.ForEach(analysed, new ParallelOptions { MaxDegreeOfParallelism = options.DegreeOfParallelism ?? Environment.ProcessorCount, CancellationToken = cancellationToken }, i =>
            {
                double distance = DistanceOutsideGrid(i);
                if (distance <= 0 && freeAxes.Length > 0) return;
                double evaluationDose;
                if (interpolator is null)
                {
                    evaluationDose = singleEvaluationValue;
                }
                else
                {
                    Span<double> nearest = stackalloc double[freeAxes.Length];
                    for (int k = 0; k < freeAxes.Length; k++)
                    {
                        int d = freeAxes[k];
                        nearest[k] = Math.Clamp(RefCoordinate(i, d), evalAxes[d][0], evalAxes[d][^1]);
                    }
                    evaluationDose = interpolator.At(nearest);
                }
                double norm = options.LocalGamma ? refDose[i] : normalisation;
                double relative = Math.Abs((evaluationDose - refDose[i]) / norm);
                double edge = Math.Sqrt(Square(relative / doseFraction) + Square(distance / distanceThreshold));
                gamma[i] = Math.Min(gamma[i], edge);
            });

            if (interpolator is not null)
            {
                var schedule = SearchSchedule(minimumTestDistance, maximumTestDistance, distanceThreshold, options);
                var shells = new ConcurrentDictionary<int, double[][]>();
                double[][] ShellAt(int k) => shells.GetOrAdd(k,
                    index => ShellCoordinates.Calculate(schedule[index].Distance, freeAxes.Length, schedule[index].Step));

                Parallel.ForEach(analysed, new ParallelOptions { MaxDegreeOfParallelism = options.DegreeOfParallelism ?? Environment.ProcessorCount, CancellationToken = cancellationToken }, i =>
                {
                    double reference_i = refDose[i];
                    double norm = options.LocalGamma ? reference_i : normalisation;
                    double perp = perpendicular?[i] ?? 0;
                    double best = gamma[i];
                    Span<double> centre = stackalloc double[freeAxes.Length];
                    Span<double> point = stackalloc double[freeAxes.Length];
                    for (int k = 0; k < freeAxes.Length; k++) centre[k] = RefCoordinate(i, freeAxes[k]);

                    for (int s = 0; s < schedule.Count; s++)
                    {
                        if ((s & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                        double[][] shell = ShellAt(s);
                        double minimumDifference = double.PositiveInfinity;
                        int points = shell[0].Length;
                        for (int p = 0; p < points; p++)
                        {
                            for (int k = 0; k < freeAxes.Length; k++) point[k] = centre[k] + shell[k][p];
                            double difference = Math.Abs((interpolator.At(point) - reference_i) / norm);
                            if (difference < minimumDifference) minimumDifference = difference;
                        }

                        double total = perp == 0 ? schedule[s].Distance : Hypot(schedule[s].Distance, perp);
                        double atDistance = Math.Sqrt(Square(minimumDifference / doseFraction) + Square(total / distanceThreshold));
                        if (atDistance < best) best = atDistance;

                        bool stillSearching = best > total / distanceThreshold;
                        if (options.SkipOncePassed) stillSearching &= best >= 1;
                        if (!stillSearching) break;
                    }
                    gamma[i] = best;
                });
            }

            var output = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (!toCalc[i]) { output[i] = double.NaN; continue; }
                double g = gamma[i];
                if (!double.IsFinite(g))
                    throw new InvalidOperationException($"Gamma was not found for analysed reference point {i}");
                output[i] = g > options.MaxGamma ? options.MaxGamma : g;
            }

            return new GammaResult(output, (int[])reference.Shape.Clone(), options, normalisation, lowerDoseCutoff, outsideGrid);
        }

        /// <summary>
        /// The search radii and the shell spacing used at each, as pymedphys steps them: from the
        /// grids' nearest approach to the maximum test distance, by the criterion over the
        /// interpolation fraction, growing with the radius once a maximum gamma is set, and always
        /// landing exactly on the distance criterion and on the end point.
        /// </summary>
        internal static List<(double Distance, double Step)> SearchSchedule(double minimum, double maximum,
            double distanceThreshold, GammaOptions options)
        {
            var schedule = new List<(double, double)>();
            double baseStep = distanceThreshold / options.InterpFraction;
            double step = baseStep;
            double distance = minimum;
            var forced = new Queue<double>(new[] { distanceThreshold, maximum }.Distinct().Where(d => d > distance).OrderBy(d => d));
            while (distance <= maximum)
            {
                schedule.Add((distance, step));
                step = Math.Max(distance / options.InterpFraction / options.MaxGamma, baseStep);
                distance += step;
                if (forced.Count > 0 && distance >= forced.Peek()) distance = forced.Dequeue();
            }
            return schedule;
        }

        /// <summary>
        /// The evaluation axes made ascending, each with the dose flipped to match, and checked to be
        /// strictly monotonic; a single-valued axis is left as it is.
        /// </summary>
        internal static (double[][] Axes, double[] Values) PrepareEvaluation(GammaGrid evaluation)
        {
            var axes = evaluation.Axes.Select(a => (double[])a.Clone()).ToArray();
            double[] values = evaluation.Values;
            for (int d = 0; d < axes.Length; d++)
            {
                double[] axis = axes[d];
                if (axis.Length == 1) continue;
                bool descending = true, ascending = true;
                for (int i = 1; i < axis.Length; i++)
                {
                    double diff = axis[i] - axis[i - 1];
                    if (!(diff < 0)) descending = false;
                    if (!(diff > 0)) ascending = false;
                }
                if (descending)
                {
                    Array.Reverse(axis);
                    values = FlipAlong(values, evaluation.Shape, d);
                }
                else if (!ascending)
                {
                    throw new ArgumentException($"Evaluation axis {d} must be strictly ascending or strictly descending");
                }
            }
            return (axes, values);
        }

        /// <summary>A C-order array with one dimension reversed.</summary>
        internal static double[] FlipAlong(double[] values, int[] shape, int dimension)
        {
            int[] strides = Strides(shape);
            var flipped = new double[values.Length];
            int size = shape[dimension], stride = strides[dimension];
            for (int i = 0; i < values.Length; i++)
            {
                int along = (i / stride) % size;
                int target = i + (size - 1 - 2 * along) * stride;
                flipped[target] = values[i];
            }
            return flipped;
        }

        internal static int[] Strides(int[] shape)
        {
            var strides = new int[shape.Length];
            int stride = 1;
            for (int d = shape.Length - 1; d >= 0; d--)
            {
                strides[d] = stride;
                stride *= shape[d];
            }
            return strides;
        }

        /// <summary>
        /// The nearest and farthest a reference point can be from the evaluation grid's extent, over the
        /// given axes. Shells below the first or above the second cannot meet the grid.
        /// </summary>
        internal static (double Nearest, double Farthest) GridDistanceBounds(double[][] referenceAxes, double[][] evaluationAxes)
        {
            double nearest = 0, farthest = 0;
            for (int d = 0; d < referenceAxes.Length; d++)
            {
                double refMin = referenceAxes[d].Min(), refMax = referenceAxes[d].Max();
                double evalLow = evaluationAxes[d][0], evalHigh = evaluationAxes[d][^1];
                double near = Math.Max(Math.Max(evalLow - refMax, refMin - evalHigh), 0);
                double far = Math.Max(Math.Abs(refMin - evalHigh), Math.Abs(refMax - evalLow));
                nearest += near * near;
                farthest += far * far;
            }
            return (Math.Sqrt(nearest), Math.Sqrt(farthest));
        }

        private static void CheckFiniteDoses(double[] reference, double[] evaluation, bool excludeNanReference)
        {
            if (reference.Any(double.IsInfinity))
                throw new ArgumentException("The reference dose must be finite, but contains infinite values");
            bool anyNan = reference.Any(double.IsNaN);
            if (anyNan && !excludeNanReference)
                throw new ArgumentException(
                    "The reference dose contains NaN. To leave those points out of the analysis set ExcludeNanReference; they are then reported as NaN");
            if (reference.All(double.IsNaN))
                throw new ArgumentException("The reference dose has no finite values to analyse");
            if (!evaluation.All(double.IsFinite))
                throw new ArgumentException(
                    "The evaluation dose must be finite, but contains NaN or infinite values. Gamma searches the evaluation dose between grid points, so it cannot skip missing values; crop the grid to the region with valid dose");
        }

        private static double NanMax(double[] values)
        {
            double max = double.NaN;
            foreach (double v in values)
                if (!double.IsNaN(v) && (double.IsNaN(max) || v > max)) max = v;
            return max;
        }

        private static double Square(double x) => x * x;

        private static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
    }
}
