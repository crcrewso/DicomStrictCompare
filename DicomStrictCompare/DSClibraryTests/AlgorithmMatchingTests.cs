using System;
using System.IO;
using System.Linq;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>The dose algorithm in the pairing key (crcrewso, 2026-11-16): from names, else the folder's.</summary>
    [TestClass]
    public class AlgorithmMatchingTests
    {
        private static RTDoseWithPlan[] Named(string fragment) =>
            RTFolderReader.Read(TestData.ResourcesDirectory!).Doses
                .Where(d => Path.GetFileName(d.Dose.SourcePath).Contains(fragment, StringComparison.Ordinal)).ToArray();

        [TestMethod]
        public void TheListParsesAndPrintsTheSame()
        {
            var t = AlgorithmTokens.Parse("AAA = AAA; AXB = AXB, Acuros; eMC");
            Assert.AreEqual("AAA = AAA; AXB = AXB, Acuros; eMC = eMC", t.ToString());
            Assert.AreEqual(t.ToString(), AlgorithmTokens.Parse(t.ToString()).ToString());
            Assert.ThrowsExactly<FormatException>(() => AlgorithmTokens.Parse("= AAA"));
        }

        [TestMethod]
        public void TokensAreFoundAnywhereIgnoringCase()
        {
            var t = AlgorithmTokens.Default;
            CollectionAssert.AreEqual(new[] { "AAA" }, t.Named("AiXflatXaaa").ToArray());
            CollectionAssert.AreEqual(new[] { "AXB" }, t.Named("STBflatXaxbSm").ToArray());
            CollectionAssert.AreEqual(new[] { "AXB" }, t.Named("P1 Acuros").ToArray());
            Assert.AreEqual(0, t.Named("AllCFields").Count);
            Assert.AreEqual(3, t.Named("AAA/AXB/eMC 11").Count, "a set described with all three names none");
        }

        [TestMethod]
        public void NamesWinThenTheFolderThenNothing()
        {
            var t = AlgorithmTokens.Default;
            var acuros = Named("P1 Acuros.X-100X").First();
            Assert.AreEqual("AXB", t.Of(acuros, fallback: "AAA"), "the plan label names it; the folder is not asked");
            var reference = Named("P1Ref.X-100A").Single();
            Assert.IsNull(t.Of(reference));
            Assert.AreEqual("AAA", t.Of(reference, "AAA 15.6"));
            Assert.IsNull(t.Of(reference, "AAA/AXB/eMC 11"), "a folder naming several algorithms gives none");
        }

        [TestMethod]
        public void DifferentAlgorithmsDoNotPairAndTheSameDo()
        {
            var doses = Named("P1Ref.");
            var rules = PairingRules.Exact(DoseMatchStrategy.PlanUidAndBeamName);
            Assert.IsTrue(rules.KeysAlgorithm);

            var apart = DoseMatcher.Match(doses, doses, rules, sourceAlgorithm: "AAA", targetAlgorithm: "AXB");
            Assert.AreEqual(0, apart.Pairs.Count, "AAA against AXB pairs nothing");

            var together = DoseMatcher.Match(doses, doses, rules, sourceAlgorithm: "AAA", targetAlgorithm: "AAA");
            Assert.AreEqual(doses.Length, together.Pairs.Count);
            StringAssert.EndsWith(together.Pairs[0].Key.ToString(), "[AAA]");

            var unknown = DoseMatcher.Match(doses, doses, rules);
            Assert.AreEqual(doses.Length, unknown.Pairs.Count, "unknown pairs with unknown, as before");
            Assert.IsNull(unknown.Pairs[0].Key.Algorithm);
        }

        [TestMethod]
        public void RulesThatPairAcrossAlgorithmsLeaveTheAlgorithmOut()
        {
            Assert.IsFalse(PairingRules.AcrossAlgorithms().KeysAlgorithm, "{algorithm} rules exist to pair across them");
            var field = new PairingRules(DoseMatchStrategy.Identifier, fieldRules: new[] { new LabelRule("-100[AX]-", "-100{algorithm}-") });
            Assert.IsFalse(field.KeysAlgorithm);
            Assert.IsFalse((PairingRules.Exact(DoseMatchStrategy.Identifier) with { Algorithms = null }).KeysAlgorithm);
        }
    }
}
