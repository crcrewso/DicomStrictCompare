using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Every <see cref="DoseComparer"/> must give <see cref="HandWrittenComparer"/>'s counts exactly: the reference
    /// files under a spread of criteria, then random small grids built to hit the threshold box's edge cases.
    /// GPU comparers in double precision join these where a device is present (see <see cref="Comparers"/>).
    /// </summary>
    [TestClass]
    public class DoseComparerEquivalenceTests
    {
        private static readonly HandWrittenComparer Reference = new();

        /// <summary>The comparers held to exact agreement.</summary>
        internal static IEnumerable<DoseComparer> Comparers()
        {
            yield return new SingleThreadComparer();
            yield return new SingleThreadComparer { UseThresholdBox = false };
            yield return new MultiThreadComparer { Threads = 4 };
            yield return new MultiThreadComparer { Threads = 1, UseThresholdBox = false };
            yield return new MultiThreadComparer();                                   // one per usable core, pinned
            yield return new MultiThreadComparer { Threads = 3, PinThreads = false };
            foreach (var g in GpuDoubles()) yield return g;
        }

        internal static IEnumerable<DoseComparer> GpuDoubles()
        {
            foreach (var kind in new[] { ComparerKind.Ilgpu, ComparerKind.OpenCl })
            {
                DoseComparer? c = null;
                try { c = DoseComparers.Create(new ComparerSettings(kind, Precision: GpuPrecision.Double)); }
                catch (ComparerUnavailableException) { }
                if (c is not null) yield return c;
            }
        }

        private static readonly (string Source, string Target)[] ReferencePairs =
        {
            ("P1Ref.X-100A-10.0-1", "P1 Acuros.X-100X-10.0-1"),   // AAA against Acuros, same grid
            ("P1Ref.F-100A-10.0-1", "P1 Acuros.F-100X-10.0-1"),
            ("P1Ref.X-100A-10.0-1", "P1 2 mm.X-100A-10.0-0.2"),   // 1 mm against 2 mm
            ("P1Ref.X-100A-10.0-1", "P1 5 mm.X-100A-10.0-5"),     // 1 mm against 5 mm
            ("P1 5 mm.X-100A-10.0-5", "P1Ref.X-100A-10.0-1"),     // coarse source
            ("P2 10.1.X-100X-10.1-1", "P3 11.0.X-100X-10.1-1"),   // two versions
            ("P5 Varied MU.X-095A-10.0-1", "P5 Varied MU.X-101A-10.0-1"), // different MU
            ("P1Ref.e-100A-10.0-1-13", "P4 E 2D.e-100A-10.0-1-12"),     // electrons
            ("P1Ref.X-100A-10.0-1", "P1Ref.X-100A-10.0-1"),       // identical
        };

        private static readonly (Dta Dta, bool Global)[] ReferenceCriteria =
        {
            (new Dta(true, 0.00, 0.0001, 0, relative: true), true),             // 0.01 %, no distance, no threshold
            (new Dta(true, 0.10, 0.001, 0.5, relative: false), false),          // 0.1 % / 0.5 mm local
            (new Dta(true, 0.10, 0.02, 2, relative: true), true),               // 2 % / 2 mm global
            (new Dta(true, 0.01, 0.03, 2, relative: false, trim: 2), false),    // 3 % / 2 mm local, 1 % threshold, trimmed
        };

        private static string Dose(string stem) =>
            TestData.DoseFiles.Single(f => Path.GetFileName(f) == $"RD.UnitTest.{stem}.dcm");

        /// <summary>
        /// Says which comparers this machine can run, so a GPU comparer that could not start is reported rather than
        /// silently left out of the equivalence tests.
        /// </summary>
        [TestMethod]
        public void EveryComparerKindStartsHere()
        {
            var all = DoseComparers.Available();
            foreach (var a in all) Console.WriteLine($"{a.Kind}: {(a.Available ? "available" : "UNAVAILABLE")} - {a.Detail}");
            var missing = all.Where(a => !a.Available).ToArray();
            if (missing.Length > 0)
                Assert.Inconclusive("Not run here, so not checked: " + string.Join("; ", missing.Select(m => $"{m.Kind} ({m.Detail})")));
        }

        [TestMethod]
        public void EveryComparerMatchesTheHandWrittenOneOnTheReferenceFiles()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            var comparers = Comparers().ToArray();
            Console.WriteLine("Checked: " + string.Join("; ", comparers.Select(c => c.Description)));
            var failures = new List<string>();
            foreach (var (s, t) in ReferencePairs)
            {
                var source = DoseGridReader.Read(Dose(s));
                var target = DoseGridReader.Read(Dose(t));
                foreach (var (dta, global) in ReferenceCriteria)
                {
                    var expected = Run(Reference, source, target, dta, global);
                    foreach (var c in comparers)
                    {
                        var got = Run(c, source, target, dta, global);
                        if (!Same(expected, got))
                            failures.Add($"{c.Description}, {s} vs {t}, {dta.ShortToString()} thr {dta.Threshhold}: {Text(got)}, expected {Text(expected)}");
                    }
                }
            }
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(20)));
        }

        [TestMethod]
        public void EveryComparerMatchesTheHandWrittenOneOnRandomEdgeCases()
        {
            var comparers = Comparers().ToArray();
            Console.WriteLine("Checked: " + string.Join("; ", comparers.Select(c => c.Description)));
            var failures = new List<string>();
            var random = new Random(20261115);
            for (int n = 0; n < 200; n++)
            {
                var (source, target, dta, global, what) = RandomCase(random, n);
                var expected = Run(Reference, source, target, dta, global);
                foreach (var c in comparers)
                {
                    var got = Run(c, source, target, dta, global);
                    if (!Same(expected, got)) failures.Add($"case {n} ({what}), {c.Description}: {Text(got)}, expected {Text(expected)}");
                }
            }
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(20)));
        }

        /// <summary>
        /// GPU comparers in float, a user's choice. Float rounding may move points that sit within rounding of a
        /// tolerance or the threshold, so this bound is a placeholder until Cody decides the policy: at most 5 points,
        /// or 0.001 % of points compared, whichever is larger. Every difference is printed.
        /// </summary>
        [TestMethod]
        public void GpuFloatStaysWithinAProvisionalBoundOfTheHandWrittenOne()
        {
            var floats = new List<DoseComparer>();
            foreach (var kind in new[] { ComparerKind.Ilgpu, ComparerKind.OpenCl })
                try { floats.Add(DoseComparers.Create(new ComparerSettings(kind, Precision: GpuPrecision.Float))); } catch (ComparerUnavailableException) { }
            if (floats.Count == 0) Assert.Inconclusive("No GPU comparer starts here.");
            var failures = new List<string>();
            foreach (var (s, t) in ReferencePairs)
            {
                var source = DoseGridReader.Read(Dose(s));
                var target = DoseGridReader.Read(Dose(t));
                foreach (var (dta, global) in ReferenceCriteria)
                {
                    var expected = Run(Reference, source, target, dta, global);
                    foreach (var c in floats)
                    {
                        var got = Run(c, source, target, dta, global);
                        long dc = got.TotalCompared - expected.TotalCompared, df = got.TotalFailed - expected.TotalFailed;
                        if (dc != 0 || df != 0) Console.WriteLine($"{c.Description}, {s} vs {t}, {dta.ShortToString()}: compared {dc:+#;-#;0}, failed {df:+#;-#;0}");
                        double bound = Math.Max(5, expected.TotalCompared * 1e-5);
                        if (got.TotalCount != expected.TotalCount || Math.Abs(dc) > bound || Math.Abs(df) > bound)
                            failures.Add($"{c.Description}, {s} vs {t}, {dta.ShortToString()}: {Text(got)}, expected {Text(expected)}");
                    }
                }
            }
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures.Take(20)));
        }

        [TestMethod]
        public void TheBoxSkipsMostOfASmallFieldButCountsEveryPoint()
        {
            var source = DoseGridReader.Read(Dose("P1Ref.X-100A-10.0-1"));
            var dta = new Dta(true, 0.10, 0.02, 2, relative: true);
            var setup = ComparisonSetup.For(source, source, dta, true, thresholdBox: true);
            Assert.IsTrue(setup.WindowPoints < setup.TotalCount, $"window {setup.WindowPoints} of {setup.TotalCount}");
            Assert.AreEqual(Run(Reference, source, source, dta, true).TotalCount, Run(new SingleThreadComparer(), source, source, dta, true).TotalCount);
        }

        // A small smooth field (a blob or two plus noise), with the features the box must survive chosen per case.
        private static (DoseMatrixOptimal, DoseMatrixOptimal, Dta, bool, string) RandomCase(Random r, int n)
        {
            int dx = r.Next(6, 23), dy = r.Next(6, 21), dz = r.Next(5, 19);
            double sres = new[] { 1.0, 2.0, 2.5, 3.0 }[r.Next(4)];
            var what = new List<string>();
            double cx = r.NextDouble() * dx, cy = r.NextDouble() * dy, cz = r.NextDouble() * dz;
            if (n % 5 == 0) { cx = 0; cy = 0; cz = 0; what.Add("maximum in a corner"); }
            double width = 1 + r.NextDouble() * Math.Max(dx, dy) / 3;
            bool zeroEdges = n % 3 == 0, negative = n % 11 == 0;
            if (zeroEdges) what.Add("zero-dose edges");
            if (negative) what.Add("negative doses");
            var v = new double[dx * dy * dz];
            for (int z = 0; z < dz; z++)
                for (int y = 0; y < dy; y++)
                    for (int x = 0; x < dx; x++)
                    {
                        double d2 = ((x - cx) * (x - cx) + (y - cy) * (y - cy) + (z - cz) * (z - cz)) / (width * width);
                        double value = 100 * Math.Exp(-d2) + r.NextDouble() * 2 - (negative ? 3 : 0);
                        if (zeroEdges && (x == 0 || y == 0 || z == 0 || x == dx - 1 || y == dy - 1 || z == dz - 1)) value = 0;
                        v[x + y * dx + z * dx * dy] = Math.Round(value, 1);   // ties are likely, on purpose
                    }
            double thr = new[] { 0.0, 0.01, 0.10, 0.10, 0.5, 1.2 }[r.Next(6)];
            if (thr == 0) what.Add("threshold 0");
            if (thr > 1) what.Add("nothing reaches the threshold");
            if (n % 7 == 0)
            {
                // A voxel exactly at the threshold, at the field's edge.
                double max = v.Max();
                int i = Array.FindIndex(v, x => x > 0 && x < max * 0.3);
                if (i >= 0 && thr is > 0 and <= 1) { v[i] = max * thr; what.Add("voxel exactly at the threshold"); }
            }
            var source = new DoseMatrixOptimal(dx, dy, dz, -dx * sres / 2, -dy * sres / 2, -dz * sres / 2, sres, sres, sres, v);

            // The target: the source, scaled and resampled onto another grid, sometimes offset.
            double tres = n % 4 == 0 ? sres * new[] { 0.5, 1.5, 2.0 }[r.Next(3)] : sres;
            double offset = n % 6 == 0 ? r.NextDouble() * tres : 0;
            if (tres != sres) what.Add("different spacing");
            if (offset != 0) what.Add("offset origin");
            int tx = Math.Max(2, (int)(dx * sres / tres)), ty = Math.Max(2, (int)(dy * sres / tres)), tz = Math.Max(2, (int)(dz * sres / tres));
            double scale = 1 + (r.NextDouble() - 0.5) * 0.06;
            var tv = new double[tx * ty * tz];
            double t0x = source.X0 + offset, t0y = source.Y0 + offset / 2, t0z = source.Z0;
            for (int z = 0; z < tz; z++)
                for (int y = 0; y < ty; y++)
                    for (int x = 0; x < tx; x++)
                    {
                        double px = Math.Min(source.XMax, t0x + x * tres), py = Math.Min(source.YMax, t0y + y * tres), pz = Math.Min(source.ZMax, t0z + z * tres);
                        tv[x + y * tx + z * tx * ty] = source.GetPointDose(px, py, pz).Dose * scale + (r.NextDouble() - 0.5);
                    }
            var target = new DoseMatrixOptimal(tx, ty, tz, t0x, t0y, t0z, tres, tres, tres, tv);

            double tol = new[] { 0.0001, 0.001, 0.005, 0.01, 0.02, 0.03 }[r.Next(6)];
            double dist = new[] { 0, 0.5, 1, 2, 3 }[r.Next(5)];
            int trim = n % 8 == 0 ? r.Next(1, 3) : 0;
            if (trim > 0) what.Add($"trim {trim}");
            bool global = r.Next(2) == 0;
            what.Add($"{tol * 100}%/{dist}mm {(global ? "global" : "local")}, threshold {thr}");
            return (source, target, new Dta(true, thr, tol, dist, relative: global, trim: trim), global, string.Join(", ", what));
        }

        private static SingleComparison Run(DoseComparer c, DoseMatrixOptimal s, DoseMatrixOptimal t, Dta dta, bool global) =>
            global ? c.CompareRelative(s, t, dta) : c.CompareAbsolute(s, t, dta);

        private static bool Same(SingleComparison a, SingleComparison b) =>
            a.TotalCount == b.TotalCount && a.TotalCompared == b.TotalCompared && a.TotalFailed == b.TotalFailed;

        private static string Text(SingleComparison c) => $"{c.TotalCount}/{c.TotalCompared}/{c.TotalFailed}";
    }
}
