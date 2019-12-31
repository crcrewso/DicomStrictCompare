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
        public bool Fuzzy { get; }
        /// <summary>
        /// distance to agreement in voxels, nominally 0 or 0.5
        /// </summary>
        public float FuzzyWidth { get; }
        public bool Gamma { get;  }
        public bool Trimmed { get; }
        public float TrimWidth { get; }
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
            bool fuzzy
            , float fuzzyWidth
            , bool gamma
            , bool trimmed
            , float trimWidth
            , bool useGPU
            , DicomStrictCompare.Model.Dta[] dtas
            , double threshhold
            , bool runDoseComparisons
            , bool runPDDComparisons
            , bool runProfileComparisons
            )
        {
            Fuzzy = fuzzy;
            FuzzyWidth = fuzzyWidth;
            Gamma = gamma;
            Trimmed = trimmed;
            TrimWidth = trimWidth;
            Dtas = dtas;
            RunDoseComparisons = runDoseComparisons;
            RunPDDComparisons = runPDDComparisons;
            RunProfileComparisons = runProfileComparisons;
        }


    }
}
