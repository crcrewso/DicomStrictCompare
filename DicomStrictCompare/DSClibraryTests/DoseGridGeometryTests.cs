using DSClibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// What the dose grid's coordinates actually are, measured through the code rather than assumed.
    /// <para>
    /// These exist because a piece of DSCcore arithmetic depends on the opposite being true.
    /// <c>MatchedDosePair.GeneratePDD</c>'s fallback computes a grid centre as
    /// <c>(XMax - X0) / 2</c>, which is the half width and equals the centre only when X0 is exactly
    /// zero. DICOM does not make it zero: ImagePositionPatient (0020,0032) is the centre of the first
    /// voxel in the patient coordinate system, whose origin is set by the Frame of Reference and for
    /// RT planning sits at or near the isocentre - so a grid spanning the isocentre has a negative
    /// low corner.
    /// </para>
    /// </summary>
    [TestClass]
    public class DoseGridGeometryTests
    {
        private static DoseMatrixOptimal Reference()
        {
            byte[] bytes = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            return new DoseMatrixOptimal(new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(bytes)));
        }

        [TestMethod]
        public void TheGridOriginIsNegative()
        {
            var dose = Reference();
            Assert.IsTrue(dose.X0 < 0, $"X0 was {dose.X0}");
            Assert.IsTrue(dose.Y0 < 0, $"Y0 was {dose.Y0}");
            Assert.IsTrue(dose.Z0 < 0, $"Z0 was {dose.Z0}");
        }

        [TestMethod]
        public void TheGridOriginMatchesImagePositionPatient()
        {
            // Read straight out of the file's (0020,0032): -99.50\-99.51\-100.00. This is the link
            // that was inference until now - that EvilDICOM's RTDose.X0 is that value and not some
            // shifted or normalised origin.
            var dose = Reference();
            Assert.AreEqual(-99.50, dose.X0, 0.01);
            Assert.AreEqual(-99.51, dose.Y0, 0.01);
            Assert.AreEqual(-100.00, dose.Z0, 0.01);
        }

        [TestMethod]
        public void HalfWidthIsNotTheCentre()
        {
            // The arithmetic in question, shown to be wrong on real data rather than argued about.
            var dose = Reference();
            double asWritten = (dose.XMax - dose.X0) / 2;   // DSCcore's expression
            double trueCentre = (dose.XMax + dose.X0) / 2;

            Assert.AreEqual(0.0, trueCentre, 0.01, "this grid is centred on the isocentre");
            Assert.AreEqual(99.5, asWritten, 0.1, "the expression yields the half width");
            // And the value it produces is the grid's edge, which is the worst place to sample a PDD.
            Assert.AreEqual(dose.XMax, asWritten, 0.1);
        }

        [TestMethod]
        public void DoseValuesAreAlreadyScaledToGray()
        {
            // Read straight from the file: DoseUnits=GY, BitsAllocated=32, maximum stored integer
            // 2904474, DoseGridScaling 3.5042182425e-07, so the physical maximum is 1.017791 Gy -
            // 101.78 cGy for this 100 MU beam.
            //
            // The question this answers is whether DoseMatrixOptimal's values are physical or raw.
            // Scaling is stored on the type but multiplied into nothing anywhere in DSClibrary or
            // DSCcore, so if the values were raw every absolute dose in the project would be in
            // arbitrary units.
            var dose = Reference();
            Assert.AreEqual(1.017791, dose.MaxPointDose.Dose, 1e-5);
            Assert.AreEqual(3.5042182425e-07, dose.Scaling, 1e-17);
        }

        [TestMethod]
        public void IsInBoundsNoLongerThrowsOnAValidPoint()
        {
            // This replaces a test that asserted the opposite. The guard read `null == pt`, which
            // dispatches to EvilDICOM's overloaded Vector3.op_Equality and dereferences the null
            // operand, so IsInBounds threw NullReferenceException for every valid point. It was never
            // noticed because nothing calls IsInBounds and IsInBoundsTest was an empty stub.
            //
            // Behaviour now lives in DoseMatrixOptimalTests.IsInBoundsTest; this keeps the record of
            // why the guard is written `is null`, so nobody tidies it back.
            var dose = Reference();
            Assert.IsTrue(dose.IsInBounds(new EvilDICOM.Core.Helpers.Vector3(0, 0, 0)));
        }
    }
}
