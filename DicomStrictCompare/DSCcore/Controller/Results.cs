using System;
using System.Collections.Generic;
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
        }

        public string SourceAlias { get; init; }
        public string TargetAlias {  get; init;}
        public string[] ResultStrings { get; init; }
        public string ResultMessageHeader {  get; init; }
        public string[] UnmatchedFileList { get; init; }


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
    }


}
