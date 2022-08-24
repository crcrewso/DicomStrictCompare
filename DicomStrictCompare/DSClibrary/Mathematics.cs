using System.Linq;

namespace DSClibrary
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
        public abstract SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta);
        public abstract SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta);
        public abstract SingleComparison CompareParallel( DoseMatrixOptimal source,  DoseMatrixOptimal target, Dta dta, int cpuParallel);
    }



}
