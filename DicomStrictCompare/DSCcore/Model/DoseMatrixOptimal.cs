using System;
using System.Collections;
using System.Collections.Generic;
using EvilDICOM.Core.Helpers;
using EvilDICOM.RT;

namespace DCSCore.Model
{
    public class DoseMatrixOptimal
    {
        public int DimensionX { get; }
        public int DimensionY { get; }
        public int DimensionZ { get; }

        public readonly double[] DoseValues;
        public double Scaling { get; }
        public double X0 { get; }
        public double Y0 { get; }
        public double Z0 { get; }
        public double XMax { get; }
        public double YMax { get; }
        public double ZMax { get; }
        public double XRes { get; }
        public double YRes { get; }
        public double ZRes { get; }
        public DoseValue MaxPointDose { get; }
        public int Length { get; }
        public int Count { get; }

        public DoseMatrixOptimal(EvilDICOM.RT.RTDose doseMatrix)
        {
            if (doseMatrix == null)
                throw new ArgumentNullException(nameof(doseMatrix));
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

        public bool IsInBounds(Vector3 pt)
        {
            if (null == pt)
                throw new ArgumentNullException(nameof(pt));
            return pt.X >= X0 && pt.X <= XMax && pt.Y >= Y0 && pt.Y <= YMax && pt.Z >= X0 && pt.Z < ZMax;
        }

        public DoseValue GetPointDose(double x, double y, double z)
        {
            //From method at http://en.wikipedia.org/wiki/Trilinear_interpolation
            bool interpX = (x - X0) / XRes % 1 != 0.0; // Interpolate X?
            int xSteps = (int)((x - X0) / XRes);
            double lowX = X0 + xSteps * XRes;
            double highX = X0 + (xSteps + 1) * XRes;
            bool interpY = (y - Y0) / YRes % 1 != 0; // Interpolate Y?
            int ySteps = (int)((y - Y0) / YRes);
            double lowY = Y0 + ySteps * YRes;
            double highY = Y0 + (ySteps + 1) * YRes;
            bool interpZ = (z - Z0) / ZRes % 1 != 0; // Interpolate Z?
            int zSteps = (int)((z - Z0) / ZRes);
            double lowZ = Z0 + zSteps * ZRes;
            double highZ = Z0 + (zSteps + 1) * ZRes;

            double xd = interpX ? (x - lowX) / (highX - lowX) : 0;
            double yd = interpY ? (y - lowY) / (highY - lowY) : 0;
            double zd = interpZ ? (z - lowZ) / (highZ - lowZ) : 0;

            double c00 = interpX
                ? GetDiscretePointDose(xSteps, ySteps, zSteps) * (1 - xd) +
                  GetDiscretePointDose(xSteps + 1, ySteps, zSteps) * xd
                : GetDiscretePointDose(xSteps, ySteps, zSteps);
            double c10 = interpY
                ? interpX
                    ? GetDiscretePointDose(xSteps, ySteps + 1, zSteps) * (1 - xd) +
                      GetDiscretePointDose(xSteps + 1, ySteps + 1, zSteps) * xd
                    : GetDiscretePointDose(xSteps, ySteps + 1, zSteps)
                : 0;
            double c01 = interpZ
                ? interpX
                    ? GetDiscretePointDose(xSteps, ySteps, zSteps + 1) * (1 - xd) +
                      GetDiscretePointDose(xSteps + 1, ySteps, zSteps + 1) * xd
                    : GetDiscretePointDose(xSteps, ySteps, zSteps + 1)
                : 0;
            double c11 = interpY && interpZ
                ? interpX
                    ? GetDiscretePointDose(xSteps, ySteps + 1, zSteps + 1) * (1 - xd) +
                      GetDiscretePointDose(xSteps + 1, ySteps + 1, zSteps + 1) * xd
                    : GetDiscretePointDose(xSteps, ySteps + 1, zSteps + 1)
                : 0;

            double c0 = interpY ? c00 * (1 - yd) + c10 * yd : c00;
            double c1 = interpY ? c01 * (1 - yd) + c11 * yd : c01;

            double c = interpZ ? c0 * (1 - zd) + c1 * zd : c0;
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
            if (pt == null)
                throw new ArgumentNullException(nameof(pt));
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

        public bool CompareDimensions(in DoseMatrixOptimal y)
        {
            if (y == null && this == null)
                return true;
            if (y == null)
                return false;
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
