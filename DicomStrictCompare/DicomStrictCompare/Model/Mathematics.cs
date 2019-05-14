namespace DicomStrictCompare
{
    public abstract class IMathematics
    {
        /// <summary>
        /// Compares the two provided dose arrays voxel to voxel. Dose difference is calculated as % of source dose
        /// </summary>
        /// <param name="source">Reference dose array</param>
        /// <param name="target">Dose Array being verified</param>
        /// <param name="tolerance">Less than this percent difference is a pass</param>
        /// <param name="epsilon">Threshold percent of max dose below which comparison will not be evaluated</param>
        /// <returns></returns>
        public abstract System.Tuple<int, int> CompareAbsolute(double[] source, double[] target, double tolerance, double epsilon);
        public abstract System.Tuple<int, int> CompareRelative(double[] source, double[] target, double tolerance, double epsilon);

        public virtual System.Tuple<int, int> CompareAbsolute(EvilDICOM.RT.DoseMatrix source, EvilDICOM.RT.DoseMatrix target, double tolerance, double epsilon)
        {

            var xMin = (source.X0 > target.X0) ? source.X0 : target.X0;
            var xMax = (source.XMax < target.XMax) ? source.XMax : target.XMax;
            var xRes = (source.XRes > target.XRes) ? source.XRes : target.XRes;
            var yMin = (source.Y0 > target.Y0) ? source.Y0 : target.Y0;
            var yMax = (source.YMax < target.YMax) ? source.YMax : target.YMax;
            var yRes = (source.YRes > target.YRes) ? source.YRes : target.YRes;
            var zMin = (source.Z0 > target.Z0) ? source.Z0 : target.Z0;
            var zMax = (source.ZMax < target.ZMax) ? source.ZMax : target.ZMax;
            var zRes = (source.ZRes > target.ZRes) ? source.ZRes : target.ZRes;

            var TotalComparisons = (xMax - xMin) * xRes;
            TotalComparisons *= (yMax - yMin) * yRes;
            TotalComparisons *= (zMax - zMin) * zRes;




            int TotalCompared = 0;
            int failed = 0;
            int ComparedToPoint = 0;
            int debugFrequency = 1000000;
            var debugMod = TotalComparisons / debugFrequency;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * epsilon;
            for (var x = xMin; x <= xMax; x += xRes)
            {
                for (var y = yMin; y <= yMax; y += yRes)
                {
                    for (var z = zMin; z <= zMax; z += zRes)
                    {
                        ComparedToPoint++;
                        var sourcei = source.GetPointDose(x, y, z).Dose;
                        var targeti = target.GetPointDose(x, y, z).Dose;
                        if (targeti < MinDoseEvaluated || sourcei < MinDoseEvaluated) { continue; }
                        else
                        {
                            TotalCompared++;
                            var sourceLow = (1.0 - tolerance) * sourcei;
                            var sourceHigh = (1.0 + tolerance) * sourcei;
                            if (targeti < sourceLow || targeti > sourceHigh)
                                failed++;
                        }

                    }
                }
            }
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            System.Tuple<int, int> ret = new System.Tuple<int, int>(failed, TotalCompared);
            return ret;
        }
        public virtual System.Tuple<int, int> CompareAbsolute(Model.DoseMatrixOptimal source, Model.DoseMatrixOptimal target, double tolerance, double epsilon)
        {

            var xMin = (source.X0 > target.X0) ? source.X0 : target.X0;
            var xMax = (source.XMax < target.XMax) ? source.XMax : target.XMax;
            var xRes = (source.XRes > target.XRes) ? source.XRes : target.XRes;
            var yMin = (source.Y0 > target.Y0) ? source.Y0 : target.Y0;
            var yMax = (source.YMax < target.YMax) ? source.YMax : target.YMax;
            var yRes = (source.YRes > target.YRes) ? source.YRes : target.YRes;
            var zMin = (source.Z0 > target.Z0) ? source.Z0 : target.Z0;
            var zMax = (source.ZMax < target.ZMax) ? source.ZMax : target.ZMax;
            var zRes = (source.ZRes > target.ZRes) ? source.ZRes : target.ZRes;

            var TotalComparisons = (xMax - xMin) * xRes;
            TotalComparisons *= (yMax - yMin) * yRes;
            TotalComparisons *= (zMax - zMin) * zRes;




            int TotalCompared = 0;
            int failed = 0;
            int ComparedToPoint = 0;
            int debugFrequency = 1000000;
            var debugMod = TotalComparisons / debugFrequency;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * epsilon;
            for (var x = xMin; x <= xMax; x += xRes)
            {
                for (var y = yMin; y <= yMax; y += yRes)
                {
                    for (var z = zMin; z <= zMax; z += zRes)
                    {
                        ComparedToPoint++;
                        var sourcei = source.GetPointDose(x, y, z).Dose;
                        var targeti = target.GetPointDose(x, y, z).Dose;
                        if (targeti < MinDoseEvaluated || sourcei < MinDoseEvaluated) { continue; }
                        else
                        {
                            TotalCompared++;
                            var sourceLow = (1.0 - tolerance) * sourcei;
                            var sourceHigh = (1.0 + tolerance) * sourcei;
                            if (targeti < sourceLow || targeti > sourceHigh)
                                failed++;
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
