using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DSClibrary.Profiles
{
    /// <summary>
    /// A vendor-free collection of measurements and calculated doses, grouped by beam. It is what a
    /// mapper produces and what a batch comparison consumes, and sets read from different files and
    /// different vendors merge into one.
    /// </summary>
    public sealed class MeasurementSet : IEnumerable<BeamGroup>
    {
        private readonly Dictionary<BeamKey, BeamGroup> _beams;

        /// <summary>Beams in the order they were first seen, so output is stable.</summary>
        public IReadOnlyList<BeamGroup> Beams { get; }

        /// <summary>
        /// Profiles that could not be grouped because their beam is not fully identified - a Sun
        /// Nuclear prm gives neither field size nor energy, for instance. They are kept rather than
        /// dropped, so that a caller can supply what is missing instead of wondering where they went.
        /// </summary>
        public IReadOnlyList<Profile> Unresolved { get; }

        private MeasurementSet(IReadOnlyList<BeamGroup> beams, IReadOnlyList<Profile> unresolved)
        {
            Beams = beams;
            Unresolved = unresolved;
            _beams = beams.ToDictionary(b => b.Key);
        }

        public static MeasurementSet Empty { get; } =
            new(Array.Empty<BeamGroup>(), Array.Empty<Profile>());

        /// <summary>
        /// Groups profiles and doses by beam. A profile whose <see cref="BeamKey.IsComplete"/> is
        /// false goes to <see cref="Unresolved"/>.
        /// </summary>
        public static MeasurementSet Create(
            IEnumerable<Profile>? profiles = null,
            IEnumerable<DoseVolume>? volumes = null)
        {
            var allProfiles = (profiles ?? Enumerable.Empty<Profile>()).ToArray();
            var allVolumes = (volumes ?? Enumerable.Empty<DoseVolume>()).ToArray();

            var unresolved = allProfiles.Where(p => !p.Beam.IsComplete).ToArray();
            var resolved = allProfiles.Where(p => p.Beam.IsComplete);

            // Order of first appearance, taking profiles before doses, so a set built twice from the
            // same inputs reads the same way.
            var order = new List<BeamKey>();
            var seen = new HashSet<BeamKey>();
            foreach (var key in resolved.Select(p => p.Beam).Concat(allVolumes.Select(v => v.Beam)))
                if (seen.Add(key))
                    order.Add(key);

            var byKey = resolved.ToLookup(p => p.Beam);
            var volumesByKey = allVolumes.ToLookup(v => v.Beam);

            var groups = order
                .Select(key => new BeamGroup(key, byKey[key], volumesByKey[key]))
                .ToArray();

            return new MeasurementSet(groups, unresolved);
        }

        public BeamGroup? Beam(BeamKey key) => _beams.TryGetValue(key, out var group) ? group : null;

        /// <summary>Beams that have curves but no calculated dose to compare them against.</summary>
        public IEnumerable<BeamGroup> MissingReferences => Beams.Where(b => b.IsMissingReference);

        public IEnumerable<Profile> AllProfiles => Beams.SelectMany(b => b.Profiles);

        /// <summary>
        /// Combines two sets. Beams present in both are merged rather than duplicated, which is what
        /// makes reading measurements from one file and doses from another produce a single set.
        /// </summary>
        public MeasurementSet Merge(MeasurementSet other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));

            return Create(
                AllProfiles.Concat(Unresolved).Concat(other.AllProfiles).Concat(other.Unresolved),
                Beams.SelectMany(b => b.Volumes).Concat(other.Beams.SelectMany(b => b.Volumes)));
        }

        public IEnumerator<BeamGroup> GetEnumerator() => Beams.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() =>
            $"{Beams.Count} beam(s), {AllProfiles.Count()} profile(s), {Unresolved.Count} unresolved";
    }
}
