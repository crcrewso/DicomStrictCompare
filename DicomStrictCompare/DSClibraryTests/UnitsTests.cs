using System;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Distance exists to decouple parsing, storage and display. These assert that it does, and that
    /// the two traps it was written to avoid - a unit assumed at a boundary, and negative zero
    /// breaking equality - stay avoided.
    /// </summary>
    [TestClass]
    public class DistanceTests
    {
        [TestMethod]
        public void SameLengthFromDifferentUnitsIsTheSameDistance()
        {
            // The case that motivated the type: PTW says SSD=1000.00 in millimetres, Sun Nuclear says
            // SSD: 100 in centimetres, and they are the same distance.
            Assert.AreEqual(Distance.FromMillimetres(1000), Distance.FromCentimetres(100));
            Assert.AreEqual(Distance.FromMillimetres(1000), Distance.FromMetres(1));
        }

        [TestMethod]
        public void ConvertsForDisplayWithoutChangingStorage()
        {
            var ssd = Distance.FromMillimetres(1000);
            Assert.AreEqual(1000.0, ssd.Millimetres, 1e-9);
            Assert.AreEqual(100.0, ssd.Centimetres, 1e-9);
            Assert.AreEqual(1.0, ssd.Metres, 1e-9);
        }

        [TestMethod]
        public void NegativeZeroEqualsPositiveZero()
        {
            // double.Equals(-0.0, 0.0) is false, and record equality uses Equals, so without the
            // normalisation in the constructor an off-axis offset of -0.0 would never match 0.0.
            var negative = Distance.FromMillimetres(-0.0);
            var positive = Distance.FromMillimetres(0.0);
            Assert.AreEqual(positive, negative);
            Assert.AreEqual(positive.GetHashCode(), negative.GetHashCode());
        }

        [TestMethod]
        public void RejectsNonFiniteValues()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Distance.FromMillimetres(double.NaN));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Distance.FromMillimetres(double.PositiveInfinity));
        }

        [TestMethod]
        public void NullableFactoryPassesNullThrough()
        {
            Assert.IsNull(Distance.FromMillimetres((double?)null));
            Assert.AreEqual(Distance.FromMillimetres(3), Distance.FromMillimetres((double?)3));
        }

        [TestMethod]
        public void QuantizesOntoTheGivenStep()
        {
            var step = Distance.FromMillimetres(0.5);
            Assert.AreEqual(Distance.FromMillimetres(30.0), Distance.FromMillimetres(30.2).QuantizedTo(step));
            Assert.AreEqual(Distance.FromMillimetres(30.5), Distance.FromMillimetres(30.3).QuantizedTo(step));
            Assert.AreEqual(Distance.FromMillimetres(-30.5), Distance.FromMillimetres(-30.3).QuantizedTo(step));
        }

        [TestMethod]
        public void ArithmeticAndComparisonWork()
        {
            var a = Distance.FromMillimetres(10);
            var b = Distance.FromMillimetres(4);
            Assert.AreEqual(Distance.FromMillimetres(14), a + b);
            Assert.AreEqual(Distance.FromMillimetres(6), a - b);
            Assert.AreEqual(Distance.FromMillimetres(20), a * 2);
            Assert.AreEqual(2.5, a / b, 1e-9);
            Assert.IsTrue(b < a);
            Assert.IsTrue(a >= Distance.FromMillimetres(10));
        }

        [TestMethod]
        public void DisplayUnitAffectsFormattingOnly()
        {
            var original = Distances.DisplayUnit;
            try
            {
                var ssd = Distance.FromMillimetres(1000);
                Distances.DisplayUnit = DistanceUnit.Centimetres;
                StringAssert.Contains(ssd.ToString(), "cm");
                // Storage is untouched by the display setting.
                Assert.AreEqual(1000.0, ssd.Millimetres, 1e-9);
                Assert.AreEqual(Distance.FromMillimetres(1000), ssd);
            }
            finally
            {
                Distances.DisplayUnit = original;
            }
        }

        [TestMethod]
        public void KeyQuantumIsNotSettable()
        {
            // Not a behavioural assertion so much as a guard on the design: the quantum decides
            // whether two measurements are the same measurement, so it must not be runtime state.
            var field = typeof(Distances).GetField(nameof(Distances.KeyQuantum));
            Assert.IsNotNull(field);
            Assert.IsTrue(field!.IsInitOnly, "KeyQuantum must stay readonly.");
        }
    }
}
