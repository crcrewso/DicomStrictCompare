using System;
using DSClibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Diagnosing the three inherited Sun Nuclear known-answer failures (backlog D6) by running the
    /// comparison those tests actually configure, rather than the one they call.
    /// </summary>
    [TestClass]
    public class SncDiagnosisTests
    {
        private static DoseMatrixOptimal Load(byte[] bytes) =>
            new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(bytes)));

        [TestMethod]
        public void ReportGlobalAgainstLocalForTheSncPair()
        {
            // The pair SNC003 uses: a 100 MU dose and a 101 MU dose whose stored voxels are
            // byte-identical and whose DoseGridScaling differs by exactly 1.01. So the two fields are
            // a uniform 1% scaling of one another.
            var source = Load(Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1);
            var target = Load(Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1);
            var mathematics = new X86Mathematics();

            // SNC003's own Dta: useMM false, threshold 0.1, tolerance 0.01, distance 0,
            // relative FALSE - which sets Dta.Type to Local, a property nothing reads.
            var dta = new Dta(false, 0.1, 0.01, 0, false, false, 0);
            Console.WriteLine($"Dta.Type as configured = {dta.Type} (never read by the engine)");

            var global = mathematics.CompareRelative(source, target, dta);
            var local = mathematics.CompareAbsolute(source, target, dta);

            Console.WriteLine($"  CompareRelative (global): {global.TotalCompared:N0} compared, "
                              + $"{global.TotalFailed:N0} failed  <- what the test calls");
            Console.WriteLine($"  CompareAbsolute (local) : {local.TotalCompared:N0} compared, "
                              + $"{local.TotalFailed:N0} failed  <- what the Dta asks for");
            Console.WriteLine($"  SNC003 expects 24,277 failed; SNC002 expects 8,080,200 compared "
                              + $"(the full voxel count, which a 0.1 threshold forbids).");

            Assert.IsTrue(true, "a report, not a gate");
        }

        [TestMethod]
        public void TheDifferenceSitsExactlyOnTheToleranceBoundary()
        {
            // The explanation, isolated. The fields differ by exactly 1%, so a 1% tolerance is met
            // with equality at every point - and the boundary is inclusive by design, which swift's
            // ToleranceBoundaryIsInclusive asserts deliberately. Nudge the tolerance a hair below one
            // percent and everything above the threshold fails; a hair above and nothing does.
            var source = Load(Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1);
            var target = Load(Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1);
            var mathematics = new X86Mathematics();

            var justUnder = mathematics.CompareRelative(source, target, new Dta(false, 0.1, 0.0099, 0));
            var exactly = mathematics.CompareRelative(source, target, new Dta(false, 0.1, 0.01, 0));
            var justOver = mathematics.CompareRelative(source, target, new Dta(false, 0.1, 0.0101, 0));

            Console.WriteLine($"  tolerance 0.99%: {justUnder.TotalFailed:N0} failed of {justUnder.TotalCompared:N0}");
            Console.WriteLine($"  tolerance 1.00%: {exactly.TotalFailed:N0} failed of {exactly.TotalCompared:N0}");
            Console.WriteLine($"  tolerance 1.01%: {justOver.TotalFailed:N0} failed of {justOver.TotalCompared:N0}");

            Assert.AreEqual(0, exactly.TotalFailed, "equality passes: the boundary is inclusive");
            Assert.AreEqual(0, justOver.TotalFailed);
            Assert.IsTrue(justUnder.TotalFailed > 0,
                "a tolerance below the uniform difference must fail points");
        }
    }
}
