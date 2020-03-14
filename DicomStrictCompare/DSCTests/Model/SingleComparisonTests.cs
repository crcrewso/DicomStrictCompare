using Microsoft.VisualStudio.TestTools.UnitTesting;
using DicomStrictCompare.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomStrictCompare.Model.Tests
{
    [TestClass()]
    public class SingleComparisonTests
    {

        [TestMethod()]
        public void CanInitialize()
        {
            var dta = new Dta(false, 0, 0);
            var ret = new SingleComparison(dta, 0, 0, 0);
            Assert.IsNotNull(ret);
        }

        [TestMethod()]
        public void EqualsTrueTest()
        {
            var dta = new Dta(false, 0, 0);
            var ret = new SingleComparison(dta, 0, 0, 0);

            Assert.IsTrue(ret.Equals(ret));
        }

        [TestMethod()]
        public void EqualsFalseTest1()
        {
            var dta = new Dta(false, 0, 0);
            var ret = new SingleComparison(dta, 0, 0, 0);
            var ret2 = new SingleComparison(dta, 1, 0, 0);
            Assert.IsFalse(ret2.Equals(ret));
        }

        [TestMethod()]
        public void CreateWithNullExceptionTest()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new SingleComparison(null, 0, 0, 0));
        }

        [TestMethod()]
        public void CompareWithNullException()
        {
            var dta = new Dta(false, 0, 0);
            var ret = new SingleComparison(dta, 0, 0, 0);
            Assert.ThrowsException<ArgumentNullException>(() => ret.Equals(null));
        }

    }
}