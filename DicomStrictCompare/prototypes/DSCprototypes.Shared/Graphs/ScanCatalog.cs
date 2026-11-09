using System.Globalization;
using DSClibrary.Mapping.PTW;
using DSClibrary.Profiles;
using DSClibrary.Results.DICOM;
using DSClibrary.Results.PTW;

namespace DSCprototypes.Shared.Graphs;

/// <summary>One water-tank scan, described the way the Graphs tab filters: energy, field size, curve, depth.</summary>
public sealed record ScanDescriptor(
    Profile Profile,
    string File,
    int ScanNumber,
    string Energy,
    string FieldSize,
    bool IsElectron,
    double EnergyValue,
    bool IsFff,
    double FieldCrossplaneMm,
    double FieldInplaneMm,
    CurveKind Kind,
    double? DepthMm)
{
    public string CurveLabel => Kind == CurveKind.DepthDose
        ? "Depth dose"
        : $"{(Kind == CurveKind.Crossplane ? "Crossplane" : "Inplane")} at {DepthMm?.ToString("0.#", CultureInfo.InvariantCulture)} mm";

    public string Label => $"{CurveLabel}  ({Path.GetFileName(File)} scan {ScanNumber})";

    public override string ToString() => Label;
}

/// <summary>A dose that matches a scan's beam, with why it was offered.</summary>
public sealed record DoseCandidate(RTDoseWithPlan Dose, string Note)
{
    public string Label => $"{Dose.Beam?.BeamName ?? "?"}  ({Path.GetFileName(Dose.Dose.SourcePath)}){(Note.Length > 0 ? "  - " + Note : "")}";
    public override string ToString() => Label;
}

/// <summary>
/// Reads every PTW mcc file under a folder and describes its scans; matches a scan to the doses
/// whose plan beam has the same modality, energy, filter and field size. This is the metadata
/// matching Cody asked for on 2026-11-09 ("matching target profile/dose files to reference through
/// metadata parameters like field size and energy"), done here for scan-to-dose only.
/// <para>
/// Measured 2026-11-09 across the 669 photon-area files in <c>Water tank scans\Raw Data</c>
/// (1,153 scans, no parse failures): SSD is 1000 mm in every scan, inplane is the Y gun-target axis
/// and crossplane the X left-right axis, depths cluster at dmax, 50, 100, 200 and 300 mm, 981 scans
/// are in arbitrary units and the rest in GY without a calibration flag - so curves are overlaid
/// normalised, which is also Cody's rule (2026-11-08: "for the most part profiles are treated as
/// relative"). The 142 wedge scans carry a WEDGE header and a blank FILTER and are left out for now.
/// </para>
/// </summary>
public static class ScanCatalog
{
    public const string AnyEnergy = PairCatalog.AnyEnergy;
    public const string AnyFieldSize = PairCatalog.AnyFieldSize;

    public sealed record LoadResult(IReadOnlyList<ScanDescriptor> Scans, int Files, int HiddenElectrons, int HiddenWedges, int HiddenOther, IReadOnlyList<string> Errors);

    public static LoadResult Load(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        var mapper = new MCCMeasurementMapper();
        var scans = new List<ScanDescriptor>();
        var errors = new List<string>();
        int files = 0, electrons = 0, wedges = 0, other = 0;

        foreach (string file in Directory.EnumerateFiles(folder, "*.mcc", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            MCCResult result;
            try { result = MCCParser.ParseFile(file); files++; }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(file)}: {ex.Message}"); continue; }

            foreach (var scan in result.Scans)
            {
                Profile profile;
                try { profile = mapper.MapScan(scan, result); }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(file)} scan {scan.ScanNumber}: {ex.Message}"); continue; }

                if (profile.Beam.Modality == Modality.Electron) { electrons++; continue; }
                if (scan.Text("WEDGE").Length > 0 || (profile.Beam.WedgeAngleDegrees ?? 0) != 0) { wedges++; continue; }
                CurveKind? kind = profile.Scan.CurveType switch
                {
                    CurveType.Pdd => CurveKind.DepthDose,
                    CurveType.Crossplane => CurveKind.Crossplane,
                    CurveType.Inplane => CurveKind.Inplane,
                    _ => null
                };
                if (kind is null || profile.Beam.Modality != Modality.Photon || profile.Beam.Energy is null
                    || profile.Beam.FieldCrossplane is null || profile.Beam.FieldInplane is null)
                { other++; continue; }

                double cross = profile.Beam.FieldCrossplane.Value.Millimetres;
                double inplane = profile.Beam.FieldInplane.Value.Millimetres;
                scans.Add(new ScanDescriptor(
                    profile, file, scan.ScanNumber,
                    EnergyLabel(profile.Beam), FieldSizeLabel(cross, inplane),
                    false, profile.Beam.Energy.Value, profile.Beam.IsFff == true, cross, inplane,
                    kind.Value, profile.Scan.Depth?.Millimetres));
            }
        }

        var ordered = scans
            .OrderBy(s => s.EnergyValue).ThenBy(s => s.IsFff)
            .ThenBy(s => s.FieldCrossplaneMm * s.FieldInplaneMm).ThenBy(s => s.FieldCrossplaneMm)
            .ThenBy(s => s.Kind).ThenBy(s => s.DepthMm ?? -1).ThenBy(s => s.File, StringComparer.Ordinal).ThenBy(s => s.ScanNumber)
            .ToArray();
        return new LoadResult(ordered, files, electrons, wedges, other, errors);
    }

    public static string EnergyLabel(BeamKey beam) =>
        beam.Energy is { } e
            ? e.ToString("0.##", CultureInfo.InvariantCulture) + (beam.Modality == Modality.Electron ? " MeV" : " MV") + (beam.IsFff == true ? " FFF" : "")
            : PairCatalog.Unknown;

    public static string FieldSizeLabel(double crossMm, double inplaneMm) =>
        (crossMm / 10).ToString("0.#", CultureInfo.InvariantCulture) + " x " + (inplaneMm / 10).ToString("0.#", CultureInfo.InvariantCulture) + " cm";

    public static IReadOnlyList<string> Energies(IEnumerable<ScanDescriptor> scans) =>
        scans.OrderBy(s => s.IsElectron).ThenBy(s => s.EnergyValue).ThenBy(s => s.IsFff).Select(s => s.Energy).Distinct().ToArray();

    public static IReadOnlyList<string> FieldSizes(IEnumerable<ScanDescriptor> scans) =>
        scans.OrderBy(s => s.FieldCrossplaneMm * s.FieldInplaneMm).ThenBy(s => s.FieldCrossplaneMm).Select(s => s.FieldSize).Distinct().ToArray();

    public static IReadOnlyList<ScanDescriptor> Filter(IEnumerable<ScanDescriptor> scans, string? energy, string? fieldSize) =>
        scans.Where(s => (energy is null || energy == AnyEnergy || s.Energy == energy)
                      && (fieldSize is null || fieldSize == AnyFieldSize || s.FieldSize == fieldSize)).ToArray();

    /// <summary>
    /// The doses whose plan beam is the scan's beam: same modality, energy to 0.01, same filter (a
    /// plan that does not say is taken as flattened), and the same field size to half a millimetre,
    /// crossplane against the X jaws and inplane against the Y jaws. SSD is not compared: the scans
    /// all say 1000 mm and the plans never say (see <c>RTDoseVolumeMapper.DefaultSsd</c>). Open
    /// fields at gantry zero come first; an MLC-shaped or rotated beam is offered after them with a
    /// note, not hidden, because the jaw size alone cannot tell an open field from a shaped one.
    /// </summary>
    public static IReadOnlyList<DoseCandidate> MatchingDoses(ScanDescriptor scan, IEnumerable<RTDoseWithPlan> doses)
    {
        var found = new List<(DoseCandidate Candidate, int Rank)>();
        foreach (var dose in doses)
        {
            var beam = dose.Beam;
            if (beam is null) continue;
            if (!string.Equals(beam.RadiationType, "PHOTON", StringComparison.OrdinalIgnoreCase)) continue;
            if (beam.NominalBeamEnergy is not { } energy || Math.Abs(energy - scan.EnergyValue) > 0.011) continue;
            bool planFff = beam.IsFluenceModeFff == true;
            if (planFff != scan.IsFff) continue;
            var (cross, inplane) = beam.FieldSizeAtIsocentre;
            if (cross is null || inplane is null) continue;
            if (Math.Abs(cross.Value - scan.FieldCrossplaneMm) > 0.5 || Math.Abs(inplane.Value - scan.FieldInplaneMm) > 0.5) continue;

            bool mlc = beam.LeafJawPositions.Keys.Any(k => k.StartsWith("MLC", StringComparison.OrdinalIgnoreCase));
            bool gantryZero = Math.Abs(beam.GantryAngle ?? 0) < 0.1 && Math.Abs(beam.BeamLimitingDeviceAngle ?? 0) < 0.1;
            var notes = new List<string>();
            if (mlc) notes.Add("MLC in the plan: the jaw size may not be the field");
            if (!gantryZero) notes.Add($"gantry {beam.GantryAngle:0.#}, collimator {beam.BeamLimitingDeviceAngle:0.#}: the geometry reading assumes 0/0");
            int rank = (mlc ? 1 : 0) + (gantryZero ? 0 : 2);
            found.Add((new DoseCandidate(dose, string.Join("; ", notes)), rank));
        }
        return found.OrderBy(f => f.Rank).ThenBy(f => f.Candidate.Dose.Dose.SourcePath, StringComparer.Ordinal).Select(f => f.Candidate).ToArray();
    }
}
