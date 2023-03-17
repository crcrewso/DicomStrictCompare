
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DSClibrary
{
    /// <summary>
    /// This class is able to take a PTW profile file and parse the individual profiles, including profile type, beam settings, and individual profiles. 
    /// </summary>
    public class ParsePTW
    {
        public string Filename { get; init; }

        public string FilePath { get; init; }
        public string FileFormat { get; init; }
        public List<PTWScan> PTWScans { get; init; }

        /// <summary>
        /// Expects the full path of a file of interest
        /// </summary>
        /// <param name="file"></param>
        public ParsePTW(string file)
        {
            Filename = Path.GetFileName(file);
            FilePath = Path.GetFullPath(file);
            PTWScans = new List<PTWScan>();
            string[] lines = System.IO.File.ReadAllLines(file);
            if (lines.Length < 2)
                throw new FileLoadException("File is either empty or unreadable");
            if (!lines[0].Contains("BEGIN_SCAN_DATA"))
                throw new ArgumentException("File is not a MCC file");
            string formatLine = lines[1];
            if (formatLine.Contains("FORMAT="))
                FileFormat = formatLine.Split('=')[1].Trim();
            else
                FileFormat = "Unknown";
            List<long> beginLines = new List<long>();
            List<long> endLines = new List<long>();
            for (long line = 2; line < lines.Length; line++)
            {
                string tempLine = lines[line];
                if (tempLine.Contains("BEGIN_SCAN"))
                    beginLines.Add(line);
                else if (tempLine.Contains("END_SCAN"))
                    endLines.Add(line);
            }
            if (endLines.Count < beginLines.Count)
                Debug.WriteLine($"Warning: Found {endLines.Count} end scan markers and {beginLines.Count} begin scan markers");

            for (int scan = 0; scan < endLines.Count - 1; scan++)
            {
                long beginMarker = beginLines[scan];
                long endMarker = endLines[scan];
                long length = endMarker - beginMarker;
                string[] scanBody = new string[length];
                Array.Copy(lines, beginMarker, scanBody, 0, length);
                PTWScan tempScan = new PTWScan(scanBody);
                PTWScans.Add(tempScan);
            }

        }




    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Position">Linear variable representing distance from zero along axis of travel</param>
    /// <param name="Value">Raw measurement as present in the mcc file</param>
    /// <param name="SecondValue">TODO: figure out what this variable actually means</param>
    public record PTWRawDose(double Position, double Value, double SecondValue)
    {
        public PTWRawDose(string s) : this(default, default, default)
        {
            var temp = s.Replace("\t\t\t", "").Split("\t\t");
            Position = double.Parse(temp[0]);
            Value = double.Parse(temp[1]);
            SecondValue = double.Parse(temp[2]);
        }

    }

    public class PTWScan
    {
        public string Task_Name { get; init; }
        public string LINAC { get; init; }
        public string Modality { get; init; }
        public double Energy { get; init; }
        public string InPlaneAxis { get; init; }
        public string CrossPlaneAxis { get; init; }
        public string InPlaneDirection { get; init; }
        public string CrossPlaneDirection { get; init; }

        public double Field_Inplane { get; init; }
        public double Field_Crossplane { get; init; }
        public int ScanNumber { get; init; }
        public List<PTWRawDose> rawDoses { get; init; }

        /// <summary>
        /// Expects a single scan extracted from an MCC file
        /// </summary>
        /// <param name="sourceScan"></param>
        public PTWScan(string[] sourceScan)
        {
            ScanNumber = int.Parse(sourceScan[0].Replace("BEGIN_SCAN ", "").Trim());

            Dictionary<string, string> _scanHeaders = new Dictionary<string, string>();
            rawDoses = new List<PTWRawDose>();
            #region Data
            int beginDataLine = -1;
            int endDataLine = -1;
            // loops over scan to find the boundaries of the data
            // loop direction ensures any silly use of *_DATA in header won't disturb 
            for (int i = 0; i < sourceScan.Length; i++)
            {
                if (sourceScan[i].EndsWith("BEGIN_DATA"))
                    beginDataLine = i + 1;
                else if (sourceScan[i].EndsWith("END_DATA"))
                    endDataLine = i - 1;
            }
            string[] rawData = new string[endDataLine - beginDataLine];
            Array.Copy(sourceScan, beginDataLine, rawData, 0, endDataLine - beginDataLine);
            foreach (var line in rawData)
            {
                rawDoses.Add(new PTWRawDose(line));
            }
            #endregion
            #region Header
            // Populate Dictionary
            for (int i = 1; i < beginDataLine -1; i++)
            {
                string[] temp = sourceScan[i].Split('=');;
                _scanHeaders.Add(temp[0].Trim(), temp[1].Trim());
            }

            Task_Name = _scanHeaders.GetValueOrDefault("TASK_NAME", "");
            LINAC = _scanHeaders.GetValueOrDefault("LINAC", "");
            Modality = _scanHeaders.GetValueOrDefault("MODALITY", "");
            Energy = double.Parse(_scanHeaders.GetValueOrDefault("ENERGY","0"));
            #endregion


        }



    }
}
