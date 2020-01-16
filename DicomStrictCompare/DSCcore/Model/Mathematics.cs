﻿using System.Linq;

namespace DicomStrictCompare
{
    public abstract class IMathematics
    {
        public static double fuzzyScale = 0.5;
        /// <summary>
        /// Compares the two provided dose arrays voxel to voxel. Dose difference is calculated as % of source dose
        /// </summary>
        /// <param name="source">Reference dose array</param>
        /// <param name="target">Dose Array being verified</param>
        /// <param name="tolerance">Less than this percent difference is a pass</param>
        /// <param name="epsilon">Threshold percent of max dose below which comparison will not be evaluated</param>
        /// <returns></returns>
        /// 
            
        static bool trimmed = true;
        static int trimWidth = 15;
        public abstract System.Tuple<int, int> CompareAbsolute(double[] source, double[] target, double tolerance, double epsilon, bool fuzzy = false);
        public abstract System.Tuple<int, int> CompareRelative(double[] source, double[] target, double tolerance, double epsilon, bool fuzzy = false);

        public virtual System.Tuple<int, int> CompareAbsolute(EvilDICOM.RT.DoseMatrix source, EvilDICOM.RT.DoseMatrix target, double tolerance, double epsilon, bool fuzzy = false)
        {


            //Fuzzy scale should always be +/- 1/4 of resolution
            double xMin = (source.X0 > target.X0) ? source.X0 : target.X0;
            double xMax = (source.XMax < target.XMax) ? source.XMax : target.XMax;
            double xRes = (source.XRes > target.XRes) ? source.XRes : target.XRes;
            double yMin = (source.Y0 > target.Y0) ? source.Y0 : target.Y0;
            double yMax = (source.YMax < target.YMax) ? source.YMax : target.YMax;
            double yRes = (source.YRes > target.YRes) ? source.YRes : target.YRes;
            double zMin = (source.Z0 > target.Z0) ? source.Z0 : target.Z0;
            double zMax = (source.ZMax < target.ZMax) ? source.ZMax : target.ZMax;
            double zRes = (source.ZRes > target.ZRes) ? source.ZRes : target.ZRes;

            if (trimmed)
            {
                xMin += trimWidth * xRes;
                xMax -= trimWidth * xRes;
                yMin += trimWidth * yRes;
                yMax -= trimWidth * yRes;
                zMin += trimWidth * zRes;
                zMax -= trimWidth * zRes;
            }


            int TotalCompared = 0;
            int failed = 0;
            int ComparedToPoint = 0;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * epsilon;
            for (double x = xMin; x <= xMax; x += xRes)
            {
                for (double y = yMin; y <= yMax; y += yRes)
                {
                    for (double z = zMin; z <= zMax; z += zRes)
                    {
                        ComparedToPoint++;
                        double sourcei = source.GetPointDose(x, y, z).Dose;
                        double targeti = target.GetPointDose(x, y, z).Dose;
                        if (targeti < MinDoseEvaluated || sourcei < MinDoseEvaluated) { continue; }
                        else
                        {
                            TotalCompared++;
                            double sourceLow = (1.0 - tolerance) * sourcei;
                            double sourceHigh = (1.0 + tolerance) * sourcei;
                            if (targeti < sourceLow || targeti > sourceHigh)
                            {
                                if (!fuzzy)
                                {
                                    failed++;
                                }
                                else
                                {
                                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - (xRes * fuzzyScale), y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + (xRes * fuzzyScale), y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - (yRes * fuzzyScale), z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + (yRes * fuzzyScale), z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - (zRes * fuzzyScale)).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + (yRes * fuzzyScale)).Dose); }
                                    double low = neighbouringDoses.Min();
                                    double high = neighbouringDoses.Max();
                                    if (targeti < low || targeti > high)
                                    {
                                        failed++;
                                    }

                                }
                            }
                        }

                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            System.Tuple<int, int> ret = new System.Tuple<int, int>(failed, TotalCompared);
            return ret;
        }
        public virtual System.Tuple<int, int> CompareAbsolute(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, double tolerance, double epsilon, bool fuzzy = false)
        {

            double xMin = (source.X0 > target.X0) ? source.X0 : target.X0;
            double xMax = (source.XMax < target.XMax) ? source.XMax : target.XMax;
            double xRes = (source.XRes > target.XRes) ? source.XRes : target.XRes;
            double yMin = (source.Y0 > target.Y0) ? source.Y0 : target.Y0;
            double yMax = (source.YMax < target.YMax) ? source.YMax : target.YMax;
            double yRes = (source.YRes > target.YRes) ? source.YRes : target.YRes;
            double zMin = (source.Z0 > target.Z0) ? source.Z0 : target.Z0;
            double zMax = (source.ZMax < target.ZMax) ? source.ZMax : target.ZMax;
            double zRes = (source.ZRes > target.ZRes) ? source.ZRes : target.ZRes;

            if (trimmed)
            {
                xMin += trimWidth * xRes;
                xMax -= trimWidth * xRes;
                yMin += trimWidth * yRes;
                yMax -= trimWidth * yRes;
                zMin += trimWidth * zRes;
                zMax -= trimWidth * zRes;
            }


            int TotalCompared = 0;
            int failed = 0;
            int ComparedToPoint = 0;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * epsilon;
            for (double x = xMin; x <= xMax; x += xRes)
            {
                for (double y = yMin; y <= yMax; y += yRes)
                {
                    for (double z = zMin; z <= zMax; z += zRes)
                    {
                        ComparedToPoint++;
                        double sourcei = source.GetPointDose(x, y, z).Dose;
                        double targeti = target.GetPointDose(x, y, z).Dose;
                        if (targeti < MinDoseEvaluated || sourcei < MinDoseEvaluated) { continue; }
                        else
                        {
                            TotalCompared++;
                            double sourceLow = (1.0 - tolerance) * sourcei;
                            double sourceHigh = (1.0 + tolerance) * sourcei;
                            if (targeti < sourceLow || targeti > sourceHigh)
                                if (!fuzzy)
                                {
                                    failed++;
                                }
                                else
                                {
                                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - (xRes * fuzzyScale), y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + (xRes * fuzzyScale), y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - (yRes * fuzzyScale), z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + (yRes * fuzzyScale), z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - (zRes * fuzzyScale)).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + (yRes * fuzzyScale)).Dose); }
                                    double low = neighbouringDoses.Min();
                                    double high = neighbouringDoses.Max();
                                    if (targeti < low || targeti > high)
                                    {
                                        failed++;
                                    }

                                }
                        }

                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            System.Tuple<int, int> ret = new System.Tuple<int, int>(failed, TotalCompared);
            return ret;
        }
        public virtual System.Tuple<int, int> CompareRelative(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, double tolerance, double epsilon, bool fuzzy = false)
        {
            double xMin = (source.X0 > target.X0) ? source.X0 : target.X0;
            double xMax = (source.XMax < target.XMax) ? source.XMax : target.XMax;
            double xRes = (source.XRes > target.XRes) ? source.XRes : target.XRes;
            double yMin = (source.Y0 > target.Y0) ? source.Y0 : target.Y0;
            double yMax = (source.YMax < target.YMax) ? source.YMax : target.YMax;
            double yRes = (source.YRes > target.YRes) ? source.YRes : target.YRes;
            double zMin = (source.Z0 > target.Z0) ? source.Z0 : target.Z0;
            double zMax = (source.ZMax < target.ZMax) ? source.ZMax : target.ZMax;
            double zRes = (source.ZRes > target.ZRes) ? source.ZRes : target.ZRes;

            if (trimmed)
            {
                xMin += trimWidth * xRes;
                xMax -= trimWidth * xRes;
                yMin += trimWidth * yRes;
                yMax -= trimWidth * yRes;
                zMin += trimWidth * zRes;
                zMax -= trimWidth * zRes;
            }


            int TotalCompared = 0;
            int failed = 0;
            int ComparedToPoint = 0;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * epsilon;
            double sourceVariance = MaxSource * tolerance;

            for (double x = xMin; x <= xMax; x += xRes)
            {
                for (double y = yMin; y <= yMax; y += yRes)
                {
                    for (double z = zMin; z <= zMax; z += zRes)
                    {
                        ComparedToPoint++;
                        double sourcei = source.GetPointDose(x, y, z).Dose;
                        double targeti = target.GetPointDose(x, y, z).Dose;
                        if (targeti < MinDoseEvaluated || sourcei < MinDoseEvaluated) { continue; }
                        else
                        {
                            TotalCompared++;
                            double sourceLow = sourcei - sourceVariance;
                            double sourceHigh = sourcei + sourceVariance;
                            if (targeti < sourceLow || targeti > sourceHigh)
                                if (!fuzzy)
                                {
                                    failed++;
                                }
                                else
                                {
                                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - (xRes * fuzzyScale), y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + (xRes * fuzzyScale), y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - (yRes * fuzzyScale), z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + (yRes * fuzzyScale), z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - (zRes * fuzzyScale)).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + (yRes * fuzzyScale)).Dose); }
                                    double low = neighbouringDoses.Min();
                                    double high = neighbouringDoses.Max();
                                    if (targeti < low || targeti > high)
                                    {
                                        failed++;
                                    }

                                }
                        }

                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            System.Tuple<int, int> ret = new System.Tuple<int, int>(failed, TotalCompared);
            return ret;
        }


    }



}