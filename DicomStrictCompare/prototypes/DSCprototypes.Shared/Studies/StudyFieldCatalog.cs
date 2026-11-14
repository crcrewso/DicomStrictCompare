using DSCprototypes.Shared.Graphs;

namespace DSCprototypes.Shared.Studies;

/// <summary>
/// One field as it appears across the study's sets (F7): every dose that some comparison paired with
/// another dose of the group. AAA 15.6 paired with AAA 18 and with AXB 15.6, which was paired with
/// AXB 18, makes one group of four.
/// </summary>
public sealed record StudyField(FieldFacts Facts, IReadOnlyList<SetDose> Doses, IReadOnlyList<string> Notes)
{
    public string Label => $"{Facts.Patient} / {Facts.SourcePlan} / {Facts.Field}  ({Doses.Count} set{(Doses.Count == 1 ? "" : "s")})";
    public override string ToString() => Label;
}

public static class StudyFieldCatalog
{
    /// <summary>
    /// Groups the session's paired doses into fields by following the pairs, so a field's identity is
    /// the F2 key and survives a plan label that changes between sets. Sets appear in study order. A
    /// set that reaches one group through two different doses keeps the first, and the group says so.
    /// </summary>
    public static IReadOnlyList<StudyField> Build(StudySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var parent = new Dictionary<(string Set, string Path), (string, string)>();
        var doseOf = new Dictionary<(string, string), SetDose>();
        var factsOf = new Dictionary<(string, string), FieldFacts>();

        (string, string) Find((string, string) x)
        {
            while (!parent[x].Equals(x)) x = parent[x] = parent[parent[x]];
            return x;
        }

        void Add(string set, DSClibrary.Results.DICOM.RTDoseWithPlan dose, FieldFacts facts)
        {
            var k = (set, dose.Dose.SourcePath);
            if (parent.ContainsKey(k)) return;
            parent[k] = k;
            doseOf[k] = new SetDose(set, dose);
            factsOf[k] = facts;
        }

        foreach (var preview in session.Previews)
            foreach (var p in preview.Pairs)
            {
                Add(preview.Comparison.SourceSet, p.Pair.Source, p.Facts);
                Add(preview.Comparison.TargetSet, p.Pair.Target, p.Facts);
                var a = Find((preview.Comparison.SourceSet, p.Pair.Source.Dose.SourcePath));
                var b = Find((preview.Comparison.TargetSet, p.Pair.Target.Dose.SourcePath));
                if (!a.Equals(b)) parent[b] = a;
            }

        var order = session.Study.Sets.Select((s, i) => (s.Name, i)).ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);
        var fields = new List<StudyField>();
        foreach (var group in parent.Keys.GroupBy(Find))
        {
            var members = group.OrderBy(k => order.TryGetValue(k.Set, out int i) ? i : int.MaxValue).ThenBy(k => k.Path, StringComparer.Ordinal).ToArray();
            var notes = new List<string>();
            var doses = new List<SetDose>();
            foreach (var bySet in members.GroupBy(k => k.Set))
            {
                var first = bySet.First();
                doses.Add(doseOf[first]);
                if (bySet.Count() > 1)
                    notes.Add($"{bySet.Key} reaches this field through {bySet.Count()} doses; showing {Path.GetFileName(first.Path)}");
            }
            fields.Add(new StudyField(factsOf[members[0]], doses, notes));
        }

        return fields
            .OrderBy(f => f.Facts.Patient, StringComparer.Ordinal)
            .ThenBy(f => f.Facts.IsElectron).ThenBy(f => f.Facts.EnergyValue).ThenBy(f => f.Facts.IsFff)
            .ThenBy(f => double.IsNaN(f.Facts.FieldAreaMm2) ? double.MaxValue : f.Facts.FieldAreaMm2)
            .ThenBy(f => f.Facts.SourcePlan, StringComparer.Ordinal).ThenBy(f => f.Facts.Field, StringComparer.Ordinal)
            .ToArray();
    }
}
