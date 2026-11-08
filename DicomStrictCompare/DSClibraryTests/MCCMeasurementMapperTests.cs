using System.Linq;
using DSClibrary.Documents.PTW;
using DSClibrary.Mapping.PTW;
using DSClibrary.Profiles;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Meaning tests. These assert what the file's contents <b>mean</b> in the canonical model, which
    /// is a different claim from the parser's tests and checkable against this project's own rules
    /// rather than against PTW's format.
    /// </summary>
    [TestClass]
    public class MCCMeasurementMapperTests
    {
        private static MeasurementSet Set() =>
            new MCCMeasurementMapper().Map(MCCParser.ParseFile(TestData.PTWScan));

        [TestMethod]
        public void TheWholeFileIsOneBeam()
        {
            // Measured: MODALITY, ENERGY, FIELD_INPLANE, FIELD_CROSSPLANE, SSD, FILTER, GANTRY,
            // COLL_ANGLE and WEDGE_ANGLE each have exactly one distinct value across all 11 scans.
            var set = Set();
            Assert.AreEqual(1, set.Beams.Count);
            Assert.AreEqual(0, set.Unresolved.Count);
        }

        [TestMethod]
        public void ElevenProfilesUnderElevenDistinctScanKeys()
        {
            var beam = Set().Beams.Single();
            Assert.AreEqual(11, beam.Profiles.Count);
            Assert.AreEqual(11, beam.Profiles.Select(p => p.Scan).Distinct().Count());
        }

        [TestMethod]
        public void OnePddFiveInplaneFiveCrossplane()
        {
            var beam = Set().Beams.Single();
            Assert.AreEqual(1, beam.OfType(CurveType.Pdd).Count());
            Assert.AreEqual(5, beam.OfType(CurveType.Inplane).Count());
            Assert.AreEqual(5, beam.OfType(CurveType.Crossplane).Count());
        }

        [TestMethod]
        public void ProfilesSitAtFiveDepths()
        {
            var depths = Set().Beams.Single().Profiles
                .Where(p => p.Scan.CurveType != CurveType.Pdd)
                .Select(p => p.Scan.Depth!.Value.Millimetres)
                .Distinct()
                .OrderBy(d => d)
                .ToArray();

            CollectionAssert.AreEqual(new[] { 16.0, 50.0, 100.0, 200.0, 300.0 }, depths);
        }

        [TestMethod]
        public void ThePddHasNoDepth()
        {
            Assert.IsFalse(Set().Beams.Single().OfType(CurveType.Pdd).Single().Scan.Depth.HasValue);
        }

        [TestMethod]
        public void LengthsAreNotRescaled()
        {
            // The 2024 ParsePTW divided SSD and both field sizes by ten into centimetres, putting
            // centimetre values beside a millimetre dose grid. PTW already reports millimetres.
            var beam = Set().Beams.Single().Key;
            Assert.AreEqual(1000.0, beam.Ssd!.Value.Millimetres, 1e-9);
            Assert.AreEqual(300.0, beam.FieldInplane!.Value.Millimetres, 1e-9);
            Assert.AreEqual(300.0, beam.FieldCrossplane!.Value.Millimetres, 1e-9);
            // The same lengths, read for display in centimetres, without anything stored changing.
            Assert.AreEqual(100.0, beam.Ssd!.Value.Centimetres, 1e-9);
            Assert.AreEqual(30.0, beam.FieldInplane!.Value.Centimetres, 1e-9);
        }

        [TestMethod]
        public void BeamIsSixMvFlattenedPhotons()
        {
            var beam = Set().Beams.Single().Key;
            Assert.AreEqual(Modality.Photon, beam.Modality);
            Assert.AreEqual(6.0, beam.Energy!.Value, 1e-9);
            Assert.AreEqual(false, beam.IsFff);
            Assert.IsTrue(beam.IsComplete);
        }

        [TestMethod]
        public void AcquisitionIsSequentialBecauseAWaterTankMovesOneDetector()
        {
            var profile = Set().Beams.Single().Profiles[0];
            Assert.AreEqual(AcquisitionMode.Sequential, profile.Conditions.Acquisition.Mode);
            // Which is why every point carries a reference chamber reading.
            Assert.IsTrue(profile.Points.All(p => p.ReferenceValue.HasValue));
        }

        [TestMethod]
        public void DetectorGeometryCorrectionIsUnknownBecauseTheFileDoesNotSay()
        {
            Assert.AreEqual(
                DetectorGeometryCorrection.Unknown,
                Set().Beams.Single().Profiles[0].Correction);
        }

        [TestMethod]
        public void ValuesArriveAsMeasured()
        {
            Assert.AreEqual(
                NormalizationType.AsMeasured,
                Set().Beams.Single().Profiles[0].Normalization);
        }

        [TestMethod]
        public void DetectorConditionsCarryTheSemiflexRadius()
        {
            // 2.75 mm, which is what a detector geometry correction would need.
            var detector = Set().Beams.Single().Profiles[0].Conditions.Detector;
            Assert.AreEqual(2.75, detector.Radius!.Value.Millimetres, 1e-9);
            Assert.AreEqual("SEMIFLEX", detector.Subcode);
        }

        [TestMethod]
        public void EnvironmentConditionsAreCarriedThrough()
        {
            var environment = Set().Beams.Single().Profiles[0].Conditions.Environment;
            Assert.AreEqual(20.00, environment.TemperatureCelsius!.Value, 1e-9);
            Assert.AreEqual(1013.25, environment.PressureHectopascal!.Value, 1e-9);
        }

        [TestMethod]
        public void NothingTheFileSaidIsDiscarded()
        {
            // The answer to "I am unsure how much of this information will be used later".
            Assert.AreEqual(71, Set().Beams.Single().Profiles[0].Conditions.RawHeaders.Count);
        }

        [TestMethod]
        public void ProvenanceNamesTheVendorAndTheScan()
        {
            var profile = Set().Beams.Single().Profiles[0];
            Assert.AreEqual("PTW", profile.Source.Vendor);
            Assert.AreEqual(1, profile.Source.ScanNumber);
            StringAssert.Contains(profile.Source.SourcePath, ".mcc");
        }

        [TestMethod]
        public void ProfilePointsAreAscendingAndSpanTheExpectedRange()
        {
            var crossplaneAt16 = Set().Beams.Single().Profiles.Single(p =>
                p.Scan.CurveType == CurveType.Crossplane
                && p.Scan.Depth!.Value == Distance.FromMillimetres(16));

            var (min, max) = crossplaneAt16.PositionRange;
            Assert.AreEqual(-182.88, min.Millimetres, 1e-9);
            Assert.AreEqual(182.88, max.Millimetres, 1e-9);
            Assert.AreEqual(93, crossplaneAt16.Points.Count);
        }

        [TestMethod]
        public void LookupByScanKeyFindsTheProfile()
        {
            var beam = Set().Beams.Single();
            // A key is a full address, so a hand-built one has to match every field the file set.
            // This scan has SCAN_OFFAXIS_INPLANE=0.00, SCAN_OFFAXIS_CROSSPLANE=0.00 and
            // SCAN_ANGLE=0.00, and a null is not a zero.
            var wanted = new ScanKey(CurveType.Inplane, Distance.FromMillimetres(100),
                Distance.Zero, Distance.Zero, 0.0);

            Assert.IsNotNull(beam.At(wanted));
            Assert.AreEqual(CurveType.Inplane, beam.At(wanted)!.Scan.CurveType);
        }

        [TestMethod]
        public void APartialScanKeyDoesNotMatch()
        {
            // Documenting the consequence of the rule above rather than leaving it to be rediscovered:
            // asking for "the inplane profile at 100 mm" without the off-axis offsets finds nothing,
            // because null off-axis is a different address from zero off-axis. Correct, and awkward -
            // it is the argument for a partial-match query type alongside the exact key, which is
            // worth having once something actually needs to search.
            var beam = Set().Beams.Single();
            var partial = new ScanKey(CurveType.Inplane, Distance.FromMillimetres(100));

            Assert.IsNull(beam.At(partial));
        }

        [TestMethod]
        public void TheBeamHasNoReferenceDoseYet()
        {
            // Nothing has read a calculated dose into this set, so the missing reference is visible
            // structurally rather than as a lookup that found nothing.
            Assert.IsTrue(Set().Beams.Single().IsMissingReference);
            Assert.AreEqual(1, Set().MissingReferences.Count());
        }

        [TestMethod]
        public void VendorStringsMapToCanonicalEnums()
        {
            Assert.AreEqual(Modality.Photon, MCCMeasurementMapper.MapModality("X"));
            Assert.AreEqual(Modality.Electron, MCCMeasurementMapper.MapModality("E"));
            Assert.AreEqual(Modality.Unknown, MCCMeasurementMapper.MapModality("?"));
            Assert.AreEqual(true, MCCMeasurementMapper.MapFilter("FFF"));
            Assert.AreEqual(false, MCCMeasurementMapper.MapFilter("FF"));
            Assert.AreEqual(CurveType.Pdd, MCCMeasurementMapper.MapCurveType("PDD"));
            Assert.AreEqual(CurveType.Inplane, MCCMeasurementMapper.MapCurveType("INPLANE_PROFILE"));
            Assert.AreEqual(CurveType.Crossplane, MCCMeasurementMapper.MapCurveType("CROSSPLANE_PROFILE"));
            Assert.AreEqual(CurveType.Unknown, MCCMeasurementMapper.MapCurveType("SOMETHING_NEW"));
        }

        [TestMethod]
        public void ReadsTheMeasurementDate()
        {
            var at = MCCMeasurementMapper.MapMeasuredAt("06-Apr-2018", "15:36:30");
            Assert.IsNotNull(at);
            Assert.AreEqual(new System.DateTime(2018, 4, 6, 15, 36, 30), at!.Value);
            Assert.IsNull(MCCMeasurementMapper.MapMeasuredAt("not a date", ""));
        }
    }
}
