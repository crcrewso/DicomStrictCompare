using Microsoft.VisualStudio.TestTools.UnitTesting;
using DSClibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        [TestMethod()]
        public void GetPointDoseTest()
        {
            Assert.Fail();
        }

        /// <summary>
        /// The Vector3 overload, implemented because its null guard had the same defect as
        /// IsInBounds'. It must agree with the (double, double, double) overload, which is the one
        /// every caller in X86Mathematics actually uses.
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

        [TestMethod()]
        public void LatticeXYZToIndexTest()
        {
            Assert.Fail();
        }

        [TestMethod()]
        public void CompareDimensionsTest()
        {
            Assert.Fail();
        }
    }
}