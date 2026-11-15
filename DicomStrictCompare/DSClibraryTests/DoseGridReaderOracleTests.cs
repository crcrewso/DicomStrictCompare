using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// <see cref="DoseGridReader"/> must give exactly the grid EvilDICOM gives: the same geometry, scaling
    /// and maximum, and every voxel bit-identical. EvilDICOM is the oracle, as in <c>RTDoseResultOracleTests</c>.
    /// </summary>
    [TestClass]
    public class DoseGridReaderOracleTests
    {
        private static DoseMatrixOptimal Evil(string path) =>
            new(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path)));

        /// <summary>Every difference between the two grids, as text; empty when they agree.</summary>
        internal static List<string> Differences(DoseMatrixOptimal theirs, DoseMatrixOptimal mine, string name)
        {
            var failures = new List<string>();
            void Check(string what, double expected, double actual)
            {
                if (!expected.Equals(actual)) failures.Add($"{name}: {what} - EvilDICOM {expected:R}, mine {actual:R}");
            }

            Check("DimensionX", theirs.DimensionX, mine.DimensionX);
            Check("DimensionY", theirs.DimensionY, mine.DimensionY);
            Check("DimensionZ", theirs.DimensionZ, mine.DimensionZ);
            Check("X0", theirs.X0, mine.X0);
            Check("Y0", theirs.Y0, mine.Y0);
            Check("Z0", theirs.Z0, mine.Z0);
            Check("XRes", theirs.XRes, mine.XRes);
            Check("YRes", theirs.YRes, mine.YRes);
            Check("ZRes", theirs.ZRes, mine.ZRes);
            Check("XMax", theirs.XMax, mine.XMax);
            Check("YMax", theirs.YMax, mine.YMax);
            Check("ZMax", theirs.ZMax, mine.ZMax);
            Check("Scaling", theirs.Scaling, mine.Scaling);
            Check("Length", theirs.Length, mine.Length);
            Check("MaxPointDose.Dose", theirs.MaxPointDose.Dose, mine.MaxPointDose.Dose);
            Check("MaxPointDose.X", theirs.MaxPointDose.X, mine.MaxPointDose.X);
            Check("MaxPointDose.Y", theirs.MaxPointDose.Y, mine.MaxPointDose.Y);
            Check("MaxPointDose.Z", theirs.MaxPointDose.Z, mine.MaxPointDose.Z);

            if (theirs.DoseValues.Length == mine.DoseValues.Length)
            {
                int differ = 0, first = -1;
                for (int i = 0; i < mine.DoseValues.Length; i++)
                    if (!theirs.DoseValues[i].Equals(mine.DoseValues[i])) { differ++; if (first < 0) first = i; }
                if (differ > 0)
                    failures.Add($"{name}: {differ} voxel(s) differ, first at {first}: EvilDICOM {theirs.DoseValues[first]:R}, mine {mine.DoseValues[first]:R}");
            }
            return failures;
        }

        [TestMethod]
        public void EveryReferenceDoseMatchesEvilDicomExactly()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            var failures = TestData.DoseFiles
                .SelectMany(p => Differences(Evil(p), DoseGridReader.Read(p), Path.GetFileName(p)))
                .ToList();
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        [TestMethod]
        public void AHeaderLongerThanTheFirstPrefixIsStillRead()
        {
            // The reader starts with 64 KB of the file; a 200 KB element before PixelData makes it grow.
            string source = TestData.DoseFiles[0];
            var dataset = DicomFileReader.ReadFile(source).Dataset
                .WithElement(RTDoseDerivation.DerivationDescription, DicomValues.Text(new string('x', 200_000)));
            string path = TempFile();
            try
            {
                DicomFileWriter.WriteFile(path, dataset);
                var failures = Differences(Evil(path), DoseGridReader.Read(path), "long header");
                Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
            }
            finally { File.Delete(path); }
        }

        /// <summary>A 16-bit copy of a reference dose; signed copies are shifted so some voxels are negative.</summary>
        private static (string Path, short[] Stored, double Scaling) SixteenBit(bool signed)
        {
            // Every reference file is 32-bit unsigned, so the other encodings are made from one of them.
            var dose = RTDoseResult.ReadFile(TestData.DoseFiles[0]);
            int count = dose.VoxelCount;
            var stored = new short[count];
            var bytes = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                int v = (int)(dose.RawVoxel(i) >> 8) - (signed ? 4000 : 0);
                stored[i] = (short)v;
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 2, 2), (ushort)v);
            }
            var dataset = dose.Dataset
                .WithElement(DicomTags.BitsAllocated, DicomValues.UnsignedShort(16))
                .WithElement(DicomTags.BitsStored, DicomValues.UnsignedShort(16))
                .WithElement(DicomTags.HighBit, DicomValues.UnsignedShort(15))
                .WithElement(DicomTags.PixelRepresentation, DicomValues.UnsignedShort((ushort)(signed ? 1 : 0)))
                .WithElement(DicomTags.DoseType, DicomValues.Text(signed ? "ERROR" : "PHYSICAL"))   // PS3.3 C.8.8.3.4.6
                .WithElement(DicomTags.PixelData, bytes);
            string path = TempFile();
            DicomFileWriter.WriteFile(path, dataset);
            return (path, stored, dose.DoseGridScaling!.Value);
        }

        [TestMethod]
        public void UnsignedSixteenBitVoxelsMatchEvilDicom()
        {
            var (path, _, _) = SixteenBit(signed: false);
            try
            {
                var failures = Differences(Evil(path), DoseGridReader.Read(path), "unsigned 16-bit");
                Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void SignedVoxelsAreTwosComplementWhereEvilDicomReadsThemUnsigned()
        {
            // PS3.3 C.8.8.3.4.6: an ERROR dose is two's complement (PixelRepresentation 1). Measured
            // 2026-11-14, EvilDICOM 2.0.6.5 ignores PixelRepresentation and DoseType: 16-bit always reads
            // unsigned (a stored -4000 becomes 61536), 32-bit always signed. Elsewhere the two must agree.
            var (path, stored, scaling) = SixteenBit(signed: true);
            try
            {
                var theirs = Evil(path);
                var mine = DoseGridReader.Read(path);
                int negative = 0;
                for (int i = 0; i < stored.Length; i++)
                {
                    Assert.AreEqual(stored[i] * scaling, mine.DoseValues[i], $"voxel {i}");
                    if (stored[i] < 0) { negative++; Assert.AreEqual((ushort)stored[i] * scaling, theirs.DoseValues[i], $"EvilDICOM voxel {i}"); }
                    else Assert.AreEqual(theirs.DoseValues[i], mine.DoseValues[i], $"voxel {i}");
                }
                Assert.IsTrue(negative > 0, "the test file should hold negative voxels");
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void TruncatedPixelDataIsRefused()
        {
            var dose = RTDoseResult.ReadFile(TestData.DoseFiles[0]);
            var dataset = dose.Dataset.WithElement(DicomTags.PixelData, dose.PixelData.Slice(0, 1000).ToArray());
            string path = TempFile();
            try
            {
                DicomFileWriter.WriteFile(path, dataset);
                Assert.ThrowsExactly<DicomFormatException>(() => DoseGridReader.Read(path));
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void APlanIsRefused()
        {
            Assert.ThrowsExactly<DicomFormatException>(() => DoseGridReader.Read(TestData.PlanFiles[0]));
        }

        private static string TempFile() => Path.Combine(Path.GetTempPath(), $"dsc-gridreader-{Guid.NewGuid():N}.dcm");
    }
}
