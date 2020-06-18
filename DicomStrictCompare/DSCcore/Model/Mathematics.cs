using DicomStrictCompare.Model;
using System.Linq;

namespace DicomStrictCompare
{
    /// <summary>
    /// Functions to compare dose arrays 
    /// Implimented as an interface to allow for GPGPU or non C# implimentations in the future 
    /// </summary>
    public abstract class IMathematics
    {


        /// <summary>
        /// Compares target to source based on the DTA settings provided, using local percent arithmetic
        /// Single threaded 
        /// </summary>
        /// <param name="source">Reference dose array</param>
        /// <param name="target">Dose Array being verified</param>
        /// <param name="dta">distance to agreement parameters</param>
        /// <returns> The total failed voxels and total number of voxels compared</returns>
        public abstract SingleComparison CompareAbsolute(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);
        /// <summary>
        /// Compares target to source based on the DTA settings provided, comparisons relative to source dmax Single threaded
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="dta"></param>
        /// <returns></returns>
        public abstract SingleComparison CompareRelative(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);
        
        /// <summary>
        /// Compares target to source, based on the DTA settings provided, using the number of cores up to max 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="dta"></param>
        /// <param name="cpuParallel">Up to this many parallel comparison threads will be generated. </param>
        /// <returns></returns>
        public abstract SingleComparison CompareParallel(Model.DoseMatrixOptimal source, Model.DoseMatrixOptimal target, Model.Dta dta, int cpuParallel);
    }



}
