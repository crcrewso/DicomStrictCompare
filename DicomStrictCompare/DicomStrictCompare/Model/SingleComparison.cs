using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomStrictCompare.Model
{
    public class SingleComparison
    {
        public Dta Dta { get; }
        public int TotalCount { get;  }
        public int TotalCompared { get;  }
        public int TotalFailed { get;  }
        public double PercentFailed => PercentCalculator(TotalCompared, TotalFailed);


        /// <summary>
        /// Calculates the percent of voxels that failed based on the total 
        /// </summary>
        /// <param name="total">The total number of voxels compared</param>
        /// <param name="failed">The number of voxels that failed</param>
        /// <returns></returns>
        private static double PercentCalculator(int total, int failed)
        {
            return failed == 0 ? 0 : failed / (double)total * 100.0;
        }

        /// <summary>
        /// Comparison result object
        /// </summary>
        /// <param name="dta">DTA parameters used as part of the </param>
        /// <param name="totalCount">Total number of indicies able to be compared</param>
        /// <param name="totalCompared">Total number of comparisons made where the values of the source were above minimum restrictions</param>
        /// <param name="totalFailed">total number of comparisons that failed dta</param>
        public SingleComparison(Dta dta, int totalCount, int totalCompared, int totalFailed)
        {
            Dta = dta ?? throw new ArgumentNullException(nameof(dta));
            TotalCount = totalCount;
            TotalCompared = totalCompared;
            TotalFailed = totalFailed;
        }

        /// <summary>
        /// Comparator for class.
        /// </summary>
        /// <param name="singleComparison"></param>
        /// <returns>true iff all elements of the comparisons match. </returns>
        public bool Equals( SingleComparison singleComparison)
        {
            if (null == singleComparison)
                throw new ArgumentNullException(nameof(singleComparison));

            bool ret = true;
            if (Dta != singleComparison.Dta)
                ret = false;
            else if (TotalCount != singleComparison.TotalCount)
                ret = false;
            else if (TotalCompared != singleComparison.TotalCompared)
                ret = false;
            else if (TotalFailed != singleComparison.TotalFailed)
                ret = false;
            return ret;
        }
    }


}
