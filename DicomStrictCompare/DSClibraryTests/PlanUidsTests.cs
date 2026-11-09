using System;
using System.Linq;
using System.Text;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    [TestClass]
    public class PlanUidsTests
    {
        // The shape of the two UIDs one Eclipse plan carried in Cody's PRD-Before and PRD-After
        // exports: same root, installation and serial, saved 2024-07-27 and again 2026-08-21. The
        // installation and serial components are made up; the real ones identify a clinic's system.
        private const string Before = "1.2.246.352.71.5.123456789012.654321.20240727205652";
        private const string After = "1.2.246.352.71.5.123456789012.654321.20260821191739";
        private const string Serial = "1.2.246.352.71.5.123456789012.654321";

        [TestMethod]
        public void TheSaveTimeIsDroppedAndTheSerialKept()
        {
            Assert.AreEqual(Serial, PlanUids.WithoutSaveTime(Before));
            Assert.AreEqual(Serial, PlanUids.WithoutSaveTime(After));
            Assert.IsTrue(PlanUids.EndsWithSaveTime(Before));
            Assert.AreEqual(PlanUids.WithoutSaveTime(Before), PlanUids.WithoutSaveTime(After));
        }

        [TestMethod]
        public void TrailingPaddingDoesNotHideTheSaveTime()
        {
            // A UID of odd length is padded with one NUL in the file.
            Assert.AreEqual(Serial, PlanUids.WithoutSaveTime(Before + "\0"));
            Assert.AreEqual(Serial, PlanUids.WithoutSaveTime(Before + " "));
        }

        [TestMethod]
        public void OnlyAPlausibleDateAndTimeIsDropped()
        {
            Assert.AreEqual(Serial + ".20241399999999", PlanUids.WithoutSaveTime(Serial + ".20241399999999"), "month 13");
            Assert.AreEqual(Serial + ".20240727245652", PlanUids.WithoutSaveTime(Serial + ".20240727245652"), "hour 24");
            Assert.AreEqual(Serial + ".2024072720565", PlanUids.WithoutSaveTime(Serial + ".2024072720565"), "13 digits");
            Assert.AreEqual(Serial + ".202407272056521", PlanUids.WithoutSaveTime(Serial + ".202407272056521"), "15 digits");
            Assert.AreEqual(Serial + ".2024072720565a", PlanUids.WithoutSaveTime(Serial + ".2024072720565a"), "not digits");
            Assert.AreEqual("1.20240727205652", PlanUids.WithoutSaveTime("1.20240727205652"), "two components only");
            Assert.AreEqual("20240727205652", PlanUids.WithoutSaveTime("20240727205652"), "no dot");
            Assert.IsFalse(PlanUids.EndsWithSaveTime(Serial));
        }

        [TestMethod]
        public void TheAnonymisedReferencePlansAreLeftAlone()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            var plans = RTFolderReader.Read(TestData.ResourcesDirectory!).Plans.ToArray();
            Assert.AreEqual(9, plans.Length);
            foreach (var plan in plans)
            {
                string uid = plan.SOPInstanceUID!;
                Assert.AreEqual(uid.TrimEnd('\0'), PlanUids.WithoutSaveTime(uid), uid);
                Assert.IsFalse(PlanUids.EndsWithSaveTime(uid), uid);
            }
        }

        [TestMethod]
        public void ADoseOfAPlanSavedTwicePairsByUidAcrossTheTwoSaves()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");

            // Take one reference dose and give its plan reference an Eclipse-shaped UID, once per save.
            // The replacement keeps the byte length, so the sequence's lengths stay right.
            var original = DicomFileReader.ReadFile(TestData.DoseFiles[0]);
            var dose = RTDoseResult.From(original);
            string referenced = dose.ReferencedRTPlanSOPInstanceUID!;
            var sequence = original.Dataset.Element(DicomTags.ReferencedRTPlanSequence)!;
            Assert.IsNull(sequence.Items, "the reference files use defined-length sequences, so the bytes are at hand");

            const string root = "1.2.246.352.71.5.123456789012.";
            int serialDigits = referenced.Length - (root + ".20240727205652").Length;
            Assert.IsTrue(serialDigits >= 1, "the reference UID is long enough to shape like Eclipse's");
            string serial = new string('7', serialDigits);
            string before = root + serial + ".20240727205652";
            string after = root + serial + ".20260821191739";
            Assert.AreEqual(referenced.Length, before.Length);

            RTDoseWithPlan Rewritten(string uid)
            {
                byte[] bytes = sequence.Value.ToArray();
                byte[] find = Encoding.ASCII.GetBytes(referenced);
                int at = bytes.AsSpan().IndexOf(find);
                Assert.IsTrue(at >= 0, "the referenced UID is inside the sequence bytes");
                Encoding.ASCII.GetBytes(uid).CopyTo(bytes, at);
                var dataset = original.Dataset.WithElement(DicomTags.ReferencedRTPlanSequence, bytes);
                var file = DicomFileReader.Read(DicomFileWriter.ToBytes(dataset), uid);
                return new RTDoseWithPlan(RTDoseResult.From(file), null, null, null);
            }

            var saveOne = Rewritten(before);
            var saveTwo = Rewritten(after);
            Assert.AreEqual(before, saveOne.Dose.ReferencedRTPlanSOPInstanceUID);
            Assert.AreEqual(after, saveTwo.Dose.ReferencedRTPlanSOPInstanceUID);
            Assert.AreNotEqual(saveOne.Dose.ReferencedRTPlanSOPInstanceUID, saveTwo.Dose.ReferencedRTPlanSOPInstanceUID);

            var keyOne = DoseMatchKey.For(saveOne, DoseMatchStrategy.PlanAndBeamUid)!;
            var keyTwo = DoseMatchKey.For(saveTwo, DoseMatchStrategy.PlanAndBeamUid)!;
            Assert.AreEqual(root + serial, keyOne.PlanSopInstanceUid);
            Assert.AreEqual(keyOne, keyTwo, "the key ignores the save time");

            var match = DoseMatcher.Match(new[] { saveOne }, new[] { saveTwo }, DoseMatchStrategy.PlanAndBeamUid);
            Assert.AreEqual(1, match.Pairs.Count, match.ToString());
            Assert.AreEqual(0, match.UnmatchedSource.Count + match.UnmatchedTarget.Count);

            // And a different serial is still a different plan.
            var other = Rewritten(root + new string('8', serialDigits) + ".20260821191739");
            Assert.AreEqual(0, DoseMatcher.Match(new[] { saveOne }, new[] { other }, DoseMatchStrategy.PlanAndBeamUid).Pairs.Count);
        }
    }
}
