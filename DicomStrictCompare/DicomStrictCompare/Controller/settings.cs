using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomStrictCompare.Controller
{
    /// <summary>
    /// Consolidated settings collection to undo feature creep spaghettification.  
    /// </summary>
    class Settings
    {

        /// <summary>
        /// Simple dta algorithm for fractional DTA adjustment
        /// </summary>


        public Model.Dta[] Dtas { get; }
        public bool RunDoseComparisons { get;  }
        public bool RunPDDComparisons { get; }
        public bool RunProfileComparisons { get; }
        public int CpuParallel { get; private set; } = 1;
        void SetCPUParallel(int coresIn)
        {
            CpuParallel = Math.Min(coresIn, Environment.ProcessorCount);
        }

        public Settings(
            DicomStrictCompare.Model.Dta[] dtas
            , bool runDoseComparisons
            , bool runPDDComparisons
            , bool runProfileComparisons
            )
        {
            Dtas = dtas;
            RunDoseComparisons = runDoseComparisons;
            RunPDDComparisons = runPDDComparisons;
            RunProfileComparisons = runProfileComparisons;
        }


    }
}
