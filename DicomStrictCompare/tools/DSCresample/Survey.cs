using System.Globalization;
using DSClibrary.Results.DICOM;

namespace DSCresample;

/// <summary>
/// Reports what a set of RT Dose files actually are - transfer syntax, grid, voxel encoding - so the
/// resampler is written against measured files and not assumed ones.
/// </summary>
internal static class Survey
{
    public static int Run(IReadOnlyList<string> files)
    {
        Console.WriteLine("file\tMB\ttransfer\timplicit\tcols\trows\tframes\tdx\tdy\tdz\tuniformZ\tbits\tsigned\tunits\tsummation\torientation\tscaling\tmaxRaw\tundefinedLengthTags");
        int failures = 0;
        foreach (string path in files)
        {
            try
            {
                var file = DicomFileReader.ReadFile(path);
                var dose = RTDoseResult.From(file);
                var ds = file.Dataset;
                var spacing = dose.PixelSpacing;
                var offsets = dose.GridFrameOffsetVector;
                double dz = offsets.Count > 1 ? offsets[1] - offsets[0] : double.NaN;
                string undefined = string.Join(" ", ds.Tags.Where(t => ds.Element(t)!.Items is not null).Select(t => t.ToString()));

                Console.WriteLine(string.Join("\t",
                    Path.GetFileName(path),
                    (new FileInfo(path).Length / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture),
                    file.TransferSyntaxUID,
                    file.TransferSyntaxUID == DicomTags.ImplicitVRLittleEndian,
                    dose.Columns, dose.Rows, dose.NumberOfFrames,
                    spacing.Count > 1 ? spacing[1].ToString(CultureInfo.InvariantCulture) : "?",
                    spacing.Count > 0 ? spacing[0].ToString(CultureInfo.InvariantCulture) : "?",
                    dz.ToString(CultureInfo.InvariantCulture),
                    dose.HasUniformFrameSpacing,
                    dose.BitsAllocated, dose.PixelRepresentation,
                    dose.DoseUnits, dose.DoseSummationType,
                    string.Join("/", dose.ImageOrientationPatient.Select(v => v.ToString(CultureInfo.InvariantCulture))),
                    dose.DoseGridScaling?.ToString("R", CultureInfo.InvariantCulture),
                    dose.MaximumRawVoxel,
                    undefined));
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"{Path.GetFileName(path)}\tERROR\t{ex.GetType().Name}: {ex.Message}");
            }
        }
        return failures == 0 ? 0 : 1;
    }
}
