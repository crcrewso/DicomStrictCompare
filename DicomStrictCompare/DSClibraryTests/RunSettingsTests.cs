using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Gamma;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using DSClibrary.Studies;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>The jaw edge check, gamma's normalisation, pairs at once, and run settings in the study file (2026-11-15).</summary>
    [TestClass]
    public class RunSettingsTests
    {
        private static RTDoseWithPlan Dose(string stem) =>
            RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true).Doses
                .Single(d => Path.GetFileName(d.Dose.SourcePath) == $"RD.UnitTest.{stem}.dcm");

        [TestMethod]
        public void AReferenceOpenFieldsEdgesMatchItsJaws()
        {
            var d = Dose("P1Ref.X-100A-10.0-1");
            var r = JawEdgeCheck.For(DoseGridReader.Read(d.Dose.SourcePath), d.Beam);
            Assert.IsTrue(r.Checked, r.NotCheckedReason);
            Assert.IsTrue(r.Agrees, r.Summary + " " + string.Join(", ", r.Edges.Select(e => $"{e.Jaw} {e.ExpectedMm:0.0}/{e.MeasuredMm:0.0}")));
            Assert.AreEqual(4, r.Edges.Count);
        }

        [TestMethod]
        public void ElectronsAndMissingPlansAreNotChecked()
        {
            var e = Dose("P1Ref.e-100A-10.0-1-13");
            var grid = DoseGridReader.Read(e.Dose.SourcePath);
            StringAssert.Contains(JawEdgeCheck.For(grid, e.Beam).NotCheckedReason, "electron");
            StringAssert.Contains(JawEdgeCheck.For(grid, null).NotCheckedReason, "no plan");
            Assert.AreEqual("not checked (no plan)", JawEdgeCheck.Describe(JawEdgeCheck.For(grid, null), null));
        }

        [TestMethod]
        public void GlobalGammaNormalisesToTheChosenMaximum()
        {
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            var pair = DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.Identifier).Pairs
                .Single(p => Path.GetFileName(p.Source.Dose.SourcePath) == "RD.UnitTest.P1Ref.X-100A-10.0-1.dcm");
            var batch = new BatchComparer(DoseGridReader.Read);
            var options = new GammaOptions(3, 3, LowerPercentDoseCutoff: 10, MaxGamma: 1.1, SkipOncePassed: true, RandomSubset: 2000, RandomSeed: 1);
            var cax = batch.CompareGamma(new[] { pair }, options, maximum: MaximumKind.CaxMax).Outcomes.Single();
            var global = batch.CompareGamma(new[] { pair }, options).Outcomes.Single();
            Assert.AreEqual(MaximumKind.CaxMax, cax.Maximum!.Used, cax.Maximum.Note);
            Assert.AreEqual(cax.Maximum.Dose, cax.Gamma!.GlobalNormalisation, 1e-12);
            var grid = DoseGridReader.Read(pair.Source.Dose.SourcePath);
            Assert.AreEqual(grid.MaxPointDose.Dose, global.Gamma!.GlobalNormalisation, 1e-12);
        }

        [TestMethod]
        public void PairsAtOnceFollowsMemoryAndTheEngine()
        {
            var folder = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            var pairs = DoseMatcher.Match(folder.Doses, folder.Doses, DoseMatchStrategy.Identifier).Pairs.ToArray();
            long pairBytes = pairs.Max(p => (long)(p.Source.Dose.DeclaredVoxelCount ?? 0) + (p.Target.Dose.DeclaredVoxelCount ?? 0)) * 8;
            // Half the budget per pair: exactly 3 pairs fit in 6 pairs' worth.
            Assert.AreEqual(Math.Min(3, CpuTopology.PhysicalCores), BatchComparer.PairsAtOnce(pairs, new HandWrittenComparer(), 6 * pairBytes));
            Assert.AreEqual(1, BatchComparer.PairsAtOnce(pairs, new HandWrittenComparer(), pairBytes));      // never fewer than 1
            Assert.AreEqual(Math.Min(2, pairs.Length), BatchComparer.PairsAtOnce(pairs, new MultiThreadComparer(), 1000 * pairBytes));
            Assert.AreEqual(Math.Min(CpuTopology.PhysicalCores, pairs.Length), BatchComparer.PairsAtOnce(pairs, new SingleThreadComparer(), 1000 * pairBytes));
        }

        [TestMethod]
        public void RunSettingsAreSavedWithTheStudy()
        {
            var run = new StudyRunSettings(MaximumKind.CaxMax,
                new ComparerSettings(ComparerKind.OpenCl, Precision: GpuPrecision.Float, Device: "Arc"), PairsAtOnce: 3,
                PartialTargetReads: true, Readers: 2, PairsHeld: 4);
            var study = new Study(new[] { new DoseSet("A", "/a"), new DoseSet("B", "/b") }, new[] { new SetComparison("A", "B", PairingRules.Exact(DoseMatchStrategy.Identifier)) }, null, run);
            var back = Study.FromJson(study.ToJson());
            Assert.AreEqual(run, back.Run);
            Assert.AreEqual(new BatchComparer.ReadQueue(2, 4), back.Run.Queue);
            Assert.IsNull(StudyRunSettings.Default.Queue);
        }

        [TestMethod]
        public void AStudySavedBeforeRunSettingsOpensWithTheDefaults()
        {
            string old = """
                { "Version": 1, "Sets": [ { "Name": "A", "Folder": "/a" }, { "Name": "B", "Folder": "/b" } ],
                  "Comparisons": [ { "SourceSet": "A", "TargetSet": "B", "Strategy": "Identifier" } ],
                  "Depths": { "IncludeDmax": true, "DepthsMm": [ 50, 100 ] } }
                """;
            var study = Study.FromJson(old);
            Assert.AreEqual(StudyRunSettings.Default, study.Run);
            Assert.AreEqual(ComparerKind.HandWritten, study.Run.ComparerOrDefault.Kind);
            Assert.IsFalse(study.Run.PartialTargetReads);
        }
    }
}
