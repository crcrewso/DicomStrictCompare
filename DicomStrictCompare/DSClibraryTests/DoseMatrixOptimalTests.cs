using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSClibrary.Comparing;

namespace DSClibraryTests
{
    [TestClass()]
    public class DoseMatrixOptimalTests
    {


        [TestMethod()]
        public void DoseMatrixOptimalCreateFromRef()
        {
            var doseFile = Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1;
            var doseMatrix = new EvilDICOM.RT.RTDose(EvilDICOM.Core.DICOMObject.Read(doseFile));
            var source = new DoseMatrixOptimal(doseMatrix);
            Assert.AreEqual(doseMatrix.MaxPointDose.Dose, source.MaxPointDose.Dose);
            Assert.AreEqual(doseMatrix.DoseValues.ToArray().Max(), source.MaxPointDose.Dose);
        }

        private static DoseMatrixOptimal Reference() =>
            new(new EvilDICOM.RT.RTDose(
                EvilDICOM.Core.DICOMObject.Read(Properties.Resources.RD_UnitTest_P1Ref_X_100A_10_0_1)));

        private static EvilDICOM.Core.Helpers.Vector3 At(double x, double y, double z) => new(x, y, z);

        /// <summary>
        /// Implemented because the guard inside IsInBounds was fixed: it read `null == pt`, which
        /// dispatches to Vector3's == overload and dereferences the null operand, so every call threw
        /// NullReferenceException. This stub being empty is why that was never noticed.
        /// </summary>
        [TestMethod()]
        public void IsInBoundsTest()
        {
            var dose = Reference();

            Assert.IsTrue(dose.IsInBounds(At(0, 0, 0)), "the isocentre is inside this grid");

            // Both corners count as inside: the bounds are inclusive at each end. The upper z bound
            // being inclusive is swift/comparison-fixes' correction - it previously used a strict <
            // where x and y used <=, and compared z against X0 rather than Z0.
            Assert.IsTrue(dose.IsInBounds(At(dose.X0, dose.Y0, dose.Z0)));
            Assert.IsTrue(dose.IsInBounds(At(dose.XMax, dose.YMax, dose.ZMax)));

            Assert.IsFalse(dose.IsInBounds(At(dose.X0 - 1, 0, 0)));
            Assert.IsFalse(dose.IsInBounds(At(0, dose.YMax + 1, 0)));
            Assert.IsFalse(dose.IsInBounds(At(0, 0, dose.ZMax + 1)), "z upper bound");
            Assert.IsFalse(dose.IsInBounds(At(0, 0, dose.Z0 - 1)), "z lower bound, against Z0 not X0");

            Assert.ThrowsExactly<ArgumentNullException>(() => dose.IsInBounds(null!));
        }

        /// <summary>
        /// The (x, y, z) overload: a voxel's own value on the lattice, the mean of two neighbours halfway between them,
        /// and trilinear interpolation of the eight surrounding voxels anywhere else.
        /// </summary>
        [TestMethod()]
        public void GetPointDoseTest()
        {
            var dose = Reference();
            double tol = 1e-12 * dose.MaxPointDose.Dose;
            double V(int i, int j, int k) => dose.DoseValues[DoseMatrixOptimal.LatticeXYZToIndex(i, j, k, dose.DimensionX, dose.DimensionY)];
            double X(double i) => dose.X0 + i * dose.XRes;
            double Y(double j) => dose.Y0 + j * dose.YRes;
            double Z(double k) => dose.Z0 + k * dose.ZRes;
            var (i0, j0, k0) = (dose.DimensionX / 2, dose.DimensionY / 2, dose.DimensionZ / 2);

            // On the lattice: the voxel itself, including the maximum and the first and last voxels.
            Assert.AreEqual(V(i0, j0, k0), dose.GetPointDose(X(i0), Y(j0), Z(k0)).Dose, tol);
            Assert.AreEqual(V(0, 0, 0), dose.GetPointDose(dose.X0, dose.Y0, dose.Z0).Dose, tol);
            var max = dose.MaxPointDose;
            Assert.AreEqual(max.Dose, dose.GetPointDose(max.X, max.Y, max.Z).Dose, tol);

            // Halfway along one axis: the mean of the two voxels.
            Assert.AreEqual((V(i0, j0, k0) + V(i0 + 1, j0, k0)) / 2, dose.GetPointDose(X(i0 + 0.5), Y(j0), Z(k0)).Dose, tol);
            Assert.AreEqual((V(i0, j0, k0) + V(i0, j0, k0 + 1)) / 2, dose.GetPointDose(X(i0), Y(j0), Z(k0 + 0.5)).Dose, tol);

            // Anywhere inside a cell: trilinear, written out independently.
            var (fx, fy, fz) = (0.25, 0.5, 0.75);
            double expected = 0;
            for (int a = 0; a <= 1; a++)
                for (int b = 0; b <= 1; b++)
                    for (int c = 0; c <= 1; c++)
                        expected += V(i0 + a, j0 + b, k0 + c) * (a == 1 ? fx : 1 - fx) * (b == 1 ? fy : 1 - fy) * (c == 1 ? fz : 1 - fz);
            Assert.AreEqual(expected, dose.GetPointDose(X(i0 + fx), Y(j0 + fy), Z(k0 + fz)).Dose, tol);

            // The value comes back with the position asked for.
            var p = dose.GetPointDose(X(i0 + fx), Y(j0), Z(k0));
            Assert.AreEqual(X(i0 + fx), p.X);
        }

        /// <summary>
        /// The Vector3 overload, implemented because its null guard had the same defect as
        /// IsInBounds'. It must agree with the (double, double, double) overload, which is the one
        /// every caller in HandWrittenComparer actually uses.
        /// </summary>
        [TestMethod()]
        public void GetPointDoseTest1()
        {
            var dose = Reference();

            Assert.AreEqual(dose.GetPointDose(0, 0, 0).Dose, dose.GetPointDose(At(0, 0, 0)).Dose, 1e-12);
            Assert.AreEqual(
                dose.GetPointDose(dose.X0, dose.Y0, dose.Z0).Dose,
                dose.GetPointDose(At(dose.X0, dose.Y0, dose.Z0)).Dose, 1e-12);

            Assert.ThrowsExactly<ArgumentNullException>(() => dose.GetPointDose(null!));
        }

        /// <summary>x varies fastest, then y, then z: the order DICOM stores the voxels in.</summary>
        [TestMethod()]
        public void LatticeXYZToIndexTest()
        {
            Assert.AreEqual(0, DoseMatrixOptimal.LatticeXYZToIndex(0, 0, 0, 5, 6));
            Assert.AreEqual(1, DoseMatrixOptimal.LatticeXYZToIndex(1, 0, 0, 5, 6));
            Assert.AreEqual(5, DoseMatrixOptimal.LatticeXYZToIndex(0, 1, 0, 5, 6));
            Assert.AreEqual(30, DoseMatrixOptimal.LatticeXYZToIndex(0, 0, 1, 5, 6));
            Assert.AreEqual(2 + 3 * 5 + 4 * 30, DoseMatrixOptimal.LatticeXYZToIndex(2, 3, 4, 5, 6));

            // On a real grid, the last voxel is the last value, and the index reaches the value GetPointDose reads there.
            var dose = Reference();
            var (nx, ny, nz) = (dose.DimensionX, dose.DimensionY, dose.DimensionZ);
            Assert.AreEqual(dose.DoseValues.Length - 1, DoseMatrixOptimal.LatticeXYZToIndex(nx - 1, ny - 1, nz - 1, nx, ny));
            Assert.AreEqual(dose.DoseValues[DoseMatrixOptimal.LatticeXYZToIndex(nx - 1, ny - 1, nz - 1, nx, ny)],
                dose.GetPointDose(dose.XMax, dose.YMax, dose.ZMax).Dose, 1e-12 * dose.MaxPointDose.Dose);
        }

        /// <summary>True only for the same lattice: size, origin, end, spacing and scaling. Dose values aren't compared.</summary>
        [TestMethod()]
        public void CompareDimensionsTest()
        {
            var dose = Reference();
            Assert.IsTrue(dose.CompareDimensions(dose));
            Assert.IsTrue(dose.CompareDimensions(Reference()), "a second read of the same file");
            Assert.IsFalse(dose.CompareDimensions(null!));

            string Res(string name) => System.IO.Path.Combine(TestData.ResourcesDirectory!, name);
            // The 2 mm calculation of the same plan: a different lattice.
            var coarse = DSClibrary.Results.DICOM.DoseGridReader.Read(Res("RD.UnitTest.P1 2 mm.X-100A-10.0-0.2.dcm"));
            Assert.IsFalse(dose.CompareDimensions(coarse));
            Assert.IsFalse(coarse.CompareDimensions(dose));
            // The same plan at 95 and 101 MU: one lattice, but the files differ only in DoseGridScaling, which counts.
            var mu95 = DSClibrary.Results.DICOM.DoseGridReader.Read(Res("RD.UnitTest.P5 Varied MU.X-095A-10.0-1.dcm"));
            var mu101 = DSClibrary.Results.DICOM.DoseGridReader.Read(Res("RD.UnitTest.P5 Varied MU.X-101A-10.0-1.dcm"));
            Assert.AreEqual(mu95.DoseValues.Length, mu101.DoseValues.Length);
            Assert.AreNotEqual(mu95.Scaling, mu101.Scaling);
            Assert.IsFalse(mu95.CompareDimensions(mu101));

            // Built grids (scaling 1): same lattice with different values is the same; one spacing apart is not.
            var a = new DoseMatrixOptimal(2, 2, 2, 0, 0, 0, 1, 1, 1, new double[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            var b = new DoseMatrixOptimal(2, 2, 2, 0, 0, 0, 1, 1, 1, new double[8]);
            var c = new DoseMatrixOptimal(2, 2, 2, 0, 0, 0, 1, 1, 2, new double[8]);
            Assert.IsTrue(a.CompareDimensions(b));
            Assert.IsFalse(a.CompareDimensions(c));
        }
    }
}