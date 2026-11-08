using System.Collections.Generic;
using System.Linq;
using DSClibrary.Mapping.PTW;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using DSClibrary.Results.PTW;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// What values mean, and what may therefore be compared with what.
    /// <para>
    /// Cody, 2026-11-08: "for the most part profiles are treated as relative, and only RD to RD
    /// comparisons will be looking for absolute dose comparisons", and separately that some vendors
    /// may provide absolutely calibrated output although he has not yet seen one.
    /// </para>
    /// </summary>
    [TestClass]
    public class ValueScaleTests
    {
        private static BeamKey Beam() => new(
            Modality.Photon, 6, false,
            Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000));

        private static Profile Make(ValueScale scale) => new(
            Beam(),
            new ScanKey(CurveType.Crossplane, Distance.FromMillimetres(100)),
            new[]
            {
                new ProfilePoint(Distance.FromMillimetres(-10), 1, null),
                new ProfilePoint(Distance.Zero, 4, null),
                new ProfilePoint(Distance.FromMillimetres(10), 2, null)
            },
            valueScale: scale);

        private static MCCScan Scan(params (string Key, string Value)[] headers) =>
            new(1, new Dictionary<string, string>(
                    headers.Select(h => new KeyValuePair<string, string>(h.Key, h.Value))),
                new[] { new MCCDataPoint(0, 1, 1) });

        // ---- what the real file says ----

        [TestMethod]
        public void TheCommittedScanIsArbitraryUnits()
        {
            // MEAS_UNIT=A.U., DETECTOR_IS_CALIBRATED=0. An uncalibrated reading is not a dose.
            var set = new MCCMeasurementMapper().Map(MCCParser.ParseFile(TestData.PTWScan));
            Assert.IsTrue(set.Beams.Single().Profiles.All(p => p.ValueScale == ValueScale.ArbitraryUnits));
        }

        [TestMethod]
        public void TheScanAlsoRecordsTheUnitItReported()
        {
            var document = MCCParser.ParseFile(TestData.PTWScan);
            Assert.AreEqual("A.U.", document.Scans[0].MeasUnit);
            Assert.AreEqual(false, document.Scans[0].DetectorIsCalibrated);
        }

        // ---- the mapping rule ----

        [TestMethod]
        public void AnUncalibratedDetectorIsArbitraryWhateverUnitItClaims()
        {
            // The dangerous direction is claiming absolute wrongly, so a dose unit is not enough on
            // its own: an uncalibrated reading nominally in Gy is still not a dose.
            Assert.AreEqual(ValueScale.ArbitraryUnits,
                MCCMeasurementMapper.MapValueScale(Scan(("MEAS_UNIT", "Gy"), ("DETECTOR_IS_CALIBRATED", "0"))));
        }

        [TestMethod]
        public void AnUnrecognisedUnitFallsBackToArbitrary()
        {
            Assert.AreEqual(ValueScale.ArbitraryUnits, MCCMeasurementMapper.MapValueScale(Scan(("MEAS_UNIT", "A.U."))));
            Assert.AreEqual(ValueScale.ArbitraryUnits, MCCMeasurementMapper.MapValueScale(Scan(("MEAS_UNIT", "nC"))));
            Assert.AreEqual(ValueScale.ArbitraryUnits, MCCMeasurementMapper.MapValueScale(Scan()));
        }

        [TestMethod]
        public void ACalibratedDoseUnitReadsAsAbsolute()
        {
            // Written so the possibility is representable. NO FILE EXERCISES THIS - Cody has not seen
            // a device that produces it - so the first real file taking this branch wants checking by
            // hand rather than trusting.
            Assert.AreEqual(ValueScale.AbsoluteCentigray,
                MCCMeasurementMapper.MapValueScale(Scan(("MEAS_UNIT", "Gy"), ("DETECTOR_IS_CALIBRATED", "1"))));
            Assert.AreEqual(ValueScale.RelativePercent,
                MCCMeasurementMapper.MapValueScale(Scan(("MEAS_UNIT", "%"), ("DETECTOR_IS_CALIBRATED", "1"))));
        }

        // ---- what that permits ----

        [TestMethod]
        public void SameScaleIsComparable()
        {
            Assert.IsTrue(Make(ValueScale.ArbitraryUnits).IsComparableWith(Make(ValueScale.ArbitraryUnits)));
            Assert.IsTrue(Make(ValueScale.AbsoluteCentigray).IsComparableWith(Make(ValueScale.AbsoluteCentigray)));
        }

        [TestMethod]
        public void DifferentScalesAreNotComparableWhileAsMeasured()
        {
            // This is what stops an uncalibrated 1.7474 being compared against 101.78 cGy and
            // reported as a 99.98% failure.
            var measured = Make(ValueScale.ArbitraryUnits);
            var calculated = Make(ValueScale.AbsoluteCentigray);

            Assert.IsFalse(measured.IsComparableWith(calculated));
            StringAssert.Contains(measured.DescribeIncomparability(calculated), "ArbitraryUnits");
            StringAssert.Contains(measured.DescribeIncomparability(calculated), "AbsoluteCentigray");
        }

        [TestMethod]
        public void NormalizingBothMakesDifferentScalesComparable()
        {
            // Once each curve is a fraction of its own reference, the original scale no longer
            // matters - which is why profile comparison is relative in practice.
            var measured = Make(ValueScale.ArbitraryUnits).Normalize(NormalizationType.Maximum);
            var calculated = Make(ValueScale.AbsoluteCentigray).Normalize(NormalizationType.Maximum);

            Assert.IsTrue(measured.IsComparableWith(calculated));
            Assert.AreEqual("nothing", measured.DescribeIncomparability(calculated));
        }

        [TestMethod]
        public void NormalizingOnlyOneSideIsNotEnough()
        {
            var measured = Make(ValueScale.ArbitraryUnits).Normalize(NormalizationType.Maximum);
            Assert.IsFalse(measured.IsComparableWith(Make(ValueScale.AbsoluteCentigray)));
        }

        [TestMethod]
        public void TransformsCarryTheScaleForward()
        {
            var normalized = Make(ValueScale.AbsoluteCentigray).Normalize(NormalizationType.Maximum);
            Assert.AreEqual(ValueScale.AbsoluteCentigray, normalized.ValueScale);

            var corrected = Make(ValueScale.ArbitraryUnits).WithDetectorCorrection(new IdentityDetectorCorrection());
            Assert.AreEqual(ValueScale.ArbitraryUnits, corrected.ValueScale);
        }

        [TestMethod]
        public void ACalculatedDoseIsNeverInArbitraryUnits()
        {
            var grid = new DSClibrary.DoseMatrixOptimal(
                2, 2, 2, -5, -5, -5, 5, 5, 5, new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 1.0 });

            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(
                () => DoseVolume.FromCentigrayGrid(Beam(), grid, ValueScale.ArbitraryUnits));
        }
    }
}
