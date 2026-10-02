using System.Linq;

namespace DSClibrary
{
    public abstract class IMathematics
    {


        /// <summary>
        /// Local comparison: compares the two dose arrays point by point, with the dose tolerance taken as a
        /// fraction of the source dose at each point.
        /// </summary>
        /// <param name="source">Reference dose array</param>
        /// <param name="target">Dose Array being verified</param>
        /// <param name="dta">distance to agreement parameters</param>
        /// <returns> The total failed voxels and total number of voxels compared</returns>
        public abstract SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta);

        /// <summary>
        /// Global comparison: as CompareAbsolute, but the dose tolerance is a fraction of the source maximum dose.
        /// </summary>
        public abstract SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta);

        // Parallelism belongs at the level of batches of comparisons (one file pair per task), not inside a
        // single comparison. CompareParallel was removed for that reason.
    }



}
