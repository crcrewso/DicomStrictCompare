using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;
using DSClibrary.Comparing;
using DSClibrary.Results.DICOM;
using System;
using System.IO;

namespace DSClibraryTests
{
    /// <summary><c>IMathematics</c> is now <see cref="DoseComparer"/>; these test its rules on the reference engine.</summary>
    [TestClass()]
    public class IMathematicsTests
    {
        /// <summary>
        /// CompareAbsolute is the local rule: the tolerance is a fraction of the source dose at each point. The 95 and
        /// 101 MU doses of one plan differ only in DoseGridScaling, so every point differs by the same fraction, r - 1.
        /// With no DTA distance, a tolerance just under that fails every compared point and one just over passes them all.
        /// </summary>
        [TestMethod()]
        public void CompareAbsoluteTest()
        {
            string Res(string name) => Path.Combine(TestData.ResourcesDirectory!, name);
            var mu95 = DoseGridReader.Read(Res("RD.UnitTest.P5 Varied MU.X-095A-10.0-1.dcm"));
            var mu101 = DoseGridReader.Read(Res("RD.UnitTest.P5 Varied MU.X-101A-10.0-1.dcm"));
            double r = mu101.Scaling / mu95.Scaling;
            Assert.IsTrue(r > 1.03 && r < 1.10, $"the MU ratio, about 101/95, was {r}");
            Dta Criterion(double tolerance) => new(true, 0.10, tolerance, 0);   // distance 0: dose difference alone

            foreach (DoseComparer engine in new DoseComparer[] { new HandWrittenComparer(), new MultiThreadComparer() })
            {
                var self = engine.CompareAbsolute(mu95, mu95, Criterion(0.001));
                Assert.IsTrue(self.TotalCompared > 0);
                Assert.AreEqual(0, self.TotalFailed, $"{engine.Description}: a grid against itself");

                var under = engine.CompareAbsolute(mu95, mu101, Criterion((r - 1) * 0.9));
                var over = engine.CompareAbsolute(mu95, mu101, Criterion((r - 1) * 1.1));
                Assert.AreEqual(self.TotalCompared, under.TotalCompared, "the threshold tests the source only");
                Assert.AreEqual(under.TotalCompared, under.TotalFailed, $"{engine.Description}: every point is {r - 1:P2} off, over a smaller tolerance");
                Assert.AreEqual(0, over.TotalFailed, $"{engine.Description}: and inside a larger one");

                // The global rule takes the tolerance from the source maximum, so the same tolerance passes the low doses.
                var global = engine.CompareRelative(mu95, mu101, Criterion((r - 1) * 0.9));
                Assert.IsTrue(global.TotalFailed > 0 && global.TotalFailed < under.TotalFailed,
                    $"{engine.Description}: global failed {global.TotalFailed} of {global.TotalCompared}, local {under.TotalFailed}");
            }
        }
    }
}
