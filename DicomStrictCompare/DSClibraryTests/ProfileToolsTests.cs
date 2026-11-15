using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;
using EvilDICOM.RT;
using System;

namespace DSClibraryTests
{
    [TestClass()]
    public class ProfileToolsTests
    {
        /// <summary>Straight-line distance between two dose points; their doses play no part.</summary>
        [TestMethod()]
        public void DistanceTest()
        {
            var origin = new DoseValue(0, 0, 0, 5);
            var far = new DoseValue(3, 4, 12, 99);
            Assert.AreEqual(13.0, ProfileTools.Distance(origin, far), 1e-12);
            Assert.AreEqual(13.0, ProfileTools.Distance(far, origin), 1e-12, "symmetric");
            Assert.AreEqual(0.0, ProfileTools.Distance(far, new DoseValue(3, 4, 12, 0)), "same place, different dose");
            Assert.AreEqual(Math.Sqrt(3) * 2, ProfileTools.Distance(new DoseValue(-1, -1, -1, 0), new DoseValue(1, 1, 1, 0)), 1e-12);
        }
    }
}
