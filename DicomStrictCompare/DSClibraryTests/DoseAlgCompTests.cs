using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;

namespace DSClibraryTests
{
    /// <summary>
    /// Compares the various algorithm methods to allow for regression testing. 
    /// </summary>
    [TestClass()]
    public class DoseAlgCompTests : X86Mathematics
    {
        const int threadMin = 1;
        int ThreadMax => Environment.ProcessorCount;
        X86Mathematics mathematics;
        enum DtaTypes { t0d0p0mm, t0d0p0vox, t10d0p0vox, t10d10p10vox, t10d10p10voxRel, t0d0p0mmTrim10, t0d0p0voxTrim10 };

        Dictionary<DtaTypes, Dta> dtas =
            new Dictionary<DtaTypes, Dta>
            {
                [DtaTypes.t0d0p0mm] = new Dta(true, 0, 0, 0, false, false, 0),
                [DtaTypes.t0d0p0vox] = new Dta(false, 0, 0, 0, false, false, 0),
                [DtaTypes.t10d0p0vox] = new Dta(false, 0.10, 0, 0, false, false, 0),
                [DtaTypes.t10d10p10vox] = new Dta(false, 0.10, 0.10, 0, false, false, 0),
                [DtaTypes.t10d10p10voxRel] = new Dta(false, 0.10, 0.10, 0, true, false, 0),
                [DtaTypes.t0d0p0mmTrim10] = new Dta(true, 0, 0, 0, false, false, 10),
                [DtaTypes.t0d0p0voxTrim10] = new Dta(false, 0, 0, 0, false, false, 10)
            };

        DoseMatrixOptimal refDose, targetDose;

        [TestInitialize()]
        public void Initialize()
        {
            mathematics = new X86Mathematics();
        }

        [TestCleanup()]
        public void Cleanup()
        {
            mathematics = null;
        }



        [TestMethod()]
        public void CompareAbsoluteSampleTest()
        {
            byte[] refFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            byte[] targetFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            int expectedFail = 0;

            refDose = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(targetDose.Count, result.TotalCompared); // confirm all voxels are compared 
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(expectedFail, result.TotalFailed); //confirms the number of failed voxels is zero
        }

        [TestMethod()]
        public void CompareRelativeSampleTest()
        {
            byte[] refFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            byte[] targetFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;

            refDose = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result = mathematics.CompareRelative(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            int expectedFail = 0;

            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(targetDose.Count, result.TotalCompared); // confirm all voxels are compared 
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(expectedFail, result.TotalFailed); //confirms the number of failed voxels is zero
        }

    }
}