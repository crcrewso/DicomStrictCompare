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

        string SourceAlias { get; init; }
        string TargetAlias {  get; init;}
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
    }


}
