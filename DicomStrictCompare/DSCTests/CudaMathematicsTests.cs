using Microsoft.VisualStudio.TestTools.UnitTesting;
using DicomStrictCompare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alea;
using Alea.CSharp;
using Alea.Parallel;


namespace DicomStrictCompare.Tests
{
    [TestClass()]
    public class CudaMathematicsTests : CudaMathematics
    {
        /// <summary>
        /// Systems the can load the gpu and determine the Device Compute Capability.
        /// </summary>
        [TestMethod()]
        public void SystemCanLoadGPU()
        {
            Assert.IsTrue(Alea.DeviceArch.Default.Major>=2, "The actual Compute version is " + Alea.DeviceArch.Default.Number);
        }


        [TestMethod()]
        public void CompareTestAllSame()
        {
            var source = new List<double>{ 1.0, 1.0, 1.0, 1.0 };
            var target = new List<double> { 1.0, 1.0, 1.0, 1.0 };
            var cudaMath = new CudaMathematics();
            var result = cudaMath.Compare(ref source, ref target, 0.01, 0.001);
            Assert.AreEqual(0, result);
        }

        [TestMethod()]
        public void CompareTestAllFailed()
        {
            var source = new List<double> { 1.0, 1.0, 1.0, 1.0 };
            var target = new List<double> { 2.0, 2.0, 2.0, 2.0 };
            var cudaMath = new CudaMathematics();
            var result = cudaMath.Compare(ref source, ref target, 0.01, 0.001);
            Assert.AreEqual(4, result);
        }


        [TestMethod()]
        public void CompareTestAllFailedAtBoundary()
        {
            var source = new List<double> { 1.0, 1.0, 1.0, 1.0 };
            var target = new List<double> { 1.1, 1.1, 1.1, 1.1 };
            var cudaMath = new CudaMathematics();
            var result = cudaMath.Compare(ref source, ref target, 0.1, 0.01);
            Assert.AreEqual(4, result);
        }


    }
}