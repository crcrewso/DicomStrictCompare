using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EvilDICOM.RT;

namespace DicomStrictCompare
{
    /// <summary>
    /// Will contain the evaluation of any stored pair of DoseFiles
    /// </summary>
    class MatchedDosePair
    {
        public int TotalCount { get; private set; }
        public int TotalComparedTightTolAbs { get; private set; } = 0;
        public int TotalFailedTightTolAbs { get; private set; }
        public double PercentFailedTightTolAbs => PercentCalculator(TotalComparedTightTolAbs, TotalFailedTightTolAbs);
        public int TotalComparedMainTolAbs { get; private set; } = 0;
        public int TotalFailedMainTolAbs { get; private set; }
        public double PercentFailedMainTolAbs => PercentCalculator(TotalComparedMainTolAbs, TotalFailedMainTolAbs);
        public int TotalComparedTightTolRel { get; private set; } = 0;
        public int TotalFailedTightTolRel { get; private set; }
        public double PercentFailedTightTolRel => PercentCalculator(TotalComparedTightTolRel, TotalFailedTightTolRel);
        public int TotalComparedMainTolRel { get; private set; } = 0;
        public int TotalFailedMainTolRel { get; private set; }
        public double PercentFailedMainTolRel => PercentCalculator(TotalComparedMainTolRel, TotalFailedMainTolRel);
        /// <summary>
        /// The matched pair has been evaluated to measure results
        /// </summary>
        public bool IsEvaluated { get; private set; } = false;
        /// <summary>
        /// Machine Tol for smallest difference between two doses
        /// </summary>
        public double ThreshholdTol { get; }
        /// <summary>
        /// User controlled tight tolerance
        /// </summary>
        public double TightTol { get; }
        /// <summary>
        /// User controlled Main tolerance
        /// </summary>
        public double MainTol { get; }

        /// <summary>
        /// Name of the pair evaluated
        /// </summary>
        public string Name => _source.PlanID + ',' + _source.FieldName;
            
        /// <summary>
        /// Filenames of the pair of files
        /// </summary>
        public string FileNames => _source.ShortFileName + ',' + _target.ShortFileName;

        public string ResultString => String.Join(",", resultArray());
        public static string ResultHeader => String.Join(",", resultArrayHeaderRow0()) + "\n" + String.Join(",", resultArrayHeaderRow1()) + "\n"+String.Join(",", resultArrayHeaderRow2()) + "\n";

        private readonly DoseFile _source;
        private readonly DoseFile _target;

        public List<DoseValue> SourcePDD { get; private set; }
        public List<DoseValue> TargetPDD { get; private set; }
        public string ChartTitle { get; private set; }
        public string ChartFileName { get; private set; }

        /// <summary>
        /// Calculates the percent of voxels that failed based on the total 
        /// </summary>
        /// <param name="total">The total number of voxels compared</param>
        /// <param name="failed">The number of voxels that failed</param>
        /// <returns></returns>
        private static double PercentCalculator(int total, int failed)
        {
            return (double)failed / (double)total * 100.0;
        }

        string[] resultArray()
        {
            string[] ret = new string[15];
            int i = 0;
            ret[i++] = Name;
            ret[i++] = PercentFailedTightTolAbs.ToString("0.0000");
            ret[i++] = PercentFailedMainTolAbs.ToString("0.0000");
            ret[i++] = PercentFailedTightTolRel.ToString("0.0000");
            ret[i++] = PercentFailedMainTolRel.ToString("0.0000");
            ret[i++] = TotalCount.ToString();
            ret[i++] = TotalComparedTightTolAbs.ToString();
            ret[i++] = TotalFailedTightTolAbs.ToString();
            ret[i++] = TotalFailedMainTolAbs.ToString();
            ret[i++] = TotalFailedTightTolRel.ToString();
            ret[i++] = TotalFailedMainTolRel.ToString();
            ret[i++] = FileNames;
            ret[i++] = _source.FieldMUs;
            ret[i++] = _target.FieldMUs;
            return ret;
        }
        static string[] resultArrayHeaderRow0()
        {
            string[] ret = new string[15];
            int i = 0;
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "Percent Failed";
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "Total Failed";
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "";
            return ret;
        }
        static string[] resultArrayHeaderRow1()
        {
            string[] ret = new string[15];
            int i = 0;
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "Absolute";
            ret[i++] = "";
            ret[i++] = "Relative";
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "";
            ret[i++] = "Absolute";
            ret[i++] = "";
            ret[i++] = "Relative";
            ret[i++] = "";
            return ret;
        }
        static string[] resultArrayHeaderRow2()
        {
            string[] ret = new string[15];
            int i = 0;
            ret[i++] = "Plan Name";
            ret[i++] = "Field Name";
            ret[i++] = "Tight";
            ret[i++] = "Main";
            ret[i++] = "Tight";
            ret[i++] = "Main";
            ret[i++] = "TotalCount";
            ret[i++] = "Total Compared";
            ret[i++] = "Tight";
            ret[i++] = "Main";
            ret[i++] = "Tight";
            ret[i++] = "Main";
            ret[i++] = "Source File Name,Target File Name";
            ret[i++] = "Source MUs";
            ret[i++] = "Target MUs";
            return ret;
        }

        public MatchedDosePair(DoseFile source, DoseFile target, double epsilonTol, double tightTol, double mainTol)
        {
            _source = source;
            _target = target;
            ThreshholdTol = epsilonTol;
            TightTol = tightTol;
            MainTol = mainTol;
            ChartTitle = "PDD of " + _source.PlanID + @" " + _source.FieldName;
            ChartFileName = _source.PlanID + @"\" + _source.FieldName;
        }

        public void GeneratePDD()
        {
            DoseMatrix sourceMatrix = _source.DoseMatrix();
            DoseMatrix targetMatrix = _target.DoseMatrix();
            double yMin = (sourceMatrix.Y0 > targetMatrix.Y0) ? sourceMatrix.Y0 : targetMatrix.Y0;
            double yMax = (sourceMatrix.YMax < targetMatrix.YMax) ? sourceMatrix.YMax : targetMatrix.YMax;
            double yRes = sourceMatrix.YRes;
            EvilDICOM.Core.Helpers.Vector3 startPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMin, 0 );
            EvilDICOM.Core.Helpers.Vector3 endPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMax, 0);
            SourcePDD = sourceMatrix.GetLineDose(startPoint, endPoint, yRes);
            TargetPDD = targetMatrix.GetLineDose(startPoint, endPoint, yRes);


        }

        /// <summary>
        /// Performs the actual work not the most efficient way but I'll work it out
        /// </summary>
        public void Evaluate(IMathematics mathematics)
        {
            Model.DoseMatrixOptimal sourceDose = new Model.DoseMatrixOptimal(_source.DoseMatrix());
            Model.DoseMatrixOptimal targetDose = new Model.DoseMatrixOptimal(_target.DoseMatrix());
            TotalCount = targetDose.Length;
            if (sourceDose.CompareDimensions(targetDose))
            //if (false)
            {
                Debug.WriteLine("\n\n\nEvaluating " + _source.FileName + " and " + _target.FileName);
                Tuple<int, int> ret;
                ret = mathematics.CompareAbsolute(sourceDose.DoseValues, targetDose.DoseValues, TightTol, ThreshholdTol);
                TotalFailedTightTolAbs = ret.Item1;
                TotalComparedTightTolAbs = ret.Item2;
                if (ret.Item1 == 0) // if failed tightest evaluation skip further evaluation Requires tight < main to work. 
                {
                    TotalFailedMainTolAbs = ret.Item1;
                    TotalComparedMainTolAbs = ret.Item2;
                    TotalFailedTightTolRel = ret.Item1;
                    TotalComparedTightTolRel = ret.Item2;
                    TotalFailedMainTolRel = ret.Item1;
                    TotalComparedMainTolRel = ret.Item2;
                }
                else
                {
                    ret = mathematics.CompareAbsolute(sourceDose.DoseValues, targetDose.DoseValues, MainTol, ThreshholdTol);
                    TotalFailedMainTolAbs = ret.Item1;
                    TotalComparedMainTolAbs = ret.Item2;
                    ret = mathematics.CompareRelative(sourceDose.DoseValues, targetDose.DoseValues, TightTol, ThreshholdTol);
                    TotalFailedTightTolRel = ret.Item1;
                    TotalComparedTightTolRel = ret.Item2;
                    ret = mathematics.CompareRelative(sourceDose.DoseValues, targetDose.DoseValues, MainTol, ThreshholdTol);
                    TotalFailedMainTolRel = ret.Item1;
                    TotalComparedMainTolRel = ret.Item2;
                }
                IsEvaluated = true;
            }
            else
            {
                Debug.WriteLine("\n\n\nEvaluating " + _source.FileName + " and " + _target.FileName + " Dimensions disagree");
                Debug.WriteLine("Max dose: Source - " + sourceDose.MaxPointDose.Dose + " Target - " + targetDose.MaxPointDose.Dose);
                Tuple<int, int> ret;
                ret = mathematics.CompareAbsolute(sourceDose, targetDose, TightTol, ThreshholdTol);
                TotalFailedTightTolAbs = ret.Item1;
                TotalComparedTightTolAbs = ret.Item2;
                if (ret.Item1 == 0)
                {
                    TotalFailedMainTolAbs = ret.Item1;
                    TotalComparedMainTolAbs = ret.Item2;
                    TotalFailedTightTolRel = ret.Item1;
                    TotalComparedTightTolRel = ret.Item2;
                    TotalFailedMainTolRel = ret.Item1;
                    TotalComparedMainTolRel = ret.Item2;
                }
                else
                {
                    ret = mathematics.CompareAbsolute(sourceDose, targetDose, MainTol, ThreshholdTol);
                    TotalFailedMainTolAbs = ret.Item1;
                    TotalComparedMainTolAbs = ret.Item2;
                    ret = mathematics.CompareRelative(sourceDose, targetDose, TightTol, ThreshholdTol);
                    TotalFailedTightTolRel = ret.Item1;
                    TotalComparedTightTolRel = ret.Item2;
                    ret = mathematics.CompareRelative(sourceDose, targetDose, MainTol, ThreshholdTol);
                    TotalFailedMainTolRel = ret.Item1;
                    TotalComparedMainTolRel = ret.Item2;
                }
                IsEvaluated = true;
            }




        }

        /// <summary>
        /// Calculates the actual # of failed comparisons given the tolerance
        /// TODO Optimize this it's bad, slowest possible implimentation here. 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="tol">maximum allowable percent difference between two dose values for the voxels to be considered equal</param>
        /// <returns></returns>
        /*private Tuple<int,int> EvaluateAbsolute( double[] source,  double[] target, double tol)
        {
            int failed = 0;
            int TotalCompared = 0;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * ThreshholdTol;
            for (int i = 0; i < target.Length; i++)
            {
                var sourcei = source[i];
                var targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    TotalCompared++;
                    var sourceLow = (1.0 - tol) * sourcei;
                    var sourceHigh = (1.0 + tol) * sourcei;
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failed++;
                }

            }
            Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            Tuple<int, int> ret = new Tuple<int, int>(failed, TotalCompared);
            return ret;
        }

        /// <summary>
        /// Calculates the actual # of failed comparisons given the tolerance
        /// TODO Optimize this it's bad, slowest possible implimentation here. 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="tol">maximum allowable percent difference between two dose values for the voxels to be considered equal</param>
        /// <returns></returns>
        private Tuple<int, int> EvaluateRelative(double[] source, double[] target, double tol)
        {
            int failed = 0;
            int TotalCompared = 0;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * ThreshholdTol;
            double sourceVariance = MaxSource * tol;
            for (int i = 0; i < target.Length; i++)
            {
                var sourcei = source[i];
                var targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    TotalCompared++;
                    var sourceLow = sourcei - sourceVariance;
                    var sourceHigh = sourcei + sourceVariance;
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failed++;
                }

            }
            Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            Tuple<int, int> ret = new Tuple<int, int>(failed, TotalCompared);
            return ret;
        }

        private Tuple<int, int> EvaluateAbsolute( DoseMatrix source, DoseMatrix target, double tol)
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
            double MinDoseEvaluated = MaxSource * ThreshholdTol;
            for (var x = xMin; x <= xMax; x += xRes)
            {
                for (var y = yMin; y <= yMax; y += yRes)
                {
                    for (var z = zMin; z <= zMax; z += zRes)
                    {
                        ComparedToPoint++;
                        if (ComparedToPoint % debugMod == 0)
                        {
                            Debug.WriteLine("I have finished " + (ComparedToPoint / debugMod).ToString() +" %");
                        }
                        var sourcei = source.GetPointDose(x, y, z).Dose;
                        var targeti = target.GetPointDose(x, y, z).Dose;
                        if (targeti < MinDoseEvaluated || sourcei < MinDoseEvaluated) { continue; }
                        else
                        {
                            TotalCompared++;
                            var sourceLow = (1.0 - tol) * sourcei;
                            var sourceHigh = (1.0 + tol) * sourcei;
                            if (targeti < sourceLow || targeti > sourceHigh)
                                failed++;
                        }    
                        
                    }
                }
            }
            Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            Tuple<int, int> ret = new Tuple<int, int>(failed, TotalCompared);
            return ret;
        }
        */
    }


}
