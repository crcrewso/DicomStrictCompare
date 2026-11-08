using System.IO;
using System.Linq;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// The two-pass handler: plans first, then doses matched to them by UID (Cody's design,
    /// 2026-11-08).
    /// </summary>
    [TestClass]
    public class RTFolderReaderTests
    {
        private static RTFolderContents Contents() => RTFolderReader.Read(TestData.ResourcesDirectory!);

        [TestMethod]
        public void ReadsEveryPlanAndDoseInTheFolder()
        {
            var contents = Contents();
            Assert.AreEqual(9, contents.PlansByUid.Count);
            Assert.AreEqual(26, contents.Doses.Count);
            Assert.AreEqual(0, contents.Unreadable.Count,
                string.Join("; ", contents.Unreadable.Select(u => $"{Path.GetFileName(u.Path)}: {u.Reason}")));
        }

        [TestMethod]
        public void EveryDoseIsPairedWithItsPlan()
        {
            var contents = Contents();
            Assert.AreEqual(0, contents.DosesWithoutPlan.Count(),
                "unpaired: " + string.Join(", ",
                    contents.DosesWithoutPlan.Select(d => Path.GetFileName(d.Dose.SourcePath))));
            Assert.IsTrue(contents.Doses.All(d => d.Beam is not null), "every dose resolved a beam");
            Assert.IsFalse(contents.Doses.Any(d => d.HasDanglingBeamReference));
        }

        [TestMethod]
        public void NinePlansServeTwentySixDoses()
        {
            // The many-to-one relationship that makes plans-first worth doing: read each plan once
            // rather than up to nine times.
            var contents = Contents();
            var perPlan = contents.Doses
                .GroupBy(d => d.Plan!.SOPInstanceUID)
                .ToDictionary(g => g.Key!, g => g.Count());

            Assert.AreEqual(9, perPlan.Count);
            Assert.AreEqual(26, perPlan.Values.Sum());
            Assert.AreEqual(9, perPlan.Values.Max(), "the varied-MU plan serves nine doses");
        }

        [TestMethod]
        public void MonitorUnitsArriveFromThePlanBesideTheDose()
        {
            // The information a dose file cannot supply about itself, which is the point of pairing.
            var contents = Contents();
            var varied = contents.Doses
                .Where(d => d.Plan?.RTPlanLabel == "P5 Varied MU" && d.Beam!.BeamName!.StartsWith("X-"))
                .OrderBy(d => d.MonitorUnits)
                .ToArray();

            Assert.AreEqual(3, varied.Length);
            CollectionAssert.AreEqual(new[] { 95.0, 99.0, 101.0 },
                varied.Select(d => d.MonitorUnits!.Value).ToArray());

            // And the dose values are untouched by it: the three differ only in DoseGridScaling.
            CollectionAssert.AreEqual(
                new[] { 2904474u, 2904474u, 2904474u },
                varied.Select(d => d.Dose.MaximumRawVoxel).ToArray());
            Assert.AreEqual(3, varied.Select(d => d.Dose.DoseGridScaling).Distinct().Count());
        }

        [TestMethod]
        public void BeamIdentityArrivesFromThePlanToo()
        {
            var contents = Contents();
            var pairing = contents.Doses.Single(d =>
                Path.GetFileName(d.Dose.SourcePath) == "RD.UnitTest.P5 Varied MU.X-095A-10.0-1.dcm");

            Assert.AreEqual("X-095A-10.0-1", pairing.Beam!.BeamName);
            Assert.AreEqual("PHOTON", pairing.Beam.RadiationType);
            Assert.AreEqual(10.0, pairing.Beam.NominalBeamEnergy!.Value, 1e-9);
            Assert.AreEqual(false, pairing.Beam.IsFluenceModeFff);
            Assert.AreEqual(95.0, pairing.MonitorUnits!.Value, 1e-9);

            var (crossplane, inplane) = pairing.Beam.FieldSizeAtIsocentre;
            Assert.AreEqual(100.0, crossplane!.Value, 1e-9);
            Assert.AreEqual(100.0, inplane!.Value, 1e-9);
        }

        [TestMethod]
        public void ADoseWithoutItsPlanIsStillRead()
        {
            // "Potentially accompanying" - a dose alone keeps its grid, scaling and units, and loses
            // only the beam identity. Read the folder with a pattern that excludes the plans.
            var contents = RTFolderReader.Read(TestData.ResourcesDirectory!, "RD*.dcm");

            Assert.AreEqual(0, contents.PlansByUid.Count);
            Assert.AreEqual(26, contents.Doses.Count);
            Assert.AreEqual(26, contents.DosesWithoutPlan.Count());
            Assert.IsTrue(contents.Doses.All(d => d.MonitorUnits is null));
            Assert.IsTrue(contents.Doses.All(d => d.Dose.DoseGridScaling is not null),
                "the dose itself is unaffected by the plan being absent");
        }

        [TestMethod]
        public void AFolderWithNoRtFilesIsEmptyRatherThanAnError()
        {
            string empty = Path.Combine(Path.GetTempPath(), "dsc-empty-" + Path.GetRandomFileName());
            Directory.CreateDirectory(empty);
            try
            {
                var contents = RTFolderReader.Read(empty);
                Assert.AreEqual(0, contents.Doses.Count);
                Assert.AreEqual(0, contents.PlansByUid.Count);
                Assert.AreEqual(0, contents.Unreadable.Count);
            }
            finally
            {
                Directory.Delete(empty, recursive: true);
            }
        }
    }
}
