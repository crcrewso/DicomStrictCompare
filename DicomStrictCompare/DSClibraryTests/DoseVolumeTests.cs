using System;
using System.Linq;
using DSClibrary;
using DSClibrary.Profiles;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    [TestClass]
    public class DoseTests
    {
        [TestMethod]
        public void GrayAndCentigrayAreTheSameDose()
        {
            Assert.AreEqual(Dose.FromCentigray(100), Dose.FromGray(1));
            // The measured maximum of RD.UnitTest.P1Ref.X-100A-10.0-1.
            Assert.AreEqual(101.7791, Dose.FromGray(1.017791).Centigray, 1e-9);
        }

        [TestMethod]
        public void ConvertsForDisplayWithoutChangingStorage()
        {
            var d = Dose.FromCentigray(250);
            Assert.AreEqual(250.0, d.Centigray, 1e-9);
            Assert.AreEqual(2.5, d.Gray, 1e-9);
        }

        [TestMethod]
        public void NegativeZeroEqualsPositiveZero()
        {
            Assert.AreEqual(Dose.FromCentigray(0.0), Dose.FromCentigray(-0.0));
            Assert.AreEqual(Dose.FromCentigray(0.0).GetHashCode(), Dose.FromCentigray(-0.0).GetHashCode());
        }

        [TestMethod]
        public void RejectsNonFiniteValues()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Dose.FromCentigray(double.NaN));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Dose.FromGray(double.PositiveInfinity));
        }

        [TestMethod]
        public void FractionOfRefusesAZeroReference()
        {
            Assert.AreEqual(0.5, Dose.FromCentigray(50).FractionOf(Dose.FromCentigray(100)), 1e-12);
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => Dose.FromCentigray(50).FractionOf(Dose.Zero));
        }

        [TestMethod]
        public void DisplayUnitAffectsFormattingOnly()
        {
            var original = Doses.DisplayUnit;
            try
            {
                var d = Dose.FromCentigray(250);
                Doses.DisplayUnit = DoseUnit.Gray;
                StringAssert.Contains(d.ToString(), "Gy");
                Assert.AreEqual(250.0, d.Centigray, 1e-9);
            }
            finally
            {
                Doses.DisplayUnit = original;
            }
        }
    }

    [TestClass]
    public class DoseVolumeTests
    {
        private static BeamKey Beam() => new(
            Modality.Photon, 6, false,
            Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000));

        /// <summary>A 2x2x2 grid whose maximum value is 1.0, in the grid's own units of gray.</summary>
        private static DoseMatrixOptimal Grid(double max = 1.0) => new(
            2, 2, 2, -5, -5, -5, 5, 5, 5,
            new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, max });

        private static DoseMatrixOptimal RealReference() =>
            new(new EvilDICOM.RT.RTDose(
                EvilDICOM.Core.DICOMObject.Read(Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1)));

        [TestMethod]
        public void AVolumeMustStateItsScale()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new DoseVolume(Beam(), Grid(), (ValueScale)0));
        }

        [TestMethod]
        public void AnAbsoluteVolumeReportsItsMaximumInCentigray()
        {
            // The grid is in gray, as EvilDICOM hands it back; the volume converts at its boundary.
            var volume = new DoseVolume(Beam(), RealReference(), ValueScale.AbsoluteCentigray);
            Assert.AreEqual(101.7791, volume.MaximumDose!.Value.Centigray, 1e-4);
            Assert.AreEqual(1.017791, volume.MaximumDose!.Value.Gray, 1e-6);
            Assert.IsNull(volume.MaximumRelativePercent);
        }

        [TestMethod]
        public void ARelativeVolumeHasNoAbsoluteMaximum()
        {
            var volume = new DoseVolume(Beam(), Grid(max: 100.0), ValueScale.RelativePercent);
            Assert.IsNull(volume.MaximumDose);
            Assert.AreEqual(100.0, volume.MaximumRelativePercent!.Value, 1e-9);
        }

        [TestMethod]
        public void MonitorUnitsAreRecorded()
        {
            var volume = new DoseVolume(Beam(), Grid(), ValueScale.AbsoluteCentigray, monitorUnits: 95);
            Assert.AreEqual(95.0, volume.MonitorUnits!.Value, 1e-9);
            StringAssert.Contains(volume.ToString(), "95 MU");
        }

        [TestMethod]
        public void MonitorUnitsNeverChangeTheDose()
        {
            // The rule, asserted directly: "MU should be a parameter of the DoseVolume, if it exists,
            // but should not be used to change the dose" (Cody, 2026-11-08). Three volumes over one
            // grid at 95, 99 and 101 MU - the P5 Varied MU metersets - must report one dose.
            var grid = Grid();
            var doses = new[] { 95.0, 99.0, 101.0 }
                .Select(mu => new DoseVolume(Beam(), grid, ValueScale.AbsoluteCentigray, mu))
                .Select(v => v.MaximumDose!.Value)
                .Distinct()
                .ToArray();

            Assert.AreEqual(1, doses.Length, "MU must not scale the dose");
            Assert.AreEqual(Dose.FromGray(1.0), doses[0]);
        }

        [TestMethod]
        public void AVolumeWithNoMonitorUnitsIsStillValid()
        {
            // A dose read without its plan: the 1 MU to 1 cGy convention must not be used to invent
            // one, because it is a calibration a site adopts and not a property of the data.
            var volume = new DoseVolume(Beam(), Grid(), ValueScale.AbsoluteCentigray);
            Assert.IsNull(volume.MonitorUnits);
            Assert.IsNotNull(volume.MaximumDose);
        }

        [TestMethod]
        public void VolumesInDifferentScalesAreNotComparable()
        {
            var absolute = new DoseVolume(Beam(), Grid(), ValueScale.AbsoluteCentigray);
            var relative = new DoseVolume(Beam(), Grid(), ValueScale.RelativePercent);

            Assert.IsFalse(absolute.IsComparableWith(relative));
            Assert.IsTrue(absolute.IsComparableWith(
                new DoseVolume(Beam(), Grid(), ValueScale.AbsoluteCentigray)));
        }

        [TestMethod]
        public void AVolumeJoinsItsBeamGroupAndClearsTheMissingReference()
        {
            var beam = Beam();
            var profile = new Profile(beam, new ScanKey(CurveType.Pdd),
                new[] { new ProfilePoint(Distance.Zero, 1, null), new ProfilePoint(Distance.FromMillimetres(10), 2, null) });
            var volume = new DoseVolume(beam, Grid(), ValueScale.AbsoluteCentigray, 100);

            var set = MeasurementSet.Create(new[] { profile }, new[] { volume });

            Assert.AreEqual(1, set.Beams.Count);
            Assert.AreEqual(1, set.Beams[0].Volumes.Count);
            Assert.IsFalse(set.Beams[0].IsMissingReference);
            Assert.AreEqual(0, set.MissingReferences.Count());
        }
    }
}
