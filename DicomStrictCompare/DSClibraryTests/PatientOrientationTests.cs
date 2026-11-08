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
    /// Patient orientation, handled for the two cases this project actually has: RT Dose to RT Dose
    /// like for like, and RT Dose to profiles in matching geometry (Cody, 2026-11-08). In both the two
    /// sides are already in one frame, so what is needed is a compatibility check and an axis mapping
    /// - not a transform engine.
    /// </summary>
    [TestClass]
    public class PatientOrientationTests
    {
        [TestMethod]
        public void ParsesTheDicomDefinedTerms()
        {
            Assert.AreEqual(PatientOrientation.HeadFirstSupine, PatientOrientations.Parse("HFS"));
            Assert.AreEqual(PatientOrientation.HeadFirstProne, PatientOrientations.Parse("hfp"));
            Assert.AreEqual(PatientOrientation.FeetFirstSupine, PatientOrientations.Parse(" FFS "));
            Assert.AreEqual(PatientOrientation.FeetFirstDecubitusLeft, PatientOrientations.Parse("FFDL"));
            Assert.AreEqual(PatientOrientation.Unknown, PatientOrientations.Parse("sideways"));
            Assert.AreEqual(PatientOrientation.Unknown, PatientOrientations.Parse(null));
            Assert.AreEqual("HFS", PatientOrientations.ToDicom(PatientOrientation.HeadFirstSupine));
        }

        [TestMethod]
        public void HeadFirstSupineAtGantryZeroMapsTheAxes()
        {
            // The convention swift's addendum took from pymedphys, and it holds BECAUSE of this
            // orientation: crossplane is the patient's left-right axis, inplane is gun-target, and
            // depth runs posterior - which for a supine patient under a vertical beam is into the
            // phantom.
            var geometry = ScanGeometry.HeadFirstSupineGantryZero;

            Assert.IsTrue(geometry.IsSupported);
            Assert.AreEqual(PatientDirection.PatientLeft, geometry.DirectionFor(CurveType.Crossplane));
            Assert.AreEqual(PatientDirection.TowardHead, geometry.DirectionFor(CurveType.Inplane));
            Assert.AreEqual(PatientDirection.Posterior, geometry.DirectionFor(CurveType.Pdd));
        }

        [TestMethod]
        public void UnsupportedOrientationsAreRefusedByName()
        {
            // Prone would flip the depth axis: the same beam travels along -y, so a PDD read as +y
            // would come out backwards - plausible and silent. Refused rather than guessed.
            var prone = new ScanGeometry(PatientOrientation.HeadFirstProne, 0);
            Assert.IsFalse(prone.IsSupported);
            StringAssert.Contains(prone.DescribeUnsupported(), "HeadFirstProne");
            Assert.ThrowsException<InvalidOperationException>(() => prone.DirectionFor(CurveType.Pdd));

            var unknown = new ScanGeometry(PatientOrientation.Unknown, 0);
            Assert.IsFalse(unknown.IsSupported);
        }

        [TestMethod]
        public void AGantryAngleOtherThanZeroIsRefused()
        {
            // The depth axis turns with the gantry, and no reference file exercises another angle.
            var oblique = new ScanGeometry(PatientOrientation.HeadFirstSupine, 90);
            Assert.IsFalse(oblique.IsSupported);
            StringAssert.Contains(oblique.DescribeUnsupported(), "gantry");

            // A tenth of a degree of slop, since an angle is stored as a decimal string.
            Assert.IsTrue(new ScanGeometry(PatientOrientation.HeadFirstSupine, 0.05).IsSupported);
            Assert.IsFalse(new ScanGeometry(PatientOrientation.HeadFirstSupine, 0.5).IsSupported);
        }

        [TestMethod]
        public void DiagonalsHaveNoDirectionYet()
        {
            Assert.ThrowsException<NotSupportedException>(
                () => ScanGeometry.HeadFirstSupineGantryZero.DirectionFor(CurveType.PositiveDiagonal));
        }

        [TestMethod]
        public void EveryReferenceVolumeMapsToASupportedGeometry()
        {
            var mapper = new RTDoseVolumeMapper();
            foreach (var pairing in RTFolderReader.Read(TestData.ResourcesDirectory!).Doses)
            {
                var volume = mapper.Map(pairing);
                string name = Path.GetFileName(pairing.Dose.SourcePath);

                Assert.IsNotNull(volume.Geometry, name);
                Assert.AreEqual(PatientOrientation.HeadFirstSupine, volume.Geometry!.Orientation, name);
                Assert.IsTrue(volume.Geometry.IsSupported, name + ": " + volume.Geometry.DescribeUnsupported());
            }
        }

        [TestMethod]
        public void EveryReferenceVolumeSharesOneFrameOfReference()
        {
            // What makes RT Dose to RT Dose safe here without any transform. Measured: one frame
            // across all 26 doses and all 9 plans.
            var mapper = new RTDoseVolumeMapper();
            var volumes = RTFolderReader.Read(TestData.ResourcesDirectory!).Doses
                .Select(mapper.Map)
                .ToArray();

            var frames = volumes.Select(v => v.FrameOfReferenceUID).Distinct().ToArray();
            Assert.AreEqual(1, frames.Length, "expected one frame of reference");
            Assert.IsNotNull(frames[0]);

            // So any two of them are comparable, which is the property the guard exists to express.
            Assert.IsTrue(volumes[0].IsComparableWith(volumes[^1]));
            Assert.AreEqual("nothing", volumes[0].DescribeIncomparability(volumes[^1]));
        }
    }
}
