using Microsoft.VisualStudio.TestTools.UnitTesting;
using DicomStrictCompare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomStrictCompare.Tests
{
    [TestClass()]
    public class X86MathematicsTests: X86Mathematics
    {
        [TestMethod()]
        public void CanInitialize()
        {
            var testing = new X86Mathematics();
        }



        [TestMethod()]
        public void CompareTestSameArray()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();
            double tolerance = 0.01;
            double epsilon = 0.001;


            for (int i = 0; i < 10; i++)
            {
                source.Add(i);
                target.Add(i);
            }

            var testing = new X86Mathematics();
            var retCompare = testing.Compare(ref source, ref target, tolerance, epsilon);
            Assert.AreEqual(0, retCompare);

        }

        [TestMethod()]
        public void CompareTestSingleFailure()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();
            double tolerance = 0.01;
            double epsilon = 0.01;


            for (double i = 0; i < 10; i++)
            {
                source.Add(1);
                target.Add(1);
            }

            target[0] = 1+ tolerance+ epsilon;

            var testing = new X86Mathematics();
            var retCompare = testing.Compare(ref source, ref target, tolerance, epsilon);
            Assert.AreEqual(1, retCompare);

        }

        [TestMethod()]
        public void CompareTestSingleNotFailure()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();
            double tolerance = 0.01;
            double epsilon = 0.01;


            for (double i = 0; i < 10; i++)
            {
                source.Add(1);
                target.Add(1);
            }

            target[0] = 1 + tolerance - epsilon;

            var testing = new X86Mathematics();
            var retCompare = testing.Compare(ref source, ref target, tolerance, epsilon);
            Assert.AreEqual(0, retCompare);

        }


        [TestMethod()]
        public void CompareTestDifferencesTooSmall()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();
            double tolerance = 0.01;
            double epsilon = 0.001;


            for (double i = 0; i < 10; i++)
            {
                source.Add(1);
                target.Add( 1+epsilon);
            }

            var testing = new X86Mathematics();
            var retCompare = testing.Compare(ref source, ref target, tolerance, epsilon);
            Assert.AreEqual(0, retCompare);

        }

        [TestMethod()]
        public void CompareSingleValueSame()
        {
            Assert.AreEqual(true, IsWithinTolerance(1.0, 1.0, 0.1));
        }

        [TestMethod()]
        public void CompareSingleValueWithinTolerance()
        {
            Assert.AreEqual(true, IsWithinTolerance(1.0, 1.09, 0.1));
        }

        [TestMethod()]
        public void CompareSingleValueAtTolerance()
        {
            Assert.AreEqual(false, IsWithinTolerance(1.0, 1.1, 0.1));
        }

    }
}