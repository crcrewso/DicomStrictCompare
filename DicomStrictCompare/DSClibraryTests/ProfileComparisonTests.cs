using System;
using System.Collections.Generic;
using System.Linq;
using DSClibrary;
using DSClibrary.Mapping.PTW;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using DSClibrary.Results.PTW;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Scan-to-dose profile comparison: the phantom frame, sampling a dose along a scan's line, the comparison itself and
    /// pairing a scan with its dose. Synthetic grids whose values are a formula give answers worked out by hand; the
    /// resources' P1Ref 10 x 10 cm dose and the committed PTW scan check the geometry on real files.
    /// </summary>
    [TestClass]
    public class ProfileComparisonTests
    {
        // ---- synthetic grids ----

        /// <summary>A grid from -100 to 100 mm in x and z and -10 to 200 mm in y, 2 mm spacing, values f(x, y, z).</summary>
        private static DoseMatrixOptimal Grid(Func<double, double, double, double> f, double step = 2)
        {
            int nx = (int)(200 / step) + 1, ny = (int)(210 / step) + 1, nz = nx;
            var values = new double[nx * ny * nz];
            for (int k = 0; k < nz; k++)
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                        values[i + j * nx + k * nx * ny] = f(-100 + i * step, -10 + j * step, -100 + k * step);
            return new DoseMatrixOptimal(nx, ny, nz, -100, -10, -100, step, step, step, values);
        }

        /// <summary>Head-first supine at gantry 0, axis at x = z = 0, surface at y = 0.</summary>
        private static readonly PhantomFrame Frame =
            new(0, 0, 0, ScanGeometry.HeadFirstSupineGantryZero, SurfaceSource.MeasuredSsd, null, 2);

        private static readonly BeamKey Beam = new(Modality.Photon, 6, false,
            Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000));

        private static Profile Scan(CurveType curve, double? depth, IEnumerable<(double Position, double Value)> points,
            double? offCross = null, double? offIn = null) =>
            new(Beam, new ScanKey(curve, Distance.FromMillimetres(depth), Distance.FromMillimetres(offIn), Distance.FromMillimetres(offCross)),
                points.Select(p => new ProfilePoint(Distance.FromMillimetres(p.Position), p.Value, null)));

        private static IEnumerable<double> Range(double from, double to, double step)
        {
            for (double p = from; p <= to + 1e-9; p += step) yield return Math.Round(p, 9);
        }

        /// <summary>A flat 100 mm field with 5 mm linear penumbrae, at any depth: 1 inside |p| &lt;= 47.5, 0 beyond 52.5.</summary>
        private static double Field(double p) => Math.Clamp((52.5 - Math.Abs(p)) / 5, 0, 1) * 0.98 + 0.02;

        // ---- sampling ----

        [TestMethod]
        public void DepthDoseRunsAlongPlusYFromTheSurface()
        {
            // Head-first supine at gantry 0: the beam travels along +y, so depth d is y = surface + d.
            var grid = Grid((x, y, z) => y + 1000);
            var frame = Frame with { SurfaceYMm = 20 };
            var scan = Scan(CurveType.Pdd, null, Range(0, 100, 10).Select(d => (d, 1.0)));
            var line = DoseLineSampler.Sample(grid, frame, scan, "synthetic", gridInGray: false);
            foreach (var p in line.Calculated.Points)
                Assert.AreEqual(1000 + 20 + p.Position.Millimetres, p.Value, 1e-9);
            Assert.AreEqual(0, line.PointsOutsideGrid);
        }

        [TestMethod]
        public void ProfilesRunAlongXAndZAtTheirDepthAndOffsets()
        {
            // Crossplane is +x and inplane +z (ScanGeometry); the depth is along +y; the scan's own off-axis offsets move the line.
            var grid = Grid((x, y, z) => 3 * x + 7 * y + 5 * z);
            var cross = Scan(CurveType.Crossplane, 50, Range(-40, 40, 10).Select(p => (p, 1.0)), offIn: 10);
            var inplane = Scan(CurveType.Inplane, 50, Range(-40, 40, 10).Select(p => (p, 1.0)), offCross: -6);
            foreach (var p in DoseLineSampler.Sample(grid, Frame, cross, "s", false).Calculated.Points)
                Assert.AreEqual(3 * p.Position.Millimetres + 7 * 50 + 5 * 10, p.Value, 1e-9);
            foreach (var p in DoseLineSampler.Sample(grid, Frame, inplane, "s", false).Calculated.Points)
                Assert.AreEqual(3 * -6 + 7 * 50 + 5 * p.Position.Millimetres, p.Value, 1e-9);
        }

        [TestMethod]
        public void GrayGridsAreSampledInCentigray()
        {
            var grid = Grid((x, y, z) => 1.5);
            var line = DoseLineSampler.Sample(grid, Frame, Scan(CurveType.Pdd, null, new[] { (10.0, 1.0), (20.0, 1.0) }), "s");
            Assert.IsTrue(line.Calculated.Points.All(p => Math.Abs(p.Value - 150) < 1e-9));
            Assert.AreEqual(ValueScale.AbsoluteCentigray, line.Calculated.ValueScale);
        }

        [TestMethod]
        public void PositionsOutsideTheGridAreLeftOutAndCounted()
        {
            // The grid ends at x = 100; nothing is extrapolated.
            var grid = Grid((x, y, z) => 1);
            var scan = Scan(CurveType.Crossplane, 50, Range(-120, 120, 10).Select(p => (p, 1.0)));
            var line = DoseLineSampler.Sample(grid, Frame, scan, "s", false);
            Assert.AreEqual(4, line.PointsOutsideGrid);   // -120, -110, 110, 120
            Assert.AreEqual(-100, line.Calculated.PositionRange.Min.Millimetres, 1e-9);
            Assert.AreEqual(100, line.Calculated.PositionRange.Max.Millimetres, 1e-9);
        }

        [TestMethod]
        public void NodesAlongALineAreEvenAndExact()
        {
            var grid = Grid((x, y, z) => x * x);
            var scan = Scan(CurveType.Crossplane, 50, new[] { (0.0, 1.0) });
            var (positions, values) = DoseLineSampler.AtNodes(grid, Frame, scan.Scan, -11, 11);
            CollectionAssert.AreEqual(Range(-10, 10, 2).ToArray(), positions);
            for (int i = 0; i < positions.Length; i++) Assert.AreEqual(positions[i] * positions[i], values[i], 1e-9);
        }

        [TestMethod]
        public void DiagonalsAreRefused()
        {
            var grid = Grid((x, y, z) => 1);
            var scan = Scan(CurveType.PositiveDiagonal, 50, new[] { (0.0, 1.0) });
            Assert.ThrowsExactly<NotSupportedException>(() => DoseLineSampler.Sample(grid, Frame, scan, "s"));
        }

        // ---- the comparison ----

        private static readonly ProfileComparisonOptions TwoTwo = new(2, 2);

        [TestMethod]
        public void IdenticalCurvesPassEverywhereWithZeroConstancy()
        {
            var grid = Grid((x, y, z) => Field(x), step: 1);
            var scan = Scan(CurveType.Crossplane, 50, Range(-70, 70, 1).Select(p => (p, 3.0 * Field(p))));
            var result = ProfileComparison.Compare(scan, grid, Frame, TwoTwo, gridInGray: false);
            Assert.AreEqual(NormalizationType.CentralAxis, result.Normalization);
            Assert.AreEqual(100.0, result.PassRate, 1e-12);
            Assert.AreEqual(0.0, result.MaxGammaFound!.Value, 1e-9);
            Assert.AreEqual(0.0, result.ConstancyPercent!.Value, 1e-9);
            Assert.AreEqual(result.MeasuredMetrics!.FieldSizeMm, result.CalculatedMetrics!.FieldSizeMm, 1e-9);
        }

        [TestMethod]
        public void APureShiftGivesGammaOfAtMostShiftOverDistance()
        {
            // The measurement is the field moved 1 mm: with a negligible dose criterion every point's gamma is the
            // distance to where the calculation has its dose, at most 1 mm, so at most 1/2 at a 2 mm criterion.
            var grid = Grid((x, y, z) => Field(x), step: 1);
            var scan = Scan(CurveType.Crossplane, 50, Range(-60, 60, 1).Select(p => (p, Field(p - 1))));
            var options = new ProfileComparisonOptions(0.001, 2, CentralAxisFallbackFraction: 0);
            var result = ProfileComparison.Compare(scan, grid, Frame, options, gridInGray: false);
            Assert.AreEqual(100.0, result.PassRate, 1e-12);
            Assert.IsTrue(result.MaxGammaFound <= 0.5 + 0.06, $"max gamma {result.MaxGammaFound}");   // the search steps 0.2 mm
            var tight = ProfileComparison.Compare(scan, grid, Frame, options with { DistanceMm = 0.5 }, gridInGray: false);
            Assert.IsTrue(tight.PassRate < 100, "a 1 mm shift must fail a 0.5 mm criterion in the penumbra");
        }

        [TestMethod]
        public void StrictRuleIsDoseDifferenceThenTheRangeAtPlusAndMinusTheDistance()
        {
            // The same 1 mm shift: in the flat field the dose agrees; in the penumbra the measured dose is within the
            // calculation's range one distance either side, so every point passes at 2 mm and some fail at 0 mm.
            var grid = Grid((x, y, z) => Field(x), step: 1);
            var scan = Scan(CurveType.Crossplane, 50, Range(-60, 60, 1).Select(p => (p, Field(p - 1))));
            var strict = new ProfileComparisonOptions(1, 2, ProfileTest.Strict, CentralAxisFallbackFraction: 0);
            Assert.AreEqual(100.0, ProfileComparison.Compare(scan, grid, Frame, strict, gridInGray: false).PassRate, 1e-12);
            var doseOnly = ProfileComparison.Compare(scan, grid, Frame, strict with { DistanceMm = 0 }, gridInGray: false);
            // Each penumbra point is off by 0.98/5 per mm = 19.6 %: the 10 points of each 5 mm penumbra fail, less those below the cutoff.
            Assert.IsTrue(doseOnly.Passed < doseOnly.Analysed);
            int expectedFailures = doseOnly.PointPositionsMm.Select((p, i) => (p, v: doseOnly.PointValues[i]))
                .Count(t => !double.IsNaN(t.v) && Math.Abs(Field(t.p - 1) - Field(t.p)) > 0.01);
            Assert.AreEqual(expectedFailures, doseOnly.Analysed - doseOnly.Passed);
        }

        [TestMethod]
        public void ConstancyIsTheMeanRelativeDifferenceOverTheCentralEightyPercent()
        {
            // Measured = calculated x (1 + 1e-4 x^2): both are 1 at the axis, and the relative difference at x is 1e-4 x^2.
            var grid = Grid((x, y, z) => Field(x), step: 1);
            var scan = Scan(CurveType.Crossplane, 50, Range(-70, 70, 1).Select(p => (p, Field(p) * (1 + 1e-4 * p * p))));
            var result = ProfileComparison.Compare(scan, grid, Frame, TwoTwo, gridInGray: false);
            // The measured reference's 50 % edges are the samples at +-50 (the last above half), so the central 80 % is +-40.
            double expected = 100 * Range(-40, 40, 1).Average(p => 1e-4 * p * p / (1 + 1e-4 * p * p));
            Assert.AreEqual(expected, result.ConstancyPercent!.Value, 1e-9);
        }

        [TestMethod]
        public void AnOffAxisFieldIsNormalisedToItsMaximum()
        {
            // A half-beam field: the axis sits on the edge, at half the in-field dose.
            var grid = Grid((x, y, z) => Field(x - 50), step: 1);
            var scan = Scan(CurveType.Crossplane, 50, Range(-30, 100, 1).Select(p => (p, Field(p - 50))));
            var result = ProfileComparison.Compare(scan, grid, Frame, TwoTwo, gridInGray: false);
            Assert.AreEqual(NormalizationType.Maximum, result.Normalization);
            StringAssert.Contains(result.NormalizationNote, "below 80%");
            Assert.AreEqual(100.0, result.PassRate, 1e-12);
        }

        /// <summary>A depth dose with a build-up and an exponential fall-off: (1 - e^(-d/6)) e^(-0.005 d), zero above the surface.</summary>
        private static double Pdd(double d) => d <= 0 ? 0 : (1 - Math.Exp(-d / 6)) * Math.Exp(-0.005 * d);

        [TestMethod]
        public void TheDepthShiftSearchFindsHowMuchDeeperTheMeasurementSits()
        {
            var grid = Grid((x, y, z) => Pdd(y), step: 1);
            var scan = Scan(CurveType.Pdd, null, Range(0, 180, 1).Select(d => (d, Pdd(d - 1.5))));
            var options = TwoTwo with { DepthShift = new DepthShiftSearch(3, 0.1) };
            var result = ProfileComparison.Compare(scan, grid, Frame, options, gridInGray: false);
            Assert.AreEqual(1.5, result.DepthShiftMm!.Value, 1e-9);
            Assert.AreEqual(100.0, result.PassRate, 1e-12);
            var unshifted = ProfileComparison.Compare(scan, grid, Frame, TwoTwo, gridInGray: false);
            Assert.IsNull(unshifted.DepthShiftMm);
        }

        [TestMethod]
        public void DepthDoseMetricsReadTheCurve()
        {
            // A straight fall from 100 % at 10 mm to 0 at 210 mm: D100 = 55 %, D200 = 5 %, R80 = 50 mm, R50 = 110 mm.
            var points = new[] { (0.0, 50.0), (10.0, 100.0), (210.0, 0.0) };
            var pdd = Scan(CurveType.Pdd, null, points);
            var m = DepthDoseMetrics.Of(pdd);
            Assert.AreEqual(10, m.DmaxMm, 1e-9);
            Assert.AreEqual(55, m.D100Percent!.Value, 1e-9);
            Assert.AreEqual(5, m.D200Percent!.Value, 1e-9);
            Assert.AreEqual(5.0 / 55, m.D20OverD10!.Value, 1e-12);
            Assert.AreEqual(50, m.R80Mm!.Value, 1e-9);
            Assert.AreEqual(110, m.R50Mm!.Value, 1e-9);
        }

        [TestMethod]
        public void AMeasuredReferenceAndACalculatedOneAgreeOnIdenticalCurves()
        {
            var grid = Grid((x, y, z) => Field(x), step: 1);
            var scan = Scan(CurveType.Crossplane, 50, Range(-70, 70, 2).Select(p => (p, Field(p))));
            var calcRef = ProfileComparison.Compare(scan, grid, Frame, TwoTwo with { Reference = ProfileReference.Calculated }, gridInGray: false);
            Assert.AreEqual(100.0, calcRef.PassRate, 1e-12);
            Assert.AreEqual(0.0, calcRef.MaxGammaFound!.Value, 1e-9);
        }

        [TestMethod]
        public void OptionsAreChecked()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProfileComparisonOptions(0, 2).Validate());
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProfileComparisonOptions(2, 0).Validate());   // gamma needs a distance
            new ProfileComparisonOptions(2, 0, ProfileTest.Strict).Validate();                                          // the strict rule doesn't
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ProfileComparisonOptions(2, 2, MaxGamma: 1).Validate());
        }

        // ---- pairing ----

        [TestMethod]
        public void PtwWedgeTextIsAngleAndDirection()
        {
            void Is(double? angle, string? direction, (double? Angle, string? Direction) actual)
            {
                Assert.AreEqual(angle, actual.Angle);
                Assert.AreEqual(direction, actual.Direction);
            }
            Is(10, "IN", ScanDoseMatching.ScanWedge(null, "10 in"));
            Is(60, "OUT", ScanDoseMatching.ScanWedge(null, "60 OUT"));
            Is(15, null, ScanDoseMatching.ScanWedge(15, ""));
            Is(null, null, ScanDoseMatching.ScanWedge(0, null));
        }

        [TestMethod]
        public void ElectronApplicatorsAreReadAsSizes()
        {
            Assert.AreEqual((100.0, 100.0), ScanDoseMatching.ApplicatorSize("A10"));
            Assert.AreEqual((100.0, 60.0), ScanDoseMatching.ApplicatorSize("A10X6"));
            Assert.AreEqual((60.0, 60.0), ScanDoseMatching.ApplicatorSize("a06"));
            Assert.IsNull(ScanDoseMatching.ApplicatorSize("cone 2"));
        }

        [TestMethod]
        public void TheCollimatorTurnsTheJawsOntoTheTankAxes()
        {
            // Measured on QAtpsPTW's "Asym X1": collimator 90, X jaws 0 to 100, Y jaws -100 to 100. At 90 the X jaws lie
            // along +z, so the field is 100 mm inplane centred at +50, and 200 mm crossplane.
            Assert.AreEqual((200.0, 100.0, 0.0, 50.0), ScanDoseMatching.TankField(0, 100, -100, 100, 90));
            Assert.AreEqual((100.0, 200.0, 50.0, 0.0), ScanDoseMatching.TankField(0, 100, -100, 100, 0));
            Assert.AreEqual((200.0, 100.0, 0.0, -50.0), ScanDoseMatching.TankField(0, 100, -100, 100, 270));
        }

        [TestMethod]
        public void ACollimatorDifferenceIsANoteNotAMismatch()
        {
            var scan = new BeamSetup(Modality.Photon, 6, false, 200, 100, 0, 50, 0, null, null, 1000);
            var plan = scan with { CollimatorDegrees = 90, SsdMm = null };
            var (same, difference, notes) = ScanDoseMatching.Compare(scan, plan);
            Assert.IsTrue(same, difference);
            Assert.IsTrue(notes.Any(n => n.Contains("collimator differs")));
            Assert.IsTrue(notes.Any(n => n.Contains("no SSD")));
            Assert.AreEqual("field centre", ScanDoseMatching.Compare(scan, plan with { CentreInplaneMm = -50 }).Difference);
            Assert.AreEqual("wedge direction", ScanDoseMatching.Compare(scan with { WedgeAngleDegrees = 10, WedgeDirection = "IN" },
                plan with { WedgeAngleDegrees = 10, WedgeDirection = "OUT" }).Difference);
        }

        [TestMethod]
        public void PtwElectronModalityElIsElectron()
        {
            // The Equinox commissioning electron scans write MODALITY=EL (measured 2026-11-17).
            Assert.AreEqual(Modality.Electron, MCCMeasurementMapper.MapModality("EL"));
        }

        // ---- real files ----

        private static RTDoseWithPlan P1Ref()
        {
            if (!TestData.Found) Assert.Inconclusive("resources/ not found");
            return RTFolderReader.Read(TestData.ResourcesDirectory!, headersOnly: true).Doses
                .Single(d => d.Dose.SourcePath.EndsWith("RD.UnitTest.P1Ref.X-100A-10.0-1.dcm", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TheP1RefFrameSitsTheSurfaceAtTheIsocentrePlaneAndTheDoseAgrees()
        {
            // The plan states no SSD; at the scan's 1000 mm and the plan's SAD of 1000 the surface is the isocentre plane.
            var dose = P1Ref();
            var grid = DoseGridReader.Read(dose.Dose.SourcePath);
            var (frame, refusal) = PhantomFrame.For(dose.Beam, dose.Plan!.SetupFor(dose.Beam!), grid, Distance.FromMillimetres(1000));
            Assert.IsNotNull(frame, refusal);
            Assert.AreEqual(SurfaceSource.MeasuredSsd, frame.SurfaceFrom);
            Assert.AreEqual(dose.Beam!.IsocenterPosition[1], frame.SurfaceYMm, 1e-9);
            Assert.IsTrue(frame.SurfaceAgrees, frame.Describe());
        }

        [TestMethod]
        public void TheP1RefDoseSampledAlongScanLinesIsATenCentimetreField()
        {
            // 6 MV 10 x 10 cm at 100 cm SSD: a crossplane profile at 100 mm depth projected to the nominal plane is 10 cm
            // wide by FauxPID's FWHM, centred on the axis; the depth dose peaks in the build-up's first few centimetres.
            var dose = P1Ref();
            var grid = DoseGridReader.Read(dose.Dose.SourcePath);
            var (frame, _) = PhantomFrame.For(dose.Beam, dose.Plan!.SetupFor(dose.Beam!), grid, Distance.FromMillimetres(1000));
            var cross = Scan(CurveType.Crossplane, 100, Range(-90, 90, 1).Select(p => (p, 1.0)));
            var line = DoseLineSampler.Sample(grid, frame!, cross, dose.Dose.SourcePath);
            var metrics = ProfileComparison.Metrics(line.Calculated, "calculated", new List<string>())!;
            Assert.AreEqual(100, metrics.FieldSizeMm, 3);
            Assert.AreEqual(0, metrics.CaxOffsetMm, 1);
            var pdd = DoseLineSampler.Sample(grid, frame!, Scan(CurveType.Pdd, null, Range(0, 200, 1).Select(d => (d, 1.0))), dose.Dose.SourcePath);
            double dmax = DepthDoseMetrics.Of(pdd.Calculated).DmaxMm;
            Assert.IsTrue(dmax is >= 8 and <= 25, $"dmax {dmax} mm");
        }

        [TestMethod]
        public void TheCommittedScanDoesNotPairWithAnotherBeamAndSaysWhy()
        {
            // The committed scan is 6 MV 30 x 30 cm; P1Ref's beam is another energy and 10 x 10 cm. The reason names the
            // first criterion the closest dose failed, in the matching order: energy before field size.
            var dose = P1Ref();
            Assert.AreNotEqual(6.0, dose.Beam!.NominalBeamEnergy!.Value, 0.01, "the test needs P1Ref to be another energy");
            var result = MCCParser.ParseFile(TestData.PTWScan);
            var scan = new MCCMeasurementMapper().MapScan(result.Scans[0], result);
            var (candidates, reason) = ScanDoseMatching.Match(scan, new[] { dose });
            Assert.AreEqual(0, candidates.Count);
            StringAssert.Contains(reason, "6 MV 30 x 30 cm", $"reason: {reason}");
            StringAssert.Contains(reason, "differ in energy", $"reason: {reason}");
        }
    }
}
