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
        Dta dta11a = new Dta(false, 1, 1, 0, false);
        Dta dta00r = new Dta(false, 0, 0, 0, true);
        Dta dta11r = new Dta(false, 1, 1, 0, true);

        [TestInitialize()]
        public void Initialize()
        {
            dta00a = new Model.Dta(false, 0, 0);
            dta11a = new Model.Dta(false, 1, 1);
            dta00r = new Dta(false, 0, 0, 0, true);
            dta11r = new Dta(false, 1, 1, 0, true);
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
 
    }
}
