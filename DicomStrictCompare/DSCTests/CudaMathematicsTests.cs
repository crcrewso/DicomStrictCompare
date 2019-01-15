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
    public class CudaMathematicsTests
    {
        [TestMethod()]
        public void CompareTest()
        {

        }

        [TestMethod()]
        public void KernelTest()
        {
            double[] source = new double[] { 1.0, 1.0, 1.0, 1.0 };
            double[] target = new double[] { 1.0, 1.0, 1.0, 1.0 };
            double[] result = new double[4];
            double tolerance = 0.01;
            double epsilon = 0.001;
            var gpu = Gpu.Default;
            var lp = new LaunchParam(16, 256);

            gpu.Launch(CudaMathematics.Kernel,lp,result, source, target, tolerance, epsilon);
            Assert.AreEqual(0, result[0]);
        }
    }
}