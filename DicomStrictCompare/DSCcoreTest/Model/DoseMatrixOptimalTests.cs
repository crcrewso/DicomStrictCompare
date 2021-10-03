using Microsoft.VisualStudio.TestTools.UnitTesting;
using DCSCore.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCSCore.Model.Tests
{
    [TestClass()]
    public class DoseMatrixOptimalTests
    {


        [TestMethod()]
        public void DoseMatrixOptimalCreateFromRef()
        {
            var doseFile = DSCcoreTest.Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            var doseMatrix = new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(doseFile));
            var source = new DoseMatrixOptimal(doseMatrix);
            Assert.AreEqual(doseMatrix.MaxPointDose.Dose, source.MaxPointDose.Dose);
            Assert.AreEqual(doseMatrix.DoseValues.ToArray().Max(), source.MaxPointDose.Dose);
        }

        [TestMethod()]
        public void IsInBoundsTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void GetPointDoseTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void GetPointDoseTest1()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void LatticeXYZToIndexTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void CompareDimensionsTest()
        {
            Assert.Fail();
        }
    }
}