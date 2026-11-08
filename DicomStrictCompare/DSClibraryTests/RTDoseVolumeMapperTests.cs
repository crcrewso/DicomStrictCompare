using System;
using System.IO;
using System.Linq;
using DSClibrary.Mapping.DICOM;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// The DICOM dose mapper: stored integers to a canonical volume in centigray, with the beam
    /// identity taken from the plan beside it.
    /// </summary>
    [TestClass]
    public class RTDoseVolumeMapperTests
    {
        private static RTFolderContents Contents() => RTFolderReader.Read(TestData.ResourcesDirectory!);

        private static RTDoseWithPlan Pairing(string fileName) =>
            Contents().Doses.Single(d => Path.GetFileName(d.Dose.SourcePath) == fileName);

        [TestMethod]
        public void MapsToCentigrayWithTheKnownMaximum()
        {
            // The figure measured three ways now: stored maximum 2,904,474 times DoseGridScaling
            // 3.5042182425e-07 is 1.017791 Gy, which is 101.7791 cGy, for a 100 MU beam.
            var volume = new RTDoseVolumeMapper().Map(Pairing("RD.UnitTest.P1Ref.X-100A-10.0-1.dcm"));

            Assert.AreEqual(ValueScale.AbsoluteCentigray, volume.Scale);
            Assert.AreEqual(101.7791, volume.MaximumDose!.Value.Centigray, 1e-4);
            Assert.AreEqual(1.017791, volume.MaximumDose!.Value.Gray, 1e-6);
        }

        [TestMethod]
        public void EveryDoseMapsAndAgreesWithEvilDicomTimesAHundred()
        {
            // The oracle again, through the whole mapping this time rather than the reader alone.
            var failures = new System.Collections.Generic.List<string>();
            var mapper = new RTDoseVolumeMapper();

            foreach (var pairing in Contents().Doses)
            {
                string name = Path.GetFileName(pairing.Dose.SourcePath);
                try
                {
                    var volume = mapper.Map(pairing);
                    var theirs = new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(pairing.Dose.SourcePath));

                    double expected = theirs.MaxPointDose.Dose * 100.0;
                    double actual = volume.MaximumDose!.Value.Centigray;
                    if (Math.Abs(expected - actual) > 1e-6)
                        failures.Add($"{name}: expected {expected} cGy, got {actual}");

                    if (Math.Abs(volume.Grid.X0 - theirs.X0) > 1e-6)
                        failures.Add($"{name}: X0 {theirs.X0} vs {volume.Grid.X0}");
                    if (volume.Grid.Length != theirs.DoseValues.Count)
                        failures.Add($"{name}: {theirs.DoseValues.Count} voxels vs {volume.Grid.Length}");
                }
                catch (Exception ex)
                {
                    failures.Add($"{name}: threw {ex.GetType().Name}: {ex.Message}");
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        [TestMethod]
        public void MonitorUnitsAreCarriedAndNotApplied()
        {
            var volumes = new[] { "X-095A", "X-099A", "X-101A" }
                .Select(tag => Pairing($"RD.UnitTest.P5 Varied MU.{tag}-10.0-1.dcm"))
                .Select(new RTDoseVolumeMapper().Map)
                .ToArray();

            CollectionAssert.AreEqual(new[] { 95.0, 99.0, 101.0 },
                volumes.Select(v => v.MonitorUnits!.Value).ToArray());

            // Three different metersets, three different maxima - because the TPS expressed them in
            // DoseGridScaling, not because anything here multiplied by MU. The stored voxels are
            // identical, so the ratios are exactly the meterset ratios.
            double[] maxima = volumes.Select(v => v.MaximumDose!.Value.Centigray).ToArray();
            Assert.AreEqual(99.0 / 95.0, maxima[1] / maxima[0], 1e-6);
            Assert.AreEqual(101.0 / 95.0, maxima[2] / maxima[0], 1e-6);
        }

        [TestMethod]
        public void BeamIdentityComesFromThePlanButSsdDoesNot()
        {
            // The gap, surfaced rather than papered over: SourceToSurfaceDistance is absent from all
            // nine plans, so the beam key cannot be completed from DICOM alone and the volume will not
            // group with a measured profile until an SSD is supplied - most naturally from the scan's
            // own header, which states SSD=1000.00.
            var volume = new RTDoseVolumeMapper().Map(Pairing("RD.UnitTest.P5 Varied MU.X-095A-10.0-1.dcm"));

            Assert.AreEqual(Modality.Photon, volume.Beam.Modality);
            Assert.AreEqual(10.0, volume.Beam.Energy!.Value, 1e-9);
            Assert.AreEqual(false, volume.Beam.IsFff);
            Assert.AreEqual(100.0, volume.Beam.FieldInplane!.Value.Millimetres, 1e-9);
            Assert.AreEqual(100.0, volume.Beam.FieldCrossplane!.Value.Millimetres, 1e-9);

            Assert.IsNull(volume.Beam.Ssd, "SSD is not in these plans");
            Assert.IsFalse(volume.Beam.IsComplete);
            StringAssert.Contains(volume.Beam.DescribeMissing(), "Ssd");
        }

        [TestMethod]
        public void ProvenanceNamesTheVendorAndTheBeam()
        {
            var volume = new RTDoseVolumeMapper().Map(Pairing("RD.UnitTest.P5 Varied MU.X-095A-10.0-1.dcm"));
            Assert.AreEqual("DICOM", volume.Source.Vendor);
            Assert.AreEqual(1, volume.Source.ScanNumber, "the referenced beam number");
        }

        [TestMethod]
        public void RelativeDoseIsRefusedRatherThanGuessed()
        {
            // No file uses DoseUnits RELATIVE, so nothing establishes whether its stored values are a
            // fraction or already a percentage - and those differ by a factor of a hundred. Cody is
            // producing a sample file; until then a clear refusal beats a guessed factor.
            var relative = Assert.ThrowsException<DicomFormatException>(
                () => RTDoseVolumeMapper.MapValueScale("RELATIVE"));
            StringAssert.Contains(relative.Message, "no sample file");

            Assert.AreEqual(ValueScale.AbsoluteCentigray, RTDoseVolumeMapper.MapValueScale("GY"));
            Assert.AreEqual(ValueScale.AbsoluteCentigray, RTDoseVolumeMapper.MapValueScale(" gy "));

            Assert.ThrowsException<DicomFormatException>(() => RTDoseVolumeMapper.MapValueScale(""));
            Assert.ThrowsException<DicomFormatException>(() => RTDoseVolumeMapper.MapValueScale((string?)null));
            Assert.ThrowsException<DicomFormatException>(() => RTDoseVolumeMapper.MapValueScale("RADIOACTIVITY"));
        }
    }
}
