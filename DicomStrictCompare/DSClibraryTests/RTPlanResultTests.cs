using System;
using System.IO;
using System.Linq;
using DSClibrary.Results.DICOM;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// The plan reader, against facts measured from the files themselves. A dose file cannot identify
    /// its own beam: energy, modality, angles, field size, SSD and monitor units are all in the plan
    /// beside it (Cody, 2026-11-08).
    /// </summary>
    [TestClass]
    public class RTPlanResultTests
    {
        private static RTPlanResult VariedMu() => RTPlanResult.ReadFile(
            Path.Combine(TestData.ResourcesDirectory!, "RP.UnitTest.P5 Varied MU.dcm"));

        [TestMethod]
        public void EveryPlanParses()
        {
            foreach (string path in TestData.PlanFiles)
            {
                var plan = RTPlanResult.ReadFile(path);
                Assert.IsNotNull(plan.SOPInstanceUID, Path.GetFileName(path));
                Assert.IsTrue(plan.Beams.Count > 0, Path.GetFileName(path) + " has no beams");
            }
            Assert.AreEqual(9, TestData.PlanFiles.Length);
        }

        [TestMethod]
        public void ReadsThePlanIdentity()
        {
            var plan = VariedMu();
            Assert.AreEqual("P5 Varied MU", plan.RTPlanLabel);
            Assert.AreEqual("Many different MU's", plan.RTPlanName);
            Assert.AreEqual(9, plan.Beams.Count);
        }

        [TestMethod]
        public void ReadsBeamIdentityAndGeometry()
        {
            var beam = VariedMu().Beam(1)!;
            Assert.AreEqual("X-095A-10.0-1", beam.BeamName);
            Assert.AreEqual("PHOTON", beam.RadiationType);
            Assert.AreEqual("Mirage", beam.TreatmentMachineName);
            Assert.AreEqual(1000.0, beam.SourceAxisDistance!.Value, 1e-9);
            Assert.AreEqual(0.0, beam.GantryAngle!.Value, 1e-9);
            Assert.AreEqual(0.0, beam.BeamLimitingDeviceAngle!.Value, 1e-9);
            Assert.AreEqual(3, beam.IsocenterPosition.Count);
        }

        [TestMethod]
        public void ReadsMonitorUnitsFromTheFractionGroup()
        {
            // The varied metersets, which is what the plan is named for. MU lives in the fraction
            // group rather than on the beam: a beam is geometry, a fraction group says how much.
            var plan = VariedMu();
            Assert.AreEqual(95.0, plan.MonitorUnitsFor(1)!.Value, 1e-9);
            Assert.AreEqual(99.0, plan.MonitorUnitsFor(4)!.Value, 1e-9);
            Assert.AreEqual(101.0, plan.MonitorUnitsFor(6)!.Value, 1e-9);
            Assert.AreEqual(9, plan.MonitorUnitsByBeamNumber.Count);
        }

        [TestMethod]
        public void BeamNumbersAreNotContiguousByModality()
        {
            // 1,2,3 are the 95 MU set across X, F and e; 4,5 and 8 are 99; 6,7 and 9 are 101. Worth
            // pinning because it is the sort of thing a reader might assume is ordered.
            var plan = VariedMu();
            Assert.AreEqual("X-095A-10.0-1", plan.Beam(1)!.BeamName);
            Assert.AreEqual("X-099A-10.0-1", plan.Beam(4)!.BeamName);
            Assert.AreEqual("X-101A-10.0-1", plan.Beam(6)!.BeamName);
            Assert.AreEqual("ELECTRON", plan.Beam(3)!.RadiationType);
        }

        [TestMethod]
        public void StaticBeamsAreNotFlaggedDynamic()
        {
            Assert.IsFalse(VariedMu().Beam(1)!.MayBeDynamic);
        }

        [TestMethod]
        public void ReadingADoseAsAPlanIsRefused()
        {
            Assert.ThrowsExactly<DicomFormatException>(
                () => RTPlanResult.ReadFile(TestData.DoseFiles[0]));
        }

        [TestMethod]
        public void TheBeamNamePrefixIsTheFluenceMode()
        {
            // Settles what X-, F- and e- mean, by measurement rather than by reading filenames:
            // X is a flattened photon beam, F is flattening-filter-free, e is an electron beam.
            var plan = VariedMu();

            Assert.AreEqual("PHOTON", plan.Beam(1)!.RadiationType);
            Assert.AreEqual(false, plan.Beam(1)!.IsFluenceModeFff, "X- is flattened");

            Assert.AreEqual("PHOTON", plan.Beam(2)!.RadiationType);
            Assert.AreEqual(true, plan.Beam(2)!.IsFluenceModeFff, "F- is FFF");

            Assert.AreEqual("ELECTRON", plan.Beam(3)!.RadiationType);
        }

        [TestMethod]
        public void ReadsEnergyAndFieldSizeFromTheJaws()
        {
            // The "10.0" in a beam name is the field size in centimetres, and the energy is 10 MV for
            // the photons and 20 MeV for the electron - so the name's leading number is not energy.
            var plan = VariedMu();

            Assert.AreEqual(10.0, plan.Beam(1)!.NominalBeamEnergy!.Value, 1e-9);
            Assert.AreEqual(20.0, plan.Beam(3)!.NominalBeamEnergy!.Value, 1e-9);

            var (x1, y1) = plan.Beam(1)!.FieldSizeAtIsocentre;
            Assert.AreEqual(100.0, x1!.Value, 1e-9);
            Assert.AreEqual(100.0, y1!.Value, 1e-9);
            CollectionAssert.AreEqual(new[] { -50.0, 50.0 }, plan.Beam(1)!.LeafJawPositions["X"].ToArray());

            // The electron cone is wider: 140 mm.
            var (x3, y3) = plan.Beam(3)!.FieldSizeAtIsocentre;
            Assert.AreEqual(140.0, x3!.Value, 1e-9);
            Assert.AreEqual(140.0, y3!.Value, 1e-9);
        }

        [TestMethod]
        public void NeitherSsdNorSurfaceEntryPointIsPresentInThesePlans()
        {
            // Measured by a raw byte scan as well as through the reader: SourceToSurfaceDistance
            // (300A,0130) and SurfaceEntryPoint (300A,012E) occur ZERO times in these plans, while
            // IsocenterPosition and SourceAxisDistance are present.
            //
            // That matters for sampling a line out of a dose. swift's addendum of 2026-10-02
            // recommended pymedphys's convention - surface from SurfaceEntryPoint, or failing that the
            // isocentre with y + (SSD - SAD) - and NEITHER input exists here. So the surface cannot be
            // located from these plans alone, and the SSD has to come from the measurement, whose own
            // header states it (the committed mcc says SSD=1000.00). Arguably better: the scan knows
            // its own setup.
            //
            // This test will start failing against a plan that does carry them, which is the point.
            foreach (string path in TestData.PlanFiles)
            {
                var plan = RTPlanResult.ReadFile(path);
                foreach (var beam in plan.Beams)
                {
                    Assert.IsNull(beam.SourceToSurfaceDistance, Path.GetFileName(path) + " SSD");
                    Assert.AreEqual(0, beam.SurfaceEntryPoint.Count, Path.GetFileName(path) + " entry point");
                    Assert.AreEqual(3, beam.IsocenterPosition.Count, Path.GetFileName(path) + " isocentre");
                    Assert.IsNotNull(beam.SourceAxisDistance, Path.GetFileName(path) + " SAD");
                }
            }
        }

        [TestMethod]
        public void PatientSetupNamesTheRegimeButNotTheDistance()
        {
            // The only thing in these plans that speaks to SSD at all, and it gives the technique
            // rather than a distance. Measured across all nine plans: every photon beam is
            // ISOCENTRIC, every electron beam is FIXED_SSD, and every setup is HFS.
            var plan = VariedMu();

            foreach (var setup in plan.SetupsByNumber.Values)
                Assert.IsTrue(setup.IsHeadFirstSupine, $"setup {setup.Number} position");

            foreach (var beam in plan.Beams)
            {
                var setup = plan.SetupFor(beam)!;
                Assert.IsNotNull(setup, $"beam {beam.BeamNumber} has no setup");
                if (beam.RadiationType == "ELECTRON")
                    Assert.IsTrue(setup.IsFixedSsd, $"{beam.BeamName} should be FIXED_SSD");
                else
                    Assert.IsTrue(setup.IsIsocentric, $"{beam.BeamName} should be ISOCENTRIC");
            }

            Assert.AreEqual(9, plan.SetupsByNumber.Count);
        }

        [TestMethod]
        public void HeadFirstSupineHoldsAcrossEveryPlan()
        {
            // The orientation the geometry convention requires, measured rather than assumed.
            foreach (string path in TestData.PlanFiles)
            {
                var plan = RTPlanResult.ReadFile(path);
                Assert.IsTrue(plan.SetupsByNumber.Values.All(s => s.IsHeadFirstSupine),
                    Path.GetFileName(path));
            }
        }

        [TestMethod]
        public void TheDistancesThePlansDoCarry()
        {
            // Answering "is there anything in the plan file that might indicate some form of
            // distance?" with what is actually there. None of it is an SSD.
            var beam = VariedMu().Beam(1)!;

            Assert.AreEqual(1000.0, beam.SourceAxisDistance!.Value, 1e-9);
            CollectionAssert.AreEqual(
                new[] { 0.0, -100.00931966449, 1.4210854715e-14 },
                beam.IsocenterPosition.ToArray());
            Assert.AreEqual(0.0, beam.TableTopVerticalPosition!.Value, 1e-9);
            Assert.AreEqual(1000.0, beam.TableTopLongitudinalPosition!.Value, 1e-9);
            Assert.AreEqual(0.0, beam.TableTopLateralPosition!.Value, 1e-9);
        }

        [TestMethod]
        public void TheIsocentreIsIdenticalInEveryPlan()
        {
            foreach (string path in TestData.PlanFiles)
                foreach (var beam in RTPlanResult.ReadFile(path).Beams)
                    Assert.AreEqual(-100.00931966449, beam.IsocenterPosition[1], 1e-9,
                        Path.GetFileName(path));
        }
    }
}
