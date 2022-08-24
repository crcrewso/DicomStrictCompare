using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;

namespace DSClibraryTests
{
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
            // Compares algorithm result to SNC patient 
            // RD.UnitTest.P1Ref.X-100A-10.0-1	RD.UnitTest.P5 Varied MU.X-101A-10.0-1	29	15523	15552	DTA 	10	0.1	0	Abs
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
            // Compares algorithm result to SNC patient 
            // RD.UnitTest.P1Ref.X-100A-10.0-1	RD.UnitTest.P5 Varied MU.X-101A-10.0-1	29	15523	15552	DTA 	10	0.1	0	Abs
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
            // Compares algorithm result to SNC patient 
            // RD.UnitTest.P1Ref.X-100A-10.0-1	RD.UnitTest.P5 Varied MU.X-101A-10.0-1	29	15523	15552	DTA 	10	0.1	0	Abs
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
            // Compares algorithm result to SNC patient 
            // RD.UnitTest.P1Ref.X-100A-10.0-1	RD.UnitTest.P5 Varied MU.X-101A-10.0-1	29	15523	15552	DTA 	10	0.1	0	Abs
            Dta dta = new Dta(false, 0.1, 0.001, 0, false, false, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);

            System.Diagnostics.Debug.WriteLine("Compared");
            Assert.AreEqual(8080200, result1.TotalCompared);
        }

        [TestMethod()]
        public void Mu100vs101_SNC003()
        {
            // Compares algorithm result to SNC patient 
            // RD.UnitTest.P1Ref.X-100A-10.0-1	RD.UnitTest.P5 Varied MU.X-101A-10.0-1	29	15523	15552	DTA 	10	0.1	0	Abs
            Dta dta = new Dta(false, 0.1, 0.01, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);


            System.Diagnostics.Debug.WriteLine("Failed");
            Assert.AreEqual(24277, result1.TotalFailed);
        }
        [TestMethod()]
        public void Mu100vs101_SNC004()
        {
            // Compares algorithm result to SNC patient 
            // RD.UnitTest.P1Ref.X-100A-10.0-1	RD.UnitTest.P5 Varied MU.X-101A-10.0-1	29	15523	15552	DTA 	10	0.1	0	Abs
            Dta dta = new Dta(false, 0, 0.01, 0);
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            targetFile = Properties.Resources.RD_UnitTest_P5_Varied_MU_X_101A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            target = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(targetFile)));


            SingleComparison result1 = mathematics.CompareRelative(source, target, dta);


            System.Diagnostics.Debug.WriteLine("Percent Passed");
            Assert.AreEqual(0.4, result1.PercentFailed, 0.1);
        }
    }
}
