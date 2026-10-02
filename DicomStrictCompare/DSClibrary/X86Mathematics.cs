using System;
using System.Collections.Generic;
using System.Linq;


namespace DSClibrary
{

    public class X86Mathematics : IMathematics
    {
        public X86Mathematics()
        {

        }

        /// <summary>
        /// Local comparison: the dose tolerance is a fraction of the source dose at each point.
        /// </summary>
        public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta)
        {
            return Compare(source, target, dta, global: false);
        }

        /// <summary>
        /// Global comparison: the dose tolerance is a fraction of the source (reference) maximum dose.
        /// </summary>
        public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta)
        {
            return Compare(source, target, dta, global: true);
        }

        /// <summary>
        /// Compares source and target point by point over the region where both grids overlap, sampled on the
        /// coarser of the two grids.
        ///
        /// A point is evaluated only if the source (reference) dose is at or above dta.Threshhold of the source
        /// maximum. The target dose plays no part in that decision, so a target that falls far below the
        /// reference is counted as a failure rather than skipped.
        ///
        /// A point passes the dose test if |target - source| is within the allowed difference:
        /// global: dta.Tolerance x source maximum; local: dta.Tolerance x source dose at the point.
        ///
        /// If the dose test fails and dta.UseMM is true with dta.Distance > 0, the fast distance-to-agreement
        /// test is applied: the point passes if the target dose lies between the lowest and highest source dose
        /// found dta.Distance mm away along +-x, +-y and +-z. Neighbours outside the compared region are
        /// ignored. This is deliberately not a full spherical search (that belongs to a future gamma function).
        ///
        /// If dta.UseMM is false, the dose test alone decides pass or fail and dta.Distance is ignored. Distances
        /// in voxels are not supported yet.
        /// </summary>
        private static SingleComparison Compare(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta, bool global)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (dta == null) throw new ArgumentNullException(nameof(dta));

            double xMin = Math.Max(source.X0, target.X0);
            double xMax = Math.Min(source.XMax, target.XMax);
            double xRes = Math.Max(source.XRes, target.XRes);
            double yMin = Math.Max(source.Y0, target.Y0);
            double yMax = Math.Min(source.YMax, target.YMax);
            double yRes = Math.Max(source.YRes, target.YRes);
            double zMin = Math.Max(source.Z0, target.Z0);
            double zMax = Math.Min(source.ZMax, target.ZMax);
            double zRes = Math.Max(source.ZRes, target.ZRes);

            if (dta.TrimWidth > 0)
            {
                xMin += dta.TrimWidth * xRes;
                xMax -= dta.TrimWidth * xRes;
                yMin += dta.TrimWidth * yRes;
                yMax -= dta.TrimWidth * yRes;
                zMin += dta.TrimWidth * zRes;
                zMax -= dta.TrimWidth * zRes;
            }

            // Integer step counts, so floating point accumulation cannot drop or add a plane.
            int nx = StepCount(xMin, xMax, xRes);
            int ny = StepCount(yMin, yMax, yRes);
            int nz = StepCount(zMin, zMax, zRes);

            var region = new Region(xMin, xMax, yMin, yMax, zMin, zMax);

            int voxelsRead = 0;
            int compared = 0;
            int failed = 0;
            var neighbouringDoses = new List<double>(6);

            for (int i = 0; i < nx; i++)
            {
                double x = xMin + i * xRes;
                for (int j = 0; j < ny; j++)
                {
                    double y = yMin + j * yRes;
                    for (int k = 0; k < nz; k++)
                    {
                        double z = zMin + k * zRes;
                        voxelsRead++;
                        switch (EvaluatePoint(source, target, dta, global, region, x, y, z, neighbouringDoses))
                        {
                            case PointResult.Passed: compared++; break;
                            case PointResult.Failed: compared++; failed++; break;
                        }
                    }
                }
            }
            System.Diagnostics.Debug.WriteLine((global ? "Global" : "Local") + " failed: " + failed + " of " + compared);
            return new SingleComparison(dta, voxelsRead, compared, failed);
        }

        /// <summary>
        /// Compares source and target only at the given points (mm, DICOM patient coordinates), with exactly
        /// the same rules as the whole-grid comparison. Use it to compare like for like with another
        /// system's point set (for example the points a Sun Nuclear analysis evaluated), or along a line.
        ///
        /// Points outside the region where both grids overlap are counted in TotalCount but not evaluated.
        /// DTA neighbours are limited to the same overlap region. TrimWidth is not applied: the point list
        /// defines what is compared.
        /// </summary>
        public static SingleComparison CompareAtPoints(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta,
            IEnumerable<(double X, double Y, double Z)> points)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (dta == null) throw new ArgumentNullException(nameof(dta));
            if (points == null) throw new ArgumentNullException(nameof(points));

            var region = new Region(
                Math.Max(source.X0, target.X0), Math.Min(source.XMax, target.XMax),
                Math.Max(source.Y0, target.Y0), Math.Min(source.YMax, target.YMax),
                Math.Max(source.Z0, target.Z0), Math.Min(source.ZMax, target.ZMax));

            int count = 0, compared = 0, failed = 0;
            var neighbouringDoses = new List<double>(6);
            foreach (var (x, y, z) in points)
            {
                count++;
                if (!region.Contains(x, y, z)) { continue; }
                switch (EvaluatePoint(source, target, dta, dta.Global, region, x, y, z, neighbouringDoses))
                {
                    case PointResult.Passed: compared++; break;
                    case PointResult.Failed: compared++; failed++; break;
                }
            }
            return new SingleComparison(dta, count, compared, failed);
        }

        private enum PointResult { BelowThreshold, Passed, Failed }

        private readonly struct Region
        {
            public readonly double XMin, XMax, YMin, YMax, ZMin, ZMax;
            public Region(double xMin, double xMax, double yMin, double yMax, double zMin, double zMax)
            {
                XMin = xMin; XMax = xMax; YMin = yMin; YMax = yMax; ZMin = zMin; ZMax = zMax;
            }
            public bool Contains(double x, double y, double z) =>
                x >= XMin && x <= XMax && y >= YMin && y <= YMax && z >= ZMin && z <= ZMax;
        }

        /// <summary>
        /// The comparison rules for one point (see Compare). neighbouringDoses is scratch space, reused to avoid
        /// allocating per point.
        /// </summary>
        private static PointResult EvaluatePoint(DoseMatrixOptimal source, DoseMatrixOptimal target, Dta dta, bool global,
            in Region region, double x, double y, double z, List<double> neighbouringDoses)
        {
            double maxSource = source.MaxPointDose.Dose;
            double sourcei = source.GetPointDose(x, y, z).Dose;
            if (sourcei < maxSource * dta.Threshhold) { return PointResult.BelowThreshold; }

            double targeti = target.GetPointDose(x, y, z).Dose;
            double allowed = global ? maxSource * dta.Tolerance : dta.Tolerance * sourcei;
            if (Math.Abs(targeti - sourcei) <= allowed) { return PointResult.Passed; }

            if (!(dta.UseMM && dta.Distance > 0)) { return PointResult.Failed; }

            neighbouringDoses.Clear();
            double d = dta.Distance;
            if (x - d >= region.XMin) { neighbouringDoses.Add(source.GetPointDose(x - d, y, z).Dose); }
            if (x + d <= region.XMax) { neighbouringDoses.Add(source.GetPointDose(x + d, y, z).Dose); }
            if (y - d >= region.YMin) { neighbouringDoses.Add(source.GetPointDose(x, y - d, z).Dose); }
            if (y + d <= region.YMax) { neighbouringDoses.Add(source.GetPointDose(x, y + d, z).Dose); }
            if (z - d >= region.ZMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - d).Dose); }
            if (z + d <= region.ZMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + d).Dose); }

            if (neighbouringDoses.Count == 0 || targeti < neighbouringDoses.Min() || targeti > neighbouringDoses.Max())
            {
                return PointResult.Failed;
            }
            return PointResult.Passed;
        }

        /// <summary>
        /// Number of sample positions from min to max inclusive, at the given spacing.
        /// </summary>
        private static int StepCount(double min, double max, double res)
        {
            if (max < min || res <= 0) { return 0; }
            return (int)Math.Floor((max - min) / res + 1e-6) + 1;
        }
    }

}
