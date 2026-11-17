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
/// matching crcrewso asked for on 2026-11-09 ("matching target profile/dose files to reference through
/// metadata parameters like field size and energy"), done here for scan-to-dose only.
/// <para>
/// Measured 2026-11-09 across the 669 photon-area files in <c>Water tank scans\Raw Data</c>
/// (1,153 scans, no parse failures): SSD is 1000 mm in every scan, inplane is the Y gun-target axis
/// and crossplane the X left-right axis, depths cluster at dmax, 50, 100, 200 and 300 mm, 981 scans
/// are in arbitrary units and the rest in GY without a calibration flag - so curves are overlaid
/// normalised, which is also crcrewso's rule (2026-11-08: "for the most part profiles are treated as
/// relative"). The 142 wedge scans carry a WEDGE header and a blank FILTER.
/// </para>
/// <para>
/// Since 2026-11-17 a scan is paired with doses by the library's rule, <see cref="ScanDoseMatching"/>, which also reads
/// the field's centre and an EDW's angle and direction, so wedge scans are listed too (their field-size label carries
/// the wedge) and an asymmetric field pairs only with its own dose. A wedge scan whose wedge can't be read is still
/// left out, and counted.
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
                var setup = ScanDoseMatching.FromScan(profile).Setup;
                bool wedged = scan.Text("WEDGE").Length > 0 || (profile.Beam.WedgeAngleDegrees ?? 0) != 0;
                if (wedged && setup?.WedgeAngleDegrees is not > 0) { wedges++; continue; }
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
                    EnergyLabel(profile.Beam), setup is null ? FieldSizeLabel(cross, inplane) : FieldLabel(setup),
                    false, profile.Beam.Energy.Value, profile.Beam.IsFff == true, cross, inplane,
                    kind.Value, profile.Scan.Depth?.Millimetres));
            }
        }

        var ordered = scans
            .OrderBy(s => s.EnergyValue).ThenBy(s => s.IsFff)
            .ThenBy(s => s.FieldCrossplaneMm * s.FieldInplaneMm).ThenBy(s => s.FieldCrossplaneMm).ThenBy(s => s.FieldSize, StringComparer.Ordinal)
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

    /// <summary>"10 x 10 cm", with the centre when the field is off the axis and the wedge when there is one:
    /// "10 x 10 cm centred (0, 50) mm", "20 x 20 cm, wedge 25 OUT".</summary>
    public static string FieldLabel(BeamSetup setup)
    {
        var ci = CultureInfo.InvariantCulture;
        string label = FieldSizeLabel(setup.CrossplaneMm, setup.InplaneMm);
        if (Math.Abs(setup.CentreCrossplaneMm) > 0.05 || Math.Abs(setup.CentreInplaneMm) > 0.05)
            label += $" centred ({setup.CentreCrossplaneMm.ToString("0.#", ci)}, {setup.CentreInplaneMm.ToString("0.#", ci)}) mm";
        if (setup.WedgeAngleDegrees is > 0 and var w)
            label += $", wedge {w.ToString("0.#", ci)} {setup.WedgeDirection}".TrimEnd();
        return label;
    }

    public static IReadOnlyList<string> Energies(IEnumerable<ScanDescriptor> scans) =>
        scans.OrderBy(s => s.IsElectron).ThenBy(s => s.EnergyValue).ThenBy(s => s.IsFff).Select(s => s.Energy).Distinct().ToArray();

    public static IReadOnlyList<string> FieldSizes(IEnumerable<ScanDescriptor> scans) =>
        scans.OrderBy(s => s.FieldCrossplaneMm * s.FieldInplaneMm).ThenBy(s => s.FieldCrossplaneMm).ThenBy(s => s.FieldSize, StringComparer.Ordinal)
             .Select(s => s.FieldSize).Distinct().ToArray();

    public static IReadOnlyList<ScanDescriptor> Filter(IEnumerable<ScanDescriptor> scans, string? energy, string? fieldSize) =>
        scans.Where(s => (energy is null || energy == AnyEnergy || s.Energy == energy)
                      && (fieldSize is null || fieldSize == AnyFieldSize || s.FieldSize == fieldSize)).ToArray();

    /// <summary>
    /// The doses whose plan beam is the scan's beam, by the library's rule (<see cref="ScanDoseMatching"/>: modality,
    /// energy, filter, field size and centre in tank axes, wedge angle and direction, SSD when the plan states one;
    /// MLC-shaped and arc beams never pair), with any note about the pairing. In dose path order.
    /// </summary>
    public static IReadOnlyList<DoseCandidate> MatchingDoses(ScanDescriptor scan, IEnumerable<RTDoseWithPlan> doses) =>
        Match(scan, doses).Candidates;

    /// <summary>The same, with the reason when nothing pairs: the closest dose's first difference, or why the scan can't be described.</summary>
    public static (IReadOnlyList<DoseCandidate> Candidates, string? Reason) Match(ScanDescriptor scan, IEnumerable<RTDoseWithPlan> doses)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var (candidates, reason) = ScanDoseMatching.Match(scan.Profile, doses);
        return (candidates
            .Select(c => new DoseCandidate(c.Dose, string.Join("; ", c.Notes)))
            .OrderBy(c => c.Dose.Dose.SourcePath, StringComparer.Ordinal)
            .ToArray(), reason);
    }
}
