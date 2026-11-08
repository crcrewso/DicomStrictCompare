using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Verifies this project's own RT Dose reader against EvilDICOM on the real files.
    /// <para>
    /// This is the point of the whole approach. Cody asked whether to keep EvilDICOM or bring the
    /// reader in house, and the answer taken was neither as an either/or: build our own and keep
    /// EvilDICOM as an oracle, so the migration is <b>verified against the thing it replaces</b>
    /// rather than asserted correct. A hand-rolled DICOM parser deserves no trust otherwise.
    /// </para>
    /// <para>
    /// Attribution, kept whether or not a licence demands it: EvilDICOM is MIT, by Rex Cardan.
    /// </para>
    /// </summary>
    [TestClass]
    public class RTDoseResultOracleTests
    {
        [TestMethod]
        public void TheResourcesWereFound()
        {
            Assert.IsTrue(TestData.Found,
                "could not locate the resources folder by walking up from " + AppContext.BaseDirectory);
            Assert.AreEqual(26, TestData.DoseFiles.Length);
            Assert.AreEqual(9, TestData.PlanFiles.Length);
        }

        [TestMethod]
        public void EveryDoseFileAgreesWithEvilDicomOnGeometryAndScaling()
        {
            var failures = new System.Collections.Generic.List<string>();
            var timer = Stopwatch.StartNew();

            foreach (string path in TestData.DoseFiles)
            {
                string name = Path.GetFileName(path);
                try
                {
                    var mine = RTDoseResult.ReadFile(path);
                    var theirs = new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(path));

                    void Check(string what, double expected, double actual, double tolerance)
                    {
                        if (Math.Abs(expected - actual) > tolerance)
                            failures.Add($"{name}: {what} - EvilDICOM {expected}, mine {actual}");
                    }

                    // The grid origin. Mine reads ImagePositionPatient directly; theirs is X0/Y0/Z0.
                    Check("X0", theirs.X0, mine.ImagePositionPatient[0], 1e-6);
                    Check("Y0", theirs.Y0, mine.ImagePositionPatient[1], 1e-6);
                    Check("Z0", theirs.Z0, mine.ImagePositionPatient[2], 1e-6);

                    // Dimensions. DICOM's Columns is x and Rows is y.
                    Check("DimensionX", theirs.DimensionX, mine.Columns ?? -1, 0);
                    Check("DimensionY", theirs.DimensionY, mine.Rows ?? -1, 0);
                    Check("DimensionZ", theirs.DimensionZ, mine.NumberOfFrames ?? -1, 0);

                    // PixelSpacing is row spacing then column spacing: y then x.
                    Check("XRes", theirs.XRes, mine.PixelSpacing[1], 1e-6);
                    Check("YRes", theirs.YRes, mine.PixelSpacing[0], 1e-6);

                    Check("Scaling", theirs.Scaling, mine.DoseGridScaling ?? double.NaN, 1e-18);

                    // The voxel count the dimensions declare must match the bytes actually present.
                    if (mine.DeclaredVoxelCount != mine.VoxelCount)
                        failures.Add($"{name}: declared {mine.DeclaredVoxelCount} voxels, found {mine.VoxelCount}");

                    // And the maximum: my raw integer times the factor should be their physical dose,
                    // which is the one check that exercises the pixel data end to end.
                    double myMax = mine.MaximumRawVoxel * (mine.DoseGridScaling ?? 0);
                    Check("maximum dose (Gy)", theirs.MaxPointDose.Dose, myMax, 1e-9);
                }
                catch (Exception ex)
                {
                    failures.Add($"{name}: threw {ex.GetType().Name}: {ex.Message}");
                }
            }

            timer.Stop();
            Console.WriteLine($"{TestData.DoseFiles.Length} files in {timer.Elapsed.TotalSeconds:0.0}s");
            Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        }

        [TestMethod]
        public void EveryDoseFileResolvesToAPlanByUid()
        {
            // Measured before the reader existed: 26 of 26 resolve this way, none unmatched. This
            // asserts the reader reproduces that, which is what lets the folder handler pair by UID
            // rather than by filename as PyMPAT does.
            var plans = TestData.PlanFiles
                .Select(DicomFileReader.ReadFile)
                .ToDictionary(f => f.SOPInstanceUID!, f => Path.GetFileName(f.SourcePath));

            Assert.AreEqual(9, plans.Count, "nine distinct plan SOPInstanceUIDs");

            var unmatched = TestData.DoseFiles
                .Select(RTDoseResult.ReadFile)
                .Where(d => d.ReferencedRTPlanSOPInstanceUID is null
                            || !plans.ContainsKey(d.ReferencedRTPlanSOPInstanceUID))
                .Select(d => Path.GetFileName(d.SourcePath))
                .ToArray();

            Assert.AreEqual(0, unmatched.Length, "unmatched: " + string.Join(", ", unmatched));
        }

        [TestMethod]
        public void TheReferenceFilesAreAllGrayAxialAndUniformlySpaced()
        {
            foreach (string path in TestData.DoseFiles)
            {
                var dose = RTDoseResult.ReadFile(path);
                string name = Path.GetFileName(path);
                Assert.AreEqual("GY", dose.DoseUnits, name);
                CollectionAssert.AreEqual(new[] { 1.0, 0, 0, 0, 1.0, 0 },
                    dose.ImageOrientationPatient.ToArray(), name + " orientation");
                Assert.IsTrue(dose.HasUniformFrameSpacing, name + " frame spacing");
                Assert.IsTrue(dose.ImagePositionPatient[0] < 0, name + " X0 sign");
            }
        }

        [TestMethod]
        public void ReadingAPlanAsADoseIsRefused()
        {
            Assert.ThrowsExactly<DicomFormatException>(
                () => RTDoseResult.ReadFile(TestData.PlanFiles[0]));
        }
    }
}
