using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSClibrary;

namespace DSClibraryTests
{
    /// <summary>
    /// Basic functionality testing, no math, 
    /// can calculate without producing exceptions or nulls on known good data 
    /// </summary>
    [TestClass]
    public class X86MathSimpleTests : X86Mathematics
    {

        DoseMatrixOptimal source;
        IMathematics mathematics;

        byte[] sourceFile;

        Dta dta00a;

        [TestInitialize]
        public void Initialize()
        {
            dta00a = new Dta(false, 0, 0, 0, false);
            mathematics = new X86Mathematics();
        }

        [TestMethod]
        public void CanInitialize()
        {
            var testing = new X86Mathematics();
            Assert.IsNotNull(testing);
        }

        [TestMethod]
        public void CanCompareAbsolute()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            var ret = mathematics.CompareAbsolute(source, source, dta00a);
            Assert.IsNotNull(ret);
        }

        [TestMethod]
        public void CanCompareRelative()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            var ret = mathematics.CompareRelative(source, source, dta00a);
            Assert.IsNotNull(ret);
        }

        [TestMethod]
        public void CanCompareParallelAbs()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            var ret = mathematics.CompareParallel(source, source, dta00a, 1);
            Assert.IsNotNull(ret);
        }


        [TestMethod]
        public void CanCompareParallelRel()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            var ret = mathematics.CompareParallel(source, source, dta00a, 1);
            Assert.IsNotNull(ret);
        }

        [TestMethod]
        public void CompareAbsoluteResult()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;

            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            SingleComparison result1 = mathematics.CompareAbsolute(source, source, dta00a);

            Assert.AreEqual(0, result1.TotalFailed); // confirm all voxels are compared 
            Assert.AreEqual(source.Count, result1.TotalCompared); //confirms the number of failed voxels is zero

        }

        [TestMethod]
        public void CompareRelativeResult()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;

            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            SingleComparison result1 = mathematics.CompareRelative(source, source, dta00a);

            Assert.AreEqual(0, result1.TotalFailed); // confirm all voxels are compared 
            Assert.AreEqual(source.Count, result1.TotalCompared); //confirms the number of failed voxels is zero
        }
        [TestMethod]
        public void CompareParallelAbsResult()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;

            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            SingleComparison result1 = mathematics.CompareParallel(source, source, dta00a, 1);

            Assert.AreEqual(0, result1.TotalFailed); // confirm all voxels are compared 
            Assert.AreEqual(source.Count, result1.TotalCompared); //confirms the number of failed voxels is zero
        }
        [TestMethod]
        public void CompareParallelRelResult()
        {
            sourceFile = Properties.Resources.RD_UnitTest_P1_5_mm_X_100A_10_0_5;

            source = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(sourceFile)));
            SingleComparison result1 = mathematics.CompareParallel(source, source, dta00a, 1);

            Assert.AreEqual(0, result1.TotalFailed); // confirm all voxels are compared 
            Assert.AreEqual(source.Count, result1.TotalCompared); //confirms the number of failed voxels is zero
        }
    }
}