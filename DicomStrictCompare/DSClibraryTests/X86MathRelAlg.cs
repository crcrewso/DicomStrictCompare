using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;

namespace DSClibraryTests
{
    /// <summary>
    /// Comparisons of the 100 MU reference field against the 101 MU field.
    ///
    /// Measured premise, 2026-11-08, and every expectation below follows from it: the two files hold
    /// <b>byte-identical pixel data</b> (32,320,800 bytes, the same SHA-256) and differ only in
    /// DoseGridScaling - 3.5042182425e-07 against 3.5392604249e-07, a ratio of 1.01 to within
    /// 7e-12. So the 101 MU field is a <b>uniform 1% scaling</b> of the 100 MU field at every voxel,
    /// which is what varying the monitor units of an otherwise identical plan should produce.
    ///
    /// The old comment on these tests claimed to reproduce a Sun Nuclear Patient analysis:
    /// <c>29 failed, 15523 passed, 15552 compared, DTA, 10, 0.1, 0, Abs</c>. <b>That cannot be
    /// checked here.</b> SNC Patient evaluated 15,552 points; these methods compare the whole
    /// overlapping grid, 8,080,200 voxels. The two numbers are not the same measurement, so no
    /// whole-grid expectation can agree with the SNC one. To compare like for like, pass SNC's own
    /// point list to <see cref="X86Mathematics.CompareAtPoints"/>, which exists for exactly this and
    /// applies identical rules - see backlog D6.
    /// </summary>
    [TestClass]
    public class X86MathRelAlg
    {
        DoseMatrixOptimal source, target;
        IMathematics mathematics;

        byte[] sourceFile, targetFile;

        /// <summary>
        /// Threshhold: 0
        /// Tolerance: 0
        /// Distance: 0
        /// </summary>
        Dta dta00a = new Dta(false, 0, 0, 0, false);

        /// <summary>
        /// Threshhold: 10%
        /// Tolerance: 10%
        /// Distance: 0
        /// Absolute
        /// </summary>
        Dta dta10_01a = new Dta(false, 0.10, 0.1, 0, false);

        /// <summary>
        /// Threshhold: 10%
        /// Tolerance: 10%
        /// Distance: 0
        /// Relative
        /// </summary>
        Dta dta10_01r = new Dta(false, 0.10, 0.1, 0, true);

        /// <summary>
        /// Threshhold: 10%
        /// Tolerance: 1%
        /// Distance: 0
        /// Absolute
        /// </summary>
        Dta dta10_001a = new Dta(false, 0.10, 0.01, 0, false);

        /// <summary>
        /// Threshhold: 10%
        /// Tolerance: 1%
        /// Distance: 0
        /// Relative
        /// </summary>
        Dta dta10_001r = new Dta(false, 0.10, 0.01, 0, true);

        [TestInitialize()]
        public void Initialize()
        {
            mathematics = new X86Mathematics();
        }

        [TestCleanup()]
        public void Cleanup()
        {
            source = null;
            target = null;
            sourceFile = null;
            targetFile = null;

        }

        /// <summary>
        /// Tests that when compared to itself 
        /// </summary>
        [TestMethod()]
        public void SelfComparison()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            SingleComparison result1 = mathematics.CompareAbsolute(source, source, dta00a);
            System.Diagnostics.Debug.WriteLine("Count");
            Assert.AreEqual(8080200, result1.TotalCount);
            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(8080200, result1.TotalCompared);
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(0, result1.TotalFailed);
        }

        [TestMethod()]
        public void Mu100vs101_WidePass()
        {
            // The 100 MU / 101 MU pair: a uniform 1% scaling. See the class summary.
            Dta dta = new Dta(false, 0, 1, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);

            System.Diagnostics.Debug.WriteLine("Count");
            Assert.AreEqual(8080200, result1.TotalCount);
            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(8080200, result1.TotalCompared);
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(0, result1.TotalFailed);
        }

        [TestMethod()]
        public void Mu100vs101_lessWidePass()
        {
            // The 100 MU / 101 MU pair: a uniform 1% scaling. See the class summary.
            Dta dta = new Dta(false, 0, 0.1, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);

            System.Diagnostics.Debug.WriteLine("Count");
            Assert.AreEqual(8080200, result1.TotalCount);
            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(8080200, result1.TotalCompared);
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(0, result1.TotalFailed);
        }

        [TestMethod()]
        public void Mu100vs101_SNC001()
        {
            // The 100 MU / 101 MU pair: a uniform 1% scaling. See the class summary.
            Dta dta = new Dta(false, 0.1, 0.01, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);

            System.Diagnostics.Debug.WriteLine("Count");
            Assert.AreEqual(8080200, result1.TotalCount);
        }
        [TestMethod()]
        public void Mu100vs101_SNC002()
        {
            // The 100 MU / 101 MU pair: a uniform 1% scaling. See the class summary.
            // Local, because relative: false sets Dta.Type to Local. The call below is CompareAbsolute
            // to match it; it used to be CompareRelative, which ran the mode the Dta did not ask for.
            Dta dta = new Dta(false, 0.1, 0.001, 0, false, false, 0);
            Assert.AreEqual(Dta.CalcType.Local, dta.Type, "this test is about the local mode");

            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));

            SingleComparison result1 = mathematics.CompareAbsolute(source, target, dta);

            // A 10% threshold cannot leave every voxel compared, so the old expectation of 8,080,200
            // was unreachable by construction. Measured: 2,854,260 voxels, 35.3% of the grid, sit at
            // or above 10% of the source maximum.
            Assert.AreEqual(8080200, result1.TotalCount, "the whole overlapping grid is still counted");
            Assert.AreEqual(2854260, result1.TotalCompared, "voxels at or above 10% of the source maximum");

            // And with a 0.1% tolerance against a uniform 1% difference, every compared voxel fails.
            Assert.AreEqual(result1.TotalCompared, result1.TotalFailed,
                "a 0.1% tolerance cannot admit a 1% difference anywhere");
        }

        [TestMethod()]
        public void Mu100vs101_SNC003()
        {
            // The 100 MU / 101 MU pair: a uniform 1% scaling. See the class summary.
            Dta dta = new Dta(false, 0.1, 0.01, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);


            // The old expectation of 24,277 failures was not Sun Nuclear's number (SNC said 29 of
            // 15,552) and cannot be reached: with the fields a uniform 1% apart, a 1% global
            // tolerance admits every voxel. allowed = 0.01 x source maximum, while the difference at
            // each voxel is 0.01 x that voxel's dose, which is never larger. Zero failures is
            // arithmetically necessary, not a tuned result. Nor is 24,277 reachable by loosening or
            // tightening: the count is monotone in the tolerance and quantised by the integer pixel
            // values, and it steps from 24,358 straight to 24,178. Bracketed in SncBoundaryTests.
            Assert.AreEqual(2854260, result1.TotalCompared);
            Assert.AreEqual(0, result1.TotalFailed,
                "a 1% global tolerance cannot fail a uniform 1% difference");
        }
        [TestMethod()]
        public void Mu100vs101_SNC004()
        {
            // The 100 MU / 101 MU pair: a uniform 1% scaling. See the class summary.
            Dta dta = new Dta(false, 0, 0.01, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);


            // Threshold 0, so every voxel is compared; the 1% global tolerance still admits them all,
            // for the reason given in SNC003. The old expectation of 0.4% +- 0.1 excluded zero.
            Assert.AreEqual(8080200, result1.TotalCompared, "a zero threshold compares every voxel");
            Assert.AreEqual(0.0, result1.PercentFailed, 1e-12);
        }
    }
}
