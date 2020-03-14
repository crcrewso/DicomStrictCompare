using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DicomStrictCompare.Model.Tests
{
    [TestClass]
    public class X86MathRelAlg
    {
        DoseMatrixOptimal source, target;
        IMathematics mathematics;

        byte[] sourceFile, targetFile;

        Dta dta00a = new Dta(false, 0, 0, 0, false);
        Dta dta10_01a = new Dta(false, 10, 0.1, 0, false);
        Dta dta00r = new Dta(false, 0, 0, 0, true);
        Dta dta10_011r = new Dta(false, 10, 0.1, 0, true);

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


        [TestMethod()]
        public void Mu100vs101_dta00a()
        {
            sourceFile = DSCTests.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = DSCTests.Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));

            SingleComparison result1 = mathematics.CompareAbsolute(source, source, dta00a);
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
            // Compares algorithm result to SNC patient 
            // RD.UnitTest.P1Ref.X-100A-10.0-1	RD.UnitTest.P5 Varied MU.X-101A-10.0-1	29	15523	15552	DTA 	10	0.1	0	Abs
            Dta dta = new Dta(false, 0.1,0.001, 0);
            sourceFile = DSCTests.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = DSCTests.Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.DoseMatrix(EvilDICOM.Core.DICOMObject.Read(targetFile)));

            
            SingleComparison result1 = mathematics.CompareAbsolute(source, target, dta);
            /*
            System.Diagnostics.Debug.WriteLine("Count");
            Assert.AreEqual(8080200, result1.TotalCount);
            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(8080200, result1.TotalCompared);
            
            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(24277, result1.TotalFailed);
            */
            System.Diagnostics.Debug.WriteLine("Percent Passed");
            Assert.AreEqual(0.4, result1.PercentFailed, 0.1);
        }

    }
}
