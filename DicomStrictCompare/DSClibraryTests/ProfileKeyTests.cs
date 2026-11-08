using System.Collections.Generic;
using System.Linq;
using DSClibrary.Profiles;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    [TestClass]
    public class ProfileKeyTests
    {
        private static BeamKey Beam(double field = 300, double ssd = 1000, double energy = 6) =>
            new(Modality.Photon, energy, false,
                Distance.FromMillimetres(field), Distance.FromMillimetres(field),
                Distance.FromMillimetres(ssd), 0, 0, 0);

        [TestMethod]
        public void KeysWithinTheQuantumAreEqualAndHashAlike()
        {
            var a = Beam(field: 300.0);
            var b = Beam(field: 300.2);   // inside half a millimetre: the same field
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void KeysBeyondTheQuantumDiffer()
        {
            Assert.AreNotEqual(Beam(field: 300.0), Beam(field: 301.0));
        }

        [TestMethod]
        public void GroupingIsIndependentOfInsertionOrder()
        {
            // The reason keys quantise instead of comparing with a tolerance. With a half millimetre
            // tolerance, 30.0 ~ 30.4 and 30.4 ~ 30.8 while 30.0 is not ~ 30.8, so a tolerant grouping
            // of these three depths would depend on the order they arrived in. Quantised, it cannot.
            double[] depths = { 30.0, 30.2, 30.8 };

            List<ScanKey> KeysFrom(IEnumerable<double> order) => order
                .Select(d => new ScanKey(CurveType.Crossplane, Distance.FromMillimetres(d)))
                .ToList();

            var forwards = KeysFrom(depths).Distinct().OrderBy(k => k.Depth!.Value).ToList();
            var backwards = KeysFrom(depths.Reverse()).Distinct().OrderBy(k => k.Depth!.Value).ToList();

            CollectionAssert.AreEqual(forwards, backwards);
            Assert.AreEqual(2, forwards.Count,
                "30.0 and 30.2 both round to 30.0; 30.8 rounds to 31.0, so there are two groups.");
        }

        [TestMethod]
        public void IncompleteBeamKeyIsReportedWithTheMissingFields()
        {
            // What a Sun Nuclear prm yields: no field size, no energy.
            var key = new BeamKey(Modality.Photon, energy: null,
                fieldInplane: null, fieldCrossplane: null, ssd: Distance.FromCentimetres(100));

            Assert.IsFalse(key.IsComplete);
            string missing = key.DescribeMissing();
            StringAssert.Contains(missing, nameof(BeamKey.Energy));
            StringAssert.Contains(missing, nameof(BeamKey.FieldInplane));
        }

        [TestMethod]
        public void CompleteBeamKeySaysSo()
        {
            Assert.IsTrue(Beam().IsComplete);
            Assert.AreEqual("nothing", Beam().DescribeMissing());
        }

        [TestMethod]
        public void PddHasNoDepthEvenIfOneIsSupplied()
        {
            // A PDD runs through every depth, so a depth on it would be meaningless. Null, not zero:
            // zero is a real depth.
            var pdd = new ScanKey(CurveType.Pdd, Distance.FromMillimetres(100));
            Assert.IsFalse(pdd.Depth.HasValue);
            Assert.AreEqual(pdd, new ScanKey(CurveType.Pdd));
        }

        [TestMethod]
        public void CurveTypeDistinguishesOtherwiseIdenticalScans()
        {
            var depth = Distance.FromMillimetres(100);
            Assert.AreNotEqual(
                new ScanKey(CurveType.Inplane, depth),
                new ScanKey(CurveType.Crossplane, depth));
        }

        [TestMethod]
        public void NegativeZeroOffAxisMatchesZero()
        {
            var a = new ScanKey(CurveType.Inplane, Distance.FromMillimetres(50), Distance.FromMillimetres(-0.0));
            var b = new ScanKey(CurveType.Inplane, Distance.FromMillimetres(50), Distance.FromMillimetres(0.0));
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
