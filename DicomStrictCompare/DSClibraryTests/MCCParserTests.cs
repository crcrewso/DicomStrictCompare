using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using DSClibrary.Results.PTW;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    /// <summary>
    /// Fidelity tests. Every assertion here is about <b>what the file says</b>, in PTW's own names and
    /// units, and none of them is about what this project does with it. That separation is why the
    /// result layer exists: a unit mistake belongs to the mapper's tests, not these.
    /// </summary>
    [TestClass]
    public class MCCParserTests
    {
        private static MCCResult Result() => MCCParser.ParseFile(TestData.PTWScan);

        [TestMethod]
        public void TheTestFileIsWhereTheProjectFileSaysItIs()
        {
            Assert.IsTrue(File.Exists(TestData.PTWScan), $"missing test data at {TestData.PTWScan}");
        }

        [TestMethod]
        public void ReadsElevenScans()
        {
            // The file has 12 lines containing BEGIN_SCAN, one of which is BEGIN_SCAN_DATA, and 12
            // containing END_SCAN, one of which is END_SCAN_DATA.
            Assert.AreEqual(11, Result().Scans.Count);
        }

        [TestMethod]
        public void ScanNumbersRunOneToEleven()
        {
            CollectionAssert.AreEqual(
                Enumerable.Range(1, 11).ToArray(),
                Result().Scans.Select(s => s.ScanNumber).ToArray());
        }

        [TestMethod]
        public void ReadsTheFileLevelFormat()
        {
            StringAssert.StartsWith(Result().Format, "CC-Export");
        }

        [TestMethod]
        public void FirstScanHasSeventyOneHeaders()
        {
            Assert.AreEqual(71, Result().Scans[0].Headers.Count);
        }

        [TestMethod]
        public void CurveTypesAreTheVendorsOwnStrings()
        {
            var types = Result().Scans
                .GroupBy(s => s.ScanCurvetype)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.AreEqual(1, types["PDD"]);
            Assert.AreEqual(5, types["INPLANE_PROFILE"]);
            Assert.AreEqual(5, types["CROSSPLANE_PROFILE"]);
        }

        [TestMethod]
        public void LengthsAreTheFilesOwnNumbersUnconverted()
        {
            var scan = Result().Scans[0];
            Assert.AreEqual(1000.00, scan.Ssd!.Value, 1e-9, "SSD=1000.00 is read as 1000, not 100");
            Assert.AreEqual(300.00, scan.FieldInplane!.Value, 1e-9);
            Assert.AreEqual(300.00, scan.FieldCrossplane!.Value, 1e-9);
        }

        [TestMethod]
        public void ReadsTheFirstDataPointExactly()
        {
            var first = Result().Scans[0].Data[0];
            Assert.AreEqual(-5.00, first.Position, 1e-9);
            Assert.AreEqual(1.0570E+00, first.Value, 1e-12);
            Assert.AreEqual(3.6648E+00, first.ReferenceValue!.Value, 1e-12);
        }

        [TestMethod]
        public void CapturesTheReferenceColumnOnEveryPoint()
        {
            Assert.IsTrue(Result().Scans.SelectMany(s => s.Data).All(p => p.ReferenceValue.HasValue));
        }

        [TestMethod]
        public void DataPointCountsMatchTheFile()
        {
            var scans = Result().Scans;
            Assert.AreEqual(51, scans[0].Data.Count, "the PDD has 51 points");
            Assert.IsTrue(scans.Skip(1).All(s => s.Data.Count == 93), "each profile has 93 points");
        }

        [TestMethod]
        public void DepthAxisReadsDepthAxisAndNotTheCrossplaneDirection()
        {
            // The 2024 ParsePTW read CROSSPLANE_AXIS_DIR into both of its depth fields. Here the
            // property and the key it reads are checkable side by side, which is what a faithful
            // result layer buys.
            var scan = Result().Scans[0];
            Assert.AreEqual("Depth", scan.DepthAxis);
            Assert.AreEqual("UP_DOWN", scan.DepthAxisDir);
            Assert.AreEqual("LEFT_RIGHT", scan.CrossplaneAxisDir);
            Assert.AreEqual("GUN_TARGET", scan.InplaneAxisDir);
        }

        [TestMethod]
        public void ReadsBothDetectorsIncludingTheReferenceChamber()
        {
            // A sequential acquisition needs a reference chamber to correct for output drift across
            // the scan, and the file declares a whole second one.
            var scan = Result().Scans[0];
            Assert.AreEqual("THIMBLE_CHAMBER", scan.Detector);
            Assert.AreEqual("SEMIFLEX", scan.DetectorSubcode);
            Assert.AreEqual(2.75, scan.DetectorRadius!.Value, 1e-9);
            Assert.AreEqual("THIMBLE_CHAMBER", scan.DetectorReference);
            Assert.AreNotEqual(scan.DetectorSn, scan.DetectorReferenceSn, "two distinct chambers");
        }

        [TestMethod]
        public void ReadsEnvironmentAndCorrectionHeaders()
        {
            var scan = Result().Scans[0];
            Assert.AreEqual(20.00, scan.Temperature!.Value, 1e-9);
            Assert.AreEqual(1013.25, scan.Pressure!.Value, 1e-9);
            Assert.AreEqual(1.0000, scan.CorrectionFactor!.Value, 1e-9);
            // CORRECTION_FACTOR is a scalar and says nothing about detector geometry, which is why the
            // mapper leaves that state unknown.
            Assert.IsFalse(scan.Headers.Keys.Any(k => k.Contains("GEOMETRY")));
        }

        [TestMethod]
        public void MissingHeadersAreEmptyOrNullRatherThanAnException()
        {
            var scan = Result().Scans[0];
            Assert.AreEqual(string.Empty, scan.Text("NO_SUCH_KEY"));
            Assert.IsNull(scan.Number("NO_SUCH_KEY"));
            Assert.IsNull(scan.Flag("NO_SUCH_KEY"));
        }

        [TestMethod]
        public void ParsesTheSameUnderADecimalCommaLocale()
        {
            // The 2024 parsers used a bare double.Parse, which under de-DE either throws on
            // 1.0570E+00 or reads it as a different number. The files are invariant whatever the
            // machine is set to.
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var scan = Result().Scans[0];
                Assert.AreEqual(1000.00, scan.Ssd!.Value, 1e-9);
                Assert.AreEqual(1.0570E+00, scan.Data[0].Value, 1e-12);
                Assert.AreEqual(-5.00, scan.Data[0].Position, 1e-9);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void RejectsAFileThatIsNotMCC()
        {
            Assert.ThrowsExactly<MCCFormatException>(
                () => MCCParser.Parse(new[] { "Version:\t25", "Filename:\tsomething.prm" }));
        }

        [TestMethod]
        public void RejectsMismatchedScanNumbers()
        {
            // END_SCAN carries its scan number, so the pairing is checked rather than assumed.
            string[] lines =
            {
                "BEGIN_SCAN_DATA",
                "\tBEGIN_SCAN  1",
                "\t\tSCAN_CURVETYPE=PDD",
                "\tEND_SCAN  2",
                "END_SCAN_DATA"
            };
            Assert.ThrowsExactly<MCCFormatException>(() => MCCParser.Parse(lines));
        }

        [TestMethod]
        public void RejectsAnUnclosedScan()
        {
            string[] lines = { "BEGIN_SCAN_DATA", "\tBEGIN_SCAN  1", "\t\tSCAN_CURVETYPE=PDD" };
            Assert.ThrowsExactly<MCCFormatException>(() => MCCParser.Parse(lines));
        }

        [TestMethod]
        public void DoesNotMistakeBeginScanDataForAScan()
        {
            // Contains("BEGIN_SCAN") matches BEGIN_SCAN_DATA too, which is the trap that made the
            // 2024 loop bound compensate for an extra end marker.
            string[] lines =
            {
                "BEGIN_SCAN_DATA",
                "\tFORMAT=CC-Export V1.9",
                "\tBEGIN_SCAN  1",
                "\t\tSCAN_CURVETYPE=PDD",
                "\t\tBEGIN_DATA",
                "\t\t\t0.00\t\t1.0E+00\t\t2.0E+00",
                "\t\tEND_DATA",
                "\tEND_SCAN  1",
                "END_SCAN_DATA"
            };
            var parsed = MCCParser.Parse(lines);
            Assert.AreEqual(1, parsed.Scans.Count);
            Assert.AreEqual(1, parsed.Scans[0].Data.Count);
        }
    }
}
