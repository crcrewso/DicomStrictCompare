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
        bool Fuzzy { get; }
        /// <summary>
        /// distance to agreement in voxels, nominally 0 or 0.5
        /// </summary>
        float FuzzyWidth { get; }
        bool Gamma { get;  }
        bool DTA { get; }
        /// <summary>
        /// Distance in cm for distance to agreement algorithm, useful for gamma 
        /// </summary>
        float DTAWidth { get; }
        bool Trimmed { get; }
        float TrimWidth { get; }
        bool UseGPU { get; }
        double[] Tolerances { get; }
        double[] Depths { get; }
        double Threshhold { get;  }
        bool RunDoseComparisons { get;  }
        bool RunPDDComparisons { get; }
        bool RunProfileComparisons { get; }
        public int CpuParallel { get; private set; } = 1;
        public int GpuParallel { get; } = 0;
        public void SetCPUParallel(int coresIn)
        {
            CpuParallel = Math.Min(coresIn, Environment.ProcessorCount);
        }

    }
}
