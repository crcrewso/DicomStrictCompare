using System.Linq;

namespace DicomStrictCompare
{
    public abstract class IMathematics
    {
        /// <summary>
        /// Compares the two provided dose arrays voxel to voxel. Dose difference is calculated as % of source dose
        /// </summary>
        /// <param name="source">Reference dose array</param>
        /// <param name="target">Dose Array being verified</param>
        /// <param name="tolerance">Less than this percent difference is a pass</param>
        /// <param name="epsilon">Threshold percent of max dose below which comparison will not be evaluated</param>
        /// <returns></returns>
        /// 
        public abstract System.Tuple<int, int> CompareAbsolute(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);
        public abstract System.Tuple<int, int> CompareRelative(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);

    }



}
