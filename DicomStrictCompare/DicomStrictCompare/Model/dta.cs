using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomStrictCompare.Model
{
    /// <summary>
    /// Holds all information needed for a single comparison set
    /// </summary>
    public class Dta
    {
        // if true unit of distance is in mm
        // if false unit of distance in voxels
        public bool UseMM { get; }
        /// <summary>
        /// Fraction of peak dose below which comparison is ignored
        /// </summary>
        public double Threshhold { get; } 
        /// <summary>
        /// fractional difference between doses of interest
        /// </summary>
        public double Tolerance { get; }
        /// <summary>
        /// distance in arbitrary unit for matching 
        /// </summary>
        public double Distance { get; }
        /// <summary>
        /// If relative is true calculation is based off of max dose
        /// if not calculation is based off of source voxel dose. 
        /// </summary>
        public bool Relative { get; }

        public int TrimWidth { get;  }


        List<string> summary => new List<string> { (Tolerance*100).ToString("0.0"), Distance.ToString("0.00"), (Threshhold*100).ToString("0.0"), Relative.ToString(), UseMM ? "mm" : "voxel" };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="useMM"></param>
        /// <param name="threshhold"></param>
        /// <param name="tolerance"></param>
        /// <param name="distance"></param>
        /// <param name="relative"></param>
        public Dta (bool useMM, double threshhold, double tolerance, double distance = 0, bool relative = true, int trim = 0)
        {
            UseMM = useMM; 
            Threshhold = threshhold; 
            Tolerance = tolerance; 
            Distance = distance; 
            Relative = relative;
            TrimWidth = trim;
        }

        public Dta (string fromDtaToString)
        {
            string[] values = fromDtaToString.Replace(", ", "|").Split('|');
            Tolerance = Convert.ToDouble(values[0]);
            Distance = Convert.ToDouble(values[1]);
            Threshhold = Convert.ToDouble(values[2]);
            Relative = Convert.ToBoolean(values[3]);
            UseMM = (values[4] == "mm" ? true : false);
        }


        public string ShortToString() => (Tolerance * 100.0).ToString("0.0") + " % " + Distance.ToString() + (UseMM ? " mm" : " voxels");

        public override string ToString() => String.Join(", ", summary);
        /// <summary>
        /// Returns the header for the ToString() function
        /// </summary>
        /// <returns>String</returns>
        /// 
        static public string Titles()
        {
            string[] titles = new string[] { "Tolerance", "Distance", "Threshhold", "Relative to Max dose?", "Unit" };
            return String.Join(", ", titles);
        }
    }


}
