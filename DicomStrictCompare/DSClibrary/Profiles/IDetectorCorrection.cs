using System.Collections.Generic;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A correction for the detector's finite volume, applied to a profile's points.
    /// <para>
    /// No physical kernel ships with this interface yet, deliberately. Volume averaging and the
    /// effective point of measurement change reported penumbra and dmax, so an implementation needs
    /// validating against a known answer before anything is compared with it - that is its own piece
    /// of work. What exists now is the interface and the state machine on
    /// <see cref="Profile.WithDetectorCorrection"/>, which is what stops a correction being applied
    /// twice.
    /// </para>
    /// <para>
    /// The same physics can be approached from the other side, by blurring a calculated dose to the
    /// detector's size instead of sharpening the measurement. Both are legitimate; doing both is not,
    /// which is why <see cref="DetectorGeometryCorrection"/> is tracked rather than assumed.
    /// </para>
    /// </summary>
    public interface IDetectorCorrection
    {
        /// <summary>A short description recorded on the corrected profile, for the audit trail.</summary>
        string Describe();

        IReadOnlyList<ProfilePoint> Apply(IReadOnlyList<ProfilePoint> points);
    }

    /// <summary>
    /// A correction that changes nothing. Its purpose is to exercise the state machine - that a
    /// profile records having been corrected, and refuses to be corrected again - without asserting
    /// any physics.
    /// </summary>
    public sealed class IdentityDetectorCorrection : IDetectorCorrection
    {
        public string Describe() => "identity (no physical correction)";

        public IReadOnlyList<ProfilePoint> Apply(IReadOnlyList<ProfilePoint> points) => points;
    }
}
