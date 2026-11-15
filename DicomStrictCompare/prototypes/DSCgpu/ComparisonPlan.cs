using DSClibrary;

namespace DSCgpu;

/// <summary>
/// One pair's comparison, set up exactly as <c>X86Mathematics.Compare</c> sets it up: the overlap region,
/// sampled at the coarser spacing per axis, with the same step counts. Packed into a double and an int
/// array so every backend gets identical inputs.
/// </summary>
public sealed record ComparisonPlan(
    double XMin, double XMax, double YMin, double YMax, double ZMin, double ZMax,
    double XRes, double YRes, double ZRes, int Nx, int Ny, int Nz,
    double MaxSource, double Threshold, double Tolerance, double Distance, bool Global, bool UseDta)
{
    public long Points => (long)Nx * Ny * Nz;

    public static ComparisonPlan For(DoseMatrixOptimal s, DoseMatrixOptimal t, Dta dta, bool global)
    {
        double xMin = Math.Max(s.X0, t.X0), xMax = Math.Min(s.XMax, t.XMax), xRes = Math.Max(s.XRes, t.XRes);
        double yMin = Math.Max(s.Y0, t.Y0), yMax = Math.Min(s.YMax, t.YMax), yRes = Math.Max(s.YRes, t.YRes);
        double zMin = Math.Max(s.Z0, t.Z0), zMax = Math.Min(s.ZMax, t.ZMax), zRes = Math.Max(s.ZRes, t.ZRes);
        if (dta.TrimWidth > 0)
        {
            xMin += dta.TrimWidth * xRes; xMax -= dta.TrimWidth * xRes;
            yMin += dta.TrimWidth * yRes; yMax -= dta.TrimWidth * yRes;
            zMin += dta.TrimWidth * zRes; zMax -= dta.TrimWidth * zRes;
        }
        return new ComparisonPlan(xMin, xMax, yMin, yMax, zMin, zMax, xRes, yRes, zRes,
            StepCount(xMin, xMax, xRes), StepCount(yMin, yMax, yRes), StepCount(zMin, zMax, zRes),
            s.MaxPointDose.Dose, dta.Threshhold, dta.Tolerance, dta.Distance, global, dta.UseMM && dta.Distance > 0);
    }

    private static int StepCount(double min, double max, double res) =>
        max < min || res <= 0 ? 0 : (int)Math.Floor((max - min) / res + 1e-6) + 1;

    // Layout shared by every backend; the indices are the P* and Q* constants.
    public const int PS = 0, PT = 6, PRegion = 12, PStep = 18, PMax = 21, PThr = 22, PTol = 23, PDist = 24, PCount = 25;
    public const int QSDimX = 0, QSDimY = 1, QTDimX = 2, QTDimY = 3, QNx = 4, QNy = 5, QNz = 6, QGlobal = 7, QDta = 8, QCount = 9;

    public double[] Doubles(DoseMatrixOptimal s, DoseMatrixOptimal t) => new[]
    {
        s.X0, s.Y0, s.Z0, s.XRes, s.YRes, s.ZRes,
        t.X0, t.Y0, t.Z0, t.XRes, t.YRes, t.ZRes,
        XMin, XMax, YMin, YMax, ZMin, ZMax,
        XRes, YRes, ZRes,
        MaxSource, Threshold, Tolerance, Distance,
    };

    public int[] Ints(DoseMatrixOptimal s, DoseMatrixOptimal t) => new[]
    {
        s.DimensionX, s.DimensionY, t.DimensionX, t.DimensionY, Nx, Ny, Nz, Global ? 1 : 0, UseDta ? 1 : 0,
    };
}

/// <summary>Counts as the engine reports them.</summary>
public readonly record struct Counts(long Points, long Compared, long Failed)
{
    public override string ToString() => $"{Compared:N0} compared, {Failed:N0} failed";
}
