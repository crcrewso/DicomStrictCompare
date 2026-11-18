using System;
using System.Linq;
using DSClibrary;
using DSClibrary.Profiles;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// <see cref="ScanHeaderCheck"/>: a scan's curve against its own header (2026-11-17). Synthetic profiles with
    /// hand-placed edges; the cases are the Equinox ones that set the rules: a wedged 20 x 20 scan whose header says
    /// open 10 x 10, an FFF 40 x 40 field, a scan that stops inside its field, small and off-axis fields.
    /// </summary>
    [TestClass]
    public class ScanHeaderCheckTests
    {
        /// <summary>A scan at 100 mm depth, SSD 1000 mm, so its field is 1.1 times the nominal one along the scan.</summary>
        private static Profile Scan(Func<double, double> dose, double headerFieldMm = 100, double? wedge = null, Modality modality = Modality.Photon,
            bool fff = false, CurveType curve = CurveType.Inplane, double from = -200, double to = 200, double step = 1)
        {
            var beam = new BeamKey(modality, 6, fff, Distance.FromMillimetres(headerFieldMm), Distance.FromMillimetres(headerFieldMm),
                Distance.FromMillimetres(1000), wedgeAngleDegrees: wedge);
            var points = Enumerable.Range(0, (int)Math.Round((to - from) / step) + 1).Select(i => from + i * step)
                .Select(p => new ProfilePoint(Distance.FromMillimetres(p), dose(p), null));
            return new Profile(beam, new ScanKey(curve, Distance.FromMillimetres(100)), points);
        }

        /// <summary>
        /// A field of nominal width w (so 1.1 w wide at 100 mm depth) centred on c, with S-shaped (logistic) penumbrae
        /// about 6 mm wide, steepest exactly at the field edge, as a measured penumbra is.
        /// </summary>
        private static Func<double, double> Field(double w, double c = 0, Func<double, double>? inside = null) =>
            p => 0.98 / (1 + Math.Exp(-(0.55 * w - Math.Abs(p - c)) / 1.5)) * (inside?.Invoke(p) ?? 1) + 0.02;

        [TestMethod]
        public void AnOpenFieldThatMatchesItsHeaderAgrees()
        {
            var check = ScanHeaderCheck.Check(Scan(Field(100)));
            Assert.IsTrue(check.Agrees, check.Summary);
            Assert.AreEqual(100, check.MeasuredFieldMm!.Value, 1, "the field projected to the SSD, as fields are named");
            Assert.AreEqual(0, check.SlopePercent!.Value, 0.01);
        }

        [TestMethod]
        public void AFieldTwiceTheHeadersIsFlagged()
        {
            var check = ScanHeaderCheck.Check(Scan(Field(200), headerFieldMm: 100, to: 300, from: -300));
            Assert.IsFalse(check.Agrees);
            StringAssert.Contains(check.Summary, "the header says a 100 mm field inplane, but the scan's field is 20");
            Assert.AreEqual(200, check.MeasuredFieldMm!.Value, 1, "within a sample spacing: an edge is the middle of the steepest step");
        }

        [TestMethod]
        public void ASlopedFieldIsFlaggedOnlyWhenTheHeaderSaysNoWedge()
        {
            // Rises linearly by 20 % across the field: a wedge the header doesn't mention.
            var wedged = Field(100, inside: p => 1 + 0.2 * p / 110);
            var open = ScanHeaderCheck.Check(Scan(wedged));
            StringAssert.Contains(open.Summary, "the header says no wedge, but the dose is");
            StringAssert.Contains(open.Summary, "higher on the positive inplane side");
            Assert.IsTrue(ScanHeaderCheck.Check(Scan(wedged, wedge: 15)).Agrees, "a header that names a wedge isn't checked for slope");
            Assert.IsTrue(ScanHeaderCheck.Check(Scan(wedged, modality: Modality.Electron)).Agrees, "electrons aren't checked for slope");
        }

        [TestMethod]
        public void AnFffFieldIsMeasuredBetweenItsPenumbraeNotAtHalfItsAxisDose()
        {
            // Falls to 45 % of the axis dose at the edge of a 40 x 40 field: 50 % of the axis would make it about 36 cm.
            var fff = Field(400, inside: p => 1 - 0.55 * Math.Abs(p) / 220);
            var check = ScanHeaderCheck.Check(Scan(fff, headerFieldMm: 400, fff: true, from: -300, to: 300));
            Assert.IsTrue(check.Agrees, check.Summary);
            Assert.AreEqual(400, check.MeasuredFieldMm!.Value, 2, "the falling dose moves the steepest point a little inward");
        }

        [TestMethod]
        public void AScanThatStopsInsideItsFieldIsNotMeasured()
        {
            // The negative end is at 28 % of the axis dose, still in the field, as a 40 x 40 FFF scan at 20 cm depth ends.
            var fff = Field(400, inside: p => 1 - 0.72 * Math.Abs(p) / 220);
            var check = ScanHeaderCheck.Check(Scan(fff, headerFieldMm: 400, fff: true, from: -219, to: 300));
            Assert.IsNull(check.MeasuredFieldMm);
            Assert.IsTrue(check.Agrees);
        }

        [TestMethod]
        public void SmallAndOffAxisFieldsAreNotCheckedForSlope()
        {
            var tilted = (Func<double, double> f) => (Func<double, double>)(p => f(p) * (1 + 0.1 * p / 50));
            Assert.IsTrue(ScanHeaderCheck.Check(Scan(tilted(Field(10)), headerFieldMm: 10, step: 0.5)).Agrees, "a 1 cm field has no level region");
            // Half-blocked: the field runs from the axis to +110 mm at depth, centred 55 mm off it.
            Assert.IsTrue(ScanHeaderCheck.Check(Scan(tilted(Field(100, c: 55)))).Agrees, "a field off the axis isn't level");
        }

        [TestMethod]
        public void DepthDosesAndScansOutsideTheFieldAreNotChecked()
        {
            Assert.AreSame(ScanHeaderCheckResult.NotChecked, ScanHeaderCheck.Check(Scan(Field(100), curve: CurveType.Pdd)));
            Assert.AreSame(ScanHeaderCheckResult.NotChecked, ScanHeaderCheck.Check(Scan(Field(100, c: 150))), "the axis is outside the field");
        }
    }
}
