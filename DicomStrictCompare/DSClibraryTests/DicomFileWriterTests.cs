using System;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// The writer against its own reader and against EvilDICOM as an independent oracle: a file read,
    /// written and read again must say the same thing, and a resampled file must be a valid RT Dose
    /// that another implementation reads to the same grid.
    /// </summary>
    [TestClass]
    public class DicomFileWriterTests
    {
        private static string TempPath() => Path.Combine(Path.GetTempPath(), "dsc-writer-" + Guid.NewGuid().ToString("N") + ".dcm");

        [TestMethod]
        public void AReferenceFileSurvivesARoundTripElementForElement()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            var original = DicomFileReader.ReadFile(TestData.DoseFiles[0]);
            byte[] written = DicomFileWriter.ToBytes(original.Dataset);
            var again = DicomFileReader.Read(written, "round-trip");

            Assert.AreEqual(DicomTags.ImplicitVRLittleEndian, again.TransferSyntaxUID);
            Assert.AreEqual(original.SOPInstanceUID, again.SOPInstanceUID);
            Assert.AreEqual(DicomFileWriter.ImplementationClassUID, again.FileMeta.GetString(new DicomTag(0x0002, 0x0012)));

            var originalTags = original.Dataset.Tags.ToArray();
            CollectionAssert.AreEqual(originalTags, again.Dataset.Tags.ToArray(), "same elements in the same order");
            foreach (var tag in originalTags)
            {
                var a = original.Dataset.Element(tag)!;
                var b = again.Dataset.Element(tag)!;
                Assert.IsTrue(a.Value.Span.SequenceEqual(b.Value.Span), $"{tag} bytes differ");
                Assert.AreEqual(a.Items is null, b.Items is null, $"{tag} sequence form differs");
            }
        }

        [TestMethod]
        public void EvilDicomReadsAWrittenCopyToTheSameGrid()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            string path = TempPath();
            try
            {
                var original = DicomFileReader.ReadFile(TestData.DoseFiles[0]);
                DicomFileWriter.WriteFile(path, original.Dataset);

                var oracleOriginal = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(TestData.DoseFiles[0])));
                var oracleCopy = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path)));
                Assert.IsTrue(oracleOriginal.CompareDimensions(oracleCopy));
                CollectionAssert.AreEqual(oracleOriginal.DoseValues, oracleCopy.DoseValues);
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void AResampledDoseIsAValidRTDoseWithTheCoarseGridAndANewInstanceUid()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            string path = TempPath();
            try
            {
                var source = RTDoseResult.ReadFile(TestData.DoseFiles[0]);
                var fine = RTDoseDerivation.RawGrid(source);
                var coarse = GridResampler.Resample(fine, 5.0);
                var dataset = RTDoseDerivation.WithGrid(source, coarse, RTDoseDerivation.ResampleDescription(coarse));
                DicomFileWriter.WriteFile(path, dataset);

                // Our own reader.
                var written = RTDoseResult.ReadFile(path);
                Assert.AreNotEqual(source.SOPInstanceUID, written.SOPInstanceUID, "a different grid is a different object");
                Assert.AreEqual(source.ReferencedRTPlanSOPInstanceUID, written.ReferencedRTPlanSOPInstanceUID, "the plan reference is kept");
                Assert.AreEqual(source.ReferencedBeamNumber, written.ReferencedBeamNumber);
                Assert.AreEqual(source.FrameOfReferenceUID, written.FrameOfReferenceUID);
                Assert.AreEqual(source.DoseGridScaling, written.DoseGridScaling, "the scaling is kept so coincident voxels are bit-identical");
                Assert.AreEqual(coarse.DimensionX, (int)written.Columns!);
                Assert.AreEqual(coarse.DimensionY, (int)written.Rows!);
                Assert.AreEqual(coarse.DimensionZ, written.NumberOfFrames);
                CollectionAssert.AreEqual(new[] { 5.0, 5.0 }, written.PixelSpacing.ToArray());
                Assert.AreEqual(coarse.DimensionZ, written.GridFrameOffsetVector.Count);
                Assert.AreEqual(5.0, written.GridFrameOffsetVector[1] - written.GridFrameOffsetVector[0]);
                Assert.AreEqual(written.DeclaredVoxelCount, written.VoxelCount);
                Assert.AreEqual("DSClibrary trilinear resample to 5x5x5 mm", written.Dataset.GetString(RTDoseDerivation.DerivationDescription));

                // Coincident voxels are the source's own integers. The reference grid is 2 mm, so a
                // 5 mm node coincides with a source voxel at every even step (10 mm = 5 voxels).
                var writtenRaw = RTDoseDerivation.RawGrid(written);
                Assert.AreEqual(2.0, fine.XRes);
                for (int i = 0; i < 20; i += 2)
                    Assert.AreEqual(fine.DoseValues[(i / 2) * 5], writtenRaw.DoseValues[i], $"voxel {i} along x");

                // EvilDICOM, as the independent reader, agrees about the geometry and the dose.
                var oracle = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path)));
                Assert.AreEqual(coarse.DimensionX, oracle.DimensionX);
                Assert.AreEqual(coarse.DimensionY, oracle.DimensionY);
                Assert.AreEqual(coarse.DimensionZ, oracle.DimensionZ);
                Assert.AreEqual(5.0, oracle.XRes);
                Assert.AreEqual(5.0, oracle.ZRes);
                Assert.AreEqual(fine.X0, oracle.X0, 1e-6);
                var oracleFine = new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(TestData.DoseFiles[0])));
                // A node both grids share (50 mm from the origin: 25 fine voxels, 10 coarse), read
                // through EvilDICOM's own scaling on both sides.
                double px = oracle.X0 + 50, py = oracle.Y0 + 50, pz = oracle.Z0 + 50;
                Assert.AreEqual(oracleFine.GetPointDose(px, py, pz).Dose, oracle.GetPointDose(px, py, pz).Dose, 1e-9,
                    "the dose at a shared node is the same through EvilDICOM's own scaling");
                Assert.AreEqual(oracleFine.MaxPointDose.Dose, oracle.MaxPointDose.Dose, oracleFine.MaxPointDose.Dose * 0.05,
                    "a 5 mm sample of a smooth field keeps the maximum within a few percent");
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void TheDecimalStringEncoderWritesEvenLengthInvariantValues()
        {
            var bytes = DicomValues.DecimalString(5, 2.5, -99.51);
            string text = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.AreEqual(0, bytes.Length % 2);
            Assert.AreEqual("5\\2.5\\-99.51", text.TrimEnd());
            Assert.AreEqual(0, DicomValues.IntegerString(123).Length % 2);
            Assert.AreEqual(0, DicomValues.UniqueIdentifier("1.2.3").Length % 2);
            Assert.IsTrue(DicomValues.NewUid().StartsWith("2.25.") && DicomValues.NewUid().Length <= 64);
        }
    }
}
