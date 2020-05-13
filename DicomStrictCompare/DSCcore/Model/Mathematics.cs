using DicomStrictCompare.Model;
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
        /// <param name="dta">distance to agreement parameters</param>
        /// <returns> The total failed voxels and total number of voxels compared</returns>
        /// 
        public abstract SingleComparison CompareAbsolute(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);
        public abstract SingleComparison CompareRelative(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);
        public abstract SingleComparison CompareParallel( Model.DoseMatrixOptimal source,  Model.DoseMatrixOptimal target, Model.Dta dta, int cpuParallel);
    }



}
