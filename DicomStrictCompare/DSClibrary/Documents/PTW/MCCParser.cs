using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DSClibrary.Documents.PTW
{
    /// <summary>
    /// Reads a PTW mcc file into an <see cref="MCCDocument"/>. It converts nothing and interprets
    /// nothing: units, enum translation and canonical naming are the mapper's job.
    /// <para>
    /// The structure it expects, measured from the committed test file:
    /// <code>
    /// BEGIN_SCAN_DATA
    /// \tFORMAT=CC-Export V1.9
    /// \tBEGIN_SCAN  1
    /// \t\tKEY=VALUE                 (71 of them)
    /// \t\tBEGIN_DATA
    /// \t\t\t-5.00\t\t1.0570E+00\t\t3.6648E+00
    /// \t\tEND_DATA
    /// \tEND_SCAN  1
    /// END_SCAN_DATA
    /// </code>
    /// </para>
    /// </summary>
    public static class MCCParser
    {
        private const string BeginScanData = "BEGIN_SCAN_DATA";
        private const string EndScanData = "END_SCAN_DATA";
        private const string BeginScan = "BEGIN_SCAN";
        private const string EndScan = "END_SCAN";
        private const string BeginData = "BEGIN_DATA";
        private const string EndData = "END_DATA";

        public static MCCDocument ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("mcc file not found.", path);

            // Latin1 rather than the UTF-8 default. The format is ASCII, and Latin1 decodes every byte
            // sequence without throwing or substituting, so a stray high byte - a degree sign in an
            // operator's comment, say - cannot corrupt the parse or lose a header.
            string[] lines = File.ReadAllLines(path, Encoding.Latin1);
            return Parse(lines, path);
        }

        public static MCCDocument Parse(IReadOnlyList<string> lines, string sourcePath = "")
        {
            if (lines is null) throw new ArgumentNullException(nameof(lines));
            if (lines.Count < 2)
                throw new MCCFormatException("File is empty or truncated: fewer than two lines.");
            if (Marker(lines[0]) != BeginScanData)
                throw new MCCFormatException(
                    $"Not an mcc file: line 1 is '{lines[0].Trim()}', expected '{BeginScanData}'.");

            var fileHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
            var scans = new List<MCCScan>();

            int? openScanNumber = null;
            Dictionary<string, string>? scanHeaders = null;
            List<MCCDataPoint>? scanData = null;
            bool inData = false;

            for (int i = 1; i < lines.Count; i++)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();
                int lineNumber = i + 1;

                if (trimmed.Length == 0)
                    continue;

                string marker = Marker(raw);

                if (marker == EndScanData)
                    break;

                if (marker == BeginScan)
                {
                    if (openScanNumber.HasValue)
                        throw new MCCFormatException(
                            $"Line {lineNumber}: {BeginScan} while scan {openScanNumber} is still open.");

                    openScanNumber = ScanNumberOf(trimmed, BeginScan, lineNumber);
                    scanHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
                    scanData = new List<MCCDataPoint>();
                    inData = false;
                    continue;
                }

                if (marker == EndScan)
                {
                    if (!openScanNumber.HasValue)
                        throw new MCCFormatException($"Line {lineNumber}: {EndScan} with no scan open.");

                    // END_SCAN carries the scan number too, so the pairing is checked rather than
                    // assumed. The 2024 parser matched on substrings and compensated for the extra
                    // END_SCAN_DATA with an off-by-one in its loop bound, which happened to give the
                    // right answer on this file and would silently drop the last scan on a file that
                    // ended differently.
                    int closing = ScanNumberOf(trimmed, EndScan, lineNumber);
                    if (closing != openScanNumber.Value)
                        throw new MCCFormatException(
                            $"Line {lineNumber}: {EndScan} {closing} closes scan {openScanNumber.Value}.");

                    scans.Add(new MCCScan(openScanNumber.Value, scanHeaders!, scanData!));
                    openScanNumber = null;
                    scanHeaders = null;
                    scanData = null;
                    inData = false;
                    continue;
                }

                if (marker == BeginData)
                {
                    if (!openScanNumber.HasValue)
                        throw new MCCFormatException($"Line {lineNumber}: {BeginData} with no scan open.");
                    inData = true;
                    continue;
                }

                if (marker == EndData)
                {
                    inData = false;
                    continue;
                }

                if (inData)
                {
                    scanData!.Add(ParseDataPoint(trimmed, lineNumber));
                    continue;
                }

                // A KEY=VALUE header, at file level before the first scan or inside one.
                int equals = trimmed.IndexOf('=');
                if (equals <= 0)
                    continue;   // not a header; ignored rather than fatal, so an unknown line cannot stop a parse

                string key = trimmed[..equals].Trim();
                string value = trimmed[(equals + 1)..].Trim();
                var target = openScanNumber.HasValue ? scanHeaders! : fileHeaders;
                target[key] = value;   // last occurrence wins; duplicates are not expected in this format
            }

            if (openScanNumber.HasValue)
                throw new MCCFormatException($"Scan {openScanNumber.Value} was never closed by {EndScan}.");

            return new MCCDocument(sourcePath, fileHeaders, scans);
        }

        /// <summary>
        /// The structural marker a line starts with, or empty. Matched on the first whitespace-free
        /// token so that BEGIN_SCAN_DATA is never mistaken for BEGIN_SCAN, which is the trap the
        /// substring tests in the 2024 parser fell into.
        /// </summary>
        private static string Marker(string line)
        {
            string trimmed = line.Trim();
            int space = trimmed.IndexOf(' ');
            string token = space < 0 ? trimmed : trimmed[..space];
            return token is BeginScanData or EndScanData or BeginScan or EndScan or BeginData or EndData
                ? token
                : string.Empty;
        }

        private static int ScanNumberOf(string trimmed, string marker, int lineNumber)
        {
            string rest = trimmed[marker.Length..].Trim();
            if (!int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                throw new MCCFormatException($"Line {lineNumber}: cannot read a scan number from '{trimmed}'.");
            return number;
        }

        private static MCCDataPoint ParseDataPoint(string trimmed, int lineNumber)
        {
            // Columns are tab separated, but the separator width varies with indentation, so split on
            // tabs and discard the empties rather than depending on an exact run of them.
            string[] columns = trimmed.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (columns.Length < 2)
                throw new MCCFormatException($"Line {lineNumber}: a data line needs at least two columns, found {columns.Length}.");

            double position = ParseNumber(columns[0], lineNumber, "position");
            double value = ParseNumber(columns[1], lineNumber, "value");
            double? reference = columns.Length > 2 ? ParseNumber(columns[2], lineNumber, "reference") : null;
            return new MCCDataPoint(position, value, reference);
        }

        private static double ParseNumber(string text, int lineNumber, string what)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                throw new MCCFormatException($"Line {lineNumber}: cannot read a {what} from '{text}'.");
            return parsed;
        }
    }
}
