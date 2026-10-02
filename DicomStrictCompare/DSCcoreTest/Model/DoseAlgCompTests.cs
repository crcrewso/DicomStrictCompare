using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DCSCore.Model.Tests
{
    /// <summary>
    /// Compares the various algorithm methods to allow for regression testing. 
    /// </summary>
    [TestClass()]
    public class DoseAlgCompTests: X86Mathematics
    {
        const int threadMin = 1;
        int ThreadMax => System.Environment.ProcessorCount;
        X86Mathematics mathematics;
        enum DtaTypes { t0d0p0mm, t0d0p0vox, t10d0p0vox, t10d10p10vox, t10d10p10voxRel, t0d0p0mmTrim10, t0d0p0voxTrim10 };

        System.Collections.Generic.Dictionary<DtaTypes, Model.Dta> dtas =             
            new System.Collections.Generic.Dictionary<DtaTypes, Dta>{
                [DtaTypes.t0d0p0mm] = new Model.Dta(true, 0, 0, 0, false, false, 0),
                [DtaTypes.t0d0p0vox] = new Model.Dta(false, 0, 0, 0, false, false, 0),
                [DtaTypes.t10d0p0vox] = new Model.Dta(false, 0.10, 0, 0, false, false, 0),
                [DtaTypes.t10d10p10vox] = new Model.Dta(false, 0.10, 0.10, 0, false, false, 0),
                [DtaTypes.t10d10p10voxRel] = new Model.Dta(false, 0.10, 0.10, 0, true, false, 0),
                [DtaTypes.t0d0p0mmTrim10] = new Model.Dta(true, 0, 0, 0, false, false, 10),
                [DtaTypes.t0d0p0voxTrim10] = new Model.Dta(false, 0, 0, 0, false, false, 10)
            }; 

    Model.DoseMatrixOptimal refDose, targetDose;

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
            byte[] refFile = DSCcoreTest.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            byte[] targetFile = DSCcoreTest.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            int expectedFail = 0;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(targetDose.Count, result.TotalCompared); // confirm all voxels are compared 
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(expectedFail, result.TotalFailed); //confirms the number of failed voxels is zero
        }

        [TestMethod()]
        public void CompareRelativeSampleTest()
        {
            byte[] refFile = DSCcoreTest.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            byte[] targetFile = DSCcoreTest.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result = mathematics.CompareRelative(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            int expectedFail = 0;

            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(targetDose.Count, result.TotalCompared); // confirm all voxels are compared 
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(expectedFail, result.TotalFailed); //confirms the number of failed voxels is zero
        }

    }
}