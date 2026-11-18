using System;
using System.IO;
using System.Linq;
using DSClibrary;
using DSClibrary.Profiles;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// <see cref="ScanHeaderFixes"/>: a person's corrections to scan headers (2026-11-17), kept in a JSON file beside the
    /// scans. The Equinox case: a wedged 20 x 20 scan whose header says open 10 x 10.
    /// </summary>
    [TestClass]
    public class ScanHeaderFixesTests
    {
        private string _dir = "";

        [TestInitialize]
        public void Make() => Directory.CreateDirectory(_dir = Path.Combine(Path.GetTempPath(), "dsc-fixes-" + Guid.NewGuid().ToString("N")));

        [TestCleanup]
        public void Clean() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

        private static Profile OpenTenByTen(Func<double, double> dose) => new(
            new BeamKey(Modality.Photon, 10, false, Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000),
                wedgeAngleDegrees: 0),
            new ScanKey(CurveType.Inplane, Distance.FromMillimetres(100)),
            Enumerable.Range(-300, 601).Select(p => new ProfilePoint(Distance.FromMillimetres(p), dose(p), null)));

        /// <summary>20 x 20 at 100 mm depth (220 mm wide), sloping 20 % across, S-shaped penumbrae.</summary>
        private static double WedgedTwenty(double p) => 0.98 / (1 + Math.Exp(-(110 - Math.Abs(p)) / 1.5)) * (1 - 0.2 * p / 220) + 0.02;

        [TestMethod]
        public void AFixGivesTheScanAsIfItsHeaderHadSaidSo()
        {
            var scan = OpenTenByTen(WedgedTwenty);
            var before = ScanHeaderCheck.Check(scan);
            Assert.IsFalse(before.Agrees, "the header says open 10 x 10; the curve is a wedged 20 x 20");

            var fix = new ScanHeaderFix("10x/low res wedges/X10 OPEN 20X20.mcc", FieldInplaneMm: 200, FieldCrossplaneMm: 200, WedgeAngle: 15, WedgeDirection: "OUT");
            var fixedScan = ScanHeaderFixes.Apply(scan, fix);
            Assert.AreEqual(200, fixedScan.Beam.FieldInplane!.Value.Millimetres, 1e-9);
            Assert.AreEqual(15, fixedScan.Beam.WedgeAngleDegrees);
            Assert.AreEqual(10, fixedScan.Beam.Energy, "what the fix leaves out stays the file's");
            Assert.AreEqual("15 out", fixedScan.Conditions.RawHeaders["WEDGE"]);
            var setup = ScanDoseMatching.FromScan(fixedScan).Setup!;
            Assert.AreEqual((15.0, "OUT"), (setup.WedgeAngleDegrees!.Value, setup.WedgeDirection), "pairing reads the fixed wedge");
            Assert.AreEqual(200, setup.InplaneMm, 1e-9);
            Assert.IsTrue(ScanHeaderCheck.Check(fixedScan).Agrees, "a right fix clears the header check's flag");
            CollectionAssert.AreEqual(scan.Points.ToArray(), fixedScan.Points.ToArray(), "the curve is the file's");
        }

        [TestMethod]
        public void FixesAreSavedAndReadBackAndAScansOwnFixWinsOverItsFiles()
        {
            var fixes = new ScanHeaderFixes();
            fixes.Set(new ScanHeaderFix(@"10x\a.mcc", null, FieldInplaneMm: 200, FieldCrossplaneMm: 200, Note: "whole file"));
            fixes.Set(new ScanHeaderFix("10x/a.mcc", 3, WedgeAngle: 60, WedgeDirection: "IN"));
            string path = ScanHeaderFixes.DefaultPath(_dir);
            fixes.Save(path);

            var again = ScanHeaderFixes.Load(path);
            Assert.AreEqual(path, again.SourcePath);
            Assert.AreEqual(2, again.Fixes.Count);
            Assert.AreEqual("whole file", again.For("10x/a.mcc", 1)!.Note, "backslashes are stored as forward slashes");
            Assert.AreEqual(60, again.For("10X/A.MCC", 3)!.WedgeAngle, "the scan's own fix wins; names ignore case");
            Assert.IsNull(again.For("10x/b.mcc", 1));

            Assert.IsTrue(again.Remove("10x/a.mcc", 3));
            Assert.AreEqual("whole file", again.For("10x/a.mcc", 3)!.Note, "with its own fix gone, scan 3 takes its file's");
            Assert.AreEqual(0, ScanHeaderFixes.Load(Path.Combine(_dir, "none.json")).Fixes.Count, "a missing file is no fixes");
        }

        [TestMethod]
        public void AFixThatCantBeUsedIsRefused()
        {
            var fixes = new ScanHeaderFixes();
            Assert.ThrowsExactly<ArgumentException>(() => fixes.Set(new ScanHeaderFix("a.mcc", FieldInplaneMm: 0)));
            Assert.ThrowsExactly<ArgumentException>(() => fixes.Set(new ScanHeaderFix("a.mcc", WedgeAngle: 15, WedgeDirection: "UP")));
            Assert.ThrowsExactly<ArgumentException>(() => fixes.Set(new ScanHeaderFix("a.mcc", Scan: 0)));
            string path = Path.Combine(_dir, "bad.json");
            File.WriteAllText(path, "{\"fixes\":[{\"file\":\"a.mcc\",\"energy\":-6}]}");
            Assert.ThrowsExactly<InvalidDataException>(() => ScanHeaderFixes.Load(path));
        }

        [TestMethod]
        public void AFolderOfScansIsReadWithItsFixesAndSaysWhatTheFileSaid()
        {
            Assert.IsTrue(TestData.Found, "resources folder not found");
            string folder = Path.Combine(TestData.ResourcesDirectory!, "ptw scans");
            string file = Directory.GetFiles(folder, "*.mcc").Single();
            var fixes = new ScanHeaderFixes();
            fixes.Set(new ScanHeaderFix(ScanHeaderFixes.KeyOf(folder, file), 2, Energy: 10, Note: "test"));

            var (scans, _, _) = ProfileBatch.ReadScans(folder, fixes: fixes);
            var two = scans.Single(s => s.ScanNumber == 2);
            Assert.AreEqual(10, two.Profile.Beam.Energy);
            Assert.AreEqual(6, two.OriginalBeam!.Energy);
            StringAssert.StartsWith(two.HeaderFixNote, "header fixed by hand: energy 10 (the file says ");
            StringAssert.EndsWith(two.HeaderFixNote, "; test");
            Assert.IsTrue(scans.Where(s => s.ScanNumber != 2).All(s => s.HeaderFix is null && s.Profile.Beam.Energy == 6), "only scan 2 is fixed");
        }
    }
}
