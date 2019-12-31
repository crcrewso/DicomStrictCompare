using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomStrictCompare.Controller
{
    public class SingleComparison
    {
        public Model.Dta _dta { get; set; }
        public int TotalCount { get; set; }
        public int TotalCompared { get; set; }
        public int TotalFailed { get;  set; }
        public double PercentFailed => PercentCalculator(TotalCompared, TotalFailed);


        /// <summary>
        /// Calculates the percent of voxels that failed based on the total 
        /// </summary>
        /// <param name="total">The total number of voxels compared</param>
        /// <param name="failed">The number of voxels that failed</param>
        /// <returns></returns>
        private static double PercentCalculator(int total, int failed)
        {
            return (failed == 0? 0:(double)failed / (double)total * 100.0);
        }
    }


}
