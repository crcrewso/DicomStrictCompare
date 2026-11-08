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

        /// <summary>
        /// As <see cref="CompareAbsolute(in DoseMatrixOptimal, in DoseMatrixOptimal, Dta)"/>, observing a
        /// cancellation token while it runs. A whole-grid comparison takes seconds, so a batch that only
        /// checks between pairs cannot stop promptly (Cody, 2026-11-08: "Cancel should also work faster").
        /// The default honours the token only on entry, so an implementation that does not override it is
        /// still correct, just slow to stop.
        /// </summary>
        public virtual SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, System.Threading.CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return CompareAbsolute(source, target, dta);
        }

        /// <summary>As <see cref="CompareRelative(in DoseMatrixOptimal, in DoseMatrixOptimal, Dta)"/>, observing a cancellation token.</summary>
        public virtual SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, System.Threading.CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return CompareRelative(source, target, dta);
        }

        // Parallelism belongs at the level of batches of comparisons (one file pair per task), not inside a
        // single comparison. CompareParallel was removed for that reason.
    }



}
