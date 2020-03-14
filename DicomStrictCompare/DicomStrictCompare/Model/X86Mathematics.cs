using System.Linq;
using System.Threading.Tasks;
using EvilDICOM.RT;


namespace DicomStrictCompare.Model
{

    public class X86Mathematics : IMathematics
    {
        public X86Mathematics()
        {

        }

        public override SingleComparison CompareAbsolute(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta)
        {

            double xMin = source.X0 > target.X0 ? source.X0 : target.X0;
            double xMax = source.XMax < target.XMax ? source.XMax : target.XMax;
            double xRes = source.XRes > target.XRes ? source.XRes : target.XRes;
            double yMin = source.Y0 > target.Y0 ? source.Y0 : target.Y0;
            double yMax = source.YMax < target.YMax ? source.YMax : target.YMax;
            double yRes = source.YRes > target.YRes ? source.YRes : target.YRes;
            double zMin = source.Z0 > target.Z0 ? source.Z0 : target.Z0;
            double zMax = source.ZMax < target.ZMax ? source.ZMax : target.ZMax;
            double zRes = source.ZRes > target.ZRes ? source.ZRes : target.ZRes;

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
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - xRes * dta.Distance, y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + xRes * dta.Distance, y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - yRes * dta.Distance, z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + yRes * dta.Distance, z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - zRes * dta.Distance).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + yRes * dta.Distance).Dose); }
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
            System.Diagnostics.Debug.WriteLine(new System.Diagnostics.StackTrace().GetFrame(0).GetMethod().Name);
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            SingleComparison ret = new SingleComparison(dta, ComparedToPoint, TotalCompared, failed);

            return ret;
        }
        public override SingleComparison CompareRelative(in DoseMatrixOptimal source, in DoseMatrixOptimal target, Dta dta)
        {
            double xMin = source.X0 > target.X0 ? source.X0 : target.X0;
            double xMax = source.XMax < target.XMax ? source.XMax : target.XMax;
            double xRes = source.XRes > target.XRes ? source.XRes : target.XRes;
            double yMin = source.Y0 > target.Y0 ? source.Y0 : target.Y0;
            double yMax = source.YMax < target.YMax ? source.YMax : target.YMax;
            double yRes = source.YRes > target.YRes ? source.YRes : target.YRes;
            double zMin = source.Z0 > target.Z0 ? source.Z0 : target.Z0;
            double zMax = source.ZMax < target.ZMax ? source.ZMax : target.ZMax;
            double zRes = source.ZRes > target.ZRes ? source.ZRes : target.ZRes;

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
                                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - xRes * dta.Distance, y, z).Dose); }
                                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + xRes * dta.Distance, y, z).Dose); }
                                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - yRes * dta.Distance, z).Dose); }
                                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + yRes * dta.Distance, z).Dose); }
                                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - zRes * dta.Distance).Dose); }
                                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + yRes * dta.Distance).Dose); }
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
            System.Diagnostics.Debug.WriteLine(new System.Diagnostics.StackTrace().GetFrame(0).GetMethod().Name);
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            SingleComparison ret = new SingleComparison(dta, ComparedToPoint, TotalCompared, failed);
            return ret;
        }

        public override SingleComparison CompareParallel(DoseMatrixOptimal source, DoseMatrixOptimal target, Dta dta, int cpuParallel)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            if (dta == null) throw new System.ArgumentNullException(nameof(dta));
            if (cpuParallel < 0) throw new System.ArgumentOutOfRangeException(nameof(cpuParallel));

            double xMin = source.X0 > target.X0 ? source.X0 : target.X0;
            double xMax = source.XMax < target.XMax ? source.XMax : target.XMax;
            double xRes = source.XRes > target.XRes ? source.XRes : target.XRes;
            double yMin = source.Y0 > target.Y0 ? source.Y0 : target.Y0;
            double yMax = source.YMax < target.YMax ? source.YMax : target.YMax;
            double yRes = source.YRes > target.YRes ? source.YRes : target.YRes;
            double zMin = source.Z0 > target.Z0 ? source.Z0 : target.Z0;
            double zMax = source.ZMax < target.ZMax ? source.ZMax : target.ZMax;
            double zRes = source.ZRes > target.ZRes ? source.ZRes : target.ZRes;

            if (dta.TrimWidth > 0)
            {
                xMin += dta.TrimWidth * xRes;
                xMax -= dta.TrimWidth * xRes;
                yMin += dta.TrimWidth * yRes;
                yMax -= dta.TrimWidth * yRes;
                zMin += dta.TrimWidth * zRes;
                zMax -= dta.TrimWidth * zRes;
            }


            int[] TotalCompared = new int[cpuParallel];
            int[] failed = new int[cpuParallel];
            int[] ComparedToPoint = new int[cpuParallel];
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * dta.Threshhold;
            double sourceVariance = MaxSource * dta.Tolerance;

            double zChunck = System.Math.Floor((zMax - zMin) / zRes / cpuParallel);

            double[] zBoundaries = new double[cpuParallel + 1];

            for (int i = 0; i < TotalCompared.Length; i++)
                zBoundaries[i] = zMin + i * zChunck * zRes;
            zBoundaries[zBoundaries.Length - 1] = zMax + 0.5 * zRes;

            Task[] tasks = new Task[cpuParallel];
            for (int i = 0; i < cpuParallel; i++)
            {
                tasks[i] = new Task(delegate () { portionCalculator(i); });
                tasks[i].Start();
                System.Threading.Thread.Sleep(100);
            }
            try
            {
                Task.WaitAll(tasks);
            }
            catch (System.IndexOutOfRangeException e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                System.Diagnostics.Debug.WriteLine(e.StackTrace);
                System.Diagnostics.Debug.WriteLine(e.InnerException.Message);
            }

            System.Diagnostics.Debug.WriteLine(new System.Diagnostics.StackTrace().GetFrame(0).GetMethod().Name);
            System.Diagnostics.Debug.WriteLine("Failed: " + failed.Sum() + " of " + TotalCompared.Sum());

            SingleComparison ret = new SingleComparison(dta, ComparedToPoint.Sum(), TotalCompared.Sum(), failed.Sum());
            return ret;

            void comparison(int index, double x, double y, double z)
            {
                ComparedToPoint[index]++;
                double sourcei = source.GetPointDose(x, y, z).Dose;
                double targeti = target.GetPointDose(x, y, z).Dose;
                if (targeti < MinDoseEvaluated || sourcei < MinDoseEvaluated) { return; }
                TotalCompared[index]++;
                double sourceLow, sourceHigh;
                if (Dta.calcType.relative == dta.Type)
                {
                    sourceLow = (1.0 - dta.Tolerance) * sourcei;
                    sourceHigh = (1.0 + dta.Tolerance) * sourcei;
                }
                else
                {
                    sourceLow = sourcei - sourceVariance;
                    sourceHigh = sourcei + sourceVariance;
                }
                if (targeti >= sourceLow && targeti <= sourceHigh)
                {
                    return;
                }
                else if (dta.Distance == 0)
                {
                    failed[index]++;
                    return;
                }
                else if (dta.UseMM == false)
                {
                    System.Collections.Generic.List<double> neighbouringDoses = new System.Collections.Generic.List<double>();
                    if (x > xMin) { neighbouringDoses.Add(source.GetPointDose(x - xRes * dta.Distance, y, z).Dose); }
                    if (x < xMax) { neighbouringDoses.Add(source.GetPointDose(x + xRes * dta.Distance, y, z).Dose); }
                    if (y > yMin) { neighbouringDoses.Add(source.GetPointDose(x, y - yRes * dta.Distance, z).Dose); }
                    if (y < yMax) { neighbouringDoses.Add(source.GetPointDose(x, y + yRes * dta.Distance, z).Dose); }
                    if (z > zMin) { neighbouringDoses.Add(source.GetPointDose(x, y, z - zRes * dta.Distance).Dose); }
                    if (z < zMax) { neighbouringDoses.Add(source.GetPointDose(x, y, z + yRes * dta.Distance).Dose); }
                    double low = neighbouringDoses.Min();
                    double high = neighbouringDoses.Max();
                    if (targeti < low || targeti > high)
                    {
                        failed[index]++;
                    }
                }
                else
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
                        failed[index]++;
                    }
                }
            }

            void portionCalculator(int index)
            {
                for (double x = xMin; x <= xMax; x += xRes)
                {
                    for (double y = yMin; y <= yMax; y += yRes)
                    {
                        for (double z = zBoundaries[index]; z < zBoundaries[index + 1]; z += zRes)
                        {


                            comparison(index, x, y, z);



                        }
                    }
                }
            }

        }


    }

}
