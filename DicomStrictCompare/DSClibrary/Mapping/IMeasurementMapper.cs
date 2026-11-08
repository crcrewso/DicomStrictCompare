using DSClibrary.Profiles;

namespace DSClibrary.Mapping
{
    /// <summary>
    /// Turns one vendor result into the canonical model. Every decision about units, naming,
    /// conventions and what counts as missing lives in an implementation of this interface, and
    /// nowhere else.
    /// <para>
    /// That separation is the point: a result's tests assert what the file said, a mapper's tests
    /// assert what it means, and a new format needs a new result type plus a new mapper while the
    /// canonical model stays still.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">A vendor-faithful result type.</typeparam>
    public interface IMeasurementMapper<in TResult>
    {
        /// <summary>The vendor this mapper reads, for provenance.</summary>
        string Vendor { get; }

        MeasurementSet Map(TResult result);
    }
}
