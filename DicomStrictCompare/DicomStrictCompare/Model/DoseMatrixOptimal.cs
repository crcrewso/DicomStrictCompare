using System;
using System.Collections;
using System.Collections.Generic;
using EvilDICOM.Core.Helpers;
using EvilDICOM.RT;

namespace DicomStrictCompare.Model
{
    public class DoseMatrixOptimal
    {
        public readonly int DimensionX, DimensionY, DimensionZ;
        public readonly double[] DoseValues;
        public readonly double Scaling;
        public readonly double X0, Y0, Z0, XMax, YMax, ZMax, XRes, YRes, ZRes;
        public DoseValue MaxPointDose;
        public int Length;
        public int Count;

        public DoseMatrixOptimal(DoseMatrix doseMatrix)
        {
            DimensionX = doseMatrix.DimensionX;
            DimensionY = doseMatrix.DimensionY;
            DimensionZ = doseMatrix.DimensionZ;
            DoseValues = doseMatrix.DoseValues.ToArray();
            Scaling = doseMatrix.Scaling;
            X0 = doseMatrix.X0;
            Y0 = doseMatrix.Y0;
            Z0 = doseMatrix.Z0;
            XMax = doseMatrix.XMax;
            YMax = doseMatrix.YMax;
            ZMax = doseMatrix.ZMax;
            XRes = doseMatrix.XRes;
            YRes = doseMatrix.YRes;
            ZRes = doseMatrix.ZRes;
            Length = DoseValues.Length;
            Count = Length;
            MaxPointDose = doseMatrix.MaxPointDose;
        }

        public bool IsInBounds(Vector3 pt) => pt.X >= X0 && pt.X <= XMax && pt.Y >= Y0 && pt.Y <= YMax && pt.Z >= X0 && pt.Z < ZMax;

        public DoseValue GetPointDose(double x, double y, double z)
        {
            //From method at http://en.wikipedia.org/wiki/Trilinear_interpolation
            var interpX = (x - X0) / XRes % 1 != 0.0; // Interpolate X?
            var xSteps = (int)((x - X0) / XRes);
            var lowX = X0 + xSteps * XRes;
            var highX = X0 + (xSteps + 1) * XRes;
            var interpY = (y - Y0) / YRes % 1 != 0; // Interpolate Y?
            var ySteps = (int)((y - Y0) / YRes);
            var lowY = Y0 + ySteps * YRes;
            var highY = Y0 + (ySteps + 1) * YRes;
            var interpZ = (z - Z0) / ZRes % 1 != 0; // Interpolate Z?
            var zSteps = (int)((z - Z0) / ZRes);
            var lowZ = Z0 + zSteps * ZRes;
            var highZ = Z0 + (zSteps + 1) * ZRes;

            var xd = interpX ? (x - lowX) / (highX - lowX) : 0;
            var yd = interpY ? (y - lowY) / (highY - lowY) : 0;
            var zd = interpZ ? (z - lowZ) / (highZ - lowZ) : 0;

            var c00 = interpX
                ? GetDiscretePointDose(xSteps, ySteps, zSteps) * (1 - xd) +
                  GetDiscretePointDose(xSteps + 1, ySteps, zSteps) * xd
                : GetDiscretePointDose(xSteps, ySteps, zSteps);
            var c10 = interpY
                ? interpX
                    ? GetDiscretePointDose(xSteps, ySteps + 1, zSteps) * (1 - xd) +
                      GetDiscretePointDose(xSteps + 1, ySteps + 1, zSteps) * xd
                    : GetDiscretePointDose(xSteps, ySteps + 1, zSteps)
                : 0;
            var c01 = interpZ
                ? interpX
                    ? GetDiscretePointDose(xSteps, ySteps, zSteps + 1) * (1 - xd) +
                      GetDiscretePointDose(xSteps + 1, ySteps, zSteps + 1) * xd
                    : GetDiscretePointDose(xSteps, ySteps, zSteps + 1)
                : 0;
            var c11 = interpY && interpZ
                ? interpX
                    ? GetDiscretePointDose(xSteps, ySteps + 1, zSteps + 1) * (1 - xd) +
                      GetDiscretePointDose(xSteps + 1, ySteps + 1, zSteps + 1) * xd
                    : GetDiscretePointDose(xSteps, ySteps + 1, zSteps + 1)
                : 0;

            var c0 = interpY ? c00 * (1 - yd) + c10 * yd : c00;
            var c1 = interpY ? c01 * (1 - yd) + c11 * yd : c01;

            var c = interpZ ? c0 * (1 - zd) + c1 * zd : c0;
            return new DoseValue(x, y, z, c);

            double GetDiscretePointDose(int xStepsGDPD, int yStepsGDPD, int zStepsGDPD)
            {
                return DoseValues[LatticeXYZToIndexLocal(xStepsGDPD, yStepsGDPD, zStepsGDPD, DimensionX, DimensionY)];
            }

            int LatticeXYZToIndexLocal(int xLTI, int yLTI, int zLTI, int dimXLTI, int dimYLTI)
            {
                return xLTI + yLTI * dimXLTI + zLTI * dimXLTI * dimYLTI;
            }

        }

        public DoseValue GetPointDose(Vector3 pt)
        {
            return GetPointDose(pt.X, pt.Y, pt.Z);
        }



        /// <summary>
        /// Converts X,Y,Z coordinates in a 3D pixel lattice to a 1D index
        /// </summary>
        /// <param name="x">the x coordinate of the pixel</param>
        /// <param name="y">the y coordinate of the pixel</param>
        /// <param name="z">the z coordinate of the pixel</param>
        /// <param name="dimX">the width of the lattice in the X direction</param>
        /// <param name="dimY">the height of the lattice in the Y direction</param>
        /// <returns>the index of the pixel in the 3D lattice</returns>
        public static int LatticeXYZToIndex(int x, int y, int z, int dimX, int dimY)
        {
            return x + y * dimX + z * dimX * dimY;
        }

        public bool CompareDimensions(DoseMatrixOptimal y)
        {
            if (DoseValues.Length != y.DoseValues.Length)
                return false;
            if (Scaling != y.Scaling)
                return false;
            if (DimensionX != y.DimensionX)
                return false;
            if (DimensionY != y.DimensionY)
                return false;
            if (DimensionZ != y.DimensionZ)
                return false;
            if (X0 != y.X0 || Y0 != y.Y0 || Z0 != y.Z0)
                return false;
            if (XMax != y.XMax || YMax != y.YMax || ZMax != y.ZMax)
                return false;
            if (XRes != y.XRes || YRes != y.YRes || ZRes != y.ZRes)
                return false;
            return true;
        }
    }
}
