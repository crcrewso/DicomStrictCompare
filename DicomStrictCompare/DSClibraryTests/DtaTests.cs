using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DSClibraryTests
{
    [TestClass()]
    public class DtaTests
    {
        // ToString and ShortToString format in the current culture; pin it so the expected text is fixed.
        private CultureInfo? _culture;
        [TestInitialize] public void PinCulture() { _culture = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; }
        [TestCleanup] public void RestoreCulture() => CultureInfo.CurrentCulture = _culture!;

        [TestMethod()]
        public void ExceptionInitializationTest()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new Dta(null));
        }

        /// <summary>The main constructor: what each argument sets, and the defaults.</summary>
        [TestMethod()]
        public void DtaTest()
        {
            var d = new Dta(true, 0.10, 0.03, 2);
            Assert.IsTrue(d.UseMM);
            Assert.AreEqual(0.10, d.Threshhold);
            Assert.AreEqual(0.03, d.Tolerance);
            Assert.AreEqual(2.0, d.Distance);
            Assert.AreEqual(Dta.CalcType.Global, d.Type, "relative defaults to true, which is Global");
            Assert.IsTrue(d.Global);
            Assert.AreEqual(Dta.CalcAlgorithm.dta, d.Algorithm);
            Assert.AreEqual(0, d.TrimWidth, "trim defaults to 0");

            var local = new Dta(false, 0.05, 0.01, 0.5, relative: false, gamma: true, trim: 2);
            Assert.IsFalse(local.UseMM);
            Assert.AreEqual(Dta.CalcType.Local, local.Type);
            Assert.IsFalse(local.Global);
            Assert.AreEqual(Dta.CalcAlgorithm.gamma, local.Algorithm);
            Assert.AreEqual(2, local.TrimWidth);
            Assert.AreEqual(0.0, new Dta(true, 0.1, 0.03).Distance, "distance defaults to 0");
        }

        /// <summary>The string constructor reads back what ToString writes.</summary>
        [TestMethod()]
        public void DtaTest1()
        {
            foreach (var original in new[] { new Dta(true, 0.10, 0.03, 2), new Dta(true, 0.05, 0.02, 3, relative: false), new Dta(false, 0.20, 0.01, 1) })
            {
                var back = new Dta(original.ToString());
                Assert.IsTrue(original.Equals(back), $"'{original}' read back as '{back}'");
            }
            // A tolerance that isn't a whole number of tenths of a percent reads back within rounding of ToString's "0.0".
            var fine = new Dta(new Dta(true, 0.10, 0.001, 0.5).ToString());
            Assert.AreEqual(0.001, fine.Tolerance, 1e-15);
            Assert.AreEqual(0.5, fine.Distance);

            Assert.ThrowsExactly<FormatException>(() => new Dta("3.0, 2.00, 10.0"));
            Assert.ThrowsExactly<ArgumentException>(() => new Dta("3.0, 2.00, 10.0, Sideways, mm"));
        }

        [TestMethod()]
        public void ShortToStringTest()
        {
            Assert.AreEqual("3.0 % 2 Global mm", new Dta(true, 0.10, 0.03, 2).ShortToString());
            Assert.AreEqual("0.1 % 0.5 Local mm", new Dta(true, 0.10, 0.001, 0.5, relative: false).ShortToString());
            Assert.AreEqual("2.0 % 1 Global voxels", new Dta(false, 0.10, 0.02, 1).ShortToString());
        }

        [TestMethod()]
        public void ToStringTest()
        {
            Assert.AreEqual("3.0, 2.00, 10.0, Global, mm", new Dta(true, 0.10, 0.03, 2).ToString());
            Assert.AreEqual("0.1, 0.50, 5.0, Local, voxel", new Dta(false, 0.05, 0.001, 0.5, relative: false).ToString());
        }

        /// <summary>The header names one column per value ToString writes.</summary>
        [TestMethod()]
        public void TitlesTest()
        {
            Assert.AreEqual("Tolerance, Distance, Threshhold, Global?, Unit", Dta.Titles());
            Assert.AreEqual(Dta.Titles().Split(", ").Length, new Dta(true, 0.1, 0.03, 2).ToString().Split(", ").Length);
        }
    }
}
