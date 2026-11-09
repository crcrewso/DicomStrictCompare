using System;

namespace DSClibrary.Resampling
{
    /// <summary>
    /// Resamples a dose grid onto a coarser (or finer) regular grid by trilinear interpolation.
    /// <para>
    /// Cody, 2026-11-08: "I need some fast dicom sets to run, can you rescale every DICOM dose file
    /// ... to 5mm voxels?" - and, on the code, "the resampling code will be useful later to this
    /// project". So this is a library feature and not a script.
    /// </para>
    /// <para>
    /// <b>What it does.</b> The new grid keeps the source origin (the centre of the first voxel, which
    /// is DICOM's <c>ImagePositionPatient</c>) and steps from it at the new spacing for as long as it
    /// stays inside the source extent. Each new voxel takes the trilinearly interpolated value of the
    /// source at the new voxel's centre: the same interpolation <see cref="DoseMatrixOptimal.GetPointDose"/>
    /// uses in the comparison engine, so a coarse grid compared at its own nodes gives the numbers the
    /// fine grid would give at those points. Where a new node lands exactly on a source node the value
    /// is copied unchanged, which is the case for 1 mm and 2.5 mm sources going to 5 mm; a 2 mm source
    /// is genuinely interpolated at every other node.
    /// </para>
    /// <para>
    /// <b>What it does not do.</b> It does not average the fine voxels inside each coarse voxel. A
    /// point sample preserves point doses and gradients as the engine sees them; a box average would
    /// change every value by the local curvature and lower every maximum. If a volume-averaged
    /// variant is wanted later (a detector of finite size behaves more like that), it belongs beside
    /// this as a second method with its own name, not as an option that changes this one's meaning.
    /// </para>
    /// </summary>
    public static class GridResampler
    {
        /// <summary>Resamples to the same spacing along all three axes.</summary>
        public static DoseMatrixOptimal Resample(DoseMatrixOptimal source, double spacingMm) =>
            Resample(source, spacingMm, spacingMm, spacingMm);

        /// <summary>
        /// Resamples <paramref name="source"/> onto a grid with the given spacings, starting at the
        /// source origin and covering as much of the source extent as whole steps allow.
        /// </summary>
        public static DoseMatrixOptimal Resample(DoseMatrixOptimal source, double xRes, double yRes, double zRes)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (!(xRes > 0) || !(yRes > 0) || !(zRes > 0))
                throw new ArgumentOutOfRangeException(nameof(xRes), "Spacings must be positive.");

            int nx = StepCount(source.X0, source.XMax, xRes);
            int ny = StepCount(source.Y0, source.YMax, yRes);
            int nz = StepCount(source.Z0, source.ZMax, zRes);

            var values = new double[nx * ny * nz];
            int index = 0;
            for (int k = 0; k < nz; k++)
            {
                double z = source.Z0 + k * zRes;
                for (int j = 0; j < ny; j++)
                {
                    double y = source.Y0 + j * yRes;
                    for (int i = 0; i < nx; i++)
                    {
                        double x = source.X0 + i * xRes;
                        values[index++] = Trilinear(source, x, y, z);
                    }
                }
            }

            return new DoseMatrixOptimal(nx, ny, nz, source.X0, source.Y0, source.Z0, xRes, yRes, zRes, values);
        }

        /// <summary>
        /// Number of sample positions from min to max inclusive at the given spacing. The same rule
        /// as the comparison engine's step count, so the two agree about where a grid ends.
        /// </summary>
        public static int StepCount(double min, double max, double res)
        {
            if (max < min || res <= 0) return 0;
            return (int)Math.Floor((max - min) / res + 1e-6) + 1;
        }

        /// <summary>
        /// Trilinear interpolation with the indices clamped to the grid, so a point a rounding error
        /// past the last voxel reads that voxel instead of one beyond the array. A fractional position
        /// within a millionth of a node is snapped to the node, so coincident grids copy exactly.
        /// </summary>
        internal static double Trilinear(DoseMatrixOptimal g, double x, double y, double z)
        {
            Locate((x - g.X0) / g.XRes, g.DimensionX, out int i0, out int i1, out double tx);
            Locate((y - g.Y0) / g.YRes, g.DimensionY, out int j0, out int j1, out double ty);
            Locate((z - g.Z0) / g.ZRes, g.DimensionZ, out int k0, out int k1, out double tz);

            double[] v = g.DoseValues;
            int dx = g.DimensionX;
            int dxy = g.DimensionX * g.DimensionY;

            double c00 = Lerp(v[i0 + j0 * dx + k0 * dxy], v[i1 + j0 * dx + k0 * dxy], tx);
            double c10 = Lerp(v[i0 + j1 * dx + k0 * dxy], v[i1 + j1 * dx + k0 * dxy], tx);
            double c01 = Lerp(v[i0 + j0 * dx + k1 * dxy], v[i1 + j0 * dx + k1 * dxy], tx);
            double c11 = Lerp(v[i0 + j1 * dx + k1 * dxy], v[i1 + j1 * dx + k1 * dxy], tx);

            double c0 = Lerp(c00, c10, ty);
            double c1 = Lerp(c01, c11, ty);
            return Lerp(c0, c1, tz);
        }

        private static void Locate(double f, int dim, out int lower, out int upper, out double t)
        {
            const double snap = 1e-6;
            int i = (int)Math.Floor(f);
            t = f - i;
            if (t < snap) t = 0;
            else if (t > 1 - snap) { i++; t = 0; }

            if (i < 0) { i = 0; t = 0; }
            if (i > dim - 1) { i = dim - 1; t = 0; }
            lower = i;
            upper = Math.Min(i + 1, dim - 1);
        }

        private static double Lerp(double a, double b, double t) => t == 0 ? a : a + (b - a) * t;
    }
}
