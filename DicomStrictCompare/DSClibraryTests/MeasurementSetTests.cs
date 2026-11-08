using System.Linq;
using DSClibrary.Profiles;
using DSClibrary.Units;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSClibraryTests
{
    [TestClass]
    public class MeasurementSetTests
    {
        private static Profile ProfileFor(BeamKey beam, CurveType type = CurveType.Crossplane, double depth = 100) =>
            new(beam,
                new ScanKey(type, Distance.FromMillimetres(depth)),
                new[]
                {
                    new ProfilePoint(Distance.FromMillimetres(-10), 1, null),
                    new ProfilePoint(Distance.FromMillimetres(10), 2, null)
                });

        private static BeamKey Complete(double energy = 6) => new(
            Modality.Photon, energy, false,
            Distance.FromMillimetres(100), Distance.FromMillimetres(100), Distance.FromMillimetres(1000));

        private static BeamKey Incomplete() => new(Modality.Photon, energy: null,
            fieldInplane: null, fieldCrossplane: null, ssd: Distance.FromCentimetres(100));

        [TestMethod]
        public void ProfilesOfOneBeamGroupTogether()
        {
            var beam = Complete();
            var set = MeasurementSet.Create(new[]
            {
                ProfileFor(beam, CurveType.Crossplane, 50),
                ProfileFor(beam, CurveType.Inplane, 50),
                ProfileFor(beam, CurveType.Crossplane, 100)
            });

            Assert.AreEqual(1, set.Beams.Count);
            Assert.AreEqual(3, set.Beams[0].Profiles.Count);
        }

        [TestMethod]
        public void DifferentBeamsGetDifferentGroups()
        {
            var set = MeasurementSet.Create(new[]
            {
                ProfileFor(Complete(energy: 6)),
                ProfileFor(Complete(energy: 10))
            });
            Assert.AreEqual(2, set.Beams.Count);
        }

        [TestMethod]
        public void ProfilesWithAnIncompleteBeamGoToUnresolvedRatherThanBeingDropped()
        {
            // What a Sun Nuclear prm yields: neither field size nor energy. They are kept so a caller
            // can supply what is missing instead of wondering where they went.
            var set = MeasurementSet.Create(new[] { ProfileFor(Complete()), ProfileFor(Incomplete()) });

            Assert.AreEqual(1, set.Beams.Count);
            Assert.AreEqual(1, set.Unresolved.Count);
        }

        [TestMethod]
        public void GroupingIsIndependentOfInputOrder()
        {
            var a = ProfileFor(Complete(energy: 6));
            var b = ProfileFor(Complete(energy: 10));

            var forwards = MeasurementSet.Create(new[] { a, b });
            var backwards = MeasurementSet.Create(new[] { b, a });

            CollectionAssert.AreEquivalent(
                forwards.Beams.Select(g => g.Key).ToArray(),
                backwards.Beams.Select(g => g.Key).ToArray());
        }

        [TestMethod]
        public void LookupByBeamKeyFindsTheGroup()
        {
            var beam = Complete();
            var set = MeasurementSet.Create(new[] { ProfileFor(beam) });

            // A freshly built key, not the same instance: structural equality is what makes this work.
            Assert.IsNotNull(set.Beam(Complete()));
            Assert.IsNull(set.Beam(Complete(energy: 18)));
        }

        [TestMethod]
        public void MergeCombinesTwoSetsWithoutDuplicatingABeam()
        {
            var beam = Complete();
            var measurements = MeasurementSet.Create(new[] { ProfileFor(beam, CurveType.Crossplane, 50) });
            var more = MeasurementSet.Create(new[] { ProfileFor(beam, CurveType.Inplane, 50) });

            var merged = measurements.Merge(more);

            Assert.AreEqual(1, merged.Beams.Count);
            Assert.AreEqual(2, merged.Beams[0].Profiles.Count);
        }

        [TestMethod]
        public void MergeKeepsUnresolvedProfiles()
        {
            var merged = MeasurementSet
                .Create(new[] { ProfileFor(Incomplete()) })
                .Merge(MeasurementSet.Create(new[] { ProfileFor(Complete()) }));

            Assert.AreEqual(1, merged.Unresolved.Count);
            Assert.AreEqual(1, merged.Beams.Count);
        }

        [TestMethod]
        public void AnEmptySetIsUsable()
        {
            Assert.AreEqual(0, MeasurementSet.Empty.Beams.Count);
            Assert.AreEqual(0, MeasurementSet.Empty.AllProfiles.Count());
            Assert.IsNull(MeasurementSet.Empty.Beam(Complete()));
        }
    }
}
