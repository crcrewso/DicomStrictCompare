using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DicomStrictCompare.Model.Tests
{
    [TestClass()]
    public class DoseComparisonsTests: X86Mathematics
    {
        int threadMin = 1;
        int threadMax = System.Environment.ProcessorCount;
        X86Mathematics mathematics;
        enum DtaTypes { t0d0p0mm, t0d0p0vox, t10d0p0vox, t10d10p10vox, t10d10p10voxRel, t0d0p0mmTrim10, t0d0p0voxTrim10 };

        System.Collections.Generic.Dictionary<DtaTypes, Model.Dta> dtas;
        Model.DoseMatrixOptimal refDose, targetDose;

        [TestInitialize()]
        public void Initialize()
        {
            mathematics = new X86Mathematics();
            dtas = new System.Collections.Generic.Dictionary<DtaTypes, Model.Dta>();
            dtas[DtaTypes.t0d0p0mm] = new Model.Dta(true, 0, 0, 0, false, 0);
            dtas[DtaTypes.t0d0p0vox] = new Model.Dta(false, 0, 0, 0, false, 0);
            dtas[DtaTypes.t10d0p0vox] = new Model.Dta(false, 0.10, 0, 0, false, 0);
            dtas[DtaTypes.t10d10p10vox] = new Model.Dta(false, 0.10, 0.10, 0, false, 0);
            dtas[DtaTypes.t10d10p10voxRel] = new Model.Dta(false, 0.10, 0.10, 0, true, 0);
            dtas[DtaTypes.t0d0p0mmTrim10] = new Model.Dta(true, 0, 0, 0, false, 10);
            dtas[DtaTypes.t0d0p0voxTrim10] = new Model.Dta(false, 0, 0, 0, false, 10);
        }

        

        [TestMethod()]
        public void CompareAbsoluteSampleTest()
        {
            byte[] refFile = DSCTests.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            byte[] targetFile = DSCTests.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            int expectedFail = 0;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);

            Assert.AreEqual(targetDose.Count, result.TotalCompared); // confirm all voxels are compared 
            Assert.AreEqual(expectedFail, result.TotalFailed); //confirms the number of failed voxels is zero
        }

        [TestMethod()]
        public void CompareRelativeSampleTest()
        {
            byte[] refFile = DSCTests.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            byte[] targetFile = DSCTests.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result = mathematics.CompareRelative(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            int expectedFail = 0;

            Assert.AreEqual(targetDose.Count, result.TotalCompared); // confirm all voxels are compared 
            Assert.AreEqual(expectedFail, result.TotalFailed); //confirms the number of failed voxels is zero
        }

        [TestMethod()]
        public void CompareParallelVsAbsoluteThreadMin()
        {
            byte[] refFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;
            byte[] targetFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result1 = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            SingleComparison result2 = mathematics.CompareParallel(refDose, targetDose, dtas[DtaTypes.t0d0p0mm], threadMin, Type.absolute);

            Assert.AreEqual(result1, result2); // confirm all voxels are compared 
        }

        [TestMethod()]
        public void CompareParallelVsAbsoluteThreadMax1_1()
        {
            byte[] refFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;
            byte[] targetFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result1 = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            SingleComparison result2 = mathematics.CompareParallel(refDose, targetDose, dtas[DtaTypes.t0d0p0mm], threadMax, Type.absolute);

            Assert.AreEqual(result1, result2); //confirms the number of failed voxels is zero
        }
        [TestMethod()]
        public void CompareParallelVsAbsoluteThreadMax1_2()
        {
            byte[] refFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;
            byte[] targetFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result1 = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            SingleComparison result2 = mathematics.CompareParallel(refDose, targetDose, dtas[DtaTypes.t0d0p0mm], threadMax, Type.absolute);

            Assert.AreEqual(result1.TotalCompared, result2.TotalCompared); // confirm all voxels are compared 
        }
        [TestMethod()]
        public void CompareParallelVsAbsoluteThreadMax2()
        {
            byte[] refFile = DSCTests.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            byte[] targetFile = DSCTests.Properties.Resources.RD_UnitTest_P5_Varied_MU_X_095A_10_0_1;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result1 = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            SingleComparison result2 = mathematics.CompareParallel(refDose, targetDose, dtas[DtaTypes.t0d0p0mm], threadMax, Type.absolute);

            Assert.AreEqual(result1.TotalCompared, result2.TotalCompared); // confirm all voxels are compared 
            Assert.AreEqual(result1.TotalFailed, result2.TotalFailed); //confirms the number of failed voxels is zero
        }
        [TestMethod()]
        public void CompareParallelVsAbsoluteThreadMax3()
        {
            byte[] refFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;
            byte[] targetFile = DSCTests.Properties.Resources.RD_UnitTest_P1_5_mm_F_100A_10_0_5;

            refDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(refFile)));
            targetDose = new Model.DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));
            SingleComparison result1 = mathematics.CompareAbsolute(refDose, targetDose, dtas[DtaTypes.t0d0p0mm]);
            SingleComparison result2 = mathematics.CompareParallel(refDose, targetDose, dtas[DtaTypes.t0d0p0mm], threadMax, Type.absolute);

            Assert.AreEqual(result1.TotalCompared, result2.TotalCompared); // confirm all voxels are compared 
            Assert.AreEqual(result1.TotalFailed, result2.TotalFailed); //confirms the number of failed voxels is zero
        }
    }
}