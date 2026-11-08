using System;
using System.Collections.Generic;
using System.Linq;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// Everything belonging to one beam: the curves measured in it, and the calculated dose or doses
    /// for it.
    /// <para>
    /// This grouping is the unit a comparison runs over, which is why the collection is two levels
    /// rather than a flat list. A group holding profiles with an empty <see cref="Volumes"/> is a
    /// missing reference, visible before any comparison starts instead of surfacing later as a lookup
    /// that found nothing.
    /// </para>
    /// </summary>
    public sealed class BeamGroup
    {
        public BeamKey Key { get; }
        public IReadOnlyList<Profile> Profiles { get; }
        public IReadOnlyList<DoseVolume> Volumes { get; }

        public BeamGroup(BeamKey key, IEnumerable<Profile>? profiles = null, IEnumerable<DoseVolume>? volumes = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Profiles = (profiles ?? Enumerable.Empty<Profile>()).ToArray();
            Volumes = (volumes ?? Enumerable.Empty<DoseVolume>()).ToArray();
        }

        /// <summary>True when there are curves to compare but nothing to compare them against.</summary>
        public bool IsMissingReference => Profiles.Count > 0 && Volumes.Count == 0;

        public IEnumerable<Profile> OfType(CurveType type) => Profiles.Where(p => p.Scan.CurveType == type);

        /// <summary>
        /// The profile at a given scan address, or null. Keys are quantised, so this is an exact
        /// lookup that nonetheless tolerates the last-bit disagreement between a measured depth and a
        /// calculated one.
        /// </summary>
        public Profile? At(ScanKey scan) => Profiles.FirstOrDefault(p => p.Scan == scan);

        public BeamGroup With(IEnumerable<Profile>? addProfiles = null, IEnumerable<DoseVolume>? addVolumes = null) =>
            new(Key,
                Profiles.Concat(addProfiles ?? Enumerable.Empty<Profile>()),
                Volumes.Concat(addVolumes ?? Enumerable.Empty<DoseVolume>()));

        public override string ToString() =>
            $"{Key}: {Profiles.Count} profile(s), {Volumes.Count} dose(s)";
    }
}
