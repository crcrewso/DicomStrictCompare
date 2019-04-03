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
            var source = new double[]{ 1.0, 1.0, 1.0, 1.0 };
            var target = new double[] { 1.0, 1.0, 1.0, 1.0 };
            var cudaMath = new CudaMathematics();
            var result = cudaMath.Compare( source,  target, 0.01, 0.001);
            Assert.AreEqual(0, result);
        }

        [TestMethod()]
        public void CompareTestAllFailed()
        {
            var source = new double[] { 1.0, 1.0, 1.0, 1.0 };
            var target = new double[] { 2.0, 2.0, 2.0, 2.0 };
            var cudaMath = new CudaMathematics();
            var result = cudaMath.Compare( source,  target, 0.01, 0.001);
            Assert.AreEqual(4, result);
        }


        [TestMethod()]
        public void CompareTestAllFailedAtBoundary()
        {
            var source = new double[]{ 1.0, 1.0, 1.0, 1.0 };
            var target = new double[]{ 1.1, 1.1, 1.1, 1.1 };
            var cudaMath = new CudaMathematics();
            var result = cudaMath.Compare( source,  target, 0.1, 0.01);
            Assert.AreEqual(4, result);
        }

        [TestMethod()]
        public void CompareTestSameArrayLong()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();
            double tolerance = 0.01;
            double epsilon = 0.001;


            for (int i = 0; i < 100000000; i++)
            {
                source.Add(i);
                target.Add(i);
            }
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            var cudaMath = new CudaMathematics();
            var retCompare = cudaMath.Compare(sourceDoubles, targetDoubles, tolerance, epsilon);
            Assert.AreEqual(0, retCompare);

        }

    }
}