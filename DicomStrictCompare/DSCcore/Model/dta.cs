using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCSCore.Model
{
    /// <summary>
    /// Holds all information needed for a single comparison set
    /// </summary>
    public class Dta
    {

        /// <summary>
        /// Setting to keep track of unit of distance for dta 
        /// if true unit of distance is in mm
        /// if false unit of distance in voxels
        /// </summary>
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
        /// If Global is true calculation is based off of max Source dose
        /// if not calculation is based off of source voxel dose. 
        /// </summary>
        public CalcType Type { get; }

        /// <summary>
        /// Algorithm of use, DTA being simple, Gamma being weighted complex and expensive but more commonly used
        /// </summary>
        public CalcAlgorithm Algorithm { get; }

        /// <summary>
        /// skin depth removed from source phantom before matching
        /// </summary>
        //TODO: verify if this is source, target, or both
        public int TrimWidth { get; }

        /// <summary>
        /// Backwards compatable placeholder for logic
        /// </summary>
        public bool Global => (Type == CalcType.Global) ? true : false;

        /// <summary>
        /// lists the possible scope limit techniques for voxel to voxel comparison. 
        /// </summary>
        public enum CalcType { 
            /// <summary>
            /// perccent difference is taken to mean that pass or fail is based on the difference between source and target based on the fraction of source dmax 
            /// </summary>
            Global,
            /// <summary>
            /// percent difference conditions are based solely on the source and target doses at the point of inspection
            /// </summary>
            Local };

        /// <summary>
        /// list of supported comparison algorithms 
        /// </summary>
        //TODO: future features here, adding penumbra detection percent difference dta complex andrew analysis
        public enum CalcAlgorithm { 
            /// <summary>
            /// first the Global dose difference at the point in question is analyzed, if failed then 
            /// if its possible for the target to match the source within the distance window provided
            /// </summary>
            dta, 
            /// <summary>
            /// impliments gamma evaluation function
            /// </summary>
            //TODO: get references 
            gamma };

        /// <summary>
        /// Results summary
        /// </summary>
        List<string> Summary => new List<string> { (Tolerance * 100).ToString("0.0"), Distance.ToString("0.00"), (Threshhold * 100).ToString("0.0"), Type.ToString(), UseMM ? "mm" : "voxel" };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="useMM">if true trim is mm defined, if false trim is voxel defined</param>
        /// <param name="threshhold">minimum percent of Dmax for the dose to be calculated</param>
        /// <param name="tolerance">fractional diference from source that will return a pass</param>
        /// <param name="distance">distance for dta algorithm</param>
        /// <param name="relative">dose comparison tolerance condition</param>
        /// <param name="gamma">dose comparison algorithm</param>
        /// <param name="trim">depth of voxels to remove from surface of phantoms</param>
        public Dta(bool useMM, double threshhold, double tolerance, double distance = 0, bool relative = true, bool gamma = false, int trim = 0)
        {
            UseMM = useMM;
            Threshhold = threshhold;
            Tolerance = tolerance;
            Distance = distance;
            TrimWidth = trim;
            Type = relative ? CalcType.Global : CalcType.Local;
            Algorithm = gamma ? CalcAlgorithm.gamma : CalcAlgorithm.dta;
        }

        /// <summary>
        /// String constructor
        /// </summary>
        /// <param name="fromDtaToString"></param>
        public Dta(string fromDtaToString)
        {
            if (fromDtaToString == null) throw new ArgumentNullException(nameof(fromDtaToString));
            string[] values = fromDtaToString.Replace(", ", "|").Split('|');
            Tolerance = Convert.ToDouble(values[0]);
            Distance = Convert.ToDouble(values[1]);
            Threshhold = Convert.ToDouble(values[2]);
            Type = Convert.ToBoolean(values[3]) ? CalcType.Global : CalcType.Local;
            UseMM = (values[4] == "mm" ? true : false);
        }

        /// <summary>
        /// Shorter Summary 
        /// </summary>
        /// <returns></returns>
        public string ShortToString() => (Tolerance * 100.0).ToString("0.0") + " % " + Distance.ToString() + (Global? " Global": " Local") +  (UseMM ? " mm" : " voxels");

        /// <summary>
        /// Impliments standard tostring with comma. old way of reporting results
        /// </summary>
        /// <returns></returns>
        public override string ToString() => String.Join(", ", Summary);
        
        /// <summary>
        /// Returns the header for the ToString() function
        /// </summary>
        /// <returns>String</returns> 
        static public string Titles()
        {
            string[] titles = new string[] { "Tolerance", "Distance", "Threshhold", "Global?", "Unit" };
            return String.Join(", ", titles);
        }

        /// <summary>
        /// Tests equivalency 
        /// </summary>
        /// <param name="dta"></param>
        /// <returns></returns>
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
