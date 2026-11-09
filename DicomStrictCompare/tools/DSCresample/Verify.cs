using System.Diagnostics;
using System.Globalization;
using DSClibrary;
using DSClibrary.Resampling;
using DSClibrary.Results.DICOM;

namespace DSCresample;

/// <summary>
/// Checks resampled files against their sources two ways: directly, by re-interpolating the source
/// at every coarse node and comparing with the stored integer (must agree within the half-unit
/// rounding of the write), and through the comparison engine, which samples on the coarser grid
/// and so evaluates exactly the coarse nodes - a fine-versus-coarse comparison at a tight tolerance
/// must fail nowhere.
/// </summary>
internal static class Verify
{
    public static int Run(string sourceRoot, string coarseRoot, int sample, int seed)
    {
        var coarseFiles = Directory.EnumerateFiles(coarseRoot, "RD*.dcm", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var rng = new Random(seed);
        var picked = coarseFiles.OrderBy(_ => rng.Next()).Take(sample).OrderBy(f => f, StringComparer.Ordinal).ToArray();

        Console.WriteLine("file\tfine\tcoarse\tnodes\tmaxRawDiff\tengineCompared\tengineFailed\tfineMaxGy\tcoarseMaxGy\tseconds");
        int bad = 0;
        var engine = new X86Mathematics();
        // 0.1 % of the maximum, no DTA, every node above 1 % of the maximum evaluated.
        var tight = new Dta(useMM: false, threshhold: 0.01, tolerance: 0.001, distance: 0);

        foreach (string coarsePath in picked)
        {
            string rel = Path.GetRelativePath(coarseRoot, coarsePath);
            string finePath = Path.Combine(sourceRoot, rel);
            var timer = Stopwatch.StartNew();
            try
            {
                var fineDose = RTDoseResult.ReadFile(finePath);
                var coarseDose = RTDoseResult.ReadFile(coarsePath);
                var fine = RTDoseDerivation.RawGrid(fineDose);
                var coarse = RTDoseDerivation.RawGrid(coarseDose);

                if (fineDose.DoseGridScaling != coarseDose.DoseGridScaling)
                    throw new InvalidOperationException("DoseGridScaling differs");
                if (fineDose.ReferencedRTPlanSOPInstanceUID != coarseDose.ReferencedRTPlanSOPInstanceUID
                    || fineDose.ReferencedBeamNumber != coarseDose.ReferencedBeamNumber)
                    throw new InvalidOperationException("plan or beam reference differs");
                if (fineDose.SOPInstanceUID == coarseDose.SOPInstanceUID)
                    throw new InvalidOperationException("SOPInstanceUID was not changed");

                // Direct: every coarse node against a fresh interpolation of the source.
                var expected = GridResampler.Resample(fine, coarse.XRes, coarse.YRes, coarse.ZRes);
                if (expected.DoseValues.Length != coarse.DoseValues.Length)
                    throw new InvalidOperationException($"node count {coarse.DoseValues.Length} vs expected {expected.DoseValues.Length}");
                double maxDiff = 0;
                for (int i = 0; i < coarse.DoseValues.Length; i++)
                    maxDiff = Math.Max(maxDiff, Math.Abs(coarse.DoseValues[i] - expected.DoseValues[i]));

                // Through the engine, in gray, fine as the reference and coarse as the target.
                double scaling = fineDose.DoseGridScaling!.Value;
                var fineGy = Scaled(fine, scaling);
                var coarseGy = Scaled(coarse, scaling);
                var result = engine.CompareRelative(fineGy, coarseGy, tight);

                bool ok = maxDiff <= 0.5 && result.TotalFailed == 0 && result.TotalCompared > 0;
                if (!ok) bad++;
                Console.WriteLine(string.Join("\t",
                    rel,
                    $"{fine.DimensionX}x{fine.DimensionY}x{fine.DimensionZ}@{fine.XRes}",
                    $"{coarse.DimensionX}x{coarse.DimensionY}x{coarse.DimensionZ}@{coarse.XRes}",
                    coarse.DoseValues.Length,
                    maxDiff.ToString("0.###", CultureInfo.InvariantCulture),
                    result.TotalCompared, result.TotalFailed,
                    fineGy.MaxPointDose.Dose.ToString("0.0000", CultureInfo.InvariantCulture),
                    coarseGy.MaxPointDose.Dose.ToString("0.0000", CultureInfo.InvariantCulture),
                    timer.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture))
                    + (ok ? "" : "\tBAD"));
            }
            catch (Exception ex)
            {
                bad++;
                Console.WriteLine($"{rel}\tERROR\t{ex.GetType().Name}: {ex.Message}");
            }
        }
        Console.WriteLine($"{picked.Length} checked, {bad} bad");
        return bad == 0 ? 0 : 1;
    }

    private static DoseMatrixOptimal Scaled(DoseMatrixOptimal raw, double factor)
    {
        var values = new double[raw.DoseValues.Length];
        for (int i = 0; i < values.Length; i++) values[i] = raw.DoseValues[i] * factor;
        return new DoseMatrixOptimal(raw.DimensionX, raw.DimensionY, raw.DimensionZ,
            raw.X0, raw.Y0, raw.Z0, raw.XRes, raw.YRes, raw.ZRes, values);
    }
}
