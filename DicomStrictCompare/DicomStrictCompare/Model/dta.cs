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
        public calcType Type { get; }

        public int TrimWidth { get;  }

        public bool Relative => (Type == calcType.relative) ? true : false;


        public enum calcType { relative, absolute };


        List<string> Summary => new List<string> { (Tolerance*100).ToString("0.0"), Distance.ToString("0.00"), (Threshhold*100).ToString("0.0"), Type.ToString(), UseMM ? "mm" : "voxel" };

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
            TrimWidth = trim;
            Type = relative ? calcType.relative : calcType.absolute;
        }

        public Dta (string fromDtaToString)
        {
            if (fromDtaToString == null) throw new ArgumentNullException(nameof(fromDtaToString));
            string[] values = fromDtaToString.Replace(", ", "|").Split('|');
            Tolerance = Convert.ToDouble(values[0]);
            Distance = Convert.ToDouble(values[1]);
            Threshhold = Convert.ToDouble(values[2]);
            Type = Convert.ToBoolean(values[3]) ? calcType.relative: calcType.absolute;
            UseMM = (values[4] == "mm" ? true : false);
        }


        public string ShortToString() => (Tolerance * 100.0).ToString("0.0") + " % " + Distance.ToString() + (UseMM ? " mm" : " voxels");

        public override string ToString() => String.Join(", ", Summary);
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

        public bool Equals(Dta dta)
        {
            if (dta == null) throw new ArgumentNullException(nameof(dta));
            bool ret = true;
            if (Distance != dta.Distance)
                ret = false;
            else if (Type != dta.Type)
                ret = false;
            else if (Threshhold != dta.Threshhold)
                ret = false;
            else if (Tolerance != dta.Tolerance)
                ret = false;
            else if (TrimWidth != dta.TrimWidth)
                ret = false;
            else if (UseMM != dta.UseMM)
                ret = false;
            return ret;
        }
    }


}
