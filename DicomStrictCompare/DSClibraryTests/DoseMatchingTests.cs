using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    [TestClass]
    public class DicomClassifierTests
    {
        [TestMethod]
        public void ClassifiesEveryFileWithoutReadingItWhole()
        {
            foreach (string path in TestData.DoseFiles)
                Assert.AreEqual(DicomModality.RTDose, DicomClassifier.Classify(path), Path.GetFileName(path));
            foreach (string path in TestData.PlanFiles)
                Assert.AreEqual(DicomModality.RTPlan, DicomClassifier.Classify(path), Path.GetFileName(path));
        }

        [TestMethod]
        public void ClassifyingIsMuchCheaperThanAFullRead()
        {
            // The reason it exists: the reference doses are about 32 MB each, so classifying
            // twenty-six by reading them whole moves about 800 MB to learn one attribute.
            var prefix = Stopwatch.StartNew();
            foreach (string path in TestData.DoseFiles) DicomClassifier.Classify(path);
            prefix.Stop();

            var whole = Stopwatch.StartNew();
            foreach (string path in TestData.DoseFiles) _ = DicomFileReader.ReadFile(path).IsRTDose;
            whole.Stop();

            Console.WriteLine($"prefix {prefix.ElapsedMilliseconds} ms vs whole-file {whole.ElapsedMilliseconds} ms "
                              + $"for {TestData.DoseFiles.Length} files");
            Assert.IsTrue(prefix.ElapsedMilliseconds <= whole.ElapsedMilliseconds,
                "the prefix read should not be slower");
        }

        [TestMethod]
        public void SomethingThatIsNotDicomIsUnknown()
        {
            string temp = Path.Combine(Path.GetTempPath(), "dsc-not-dicom-" + Path.GetRandomFileName());
            File.WriteAllText(temp, new string('x', 2000));
            try
            {
                Assert.AreEqual(DicomModality.Unknown, DicomClassifier.Classify(temp));
            }
            finally
            {
                File.Delete(temp);
            }
        }
    }

    [TestClass]
    public class DoseMatchingTests
    {
        private static RTDoseWithPlan[] All() =>
            RTFolderReader.Read(TestData.ResourcesDirectory!).Doses.ToArray();

        private static RTDoseWithPlan[] Named(string fragment) =>
            All().Where(d => Path.GetFileName(d.Dose.SourcePath).Contains(fragment, StringComparison.Ordinal)).ToArray();

        [TestMethod]
        public void MatchingASetAgainstItselfPairsEverything()
        {
            foreach (var strategy in new[] { DoseMatchStrategy.Identifier, DoseMatchStrategy.PlanAndBeamUid })
            {
                var result = DoseMatcher.Match(All(), All(), strategy);
                Assert.AreEqual(26, result.Pairs.Count, $"{strategy}: {result}");
                Assert.AreEqual(0, result.UnmatchedSource.Count, $"{strategy}: "
                    + string.Join("; ", result.UnmatchedSource.Select(u => u.ToString())));
                Assert.AreEqual(0, result.UnmatchedTarget.Count, strategy.ToString());
                Assert.AreEqual(0, result.AmbiguousKeys.Count, strategy.ToString());
            }
        }

        [TestMethod]
        public void BothStrategiesProduceTheSameKeyCountOnThisData()
        {
            // 26 distinct doses, so 26 distinct keys either way - no two share a patient, plan and
            // field, and no two share a plan UID and beam number.
            foreach (var strategy in new[] { DoseMatchStrategy.Identifier, DoseMatchStrategy.PlanAndBeamUid })
            {
                var keys = All().Select(d => DoseMatchKey.For(d, strategy)).ToArray();
                Assert.IsTrue(keys.All(k => k is not null), strategy.ToString());
                Assert.AreEqual(26, keys.Distinct().Count(), strategy.ToString());
            }
        }

        [TestMethod]
        public void IdentifierMatchingCannotCrossAReplanBecauseThePlanLabelIsInTheKey()
        {
            // P2 is labelled "P2 10.1" and P3 "P3 11.0" - two TPS versions of the same fields. Their
            // beam names coincide, but the plan labels do not, so neither strategy pairs them.
            //
            // That is the finding, and it matters: a source-versus-target comparison across a TPS
            // upgrade is exactly the case this tool exists for, and NEITHER key matches it. The
            // identifier key includes the plan label, so it is no more portable than the UID here.
            var p2 = Named("P2 10.1");
            var p3 = Named("P3 11.0");
            Assert.AreEqual(2, p2.Length);
            Assert.AreEqual(2, p3.Length);

            foreach (var strategy in new[] { DoseMatchStrategy.Identifier, DoseMatchStrategy.PlanAndBeamUid })
            {
                var result = DoseMatcher.Match(p2, p3, strategy);
                Assert.AreEqual(0, result.Pairs.Count, $"{strategy} unexpectedly paired across plans");
            }

            // The beam names really are the same, which is what makes a patient-and-field key able to
            // cross a re-plan where a patient-plan-and-field key cannot.
            CollectionAssert.AreEquivalent(
                p2.Select(d => d.Beam!.BeamName).ToArray(),
                p3.Select(d => d.Beam!.BeamName).ToArray());
        }

        [TestMethod]
        public void AnUnmatchableDoseIsReportedRatherThanGivenARandomKey()
        {
            // Read the doses with no plans available, so neither key can be formed.
            var orphans = RTFolderReader.Read(TestData.ResourcesDirectory!, "RD*.dcm").Doses.ToArray();

            var byIdentifier = DoseMatcher.Match(orphans, orphans, DoseMatchStrategy.Identifier);
            Assert.AreEqual(0, byIdentifier.Pairs.Count);
            Assert.AreEqual(26, byIdentifier.UnmatchedSource.Count);
            StringAssert.Contains(byIdentifier.UnmatchedSource[0].Reason, "plan was not found");

            // The UID key needs nothing from the plan, so it still works.
            var byUid = DoseMatcher.Match(orphans, orphans, DoseMatchStrategy.PlanAndBeamUid);
            Assert.AreEqual(26, byUid.Pairs.Count);
        }

        [TestMethod]
        public void AKeyClaimedTwiceIsAmbiguousRatherThanSilentlyResolved()
        {
            // A source set containing the same dose twice: picking one arbitrarily would compare a
            // file the user did not choose and say nothing about it.
            var one = Named("P1Ref.X-100A");
            Assert.AreEqual(1, one.Length);
            var doubled = one.Concat(one).ToArray();

            var result = DoseMatcher.Match(doubled, one, DoseMatchStrategy.PlanAndBeamUid);

            Assert.AreEqual(0, result.Pairs.Count);
            Assert.AreEqual(1, result.AmbiguousKeys.Count);
            StringAssert.Contains(result.AmbiguousKeys[0], "more than one");
            StringAssert.Contains(result.UnmatchedSource.Single().Reason, "ambiguous");
        }

        [TestMethod]
        public void TheKeySaysWhichPartDiffered()
        {
            var dose = Named("P1Ref.X-100A").Single();
            var identifier = DoseMatchKey.For(dose, DoseMatchStrategy.Identifier)!;

            Assert.AreEqual("UnitTest", identifier.PatientId);
            Assert.AreEqual("P1Ref", identifier.PlanLabel);
            Assert.AreEqual("X-100A-10.0-1", identifier.BeamName);
            StringAssert.Contains(identifier.ToString(), "P1Ref");

            var uid = DoseMatchKey.For(dose, DoseMatchStrategy.PlanAndBeamUid)!;
            Assert.IsNotNull(uid.PlanSopInstanceUid);
            Assert.IsNotNull(uid.BeamNumber);
        }

        [TestMethod]
        public void TheTwoStrategiesAreNotInterchangeableAsKeys()
        {
            var dose = Named("P1Ref.X-100A").Single();
            Assert.AreNotEqual(
                (object)DoseMatchKey.For(dose, DoseMatchStrategy.Identifier)!,
                DoseMatchKey.For(dose, DoseMatchStrategy.PlanAndBeamUid)!);
        }
    }
}
