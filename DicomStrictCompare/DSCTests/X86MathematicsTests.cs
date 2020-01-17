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
        public void CompareTestSameArrayLinear()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();


            for (int i = 0; i < 10; i++)
            {
                source.Add(i);
                target.Add(i);
            }

            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            var testing = new X86Mathematics();
            throw new MissingMethodException();
            //var retCompare = testing.LinearCompareAbslute(sourceDoubles, targetDoubles, tolerance, epsilon);
            //Assert.AreEqual(0, retCompare.Item1);

        }

        [TestMethod()]
        public void CompareTestSameArrayLinearLong()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();


            for (int i = 0; i < 100000000; i++)
            {
                source.Add(i);
                target.Add(i);
            }
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            var testing = new X86Mathematics();
            throw new MissingMethodException();
            //var retCompare = testing.LinearCompareAbslute( sourceDoubles,  targetDoubles, tolerance, epsilon);
            //Assert.AreEqual(0, retCompare.Item1);

        }

        [TestMethod()]
        public void CompareTestSameParallel()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();


            for (int i = 0; i < 10; i++)
            {
                source.Add(i);
                target.Add(i);
            }

            var testing = new X86Mathematics();
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            throw new MissingMethodException();
            //var retCompare = testing.ParallelCompare(sourceDoubles, targetDoubles, tolerance, epsilon);
            //Assert.AreEqual(0, retCompare.Item1);

        }

        [TestMethod()]
        public void CompareTestSameParallelLong()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();


            for (int i = 0; i < 100000000; i++)
            {
                source.Add(i);
                target.Add(i);
            }

            var testing = new X86Mathematics();
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            throw new MissingMethodException(); 
            //var retCompare = testing.ParallelCompare(sourceDoubles, targetDoubles, tolerance, epsilon);
            //Assert.AreEqual(0, retCompare.Item1);

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
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            throw new MissingMethodException();
            //var retCompare = testing.CompareAbsolute(sourceDoubles, targetDoubles, tolerance, epsilon);
            //Assert.AreEqual(1, retCompare.Item1);

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
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            throw new MissingMethodException();
            //var retCompare = testing.CompareAbsolute(sourceDoubles,  targetDoubles, tolerance, epsilon);
            //Assert.AreEqual(0, retCompare.Item1);

        }


        [TestMethod()]
        public void CompareTestDifferencesTooSmall()
        {
            List<double> source = new List<double>();
            List<double> target = new List<double>();
            double epsilon = 0.001;


            for (double i = 0; i < 10; i++)
            {
                source.Add(1);
                target.Add( 1+epsilon);
            }
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            var testing = new X86Mathematics();
            throw new MissingMethodException();
            //var retCompare = testing.CompareAbsolute(sourceDoubles,  targetDoubles, tolerance, epsilon);
            //Assert.AreEqual(0, retCompare.Item1);

        }
        /*
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
        */
    }
}