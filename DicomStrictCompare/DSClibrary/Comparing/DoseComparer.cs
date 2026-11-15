using System.Linq;

namespace DSClibrary.Comparing
{
    /// <summary>
    /// A dose comparison engine (formerly <c>IMathematics</c>). <see cref="HandWrittenComparer"/> is the reference;
    /// the others must reproduce its counts. Front ends hold only this type, built by <see cref="DoseComparers.Create"/>.
    /// </summary>
    public abstract class DoseComparer
    {
        /// <summary>What ran, for the record kept with every result: kind, threads, device, precision.</summary>
        public virtual string Description => GetType().Name;



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

        /// <summary>
        /// As <see cref="CompareAbsolute(in DoseMatrixOptimal, in DoseMatrixOptimal, Dta, System.Threading.CancellationToken)"/>,
        /// with the threshold and tolerance taken as fractions of <paramref name="referenceMaximum"/> rather than
        /// the source's volume maximum (<see cref="ReferenceMaximum"/>).
        /// </summary>
        public abstract SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, System.Threading.CancellationToken cancellation);

        /// <summary>As the relative comparison, with a chosen reference maximum (<see cref="ReferenceMaximum"/>).</summary>
        public abstract SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, double referenceMaximum, System.Threading.CancellationToken cancellation);

        /// <summary>As <see cref="CompareRelative(in DoseMatrixOptimal, in DoseMatrixOptimal, Dta)"/>, observing a cancellation token.</summary>
        public virtual SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, System.Threading.CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return CompareRelative(source, target, dta);
        }

        // Batches of pairs (BatchComparer) and threads within one pair (MultiThreadComparer) both exist; the
        // 2026-11-15 measurements are in ~Resources/DSC-notes/2026-11-14-gpu-crossing-point.md on jackdaw.
    }



}
