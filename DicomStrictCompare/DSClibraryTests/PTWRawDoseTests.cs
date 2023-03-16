using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSClibrary.Tests
{
    [TestClass()]
    public class PTWRawDoseTests
    {
        [TestMethod()]
        public void RawDoseInitTest1()
        {
            string testString = "\t\t\t-5.00\t\t1.0570E+00\t\t3.6648E+00";
            var dose = new PTWRawDose(testString);
            Assert.AreEqual(-5.00, dose.Position);
            Assert.AreEqual(1.057, dose.Value, 0.0001);
            Assert.AreEqual(3.6648, dose.SecondValue, 0.0001);
        }
        [TestMethod()]
        public void RawDoseInitTest2()
        {
            string testString = "\t\t\t300.00\t\t476.50E-03\t\t3.6612E+00\r\n";
            var dose = new PTWRawDose(testString);
            Assert.AreEqual(300.00, dose.Position);
            Assert.AreEqual(0.4765, dose.Value, 0.0001);
            Assert.AreEqual(3.6612, dose.SecondValue, 0.0001);
        }
    }
}