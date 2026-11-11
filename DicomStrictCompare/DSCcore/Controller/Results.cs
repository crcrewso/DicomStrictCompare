using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvilDICOM.RT;

namespace DCSCore.Controller
{
    public record Results
    {
        public Results(string sourceAlias, string targetAlias, string[] resultStrings, string resultMessageHeader, string[]? unmatchedDoseFiles = null)
        {
            SourceAlias = sourceAlias;
            TargetAlias = targetAlias;
            ResultStrings = resultStrings;
            Array.Sort(ResultStrings);
            ResultMessageHeader = resultMessageHeader;
            UnmatchedFileList = unmatchedDoseFiles;
            
            // Parse result strings into structured data if possible
            ParsedResults = new List<ResultItem>();
            foreach (string resultString in ResultStrings)
            {
                string[] parts = resultString.Split(',');
                if (parts.Length > 0)
                {
                    ParsedResults.Add(new ResultItem(parts));
                }
            }
        }

        public string SourceAlias { get; init; }
        public string TargetAlias {  get; init;}
        public string[] ResultStrings { get; init; }
        public string ResultMessageHeader {  get; init; }
        public string[] UnmatchedFileList { get; init; }
        public List<ResultItem> ParsedResults { get; init; }

        public override string ToString()
        {
            string ret = ResultMessageHeader;
            foreach(string result in ResultStrings)
            {
                ret += result + "\n";
            }
            return ret;
        }
        
        public string GetSummary()
        {
            int totalPairs = ResultStrings.Length;
            int unmatchedCount = UnmatchedFileList?.Length ?? 0;
            
            string summary = $"Comparison Summary:\n";
            summary += $"Source: {SourceAlias}\n";
            summary += $"Target: {TargetAlias}\n";
            summary += $"Total pairs compared: {totalPairs}\n";
            summary += $"Unmatched files: {unmatchedCount}\n";
            
            // Add statistics for each DTA setting if available
            if (ParsedResults.Count > 0 && ParsedResults[0].PercentFailed.Length > 0)
            {
                summary += "\nDTA Statistics:\n";
                for (int dtaIndex = 0; dtaIndex < ParsedResults[0].PercentFailed.Length; dtaIndex++)
                {
                    double avgPercentFailed = ParsedResults.Average(r => r.PercentFailed[dtaIndex]);
                    double maxPercentFailed = ParsedResults.Max(r => r.PercentFailed[dtaIndex]);
                    summary += $"DTA Setting {dtaIndex+1}:\n";
                    summary += $"  Average Percent Failed: {avgPercentFailed:F2}%\n";
                    summary += $"  Maximum Percent Failed: {maxPercentFailed:F2}%\n";
                }
            }
            
            return summary;
        }
        
        public string GetUnmatchedFilesReport()
        {
            if (UnmatchedFileList == null || UnmatchedFileList.Length == 0)
            {
                return "No unmatched files.";
            }
            
            string report = "Unmatched Files:\n";
            foreach (string file in UnmatchedFileList)
            {
                report += $"- {file}\n";
            }
            
            return report;
        }
        
        public string GetResultsAsCSV()
        {
            string csv = ResultMessageHeader.Replace("\n", ",");
            foreach (string result in ResultStrings)
            {
                csv += result + ",";
            }
            return csv;
        }
        
        public bool SaveToFile(string filePath)
        {
            try
            {
                File.WriteAllText(filePath, ToString());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        public bool SaveToCSV(string filePath)
        {
            try
            {
                File.WriteAllText(filePath, GetResultsAsCSV());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
    
    public class ResultItem
    {
        public string PlanName { get; private set; } = string.Empty;
        public string FieldName { get; private set; } = string.Empty;
        public double[] PercentFailed { get; private set; } = Array.Empty<double>();
        public int[] TotalCompared { get; private set; } = Array.Empty<int>();
        public double[] TotalFailed { get; private set; } = Array.Empty<double>();
        public string SourceFileName { get; private set; } = string.Empty;
        public string TargetFileName { get; private set; } = string.Empty;
        public string SourceMUs { get; private set; } = string.Empty;
        public string TargetMUs { get; private set; } = string.Empty;
        public string PDDStatus { get; private set; } = string.Empty;
        
        public ResultItem(string[] parts)
        {
            if (parts.Length < 2) return;
            
            PlanName = parts[0];
            
            // Try to parse the other fields, considering the expected format
            int currentIdx = 1;
            
            // Figure out how many DTA sets there are (assuming they come in groups of 3)
            int dtaCount = (parts.Length - 5) / 3; // Subtract non-DTA fields (plan, field, files, MUs x 2, PDD)
            
            if (dtaCount > 0)
            {
                PercentFailed = new double[dtaCount];
                TotalCompared = new int[dtaCount];
                TotalFailed = new double[dtaCount];
                
                // Parse the field name
                if (currentIdx < parts.Length)
                    FieldName = parts[currentIdx++];
                
                // Parse percent failed values
                for (int i = 0; i < dtaCount && currentIdx < parts.Length; i++)
                {
                    if (double.TryParse(parts[currentIdx++], out double value))
                        PercentFailed[i] = value;
                }
                
                // Parse total compared values
                for (int i = 0; i < dtaCount && currentIdx < parts.Length; i++)
                {
                    if (int.TryParse(parts[currentIdx++], out int value))
                        TotalCompared[i] = value;
                }
                
                // Parse total failed values
                for (int i = 0; i < dtaCount && currentIdx < parts.Length; i++)
                {
                    if (double.TryParse(parts[currentIdx++], out double value))
                        TotalFailed[i] = value;
                }
                
                // Parse file names
                if (currentIdx < parts.Length)
                {
                    string[] fileNames = parts[currentIdx++].Split(',');
                    if (fileNames.Length >= 2)
                    {
                        SourceFileName = fileNames[0];
                        TargetFileName = fileNames[1];
                    }
                }
                
                // Parse MUs
                if (currentIdx < parts.Length)
                    SourceMUs = parts[currentIdx++];
                
                if (currentIdx < parts.Length)
                    TargetMUs = parts[currentIdx++];
                
                // Parse PDD status
                if (currentIdx < parts.Length)
                    PDDStatus = parts[currentIdx];
            }
        }
        
        public override string ToString()
        {
            return $"{PlanName}, {FieldName}";
        }
    }
}
