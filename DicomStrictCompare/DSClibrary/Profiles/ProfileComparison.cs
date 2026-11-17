using System;
using System.Collections.Generic;
using System.Linq;
using DSClibrary.Gamma;
using DSClibrary.Units;

namespace DSClibrary.Profiles
{
    /// <summary>The point test of a profile comparison.</summary>
    public enum ProfileTest
    {
        /// <summary>Gamma, by <see cref="GammaShell"/> (the pymedphys port) in one dimension.</summary>
        Gamma,
        /// <summary>
        /// <c>HandWrittenComparer</c>'s rules along the scan: a point passes when the dose difference is within the
        /// tolerance, or else when the evaluated dose lies between the reference's doses at plus and minus the distance.
        /// A distance of 0 makes it a dose difference alone.
        /// </summary>
        Strict
    }

    /// <summary>Which curve is the reference: the one whose points are tested, and whose maximum the threshold and a
    /// global tolerance are fractions of.</summary>
    public enum ProfileReference
    {
        /// <summary>The measurement is the reference, the usual choice for a measurement against a calculation.</summary>
        Measured,
        /// <summary>The calculation is the reference.</summary>
        Calculated
    }

    /// <summary>
    /// A search for the depth offset between a measured and a calculated depth dose: the calculation is read at
    /// depth + s for each s from -<see cref="RangeMm"/> to +<see cref="RangeMm"/> in <see cref="StepMm"/> steps, and
    /// the s that best fits the measurement beyond the maximum is kept. Opt-in, because a shift hides a setup error as
    /// readily as it corrects a detector's effective point of measurement.
    /// </summary>
    public sealed record DepthShiftSearch(double RangeMm = 3, double StepMm = 0.1)
    {
        public void Validate()
        {
            if (!(RangeMm >= 0) || !double.IsFinite(RangeMm)) throw new ArgumentOutOfRangeException(nameof(RangeMm));
            if (!(StepMm > 0) || !double.IsFinite(StepMm)) throw new ArgumentOutOfRangeException(nameof(StepMm));
        }
    }

    /// <summary>The settings of a profile comparison. Percentages are of the reference curve's maximum after normalisation.</summary>
    /// <param name="DosePercent">The dose criterion, in %.</param>
    /// <param name="DistanceMm">The distance criterion, in mm. For <see cref="ProfileTest.Strict"/>, 0 means dose difference only.</param>
    /// <param name="Test">Gamma or the strict rule.</param>
    /// <param name="Reference">Which side is the reference.</param>
    /// <param name="ProfileCutoffPercent">Reference points of a transverse profile below this % of the reference maximum aren't tested.</param>
    /// <param name="DepthDoseCutoffPercent">The same for a depth dose.</param>
    /// <param name="Local">Dose differences in % of the reference point's own dose, not of the maximum.</param>
    /// <param name="MaxGamma">Gamma's search stops past this value (pymedphys' max_gamma); must be above 1.</param>
    /// <param name="InterpFraction">Gamma's search step is the distance over this (pymedphys' interp_fraction).</param>
    /// <param name="DepthShift">The depth-dose shift search, or null for none.</param>
    /// <param name="CentralAxisFallbackFraction">A profile is normalised at its central axis unless either side's value
    /// there is below this fraction of its maximum (the field is off the axis), when both are normalised to their maximum.
    /// 0.8 follows design/comparison-studies.md, F10.</param>
    /// <param name="ConstancyRegionFraction">The central fraction of the reference field over which constancy is averaged.</param>
    public sealed record ProfileComparisonOptions(
        double DosePercent,
        double DistanceMm,
        ProfileTest Test = ProfileTest.Gamma,
        ProfileReference Reference = ProfileReference.Measured,
        double ProfileCutoffPercent = 10,
        double DepthDoseCutoffPercent = 10,
        bool Local = false,
        double MaxGamma = double.PositiveInfinity,
        double InterpFraction = 10,
        DepthShiftSearch? DepthShift = null,
        double CentralAxisFallbackFraction = 0.8,
        double ConstancyRegionFraction = 0.8)
    {
        public void Validate()
        {
            if (!(DosePercent > 0) || !double.IsFinite(DosePercent)) throw new ArgumentOutOfRangeException(nameof(DosePercent), "must be positive");
            if (!(DistanceMm >= 0) || !double.IsFinite(DistanceMm)) throw new ArgumentOutOfRangeException(nameof(DistanceMm), "must be 0 or more");
            if (Test == ProfileTest.Gamma && !(DistanceMm > 0)) throw new ArgumentOutOfRangeException(nameof(DistanceMm), "gamma needs a positive distance");
            if (!(MaxGamma > 1)) throw new ArgumentOutOfRangeException(nameof(MaxGamma), "must be above 1");
            if (!(ProfileCutoffPercent >= 0 && ProfileCutoffPercent < 100)) throw new ArgumentOutOfRangeException(nameof(ProfileCutoffPercent));
            if (!(DepthDoseCutoffPercent >= 0 && DepthDoseCutoffPercent < 100)) throw new ArgumentOutOfRangeException(nameof(DepthDoseCutoffPercent));
            if (!(CentralAxisFallbackFraction >= 0 && CentralAxisFallbackFraction < 1)) throw new ArgumentOutOfRangeException(nameof(CentralAxisFallbackFraction));
            if (!(ConstancyRegionFraction > 0 && ConstancyRegionFraction <= 1)) throw new ArgumentOutOfRangeException(nameof(ConstancyRegionFraction));
            DepthShift?.Validate();
        }

        /// <summary>The criterion as text: "2%/2 mm gamma, measured reference, 10% cutoff".</summary>
        public override string ToString() =>
            FormattableString.Invariant($"{DosePercent:0.###}%/{DistanceMm:0.###} mm {(Test == ProfileTest.Gamma ? "gamma" : "strict")}{(Local ? " local" : "")}, ")
            + FormattableString.Invariant($"{Reference.ToString().ToLowerInvariant()} reference, cutoff {ProfileCutoffPercent:0.#}% profile / {DepthDoseCutoffPercent:0.#}% depth dose")
            + (DepthShift is { } s ? FormattableString.Invariant($", depth shift search +-{s.RangeMm:0.##} mm by {s.StepMm:0.###}") : "");
    }

    /// <summary>What one profile comparison found. Curves are normalised; positions are the scan's own.</summary>
    public sealed class ProfileComparisonResult
    {
        public required ProfileComparisonOptions Options { get; init; }

        /// <summary>The measured curve as compared: the scan's points inside the dose grid, normalised.</summary>
        public required Profile Measured { get; init; }

        /// <summary>The calculation at the measured positions (after any depth shift), normalised.</summary>
        public required Profile Calculated { get; init; }

        /// <summary>The normalisation both sides got, and why when it isn't the usual one.</summary>
        public required NormalizationType Normalization { get; init; }
        public string? NormalizationNote { get; init; }

        /// <summary>Scan positions with no dose (outside the grid), left out.</summary>
        public required int PointsOutsideGrid { get; init; }

        /// <summary>The tested reference positions and each one's value: gamma, or for the strict rule 0 for a pass and 1
        /// for a failure. NaN where the point wasn't tested (below the cutoff, or out of reach).</summary>
        public required double[] PointPositionsMm { get; init; }
        public required double[] PointValues { get; init; }

        public required int Analysed { get; init; }
        public required int Passed { get; init; }
        public double PassRate => Analysed == 0 ? double.NaN : 100.0 * Passed / Analysed;

        /// <summary>The gamma result, for <see cref="ProfileTest.Gamma"/>.</summary>
        public GammaResult? Gamma { get; init; }
        public double? MeanGamma { get; init; }
        public double? MaxGammaFound { get; init; }

        /// <summary>The depth offset found by <see cref="ProfileComparisonOptions.DepthShift"/>: how far the measured
        /// depth dose sits deeper than the calculated one, in mm (negative: shallower). Null when not searched.</summary>
        public double? DepthShiftMm { get; init; }

        /// <summary>Mean |evaluated - reference| / reference in %, over the central part of the reference field
        /// (<see cref="ProfileComparisonOptions.ConstancyRegionFraction"/>); transverse profiles only.</summary>
        public double? ConstancyPercent { get; init; }

        /// <summary>FauxPID's profile metrics of each side (transverse profiles), from <see cref="ProfileMetrics"/>.</summary>
        public ProfileMetricsReport? MeasuredMetrics { get; init; }
        public ProfileMetricsReport? CalculatedMetrics { get; init; }

        /// <summary>Depth-dose metrics of each side (depth doses).</summary>
        public DepthDoseMetrics? MeasuredDepthDose { get; init; }
        public DepthDoseMetrics? CalculatedDepthDose { get; init; }

        /// <summary>Anything a reader of the number should know: metrics that couldn't be taken, fallbacks.</summary>
        public required IReadOnlyList<string> Notes { get; init; }

        public override string ToString() => FormattableString.Invariant(
            $"{Measured.Scan}: {PassRate:0.00}% of {Analysed} passed ({Options})");
    }

    /// <summary>
    /// Compares a measured profile with a calculated dose along the same line.
    /// <para>
    /// <b>Steps.</b> The dose is sampled at the scan's positions (<see cref="DoseLineSampler"/>); positions outside the
    /// grid are dropped from both sides. Both curves are normalised: a depth dose to its maximum, a transverse profile at
    /// its central axis, or to its maximum when the field is off the axis. A depth dose may then be shifted
    /// (<see cref="DepthShiftSearch"/>). The reference side's points are tested, by gamma or by the strict rule, and the
    /// constancy and metrics are taken.
    /// </para>
    /// <para>
    /// <b>The evaluated side.</b> When the measurement is the reference, the calculation is read at the grid's own nodes
    /// along the line, evenly spaced and exact, which is what <see cref="GammaShell"/> requires of its evaluation grid. When
    /// the calculation is the reference, the measurement is put on an even grid at its finest step by linear
    /// interpolation, the same interpolation the gamma search would make between its points.
    /// </para>
    /// </summary>
    public static class ProfileComparison
    {
        public static ProfileComparisonResult Compare(Profile measured, DoseMatrixOptimal grid, PhantomFrame frame,
            ProfileComparisonOptions options, string dosePath = "", bool gridInGray = true)
        {
            if (measured is null) throw new ArgumentNullException(nameof(measured));
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            if (frame is null) throw new ArgumentNullException(nameof(frame));
            if (options is null) throw new ArgumentNullException(nameof(options));
            options.Validate();
            if (measured.Normalization != NormalizationType.AsMeasured)
                throw new ArgumentException("Pass the measured profile as measured; the comparison normalises it.", nameof(measured));

            var notes = new List<string>();
            bool pdd = measured.Scan.CurveType == CurveType.Pdd;
            double unit = gridInGray ? 100.0 : 1.0;   // grid units to centigray

            // 1. The scan's points that have a dose; the rest are counted and left out.
            var sampled = DoseLineSampler.Sample(grid, frame, measured, dosePath, gridInGray);
            var inside = new HashSet<double>(sampled.Calculated.Points.Select(p => p.Position.Millimetres));
            var measuredInside = With(measured, measured.Points.Where(p => inside.Contains(p.Position.Millimetres)));
            if (measuredInside.Points.Count < 3)
                throw new InvalidOperationException($"Only {measuredInside.Points.Count} of {measured.Points.Count} scan points lie inside the dose grid.");

            // 2. A depth-dose shift, by reading the calculation deeper or shallower.
            double shift = 0;
            if (pdd && options.DepthShift is { } search)
                shift = FindDepthShift(measuredInside, grid, frame, search, options.DepthDoseCutoffPercent, notes);
            var calculatedRaw = shift == 0 ? sampled.Calculated : Shifted(sampled.Calculated, grid, frame, shift, unit, dosePath, notes);
            if (calculatedRaw.Points.Count != measuredInside.Points.Count)
            {
                var keep = new HashSet<double>(calculatedRaw.Points.Select(p => p.Position.Millimetres));
                measuredInside = With(measuredInside, measuredInside.Points.Where(p => keep.Contains(p.Position.Millimetres)));
            }

            // 3. Normalisation, the same rule on both sides.
            var (normalization, normNote) = ChooseNormalization(measuredInside, calculatedRaw, pdd, options.CentralAxisFallbackFraction);
            if (normNote is not null) notes.Add(normNote);
            var m = measuredInside.Normalize(normalization);
            var c = calculatedRaw.Normalize(normalization);
            double calcDivisor = Divisor(calculatedRaw, normalization);

            // 4. The point test.
            bool refIsMeasured = options.Reference == ProfileReference.Measured;
            var reference = refIsMeasured ? m : c;
            var evaluated = refIsMeasured ? c : m;
            double cutoff = pdd ? options.DepthDoseCutoffPercent : options.ProfileCutoffPercent;
            double[] refPos = reference.Points.Select(p => p.Position.Millimetres).ToArray();
            double[] refVal = reference.Points.Select(p => p.Value).ToArray();

            // The evaluated curve as a function: the calculation read straight off the grid, or the measurement interpolated.
            Func<double, double?> evalAt = refIsMeasured
                ? p => DoseLineSampler.ValueAt(grid, frame, measured.Scan, p + shift) is { } v ? v * unit / calcDivisor : null
                : p => InRange(m, p) ? ValueIn(m, p) : null;
            Func<double, double?> refAt = refIsMeasured
                ? p => InRange(m, p) ? ValueIn(m, p) : null
                : p => DoseLineSampler.ValueAt(grid, frame, measured.Scan, p + shift) is { } v ? v * unit / calcDivisor : null;

            double[] pointValues;
            int analysed, passed;
            GammaResult? gamma = null;
            double? meanGamma = null, maxGamma = null;
            if (options.Test == ProfileTest.Gamma)
            {
                var (evalPos, evalVal) = EvenEvaluation(refIsMeasured, grid, frame, measured.Scan, shift, unit, calcDivisor, m,
                    refPos[0] - 2 * options.DistanceMm, refPos[^1] + 2 * options.DistanceMm);
                var gammaOptions = new GammaOptions(options.DosePercent, options.DistanceMm, cutoff, options.InterpFraction,
                    options.MaxGamma, options.Local, DegreeOfParallelism: 1);
                gamma = GammaShell.Compute(new GammaGrid(new[] { refPos }, refVal), new GammaGrid(new[] { evalPos }, evalVal), gammaOptions);
                pointValues = gamma.Gamma;
                analysed = gamma.Analysed;
                passed = gamma.Passed;
                var finite = pointValues.Where(double.IsFinite).ToArray();
                meanGamma = finite.Length > 0 ? finite.Average() : null;
                maxGamma = finite.Length > 0 ? finite.Max() : null;
                if (gamma.NotReached > 0) notes.Add($"{gamma.NotReached} reference point(s) out of the evaluated curve's reach, not analysed");
            }
            else
            {
                (pointValues, analysed, passed) = Strict(refPos, refVal, evalAt, refAt, options, cutoff);
            }

            // 5. Constancy and metrics.
            double? constancy = null;
            ProfileMetricsReport? mm = null, cm = null;
            DepthDoseMetrics? md = null, cd = null;
            if (pdd)
            {
                md = Try(() => DepthDoseMetrics.Of(m), "measured depth-dose metrics", notes);
                cd = Try(() => DepthDoseMetrics.Of(c), "calculated depth-dose metrics", notes);
            }
            else
            {
                constancy = Constancy(reference, evaluated, options.ConstancyRegionFraction, notes);
                mm = Metrics(m, "measured", notes);
                cm = Metrics(c, "calculated", notes);
            }

            return new ProfileComparisonResult
            {
                Options = options,
                Measured = m,
                Calculated = c,
                Normalization = normalization,
                NormalizationNote = normNote,
                PointsOutsideGrid = sampled.PointsOutsideGrid,
                PointPositionsMm = refPos,
                PointValues = pointValues,
                Analysed = analysed,
                Passed = passed,
                Gamma = gamma,
                MeanGamma = meanGamma,
                MaxGammaFound = maxGamma,
                DepthShiftMm = pdd && options.DepthShift is not null ? -shift : null,
                ConstancyPercent = constancy,
                MeasuredMetrics = mm,
                CalculatedMetrics = cm,
                MeasuredDepthDose = md,
                CalculatedDepthDose = cd,
                Notes = notes
            };
        }

        /// <summary>
        /// Central axis for a transverse profile, unless either side's central-axis value is missing or below
        /// <paramref name="fallback"/> of its maximum, when both go to their maximum; a depth dose always to its maximum.
        /// </summary>
        internal static (NormalizationType Type, string? Note) ChooseNormalization(Profile measured, Profile calculated, bool pdd, double fallback)
        {
            if (pdd) return (NormalizationType.Maximum, null);
            foreach (var (side, p) in new[] { ("measured", measured), ("calculated", calculated) })
            {
                if (!InRange(p, 0)) return (NormalizationType.Maximum, $"normalised to the maximum: the {side} curve doesn't reach the central axis");
                double cax = p.ValueAt(Distance.Zero);
                if (cax < fallback * p.MaximumValue)
                    return (NormalizationType.Maximum, FormattableString.Invariant(
                        $"normalised to the maximum: the {side} central-axis value is {100 * cax / p.MaximumValue:0.#}% of its maximum, below {100 * fallback:0}%"));
            }
            return (NormalizationType.CentralAxis, null);
        }

        private static double Divisor(Profile p, NormalizationType type) =>
            type == NormalizationType.CentralAxis ? p.ValueAt(Distance.Zero) : p.MaximumValue;

        /// <summary>
        /// The strict rule along the line, at each reference point at or above the cutoff: pass when |e - r| is within
        /// the tolerance (of the reference maximum, or of r when local); else, with a distance, pass when e lies between
        /// the reference's lowest and highest value at r's position plus and minus the distance (those inside the curve).
        /// HandWrittenComparer's order, in one dimension.
        /// </summary>
        internal static (double[] Values, int Analysed, int Passed) Strict(double[] refPos, double[] refVal,
            Func<double, double?> evalAt, Func<double, double?> refAt, ProfileComparisonOptions o, double cutoffPercent)
        {
            double max = refVal.Max();
            var values = new double[refPos.Length];
            int analysed = 0, passed = 0;
            for (int i = 0; i < refPos.Length; i++)
            {
                values[i] = double.NaN;
                double r = refVal[i];
                if (r < max * cutoffPercent / 100) continue;
                if (evalAt(refPos[i]) is not { } e) continue;
                analysed++;
                double allowed = (o.Local ? r : max) * o.DosePercent / 100;
                bool ok = Math.Abs(e - r) <= allowed;
                if (!ok && o.DistanceMm > 0)
                {
                    double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                    int n = 0;
                    foreach (double d in new[] { -o.DistanceMm, o.DistanceMm })
                        if (refAt(refPos[i] + d) is { } v) { n++; lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                    ok = n > 0 && e >= lo && e <= hi;
                }
                if (ok) passed++;
                values[i] = ok ? 0 : 1;
            }
            return (values, analysed, passed);
        }

        /// <summary>
        /// The depth offset that best fits: the calculation read at depth + s, each curve to its own maximum, compared
        /// beyond the deeper of the two maxima where the measurement is above the cutoff, by mean squared difference.
        /// Ties go to the smaller |s|. Returns s.
        /// </summary>
        internal static double FindDepthShift(Profile measured, DoseMatrixOptimal grid, PhantomFrame frame, DepthShiftSearch search,
            double cutoffPercent, List<string> notes)
        {
            double[] depth = measured.Points.Select(p => p.Position.Millimetres).ToArray();
            double[] mv = measured.Points.Select(p => p.Value).ToArray();
            double mMax = mv.Max();
            int steps = (int)Math.Round(search.RangeMm / search.StepMm);
            double best = 0, bestScore = double.PositiveInfinity;
            for (int k = -steps; k <= steps; k++)
            {
                double s = k * search.StepMm;
                var c = depth.Select(d => DoseLineSampler.ValueAt(grid, frame, measured.Scan, d + s)).ToArray();
                double cMax = c.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();
                if (!(cMax > 0)) continue;
                double dmaxM = depth[Array.IndexOf(mv, mMax)];
                int iC = Array.FindIndex(c, v => v == cMax);
                double from = Math.Max(dmaxM, depth[iC]);
                double sum = 0;
                int n = 0;
                for (int i = 0; i < depth.Length; i++)
                {
                    if (depth[i] < from || c[i] is not { } cv) continue;
                    double mn = mv[i] / mMax;
                    if (mn * 100 < cutoffPercent) continue;
                    double diff = mn - cv / cMax;
                    sum += diff * diff;
                    n++;
                }
                if (n < 5) continue;
                double score = sum / n;
                if (score < bestScore - 1e-15 || (Math.Abs(score - bestScore) <= 1e-15 && Math.Abs(s) < Math.Abs(best)))
                {
                    bestScore = score;
                    best = s;
                }
            }
            if (double.IsPositiveInfinity(bestScore)) notes.Add("depth shift: too few points beyond the maximum to fit; no shift applied");
            return best;
        }

        private static Profile Shifted(Profile calculated, DoseMatrixOptimal grid, PhantomFrame frame, double shift, double unit,
            string dosePath, List<string> notes)
        {
            var points = new List<ProfilePoint>();
            foreach (var p in calculated.Points)
                if (DoseLineSampler.ValueAt(grid, frame, calculated.Scan, p.Position.Millimetres + shift) is { } v)
                    points.Add(new ProfilePoint(p.Position, v * unit, null));
            int lost = calculated.Points.Count - points.Count;
            if (lost > 0) notes.Add($"depth shift: {lost} point(s) moved outside the grid and were left out");
            return new Profile(calculated.Beam, calculated.Scan, points, calculated.Conditions,
                calculated.Source with { Reader = calculated.Source.Reader + FormattableString.Invariant($" (read {shift:+0.0##;-0.0##} mm deeper)") },
                NormalizationType.AsMeasured, calculated.Correction, null, calculated.ValueScale);
        }

        /// <summary>The evaluated side on an even grid: the calculation at the grid's nodes, or the measurement interpolated
        /// at its own finest step.</summary>
        private static (double[] Positions, double[] Values) EvenEvaluation(bool calculationIsEvaluated, DoseMatrixOptimal grid,
            PhantomFrame frame, ScanKey scan, double shift, double unit, double calcDivisor, Profile measuredNormalised,
            double fromMm, double toMm)
        {
            if (calculationIsEvaluated)
            {
                var (pos, val) = DoseLineSampler.AtNodes(grid, frame, scan, fromMm + shift, toMm + shift);
                return (pos.Select(p => p - shift).ToArray(), val.Select(v => v * unit / calcDivisor).ToArray());
            }
            double[] mp = measuredNormalised.Points.Select(p => p.Position.Millimetres).ToArray();
            double step = Enumerable.Range(1, mp.Length - 1).Select(i => mp[i] - mp[i - 1]).Where(d => d > 1e-9).Min();
            int n = (int)Math.Floor((mp[^1] - mp[0]) / step + 1e-9) + 1;
            var positions = Enumerable.Range(0, n).Select(i => mp[0] + i * step).ToArray();
            return (positions, positions.Select(p => measuredNormalised.ValueAt(Distance.FromMillimetres(Math.Min(p, mp[^1])))).ToArray());
        }

        /// <summary>Mean |evaluated - reference| / reference in %, at the reference points within the central
        /// <paramref name="fraction"/> of the reference field (between its 50 % edges, about their midpoint).</summary>
        internal static double? Constancy(Profile reference, Profile evaluated, double fraction, List<string> notes)
        {
            try
            {
                var line = ProfileLine.From(reference);
                var (left, right) = ProfileMetrics.FieldEdges(line, 0);
                double centre = (left + right) / 2, half = (right - left) / 2 * fraction;
                var terms = new List<double>();
                foreach (var p in reference.Points)
                {
                    double x = p.Position.Millimetres;
                    if (x < centre - half - 1e-9 || x > centre + half + 1e-9 || !(p.Value > 0) || !InRange(evaluated, x)) continue;
                    terms.Add(Math.Abs(ValueIn(evaluated, x) - p.Value) / p.Value);
                }
                if (terms.Count == 0) { notes.Add("constancy: no reference points in the central field"); return null; }
                return 100 * terms.Average();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                notes.Add("constancy: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// FauxPID's nine metrics as <see cref="ProfileMetrics.All(Profile, double, IecBanding)"/> takes them (in the nominal
        /// plane, axis at 0), but each on its own: a metric the curve can't support is NaN and named in the notes, rather than
        /// losing the rest. A half-beam field, for example, has a field size and offset but no flatness about its axis.
        /// </summary>
        internal static ProfileMetricsReport? Metrics(Profile p, string side, List<string> notes)
        {
            ProfileLine line;
            double magnification;
            try
            {
                magnification = ProfileLine.MagnificationOf(p);
                line = ProfileLine.From(p).ProjectedToNominal(magnification);
            }
            catch (ArgumentException ex) { notes.Add($"{side} profile metrics: {ex.Message}"); return null; }
            var failed = new List<string>();
            double M(string name, Func<double> f)
            {
                try { return f(); }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { failed.Add(name); return double.NaN; }
            }
            var report = new ProfileMetricsReport(
                M("field size", () => ProfileMetrics.FieldSize(line)),
                M("CAX offset", () => ProfileMetrics.CaxOffset(line)),
                M("flatness by variance", () => ProfileMetrics.FlatnessByVariance(line)),
                M("IEC flatness", () => ProfileMetrics.FlatnessByRatioIec(line)),
                M("CAX variance", () => ProfileMetrics.FlatnessByCaxVariance(line)),
                M("CAX ratio", () => ProfileMetrics.FlatnessByCaxRatio(line)),
                M("CAX point symmetry", () => ProfileMetrics.SymmetryByCaxPointDifference(line)),
                M("IEC point symmetry", () => ProfileMetrics.SymmetryByPointRatioIec(line)),
                M("area symmetry", () => ProfileMetrics.SymmetryByArea(line)),
                magnification);
            if (failed.Count > 0) notes.Add($"{side} profile metrics not taken: {string.Join(", ", failed)}");
            return report;
        }

        private static T? Try<T>(Func<T> f, string what, List<string> notes) where T : class
        {
            try { return f(); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                notes.Add($"{what}: {ex.Message}");
                return null;
            }
        }

        /// <summary>The value at x, which <see cref="InRange"/> allows a hair past either end, clamped onto the curve.</summary>
        private static double ValueIn(Profile p, double x) => p.ValueAt(Distance.FromMillimetres(
            Math.Clamp(x, p.PositionRange.Min.Millimetres, p.PositionRange.Max.Millimetres)));

        private static bool InRange(Profile p, double x) =>
            x >= p.PositionRange.Min.Millimetres - 1e-9 && x <= p.PositionRange.Max.Millimetres + 1e-9;

        private static Profile With(Profile p, IEnumerable<ProfilePoint> points) =>
            new(p.Beam, p.Scan, points, p.Conditions, p.Source, p.Normalization, p.Correction, p.CorrectionDescription, p.ValueScale);
    }
}
