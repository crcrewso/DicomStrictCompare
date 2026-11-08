using System;
using System.Collections.Generic;
using DSClibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Why the 100 MU / 101 MU comparisons give the answers they do (backlog D6). The three inherited
    /// expectations in <see cref="X86MathRelAlg"/> disagreed with the engine; the engine was right, and
    /// these tests hold the reasons so the corrected expectations cannot be mistaken for tuning.
    /// </summary>
    [TestClass]
    public class SncBoundaryTests
    {
        private static DoseMatrixOptimal Load(byte[] bytes) =>
            new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(bytes)));

        private static DoseMatrixOptimal Source() => Load(Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1);
        private static DoseMatrixOptimal Target() => Load(Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1);

        /// <summary>A row of doses along x: dims n x 1 x 1, 1 mm spacing, starting at 0.</summary>
        private static DoseMatrixOptimal Row(params double[] doses) =>
            new DoseMatrixOptimal(doses.Length, 1, 1, 0, 0, 0, 1, 1, 1, doses);

        private static IEnumerable<(double, double, double)> PointsOf(DoseMatrixOptimal g)
        {
            for (int i = 0; i < g.DimensionX; i++) { yield return (g.X0 + i * g.XRes, g.Y0, g.Z0); }
        }

        /// <summary>
        /// The premise every corrected expectation rests on. If someone swaps a fixture, this fails first
        /// and names the reason, instead of the expectations quietly drifting.
        /// </summary>
        [TestMethod]
        public void TheTwoFieldsAreAUniformOnePercentApart()
        {
            // Measured outside the engine, 2026-11-08: the two files hold byte-identical pixel data
            // (32,320,800 bytes, the same SHA-256) and differ only in DoseGridScaling,
            // 3.5042182425e-07 against 3.5392604249e-07. That ratio is 1.01 to within 7e-12, so the
            // reader should see a uniform 1% scaling all the way through to cGy.
            var source = Source();
            var target = Target();

            double ratio = target.MaxPointDose.Dose / source.MaxPointDose.Dose;
            Assert.AreEqual(1.01, ratio, 1e-9, "the 101 MU field is a uniform 1% scaling of the 100 MU field");

            // Not only at the maximum: the same ratio at an off-axis point inside the field.
            double s = source.GetPointDose(0, 0, 0).Dose;
            Assert.IsTrue(s > 0, "the grid centre should carry dose");
            Assert.AreEqual(1.01, target.GetPointDose(0, 0, 0).Dose / s, 1e-9);
        }

        /// <summary>
        /// The global bracket. The fields differ by exactly 1%, so a 1% global tolerance is met
        /// everywhere - and the boundary is inclusive by design, which swift's
        /// ToleranceBoundaryIsInclusive asserts deliberately.
        ///
        /// Below one percent the count does not collapse to everything: in global mode the allowance is
        /// 0.01 x maximum while the difference at a voxel is 0.01 x that voxel's dose, so a tolerance of
        /// t fails exactly the voxels above (t / 0.01) x the maximum. Measured: 0.900% -> 345,819
        /// failures, 0.950% -> 198,871, 0.980% -> 92,923, 0.990% -> 41,973, 0.995% -> 12,339,
        /// 0.999% -> 589, 1.000% -> 0. The count slides, which is the shape a dose-volume histogram of
        /// the top percent of the field has.
        ///
        /// It still cannot reach the inherited expectation of 24,277. The count is monotone in the
        /// tolerance, and because the stored pixel values are integers the dose levels are quantised, so
        /// the count moves in blocks. Measured by bisection: it steps from 24,358 to 24,178 at a
        /// tolerance of 0.0099291919911921, skipping 24,277 entirely. Not asserted here - 40 bisection
        /// steps over 8 million voxels is a minute of test time for a fact that is recorded, not watched.
        /// </summary>
        [TestMethod]
        public void TheGlobalToleranceBracketsTheUniformDifference()
        {
            var source = Source();
            var target = Target();
            var mathematics = new X86Mathematics();

            var justUnder = mathematics.CompareRelative(source, target, new Dta(false, 0.1, 0.0099, 0));
            var exactly = mathematics.CompareRelative(source, target, new Dta(false, 0.1, 0.01, 0));
            var justOver = mathematics.CompareRelative(source, target, new Dta(false, 0.1, 0.0101, 0));

            // All three compare the same voxels: the threshold does not depend on the tolerance.
            Assert.AreEqual(2854260, justUnder.TotalCompared);
            Assert.AreEqual(2854260, exactly.TotalCompared);
            Assert.AreEqual(2854260, justOver.TotalCompared);

            Assert.AreEqual(41973, justUnder.TotalFailed,
                "at 0.99% global, the voxels above 99% of the maximum fail and nothing else does");
            Assert.AreEqual(0, exactly.TotalFailed, "equality passes: the boundary is inclusive");
            Assert.AreEqual(0, justOver.TotalFailed);
        }

        /// <summary>
        /// The local bracket, which is the one that jumps. In local mode both the allowance and the
        /// difference are proportional to the voxel's own dose, so every compared voxel gives the same
        /// verdict: all of them fail below 1%, none of them at or above it. 24,277 is unreachable here
        /// for the blunter reason that only two counts are reachable at all.
        /// </summary>
        [TestMethod]
        public void TheLocalToleranceIsAllOrNothingOnAUniformlyScaledPair()
        {
            var source = Source();
            var target = Target();
            var mathematics = new X86Mathematics();

            var justUnder = mathematics.CompareAbsolute(source, target, new Dta(false, 0.1, 0.0099, 0, false));
            var exactly = mathematics.CompareAbsolute(source, target, new Dta(false, 0.1, 0.01, 0, false));

            Assert.AreEqual(2854260, justUnder.TotalCompared);
            Assert.AreEqual(2854260, justUnder.TotalFailed, "every compared voxel fails together");
            Assert.AreEqual(2854260, exactly.TotalCompared);
            Assert.AreEqual(0, exactly.TotalFailed, "and passes together");
        }

        /// <summary>
        /// Why SNC002 calling the wrong mode changed none of its numbers, and why that is luck rather
        /// than licence. On a uniformly scaled pair the two modes agree by construction: the local test
        /// (difference 0.01 x dose against tolerance x dose) and the global test (difference 0.01 x dose
        /// against tolerance x maximum) coincide wherever the dose is above the threshold, which is
        /// exactly the set of compared voxels.
        /// </summary>
        [TestMethod]
        public void GlobalAndLocalCoincideOnThisPair()
        {
            var source = Source();
            var target = Target();
            var mathematics = new X86Mathematics();

            var global = mathematics.CompareRelative(source, target, new Dta(false, 0.1, 0.001, 0, true));
            var local = mathematics.CompareAbsolute(source, target, new Dta(false, 0.1, 0.001, 0, false));

            Assert.AreEqual(global.TotalCompared, local.TotalCompared);
            Assert.AreEqual(global.TotalFailed, local.TotalFailed);
            Assert.AreEqual(2854260, local.TotalFailed, "a 0.1% tolerance admits nothing on a 1% difference");
        }

        /// <summary>
        /// Current behaviour, pinned because it is wrong and the fix is a design decision, not mine:
        /// <b>the whole-grid comparison takes global-or-local from which method you call and ignores
        /// Dta.Type, while CompareAtPoints takes it from Dta.Type.</b> So the same Dta handed to the two
        /// entry points can give two different answers, as this test demonstrates. Every production
        /// caller dispatches on Dta.Global first (MatchedDosePair, BatchComparer), so nothing is
        /// currently wrong in the application - but a Dta's Type can be silently discarded, and a
        /// SingleComparison then carries a Dta whose ShortToString() reports the mode that did not run.
        ///
        /// Reported to Cody 2026-11-09 with two ways out: honour Dta.Type in one Compare(source,
        /// target, dta) and keep the named methods as mode assertions that throw on contradiction, or
        /// delete Dta.Type and let the method name be the only authority. Update this test when he picks.
        /// </summary>
        [TestMethod]
        public void TheWholeGridComparisonTakesTheModeFromTheMethodAndNotTheDta()
        {
            // Reference maximum 100. At the second voxel the difference is 2.9.
            // Global 3% allows 3.0 everywhere and passes it; local 3% allows 0.363 there and fails it.
            var source = Row(100, 12.1);
            var target = Row(100, 15.0);
            var local = new Dta(true, 0, 0.03, 0, relative: false);
            Assert.AreEqual(Dta.CalcType.Local, local.Type);

            var mathematics = new X86Mathematics();

            Assert.AreEqual(1, mathematics.CompareAbsolute(source, target, local).TotalFailed,
                "CompareAbsolute runs the local test the Dta asks for");
            Assert.AreEqual(0, mathematics.CompareRelative(source, target, local).TotalFailed,
                "CompareRelative runs the global test and discards the Dta's Local");
            Assert.AreEqual(1, X86Mathematics.CompareAtPoints(source, target, local, PointsOf(source)).TotalFailed,
                "CompareAtPoints honours the Dta's Local - and so disagrees with CompareRelative above");
        }

        /// <summary>
        /// Current behaviour, pinned for the same reason: <b>Dta.Algorithm is never read.</b> Asking for
        /// gamma silently runs the DTA comparison. There is no gamma implementation yet (backlog D16),
        /// so the honest options are to throw until there is one, or to drop the flag.
        /// </summary>
        [TestMethod]
        public void AskingForGammaSilentlyRunsTheDtaComparison()
        {
            var source = Row(100, 12.1);
            var target = Row(100, 15.0);
            var dta = new Dta(true, 0, 0.03, 0, relative: false, gamma: true);
            Assert.AreEqual(Dta.CalcAlgorithm.gamma, dta.Algorithm, "the request is recorded");

            var mathematics = new X86Mathematics();
            var asGamma = mathematics.CompareAbsolute(source, target, dta);
            var asDta = mathematics.CompareAbsolute(source, target, new Dta(true, 0, 0.03, 0, relative: false));

            Assert.AreEqual(asDta.TotalFailed, asGamma.TotalFailed, "and then ignored");
            Assert.AreEqual(asDta.TotalCompared, asGamma.TotalCompared);
        }
    }
}
