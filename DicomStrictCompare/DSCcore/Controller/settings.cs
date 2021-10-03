using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCSCore.Controller
{
    /// <summary>
    /// Consolidated settings collection to undo feature creep spaghettification.  
    /// </summary>
    public class Settings
    {

        /// <summary>
        /// Simple dta algorithm for fractional DTA adjustment
        /// </summary>


        public Model.Dta[] Dtas { get; }
        public bool RunDoseComparisons { get;  }
        public bool RunPDDComparisons { get; }
        public bool RunProfileComparisons { get; }
        public int CpuParallel { get; private set; } = 1;

        public Settings(
            DCSCore.Model.Dta[] dtas
            , bool runDoseComparisons
            , bool runPDDComparisons
            , bool runProfileComparisons
            , int coresIn
            )
        {
            Dtas = dtas;
            RunDoseComparisons = runDoseComparisons;
            RunPDDComparisons = runPDDComparisons;
            RunProfileComparisons = runProfileComparisons;
            CpuParallel = Math.Min(coresIn, Environment.ProcessorCount);
        }


    }
}
