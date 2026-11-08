using System;
using System.Linq;
using DSClibrary.Profiles;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    [TestClass]
    public class ProfileTests
    {
        private static Profile Make(params (double Position, double Value)[] points) => new(
            new BeamKey(Modality.Photon, 6, false,
                Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000)),
            new ScanKey(CurveType.Crossplane, Distance.FromMillimetres(100)),
            points.Select(p => new ProfilePoint(Distance.FromMillimetres(p.Position), p.Value, null)));

        [TestMethod]
        public void PointsAreSortedAscendingOnConstruction()
        {
            // A PTW scan may be measured in either direction (SCAN_DIRECTION), so ascending order is
            // an invariant of this type rather than of any source.
            var profile = Make((10, 1), (-10, 3), (0, 2));
            CollectionAssert.AreEqual(
                new[] { -10.0, 0.0, 10.0 },
                profile.Points.Select(p => p.Position.Millimetres).ToArray());
        }

        [TestMethod]
        public void EmptyProfileIsRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => Make());
        }

        [TestMethod]
        public void ValueAtInterpolatesLinearly()
        {
            var profile = Make((0, 0), (10, 10));
            Assert.AreEqual(5.0, profile.ValueAt(Distance.FromMillimetres(5)), 1e-9);
            Assert.AreEqual(2.5, profile.ValueAt(Distance.FromMillimetres(2.5)), 1e-9);
        }

        [TestMethod]
        public void ValueAtRefusesToExtrapolate()
        {
            // Extrapolating a penumbra gives a plausible number that means nothing.
            var profile = Make((0, 0), (10, 10));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => profile.ValueAt(Distance.FromMillimetres(11)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => profile.ValueAt(Distance.FromMillimetres(-1)));
        }

        [TestMethod]
        public void NormalizeToMaximumScalesToOne()
        {
            var profile = Make((-10, 1), (0, 4), (10, 2)).Normalize(NormalizationType.Maximum);
            Assert.AreEqual(NormalizationType.Maximum, profile.Normalization);
            Assert.AreEqual(1.0, profile.MaximumValue, 1e-9);
            Assert.AreEqual(0.25, profile.Points[0].Value, 1e-9);
        }

        [TestMethod]
        public void NormalizeToCentralAxisScalesToTheValueAtZero()
        {
            var profile = Make((-10, 1), (0, 4), (10, 2)).Normalize(NormalizationType.CentralAxis);
            Assert.AreEqual(1.0, profile.ValueAt(Distance.Zero), 1e-9);
            Assert.AreEqual(0.5, profile.Points[^1].Value, 1e-9);
        }

        [TestMethod]
        public void NormalizingTwiceIsRefused()
        {
            // The second scaling would silently be relative to the first.
            var once = Make((0, 1), (10, 2)).Normalize(NormalizationType.Maximum);
            Assert.ThrowsException<InvalidOperationException>(() => once.Normalize(NormalizationType.CentralAxis));
        }

        [TestMethod]
        public void NormalizingByZeroIsRefused()
        {
            var flat = Make((0, 0), (10, 0));
            Assert.ThrowsException<InvalidOperationException>(() => flat.Normalize(NormalizationType.Maximum));
        }

        [TestMethod]
        public void DetectorCorrectionRecordsThatItWasAppliedAndFromWhatState()
        {
            var profile = Make((0, 1), (10, 2));
            Assert.AreEqual(DetectorGeometryCorrection.Unknown, profile.Correction);

            var corrected = profile.WithDetectorCorrection(new IdentityDetectorCorrection());

            Assert.AreEqual(DetectorGeometryCorrection.AppliedByUs, corrected.Correction);
            StringAssert.Contains(corrected.CorrectionDescription!, "prior state was unknown");
            Assert.AreEqual(DetectorGeometryCorrection.Unknown, profile.Correction, "the original is unchanged");
        }

        [TestMethod]
        public void CorrectingTwiceIsRefused()
        {
            // The whole reason the state is an enum and not a bool: a double correction is not
            // detectable from the values afterwards, so it has to be prevented beforehand.
            var once = Make((0, 1), (10, 2)).WithDetectorCorrection(new IdentityDetectorCorrection());
            Assert.ThrowsException<InvalidOperationException>(
                () => once.WithDetectorCorrection(new IdentityDetectorCorrection()));
        }

        [TestMethod]
        public void CorrectingSomethingTheSourceAlreadyCorrectedIsRefused()
        {
            var profile = new Profile(
                new BeamKey(Modality.Photon, 6, false,
                    Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000)),
                new ScanKey(CurveType.Pdd),
                new[] { new ProfilePoint(Distance.Zero, 1, null), new ProfilePoint(Distance.FromMillimetres(10), 2, null) },
                correction: DetectorGeometryCorrection.AppliedBySource);

            Assert.ThrowsException<InvalidOperationException>(
                () => profile.WithDetectorCorrection(new IdentityDetectorCorrection()));
        }
    }
}
