using System.Globalization;
using DSClibrary.Profiles;

namespace DSCprototypes.Shared.Graphs;

/// <summary>
/// How a water-tank scan's beam is labelled in the Doses vs scans tab: "6 MV FFF", "10 x 10 cm centred (0, 50) mm".
/// <para>
/// Measured 2026-11-09 across the 669 photon-area files in <c>Water tank scans\Raw Data</c> (1,153 scans, no parse
/// failures): SSD is 1000 mm in every scan, inplane is the Y gun-target axis and crossplane the X left-right axis,
/// depths cluster at dmax, 50, 100, 200 and 300 mm, 981 scans are in arbitrary units and the rest in GY without a
/// calibration flag, so curves are compared normalised, which is also crcrewso's rule (2026-11-08: "for the most part
/// profiles are treated as relative"). The 142 wedge scans carry a WEDGE header and a blank FILTER.
/// </para>
/// <para>
/// Until 2026-11-17 this class also read a scan folder and paired each scan with doses for the Graphs tab; the Doses vs
/// scans tab now runs the library's batch instead (<see cref="ScanRows"/>), so only the labels are left.
/// </para>
/// </summary>
public static class ScanCatalog
{
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
        ArgumentNullException.ThrowIfNull(setup);
        var ci = CultureInfo.InvariantCulture;
        string label = FieldSizeLabel(setup.CrossplaneMm, setup.InplaneMm);
        if (Math.Abs(setup.CentreCrossplaneMm) > 0.05 || Math.Abs(setup.CentreInplaneMm) > 0.05)
            label += $" centred ({setup.CentreCrossplaneMm.ToString("0.#", ci)}, {setup.CentreInplaneMm.ToString("0.#", ci)}) mm";
        if (setup.WedgeAngleDegrees is > 0 and var w)
            label += $", wedge {w.ToString("0.#", ci)} {setup.WedgeDirection}".TrimEnd();
        return label;
    }
}
