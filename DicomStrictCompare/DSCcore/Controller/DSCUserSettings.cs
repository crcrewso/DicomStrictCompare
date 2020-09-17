using DicomStrictCompare.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DicomStrictCompare.Controller
{
    /// <summary>
    /// Consolidated settings collection to undo feature creep spaghettification.  
    /// </summary>
    public class DSCUserSettings
    {

        /// <summary>
        /// Simple dta algorithm for fractional DTA adjustment
        /// </summary>

        
        public Model.Dta[] Dtas { get; }


        /// <summary>
        /// user setting requesting 3D dose settings according to DTA's 
        /// </summary>
        public bool RunDoseComparisons { get;  }
        /// <summary>
        /// User setting requesting the analysis of the central axis plots and comparisons
        /// </summary>
        public bool RunPDDComparisons { get; }
        /// <summary>
        /// User setting requesting Profile Comparisons 
        /// TODO: not yet implimented
        /// </summary>
        public bool RunProfileComparisons { get; }
        /// <summary>
        /// Global setting for maximum number of CPU cores to be used in parallel organization
        /// </summary>
        public int CpuParallel { get; } 

        /// <summary>
        /// Contructor
        /// </summary>
        /// <param name="dtas"></param>
        /// <param name="runDoseComparisons"></param>
        /// <param name="runPDDComparisons"></param>
        /// <param name="runProfileComparisons"></param>
        /// <param name="coresIn"></param>
        public DSCUserSettings(
            Dta[] dtas
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
