using System.Linq;
using System.Threading.Tasks;
using EvilDICOM.RT;


namespace DicomStrictCompare
{
    public class X86Mathematics : IMathematics
    {

        public X86Mathematics()
        {

        }

        public override System.Tuple<int, int> CompareAbsolute(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta)
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

            if (dta.TrimWidth > 0)
            {
                xMin += dta.TrimWidth * xRes;
                xMax -= dta.TrimWidth * xRes;
                yMin += dta.TrimWidth * yRes;
                yMax -= dta.TrimWidth * yRes;
                zMin += dta.TrimWidth * zRes;
                zMax -= dta.TrimWidth * zRes;
            }


            int TotalCompared = 0;
            int failed = 0;
            int ComparedToPoint = 0;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * dta.Threshhold;
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
                            double sourceLow = (1.0 - dta.Tolerance) * sourcei;
                            double sourceHigh = (1.0 + dta.Tolerance) * sourcei;
                            if (targeti < sourceLow || targeti > sourceHigh)
                            {
                                if (dta.Distance == 0)
                                {
                                    failed++;
                                }
                                else if (dta.UseMM == false)
                                {
                                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - (xRes * dta.Distance), y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + (xRes * dta.Distance), y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - (yRes * dta.Distance), z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + (yRes * dta.Distance), z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - (zRes * dta.Distance)).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + (yRes * dta.Distance)).Dose); }
                                    double low = neighbouringDoses.Min();
                                    double high = neighbouringDoses.Max();
                                    if (targeti < low || targeti > high)
                                    {
                                        failed++;
                                    }

                                }
                                else
                                {
                                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x -  dta.Distance, y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + dta.Distance, y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - dta.Distance, z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + dta.Distance, z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - dta.Distance).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + dta.Distance).Dose); }
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
        public override System.Tuple<int, int> CompareRelative(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta)
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

            if (dta.TrimWidth > 0)
            {
                xMin += dta.TrimWidth * xRes;
                xMax -= dta.TrimWidth * xRes;
                yMin += dta.TrimWidth * yRes;
                yMax -= dta.TrimWidth * yRes;
                zMin += dta.TrimWidth * zRes;
                zMax -= dta.TrimWidth * zRes;
            }


            int TotalCompared = 0;
            int failed = 0;
            int ComparedToPoint = 0;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * dta.Threshhold;
            double sourceVariance = MaxSource * dta.Tolerance;

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
                            {
                                if (dta.Distance == 0)
                                {
                                    failed++;
                                }
                                else if (dta.UseMM == false)
                                {
                                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - (xRes * dta.Distance), y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + (xRes * dta.Distance), y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - (yRes * dta.Distance), z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + (yRes * dta.Distance), z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - (zRes * dta.Distance)).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + (yRes * dta.Distance)).Dose); }
                                    double low = neighbouringDoses.Min();
                                    double high = neighbouringDoses.Max();
                                    if (targeti < low || targeti > high)
                                    {
                                        failed++;
                                    }

                                }
                                else if (dta.UseMM == false)
                                {
                                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - dta.Distance, y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + dta.Distance, y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - dta.Distance, z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + dta.Distance, z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - dta.Distance).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + dta.Distance).Dose); }
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




    }

}
