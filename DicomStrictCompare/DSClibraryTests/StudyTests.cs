using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using DSClibrary.Studies;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Pairing rules (F2) against the committed data. Its AAA and Acuros copies of P1 differ in the
    /// plan label (P1Ref, P1 Acuros) and in the field name (X-100A-10.0-1, X-100X-10.0-1), which is
    /// the cross-algorithm case design/comparison-studies.md describes.
    /// </summary>
    [TestClass]
    public class PairingRulesTests
    {
        private static RTDoseWithPlan[] All() => RTFolderReader.Read(TestData.ResourcesDirectory!).Doses.ToArray();

        private static RTDoseWithPlan[] Plan(string label) =>
            All().Where(d => d.Plan?.RTPlanLabel == label).ToArray();

        private static PairingRules AaaToAcuros() => new(
            DoseMatchStrategy.Identifier,
            labelRules: null,
            labelOverrides: new Dictionary<string, string> { ["P1Ref"] = "P1 Acuros" },
            fieldRules: new[] { new LabelRule("-100[AX]-", "-100{algorithm}-") });

        [TestMethod]
        public void WithoutRulesTheAlgorithmCopiesPairNothing()
        {
            var result = DoseMatcher.Match(Plan("P1Ref"), Plan("P1 Acuros"), DoseMatchStrategy.Identifier);
            Assert.AreEqual(0, result.Pairs.Count, result.ToString());
        }

        [TestMethod]
        public void AnOverrideAndAFieldRulePairTheAlgorithmCopies()
        {
            var result = DoseMatcher.Match(Plan("P1Ref"), Plan("P1 Acuros"), AaaToAcuros());

            // P1Ref has X, F and e; P1 Acuros has X and F only, so the electron stays unmatched.
            Assert.AreEqual(2, result.Pairs.Count, result.ToString());
            foreach (var pair in result.Pairs)
            {
                Assert.AreEqual("P1Ref", pair.Source.Plan!.RTPlanLabel);
                Assert.AreEqual("P1 Acuros", pair.Target.Plan!.RTPlanLabel);
                Assert.AreEqual(pair.Source.Beam!.BeamName![0], pair.Target.Beam!.BeamName![0], pair.ToString());
            }
            Assert.AreEqual(1, result.UnmatchedSource.Count);
            StringAssert.StartsWith(result.UnmatchedSource[0].Dose.Beam!.BeamName, "e-");
        }

        [TestMethod]
        public void NoRuleIsTheExistingMatch()
        {
            var exact = DoseMatcher.Match(All(), All(), DoseMatchStrategy.Identifier);
            var viaRules = DoseMatcher.Match(All(), All(), PairingRules.Exact(DoseMatchStrategy.Identifier));
            Assert.AreEqual(exact.Pairs.Count, viaRules.Pairs.Count);
            Assert.AreEqual(26, viaRules.Pairs.Count);
        }

        [TestMethod]
        public void TheAlgorithmTokenRuleGivesBothLabelsOneKey()
        {
            var rules = PairingRules.AcrossAlgorithms();
            Assert.AreEqual("STBjaw{algorithm}", rules.SourceKeyLabel("STBjawAAA"));
            Assert.AreEqual("STBjaw{algorithm}", rules.TargetKeyLabel("STBjawAXB"));
            // A Small Cube plan keeps its Sm, so it cannot pair with a Large Cube plan.
            Assert.AreEqual("STBjaw{algorithm}Sm", rules.SourceKeyLabel("STBjawAAASm"));
        }

        [TestMethod]
        public void AnOverrideWinsOnBothSides()
        {
            var rules = new PairingRules(DoseMatchStrategy.Identifier,
                new[] { new LabelRule("S", "x") },
                new Dictionary<string, string> { ["SquareFieldsS"] = "SquFldsSAXB" });
            Assert.AreEqual("SquFldsSAXB", rules.SourceKeyLabel("SquareFieldsS"));
            Assert.AreEqual("SquFldsSAXB", rules.TargetKeyLabel("SquFldsSAXB"));
        }

        [TestMethod]
        public void RulesNeedIdentifierMatching()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new PairingRules(
                DoseMatchStrategy.PlanAndBeamUid, new[] { new LabelRule("AAA", "x") }));
        }

        [TestMethod]
        public void ABadPatternSaysWhichPattern()
        {
            var ex = Assert.ThrowsExactly<ArgumentException>(() => new LabelRule("(AAA", "x"));
            StringAssert.Contains(ex.Message, "(AAA");
        }

        [TestMethod]
        public void NearMissesNameTheLabelPairARuleWouldFix()
        {
            // P2 10.1 and P3 11.0 hold the same two fields under two plan labels.
            var result = DoseMatcher.Match(Plan("P2 10.1"), Plan("P3 11.0"), DoseMatchStrategy.Identifier);
            Assert.AreEqual(0, result.Pairs.Count);

            var misses = PairingPreview.NearMisses(result);
            Assert.AreEqual(1, misses.Count, string.Join("; ", misses));
            Assert.AreEqual("P2 10.1", misses[0].SourcePlanLabel);
            Assert.AreEqual("P3 11.0", misses[0].TargetPlanLabel);
            Assert.AreEqual(2, misses[0].FieldCount);
        }

        [TestMethod]
        public void NearMissesApplyTheFieldRules()
        {
            var rules = new PairingRules(DoseMatchStrategy.Identifier, fieldRules: new[] { new LabelRule("-100[AX]-", "-100{algorithm}-") });
            var result = DoseMatcher.Match(Plan("P1Ref"), Plan("P1 Acuros"), rules);
            Assert.AreEqual(0, result.Pairs.Count, "the labels still differ");
            var misses = PairingPreview.NearMisses(result, rules);
            Assert.AreEqual(1, misses.Count);
            Assert.AreEqual(2, misses[0].FieldCount);
        }
    }

    /// <summary>The plan check (F3) and dose warnings (F4), on pairs whose differences are known.</summary>
    [TestClass]
    public class PlanCheckAndWarningsTests
    {
        private static RTDoseWithPlan[] All() => RTFolderReader.Read(TestData.ResourcesDirectory!).Doses.ToArray();

        private static RTDoseWithPlan File(string fragment) =>
            All().Single(d => Path.GetFileName(d.Dose.SourcePath).Contains(fragment, StringComparison.Ordinal));

        [TestMethod]
        public void ADoseAgreesWithItself()
        {
            var dose = File("P1Ref.X-100A");
            var check = PlanCheck.Compare(dose, dose);
            Assert.IsTrue(check.Agrees, check.Summary);
        }

        [TestMethod]
        public void DifferentMonitorUnitsAreReported()
        {
            // The varied-MU plan: the same field at 95 and 101 MU.
            var check = PlanCheck.Compare(File("P5 Varied MU.X-095A"), File("P5 Varied MU.X-101A"));
            Assert.IsTrue(check.Checked);
            var mu = check.Differences.SingleOrDefault(d => d.Setting == "MU");
            Assert.IsNotNull(mu, check.Summary);
            Assert.AreEqual("95", mu.Source);
            Assert.AreEqual("101", mu.Target);
        }

        [TestMethod]
        public void APhotonAgainstAnElectronIsReportedAsSuch()
        {
            var check = PlanCheck.Compare(File("P1Ref.X-100A"), File("P1Ref.e-100A"));
            Assert.IsTrue(check.Differences.Any(d => d.Setting == "radiation"), check.Summary);
            Assert.IsTrue(check.Differences.Any(d => d.Setting == "applicator"), check.Summary);
        }

        [TestMethod]
        public void NoPlanIsNotCheckedRatherThanAgrees()
        {
            var orphans = RTFolderReader.Read(TestData.ResourcesDirectory!, "RD*.dcm").Doses.ToArray();
            var check = PlanCheck.Compare(orphans[0], orphans[1]);
            Assert.IsFalse(check.Checked);
            Assert.IsFalse(check.Agrees);
            StringAssert.StartsWith(check.Summary, "not checked");
        }

        [TestMethod]
        public void TwoGridSpacingsAreAWarningNotARefusal()
        {
            var set = new DoseSet("A", "/a");
            var warnings = DoseWarnings.For(set, set with { Name = "B" }, File("P1 2 mm.X-100A"), File("P1 5 mm.X-100A"));
            var spacing = warnings.SingleOrDefault(w => w.StartsWith("grid spacing", StringComparison.Ordinal));
            Assert.IsNotNull(spacing, string.Join("; ", warnings));
            StringAssert.Contains(spacing, "coarser grid");
        }

        [TestMethod]
        public void TheSameGridHasNoSpacingWarning()
        {
            var set = new DoseSet("A", "/a");
            var dose = File("P1Ref.X-100A");
            Assert.AreEqual(0, DoseWarnings.For(set, set with { Name = "B" }, dose, dose).Count);
        }

        [TestMethod]
        public void AnAcurosSetWithNoReportingDeclaredIsWarnedAbout()
        {
            var aaa = new DoseSet("AAA 15.6", "/a") { Reporting = DoseReporting.DoseToWater };
            var axb = new DoseSet("AXB 15.6", "/b");
            var warning = DoseWarnings.ForSets(aaa, axb).Single();
            StringAssert.Contains(warning, "AXB 15.6");
        }

        [TestMethod]
        public void DifferentDeclaredReportingIsWarnedAbout()
        {
            var a = new DoseSet("X", "/a") { Reporting = DoseReporting.DoseToWater };
            var b = new DoseSet("Y", "/b") { Reporting = DoseReporting.DoseToMedium };
            StringAssert.Contains(DoseWarnings.ForSets(a, b).Single(), "dose to water vs dose to medium");
        }

        [TestMethod]
        public void TwoAaaSetsNeedNoReportingDeclaration()
        {
            Assert.AreEqual(0, DoseWarnings.ForSets(new DoseSet("AAA 15.6", "/a"), new DoseSet("AAA 18", "/b")).Count);
        }
    }

    /// <summary>Studies (F1) and depth settings (F7): validation, parsing and the saved file.</summary>
    [TestClass]
    public class StudyFileTests
    {
        private static Study Sample() => new(
            new[]
            {
                new DoseSet("AAA 15.6", "/exports/aaa156") { Algorithm = "AAA 15.6.06", Reporting = DoseReporting.DoseToWater },
                new DoseSet("AXB 15.6", "/exports/axb156") { Algorithm = "AXB 15.6.06", Reporting = DoseReporting.DoseToMedium, Notes = "Dm" },
            },
            new[]
            {
                new SetComparison("AAA 15.6", "AXB 15.6", new PairingRules(DoseMatchStrategy.Identifier,
                    new[] { new LabelRule("(AAA|AXB)", "{algorithm}") },
                    new Dictionary<string, string> { ["SquareFieldsS"] = "SquFldsSAXB" },
                    new[] { new LabelRule("-100[AX]-", "-100{algorithm}-") })),
            },
            DepthSettings.Parse("dmax, 15, 50, 100, 200, 300"));

        [TestMethod]
        public void AStudySurvivesSavingAndReopening()
        {
            var original = Sample();
            var reopened = Study.FromJson(original.ToJson());

            Assert.AreEqual(2, reopened.Sets.Count);
            Assert.AreEqual(original.Sets[1], reopened.Sets[1]);
            var c = reopened.Comparisons.Single();
            Assert.AreEqual("AAA 15.6 vs AXB 15.6", c.Name);
            Assert.AreEqual("STBjaw{algorithm}", c.Rules.SourceKeyLabel("STBjawAAA"));
            Assert.AreEqual("SquFldsSAXB", c.Rules.SourceKeyLabel("SquareFieldsS"));
            Assert.AreEqual("X-100{algorithm}-10.0-1", c.Rules.KeyFieldName("X-100X-10.0-1"));
            Assert.IsTrue(reopened.Depths.IncludeDmax);
            CollectionAssert.AreEqual(new double[] { 15, 50, 100, 200, 300 }, reopened.Depths.DepthsMm.ToArray());
            Assert.AreEqual(0, reopened.Problems().Count);
        }

        [TestMethod]
        public void ProblemsAreNamed()
        {
            var study = new Study(
                new[] { new DoseSet("A", "/a"), new DoseSet("A", "/b") },
                new[] { new SetComparison("A", "Missing"), new SetComparison("A", "A") });
            var problems = study.Problems();
            Assert.IsTrue(problems.Any(p => p.Contains("More than one dose set is named 'A'")), string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("no dose set is named 'Missing'")));
            Assert.IsTrue(problems.Any(p => p.Contains("compared with itself")));
        }

        [TestMethod]
        public void APlanFilterKeepsOnlyTheNamedPlans()
        {
            var doses = RTFolderReader.Read(TestData.ResourcesDirectory!).Doses;
            var acuros = new DoseSet("Acuros", "/x") { PlanFilter = "Acuros$" };
            var rest = new DoseSet("Rest", "/x") { PlanFilter = "^(?!.*Acuros)" };
            Assert.AreEqual(2, doses.Count(acuros.Includes));
            Assert.AreEqual(24, doses.Count(rest.Includes));
            Assert.AreEqual(26, doses.Count(new DoseSet("All", "/x").Includes));
        }

        [TestMethod]
        public void ABadPlanFilterIsAProblem()
        {
            var study = new Study(new[] { new DoseSet("A", "/a") { PlanFilter = "(AXB" }, new DoseSet("B", "/b") }, new[] { new SetComparison("A", "B") });
            StringAssert.Contains(study.Problems().Single(), "not a valid regular expression");
        }

        [TestMethod]
        public void APlanFilterSurvivesSavingAndReopening()
        {
            var study = new Study(new[] { new DoseSet("A", "/a") { PlanFilter = "AXB$" }, new DoseSet("B", "/b") }, new[] { new SetComparison("A", "B") });
            Assert.AreEqual("AXB$", Study.FromJson(study.ToJson()).Sets[0].PlanFilter);
        }

        [TestMethod]
        public void DepthsParseFromWhatAPersonTypes()
        {
            var d = DepthSettings.Parse("dmax; 5 cm, 100mm 200");
            Assert.IsTrue(d.IncludeDmax);
            CollectionAssert.AreEqual(new double[] { 50, 100, 200 }, d.DepthsMm.ToArray());
            Assert.AreEqual("dmax, 50, 100, 200 mm", d.ToString());

            var noDmax = DepthSettings.Parse("15, 50");
            Assert.IsFalse(noDmax.IncludeDmax);
        }

        [TestMethod]
        public void ADepthThatIsNotANumberIsRefused()
        {
            Assert.ThrowsExactly<FormatException>(() => DepthSettings.Parse("dmax, ten"));
            Assert.ThrowsExactly<FormatException>(() => DepthSettings.Parse(" "));
            Assert.ThrowsExactly<ArgumentException>(() => new DepthSettings(new[] { -5.0 }));
        }

        [TestMethod]
        public void TheDefaultDepthsAreTheGraphsTabsDepths()
        {
            Assert.AreEqual("dmax, 50, 100, 200 mm", DepthSettings.Default.ToString());
        }
    }
}

namespace DSClibraryTests
{
    /// <summary>The header-only read that lets a study pair thousands of doses without their voxels (F12).</summary>
    [TestClass]
    public class HeaderOnlyReadTests
    {
        [TestMethod]
        public void TheHeaderIsEverythingButThePixelData()
        {
            foreach (string path in TestData.DoseFiles.Take(4))
            {
                var full = DicomFileReader.ReadFile(path);
                var header = DicomFileReader.ReadHeaderFile(path);
                Assert.IsTrue(header.HeaderOnly, path);
                Assert.IsNull(header.Dataset.Element(DicomTags.PixelData), path);
                Assert.IsNotNull(full.Dataset.Element(DicomTags.PixelData), path);
                Assert.AreEqual(full.Dataset.Count - 1, header.Dataset.Count, Path.GetFileName(path));

                var a = RTDoseResult.From(full);
                var b = RTDoseResult.From(header);
                Assert.AreEqual(a.DoseGridScaling, b.DoseGridScaling);
                Assert.AreEqual(a.ReferencedRTPlanSOPInstanceUID, b.ReferencedRTPlanSOPInstanceUID);
                Assert.AreEqual(a.ReferencedBeamNumber, b.ReferencedBeamNumber);
                CollectionAssert.AreEqual(a.GridFrameOffsetVector.ToArray(), b.GridFrameOffsetVector.ToArray());
                CollectionAssert.AreEqual(a.ImagePositionPatient.ToArray(), b.ImagePositionPatient.ToArray());
            }
        }

        [TestMethod]
        public void AHeaderOnlyDoseRefusesToHandOutVoxels()
        {
            var dose = RTDoseResult.From(DicomFileReader.ReadHeaderFile(TestData.DoseFiles[0]));
            Assert.IsTrue(dose.HeaderOnly);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = dose.MaximumRawVoxel);
            Assert.ThrowsExactly<InvalidOperationException>(() => dose.CopyScaledTo(new double[10], 1.0));
        }

        [TestMethod]
        public void AHeaderKeepsOnlyItsOwnBytes()
        {
            foreach (string path in TestData.DoseFiles.Take(4))
            {
                using var stream = File.OpenRead(path);
                var header = DicomFileReader.ReadHeader(stream, path, out long pixelDataAt);
                Assert.IsTrue(pixelDataAt > 0, path);
                var backing = new HashSet<int>();
                void Walk(DicomDataset d)
                {
                    foreach (var tag in d.Tags)
                    {
                        var e = d.Element(tag)!;
                        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(e.Value, out var seg) && seg.Array is { } a)
                            backing.Add(a.Length);
                        foreach (var item in e.Items ?? []) Walk(item);
                    }
                }
                Walk(header.Dataset);
                // One buffer, exactly the header: not the 64 kB (or larger) prefix it was found in.
                CollectionAssert.AreEqual(new[] { (int)pixelDataAt }, backing.ToArray(), Path.GetFileName(path));
            }
        }

        [TestMethod]
        public void AFolderReadHeadersOnlyPairsTheSameAsAFullRead()
        {
            var full = RTFolderReader.Read(TestData.ResourcesDirectory!);
            var headers = RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true);
            Assert.AreEqual(full.Doses.Count, headers.Doses.Count);
            Assert.IsTrue(headers.Doses.All(d => d.Dose.HeaderOnly && d.Plan is not null && d.Beam is not null));
            foreach (var strategy in new[] { DoseMatchStrategy.Identifier, DoseMatchStrategy.PlanAndBeamUid })
                Assert.AreEqual(26, DoseMatcher.Match(headers.Doses, headers.Doses, strategy).Pairs.Count, strategy.ToString());
        }
    }
}
