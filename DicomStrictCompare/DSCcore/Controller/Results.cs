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
        public List<DoseValue> sourcePDD { get; } 
        public List<DoseValue> targetPDD;
        public string chartTitleString;
        string SourceAlias;
        string TargetAlias;

        public Results()
        {
        }
    }


}
