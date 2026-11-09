using System.Globalization;
using DSClibrary.Matching;
using DSClibrary.Results.DICOM;

namespace DSCprototypes.Shared.Graphs;

/// <summary>
/// A matched pair described by the beam metadata a person filters on: energy and field size, read
/// from the plan beside the dose. Cody, 2026-11-08: view results "scrolling from pdd through the
/// depths for one FieldSize and one Energy type".
/// </summary>
public sealed record BeamDescriptor(
    MatchedDosePair Pair,
    string Energy,
    string FieldSize,
    bool IsElectron,
    double EnergyValue,
    bool IsFff,
    double FieldCrossplaneMm,
    double FieldInplaneMm)
{
    /// <summary>What the beam list shows: the plan's beam name when it has one, else the match key.</summary>
    public string Label
    {
        get
        {
            string name = Pair.Source.Beam?.BeamName is { Length: > 0 } n ? n : Pair.Key.ToString();
            string files = Path.GetFileName(Pair.Source.Dose.SourcePath);
            return $"{name}  ({files})";
        }
    }

    public override string ToString() => Label;
}

public static class PairCatalog
{
    public const string AnyEnergy = "(all energies)";
    public const string AnyFieldSize = "(all field sizes)";
    public const string Unknown = "unknown";

    /// <summary>"6 MV", "10 MV FFF", "12 MeV"; <see cref="Unknown"/> when the plan is absent or silent.</summary>
    public static string EnergyLabel(RTPlanBeam? beam)
    {
        if (beam?.NominalBeamEnergy is not { } energy) return Unknown;
        bool electron = string.Equals(beam.RadiationType, "ELECTRON", StringComparison.OrdinalIgnoreCase);
        string unit = electron ? "MeV" : "MV";
        string fff = beam.IsFluenceModeFff == true ? " FFF" : "";
        return energy.ToString("0.##", CultureInfo.InvariantCulture) + " " + unit + fff;
    }

    /// <summary>
    /// The field size a person means. For an electron beam with an applicator that is the
    /// applicator - "10 x 10 cm (A10)" - because the jaws behind it are opened wider (measured
    /// 2026-11-09: +-70 mm behind every A10 in resources/, which first labelled those beams
    /// "14 x 14 cm"; Cody: "some confusion with the field size"). Otherwise it is crossplane by
    /// inplane at the isocentre from the jaws, "10 x 10 cm". An applicator whose ID does not give
    /// its size shows the ID beside the jaw opening. <see cref="Unknown"/> when nothing says.
    /// </summary>
    public static string FieldSizeLabel(RTPlanBeam? beam)
    {
        if (beam is null) return Unknown;
        var (cross, inplane) = EffectiveFieldSizeMm(beam);
        string? applicator = beam.ApplicatorID?.Trim();
        if (cross is null || inplane is null)
            return applicator is { Length: > 0 } ? $"applicator {applicator}" : Unknown;
        string size = (cross.Value / 10).ToString("0.#", CultureInfo.InvariantCulture) + " x "
                    + (inplane.Value / 10).ToString("0.#", CultureInfo.InvariantCulture) + " cm";
        return applicator is { Length: > 0 } ? $"{size} ({applicator})" : size;
    }

    /// <summary>The applicator's size when the beam has one that states it, else the jaw opening.</summary>
    public static (double? CrossplaneMm, double? InplaneMm) EffectiveFieldSizeMm(RTPlanBeam beam)
    {
        var applicator = beam.ApplicatorSizeMm;
        if (applicator.CrossplaneMm is not null && applicator.InplaneMm is not null) return applicator;
        return beam.FieldSizeAtIsocentre;
    }

    /// <summary>
    /// Photons only, for now. Cody, 2026-11-09, reviewing the first graphs: "I'm having a hard time
    /// reviewing these graphs and comparing them to what I remember, could we stick to photon
    /// profiles for now?" The electron beams are still described (so the count can be shown) and
    /// still compared; they are just not offered in the Graphs tab until he asks for them.
    /// </summary>
    public static bool GraphsShowElectrons { get; set; } = false;

    /// <summary>What the Graphs tab offers: the photon beams, and the electrons only when <see cref="GraphsShowElectrons"/>.</summary>
    public static (IReadOnlyList<BeamDescriptor> Shown, int HiddenElectrons) ForGraphs(IEnumerable<MatchedDosePair> pairs)
    {
        var all = Describe(pairs);
        if (GraphsShowElectrons) return (all, 0);
        return (all.Where(b => !b.IsElectron).ToArray(), all.Count(b => b.IsElectron));
    }

    public static IReadOnlyList<BeamDescriptor> Describe(IEnumerable<MatchedDosePair> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return pairs.Select(p =>
        {
            var beam = p.Source.Beam;
            var (cross, inplane) = beam is null ? (null, null) : EffectiveFieldSizeMm(beam);
            return new BeamDescriptor(
                p,
                EnergyLabel(beam),
                FieldSizeLabel(beam),
                string.Equals(beam?.RadiationType, "ELECTRON", StringComparison.OrdinalIgnoreCase),
                beam?.NominalBeamEnergy ?? double.NaN,
                beam?.IsFluenceModeFff == true,
                cross ?? double.NaN,
                inplane ?? double.NaN);
        })
        .OrderBy(d => d.IsElectron).ThenBy(d => d.EnergyValue).ThenBy(d => d.IsFff)
        .ThenBy(d => d.FieldCrossplaneMm * d.FieldInplaneMm).ThenBy(d => d.FieldCrossplaneMm)
        .ThenBy(d => d.Label, StringComparer.Ordinal)
        .ToArray();
    }

    /// <summary>Distinct energies in physical order: photons by energy, flattened before FFF, then electrons by energy.</summary>
    public static IReadOnlyList<string> Energies(IEnumerable<BeamDescriptor> beams) =>
        beams.OrderBy(d => d.IsElectron).ThenBy(d => d.EnergyValue).ThenBy(d => d.IsFff)
             .Select(d => d.Energy).Distinct().ToArray();

    /// <summary>Distinct field sizes by area, smallest first.</summary>
    public static IReadOnlyList<string> FieldSizes(IEnumerable<BeamDescriptor> beams) =>
        beams.OrderBy(d => d.FieldCrossplaneMm * d.FieldInplaneMm).ThenBy(d => d.FieldCrossplaneMm)
             .Select(d => d.FieldSize).Distinct().ToArray();

    public static IReadOnlyList<BeamDescriptor> Filter(IEnumerable<BeamDescriptor> beams, string? energy, string? fieldSize) =>
        beams.Where(d => (energy is null || energy == AnyEnergy || d.Energy == energy)
                      && (fieldSize is null || fieldSize == AnyFieldSize || d.FieldSize == fieldSize))
             .ToArray();
}
