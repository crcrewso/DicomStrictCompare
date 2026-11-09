using System;
using System.IO;
using System.Linq;
using DSClibrary.Mapping.PTW;
using DSClibrary.Profiles;
using DSClibrary.Results.PTW;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// The FauxPID profile metrics, checked against FauxPID's own oracle (a perfect 100 x 100 mm
    /// field gives flatness 0 / 100 / 0 / 1, symmetry 0 / 100 / 0, field size 100, offset 0) and
    /// against fields built to have known flatness, symmetry and offset.
    /// </summary>
    [TestClass]
    public class ProfileMetricsTests
    {
        /// <summary>A field of width <paramref name="widthMm"/> centred at <paramref name="centreMm"/>, sampled every <paramref name="spacing"/> mm from -150 to +150, with a chosen in-field shape.</summary>
        private static ProfileLine Field(double widthMm, Func<double, double> inField, double centreMm = 0, double spacing = 1.0, double halfSpan = 150)
        {
            // The grid is symmetric about 0 whatever the spacing, as an EPID's pixels are about its centre.
            int n = (int)Math.Round(2 * halfSpan / spacing) + 1;
            var xs = Enumerable.Range(0, n).Select(i => (i - (n - 1) / 2.0) * spacing).ToArray();
            var ys = xs.Select(x => Math.Abs(x - centreMm) <= widthMm / 2 + 1e-9 ? inField(x) : 0.0).ToArray();
            return new ProfileLine(xs, ys);
        }

        [TestMethod]
        public void FauxPidsPerfectFieldOracle()
        {
            // tests/test_metrics.py: a 100 x 100 mm perfect field.
            foreach (double spacing in new[] { 1.0, 0.5, 0.336 })
            {
                var line = Field(100, _ => 1.0, spacing: spacing);
                var m = ProfileMetrics.All(line);
                string why = $"spacing {spacing}: {m}";
                Assert.AreEqual(0.0, m.FlatnessByVariancePercent, why);
                Assert.AreEqual(100.0, m.FlatnessByRatioIecPercent, why);
                Assert.AreEqual(0.0, m.FlatnessByCaxVariance, why);
                Assert.AreEqual(1.0, m.FlatnessByCaxRatio, why);
                Assert.AreEqual(0.0, m.SymmetryByCaxPointDifferencePercent, why);
                Assert.AreEqual(100.0, m.SymmetryByPointRatioIecPercent, why);
                Assert.AreEqual(0.0, m.SymmetryByAreaPercent, why);
                Assert.IsTrue(Math.Abs(m.FieldSizeMm - 100.0) <= spacing + 1e-9, why);
                Assert.IsTrue(Math.Abs(m.CaxOffsetMm) < 1e-9, why);
            }
        }

        [TestMethod]
        public void TheOracleHoldsWhenTheAxisFallsBetweenSamples()
        {
            // An even number of samples: no sample at 0, the CAX value is the mean of the two middle
            // ones, as FauxPID's get_cax_value takes it.
            var xs = Enumerable.Range(0, 300).Select(i => -149.5 + i).ToArray();
            var ys = xs.Select(x => Math.Abs(x) <= 50 ? 1.0 : 0.0).ToArray();
            var m = ProfileMetrics.All(new ProfileLine(xs, ys));
            Assert.AreEqual(0.0, m.FlatnessByVariancePercent);
            Assert.AreEqual(0.0, m.SymmetryByAreaPercent);
            Assert.AreEqual(100.0, m.SymmetryByPointRatioIecPercent);
            Assert.AreEqual(100.0, m.FieldSizeMm, 1e-9, "the samples -49.5..49.5 span 99 mm, plus one spacing");
        }

        [TestMethod]
        public void AShiftedFieldHasThatCaxOffset()
        {
            var line = Field(100, _ => 1.0, centreMm: 3);
            Assert.AreEqual(3.0, ProfileMetrics.CaxOffset(line), 1e-9);
            Assert.AreEqual(-4.0, ProfileMetrics.CaxOffset(Field(100, _ => 1.0, centreMm: -4)), 1e-9);
            // The axis can be somewhere other than 0.
            Assert.AreEqual(0.0, ProfileMetrics.CaxOffset(line, caxPosition: 3), 1e-9);
        }

        [TestMethod]
        public void ATiltedFieldHasTheFlatnessTheFormulasGive()
        {
            // Linear tilt from 0.98 at -50 mm to 1.02 at +50 mm: value = 1 + 0.0004 x.
            var line = Field(100, x => 1 + 0.0004 * x);
            // 80 % region is +-40 mm: 0.984 .. 1.016.
            Assert.AreEqual(100 * (1.016 - 0.984) / (1.016 + 0.984), ProfileMetrics.FlatnessByVariance(line), 1e-9);
            // IEC region for a 50 mm half-field: 50 - 20 = 30 mm: 0.988 .. 1.012.
            Assert.AreEqual(100 * 1.012 / 0.988, ProfileMetrics.FlatnessByRatioIec(line), 1e-9);
            // Whole field over the CAX value 1: (1.02 - 0.98) / 1.
            Assert.AreEqual(0.04, ProfileMetrics.FlatnessByCaxVariance(line), 1e-9);
            // 90 % region: +-45 mm, max 1.018.
            Assert.AreEqual(1.018, ProfileMetrics.FlatnessByCaxRatio(line), 1e-9);
            // The tilt is also an asymmetry: the largest mirrored difference is at +-40 mm (80 % region).
            Assert.AreEqual((0.984 - 1.016) / 1.0 * 100, ProfileMetrics.SymmetryByCaxPointDifference(line), 1e-9);
            Assert.AreEqual(100 * 1.012 / 0.988, ProfileMetrics.SymmetryByPointRatioIec(line), 1e-9, "IEC region +-30 mm");
            Assert.IsTrue(ProfileMetrics.SymmetryByArea(line) > 0, "the right side is higher, so area symmetry is positive");
        }

        [TestMethod]
        public void AStepBetweenTheHalvesIsASymmetryAndNotMuchElse()
        {
            // Left half 1.02, right half 1.00; the CAX value is the interpolation at 0 between a
            // 1.02 sample at -1 and a 1.00 sample at +1... with a sample at 0 itself. Put the step
            // between samples by sampling at half-integers.
            var xs = Enumerable.Range(0, 300).Select(i => -149.5 + i).ToArray();
            var ys = xs.Select(x => Math.Abs(x) <= 50 ? (x < 0 ? 1.02 : 1.00) : 0.0).ToArray();
            var line = new ProfileLine(xs, ys);
            double cax = ProfileMetrics.CaxValue(line);
            Assert.AreEqual(1.01, cax, 1e-12);
            Assert.AreEqual(0.02 / 1.01 * 100, ProfileMetrics.SymmetryByCaxPointDifference(line), 1e-9, "left higher: positive");
            Assert.AreEqual(102.0, ProfileMetrics.SymmetryByPointRatioIec(line), 1e-9);
            Assert.IsTrue(ProfileMetrics.SymmetryByArea(line) < 0, "left higher: negative");
            Assert.AreEqual((1.00 - 1.02) / (2.02) * 200, ProfileMetrics.SymmetryByArea(line), 0.05);
            Assert.AreEqual(100 * 0.02 / 2.02, ProfileMetrics.FlatnessByVariance(line), 1e-9);
        }

        [TestMethod]
        public void FieldEdgesUseTheCaxValueWhenTheAxisIsInsideTheField()
        {
            // A field with a horn: maximum 1.1 at the edges, 1.0 on the axis. FWHM against the overall
            // maximum would put the threshold at 0.55; against the CAX it is 0.5. A penumbra sample at
            // 0.52 is inside by the CAX rule and outside by the maximum rule.
            var xs = Enumerable.Range(0, 301).Select(i => -150.0 + i).ToArray();
            var ys = xs.Select(x =>
                Math.Abs(x) <= 49 ? 1.0 + 0.1 * Math.Abs(x) / 49
                : Math.Abs(x) == 50 ? 0.52
                : 0.0).ToArray();
            var line = new ProfileLine(xs, ys);
            var (l, r) = ProfileMetrics.FieldEdges(line);
            Assert.AreEqual(-50, l);
            Assert.AreEqual(50, r);
            Assert.AreEqual(101, ProfileMetrics.FieldSize(line), 1e-9);
        }

        [TestMethod]
        public void NoFieldMeansAClearError()
        {
            var flat = new ProfileLine(new[] { -1.0, 0, 1 }, new[] { 0.0, 0, 0 });
            Assert.ThrowsExactly<InvalidOperationException>(() => ProfileMetrics.FieldSize(flat));
            // A field entirely to one side of the axis: flatness about the CAX is undefined.
            var offAxis = Field(40, _ => 1.0, centreMm: 60);
            Assert.ThrowsExactly<InvalidOperationException>(() => ProfileMetrics.FlatnessByVariance(offAxis));
            // but the field size and offset still are.
            Assert.AreEqual(40, ProfileMetrics.FieldSize(offAxis), 1 + 1e-9);
            Assert.AreEqual(60, ProfileMetrics.CaxOffset(offAxis), 1e-9);
        }

        [TestMethod]
        public void TheCommittedPtwScanGivesPlausibleNumbers()
        {
            // The 6 MV 30 x 30 cm PTW file: crossplane and inplane at every depth. Nothing here is an
            // oracle; the numbers are printed for the record and only their ranges asserted.
            string path = TestData.PTWScan;
            if (!File.Exists(path)) Assert.Inconclusive("ptw-scan.mcc not beside the tests");
            var parsed = MCCParser.ParseFile(path);
            var profiles = new MCCMeasurementMapper().Map(parsed).SelectMany(g => g.Profiles).ToList();
            var transverse = profiles.Where(p => p.Scan.CurveType is CurveType.Crossplane or CurveType.Inplane).ToList();
            Assert.IsTrue(transverse.Count >= 4, $"found {transverse.Count} profiles");
            foreach (var profile in transverse)
            {
                var m = ProfileMetrics.All(profile);
                Console.WriteLine($"{profile.Scan}: {m}");
                // 300 mm at SSD 1000 diverges to 300 (1000 + d) / 1000 at depth d.
                double depth = profile.Scan.Depth?.Millimetres ?? 0;
                double expectedWidth = 300 * (1000 + depth) / 1000;
                Assert.IsTrue(Math.Abs(m.FieldSizeMm - expectedWidth) < 8, $"{profile.Scan}: field {m.FieldSizeMm}, divergence says {expectedWidth:0.0}");
                Assert.IsTrue(Math.Abs(m.CaxOffsetMm) < 5, $"{profile.Scan}: offset {m.CaxOffsetMm}");
                Assert.IsTrue(m.FlatnessByVariancePercent >= 0 && m.FlatnessByVariancePercent < 10, $"{profile.Scan}: flatness {m.FlatnessByVariancePercent}");
                Assert.IsTrue(Math.Abs(m.SymmetryByCaxPointDifferencePercent) < 5, $"{profile.Scan}: symmetry {m.SymmetryByCaxPointDifferencePercent}");
                Assert.IsTrue(m.SymmetryByPointRatioIecPercent >= 100 && m.SymmetryByPointRatioIecPercent < 105);
            }
        }

        [TestMethod]
        public void TheIecRegionIsBandedOnTheHalfFieldAsFauxPidsCodeDoesIt()
        {
            // FauxPID's docs state IEC 60976's bands on the field size X (X - 2 cm up to 10 cm, 80 % up
            // to 30 cm, X - 6 cm beyond); its code applies the same thresholds, 100 and 300 mm, to each
            // HALF of the field. The port follows the code. So a 40 cm field (half 200 mm) gets 80 %
            // here, where the standard's wording would give 140 mm per side.
            Assert.AreEqual(30, ProfileMetrics.IecRegion(50));       // 10 cm field: half - 20 mm
            Assert.AreEqual(80, ProfileMetrics.IecRegion(100));      // 20 cm field: 80 %
            Assert.AreEqual(120, ProfileMetrics.IecRegion(150));     // 30 cm field: 80 %
            Assert.AreEqual(160, ProfileMetrics.IecRegion(200));     // 40 cm field: 80 % (code), not 140 (docs)
            Assert.AreEqual(240, ProfileMetrics.IecRegion(300));     // 60 cm field: half - 60 mm
        }
    }
}
