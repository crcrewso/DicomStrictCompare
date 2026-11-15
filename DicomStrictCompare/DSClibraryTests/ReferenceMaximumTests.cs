using System;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>The "Max" setting: Global Max, or CAX decided by the jaws alone (design/comparison-studies.md, F10).</summary>
    [TestClass]
    public class ReferenceMaximumTests
    {
        [TestMethod]
        public void SymmetricJawsAreOnAxis()
        {
            Assert.IsNull(ReferenceMaximum.JawsOffAxis(new[] { -50.0, 50.0 }, new[] { -50.0, 50.0 }));
            // Exactly 1 cm apart is still on axis: the rule is "more than 1 cm".
            Assert.IsNull(ReferenceMaximum.JawsOffAxis(new[] { -45.0, 55.0 }, new[] { -50.0, 50.0 }));
        }

        [TestMethod]
        public void AsymmetricJawsOrJawsMissingTheAxisAreOffAxis()
        {
            StringAssert.Contains(ReferenceMaximum.JawsOffAxis(new[] { -30.0, 70.0 }, new[] { -50.0, 50.0 }), "X jaws");
            StringAssert.Contains(ReferenceMaximum.JawsOffAxis(new[] { -50.0, 50.0 }, new[] { -20.0, 80.0 }), "Y jaws");
            StringAssert.Contains(ReferenceMaximum.JawsOffAxis(new[] { 10.0, 60.0 }, new[] { -50.0, 50.0 }), "do not contain the CAX");
            StringAssert.Contains(ReferenceMaximum.JawsOffAxis(null, new[] { -50.0, 50.0 }), "no X or Y jaw");
        }

        /// <summary>A 7 x 9 x 7 grid at 1 mm about the origin, with a known column and plane.</summary>
        private static DoseMatrixOptimal Grid(out double caxExpected, out double isoExpected)
        {
            int nx = 7, ny = 9, nz = 7;
            var v = new double[nx * ny * nz];
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                        v[x + y * nx + z * nx * ny] = 1 + x + 10 * z + (y == 2 ? 100 : 0) + (y == 4 ? 50 : 0);
            v[0] = 1000;   // a volume maximum away from the axis, so the two maxima differ
            var g = new DoseMatrixOptimal(nx, ny, nz, -3, -4, -3, 1, 1, 1, v);
            // The CAX column is x = 3, z = 3; its maximum is at y = 2. The 3 x 3 block there averages to the centre value.
            caxExpected = 1 + 3 + 30 + 100;
            // The isocentre (0, 0, 0) is voxel (3, 4, 3), in the y = 4 plane.
            isoExpected = 1 + 3 + 30 + 50;
            return g;
        }

        [TestMethod]
        public void TheCaxMaximumIsTheThreeByThreeAverageAtTheColumnsMaximum()
        {
            var g = Grid(out double cax, out _);
            Assert.AreEqual(cax, ReferenceMaximum.CaxMaximum(g, new Position3(0, 0, 0))!.Value, 1e-12);
            Assert.AreEqual(1000, g.MaxPointDose.Dose);
        }

        [TestMethod]
        public void AnArcIsNormalisedAboutTheIsocentre()
        {
            var g = Grid(out _, out double iso);
            Assert.AreEqual(iso, ReferenceMaximum.IsocentreAverage(g, new Position3(0, 0, 0))!.Value, 1e-12);
        }

        [TestMethod]
        public void AnAxisAtTheGridEdgeHasNoCaxValue()
        {
            var g = Grid(out _, out _);
            Assert.IsNull(ReferenceMaximum.CaxMaximum(g, new Position3(-3, 0, 0)));   // column on the edge: no 3 x 3 block
            Assert.IsNull(ReferenceMaximum.IsocentreAverage(g, new Position3(0, 0, 99)));
        }

        [TestMethod]
        public void GlobalMaxIsTheVolumeMaximumWhateverThePlanSays()
        {
            var g = Grid(out _, out _);
            var m = ReferenceMaximum.For(g, null, MaximumKind.GlobalMax);
            Assert.AreEqual(MaximumKind.GlobalMax, m.Used);
            Assert.AreEqual(g.MaxPointDose.Dose, m.Dose);
        }

        [TestMethod]
        public void CaxWithoutAPlanFallsBackToGlobalMax()
        {
            var g = Grid(out _, out _);
            var m = ReferenceMaximum.For(g, null, MaximumKind.CaxMax);
            Assert.AreEqual(MaximumKind.GlobalMax, m.Used);
            StringAssert.Contains(m.Note, "no plan");
        }

        [TestMethod]
        public void ASymmetricReferenceFieldUsesItsCax()
        {
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            var dose = folder.Doses.Single(d => Path.GetFileName(d.Dose.SourcePath) == "RD.UnitTest.P1Ref.X-100A-10.0-1.dcm");
            Assert.IsNotNull(dose.Beam, "the reference dose's beam");
            Assert.IsNull(ReferenceMaximum.WhyNotCax(dose.Beam!), ReferenceMaximum.WhyNotCax(dose.Beam!));
            var grid = DoseGridReader.Read(dose.Dose.SourcePath);
            var m = ReferenceMaximum.For(grid, dose.Beam, MaximumKind.CaxMax);
            Assert.AreEqual(MaximumKind.CaxMax, m.Used, m.Note);
            Assert.IsTrue(m.Dose > 0.8 * grid.MaxPointDose.Dose && m.Dose <= grid.MaxPointDose.Dose,
                $"CAX {m.Dose} against volume maximum {grid.MaxPointDose.Dose}");
        }

        [TestMethod]
        public void EveryComparerAgreesWhenTheMaximumIsChosen()
        {
            var comparers = DoseComparerEquivalenceTests.Comparers().ToArray();
            var reference = new HandWrittenComparer();
            var source = DoseGridReader.Read(TestData.DoseFiles.Single(f => f.EndsWith("RD.UnitTest.P1Ref.X-100A-10.0-1.dcm")));
            var target = DoseGridReader.Read(TestData.DoseFiles.Single(f => f.EndsWith("RD.UnitTest.P1 Acuros.X-100X-10.0-1.dcm")));
            var failures = new System.Collections.Generic.List<string>();
            foreach (double scale in new[] { 0.8, 1.1 })
                foreach (var (dta, global) in new[] { (new Dta(true, 0.10, 0.02, 2, relative: true), true), (new Dta(true, 0.10, 0.001, 0.5, relative: false), false) })
                {
                    double max = source.MaxPointDose.Dose * scale;
                    var expected = global ? reference.CompareRelative(source, target, dta, max, default) : reference.CompareAbsolute(source, target, dta, max, default);
                    foreach (var c in comparers)
                    {
                        var got = global ? c.CompareRelative(source, target, dta, max, default) : c.CompareAbsolute(source, target, dta, max, default);
                        if (got.TotalCount != expected.TotalCount || got.TotalCompared != expected.TotalCompared || got.TotalFailed != expected.TotalFailed)
                            failures.Add($"{c.Description} at {scale} x max, {dta.ShortToString()}: {got.TotalCompared}/{got.TotalFailed}, expected {expected.TotalCompared}/{expected.TotalFailed}");
                    }
                    // And the chosen maximum really changes the answer, or this test would prove nothing.
                    var plain = global ? reference.CompareRelative(source, target, dta) : reference.CompareAbsolute(source, target, dta);
                    Assert.AreNotEqual(plain.TotalCompared, expected.TotalCompared, $"scale {scale} changed nothing");
                }
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        [TestMethod]
        public void BatchResultsSayWhichMaximumWasUsed()
        {
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            var pairs = DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.Identifier).Pairs
                .Where(p => Path.GetFileName(p.Source.Dose.SourcePath).Contains("P1Ref.X-100A")).ToArray();
            Assert.AreEqual(1, pairs.Length);
            var dta = new Dta(true, 0.10, 0.02, 2, relative: true);
            var batch = new BatchComparer(DoseGridReader.Read, new SingleThreadComparer());
            var cax = batch.Compare(pairs, dta, maximum: MaximumKind.CaxMax).Outcomes.Single();
            var global = batch.Compare(pairs, dta).Outcomes.Single();
            Assert.AreEqual(MaximumKind.CaxMax, cax.Maximum!.Used, cax.Maximum.Note);
            Assert.AreEqual(MaximumKind.GlobalMax, global.Maximum!.Used);
            StringAssert.Contains(cax.Comparer, "SingleThread");
        }
    }
}
