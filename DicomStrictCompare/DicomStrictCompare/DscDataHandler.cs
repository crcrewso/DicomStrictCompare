using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvilDICOM.RT;

namespace DicomStrictCompare
{
    /// <summary>
    /// Holds the user entered data and builds the file list for the file handler
    /// </summary>
    class DscDataHandler
    {


        public string SourceDirectory { get; private set; }
        public string TargetDirectory { get; private set; }
        public string[] SourceListStrings { get; private set; }
        public string[] TargetListStrings { get; private set; }

        /// <summary>
        /// List of RD (Dose) files in the source directory
        /// </summary>
        public List<DoseFile> SourceDosesList { get; private set; }
        /// <summary>
        /// List of RP (Plan) files in the source directory
        /// </summary>
        public List<PlanFile> SourcePlanList { get; private set; }
        /// <summary>
        /// List of RD (Dose) files in the target directory
        /// </summary>
        public List<DoseFile> TargetDosesList { get; private set; }
        /// <summary>
        /// List of RP (Plan) Files in the target directory
        /// </summary>
        public List<PlanFile> TargetPlanList { get; private set; }
        /// <summary>
        /// List of matched source and plan dose files, at the time of this comment matching was made by plan name, field name and patient ID. 
        /// </summary>
        public List<MatchedDosePair> DosePairsList { get; private set; }

        public double ThresholdTol { get; set; }
        public double TightTol { get; set; }
        public double MainTol { get; set; }

        /// <summary>
        /// Increasingly inaccurate name for the csv separated results from the analysis, 
        /// includes error messages for matches that didn't work. 
        /// </summary>
        public string ResultMessage { get; private set; }

        public DscDataHandler()
        {
            DosePairsList = new List<MatchedDosePair>();
        }




        public int CreateSourceList(string folder)
        {
            SourceDirectory = folder;
            SourceListStrings = FileHandler.LoadListRdDcmList(folder);
            return SourceListStrings.Length;
        }

        public int CreateTargetList(string folder)
        {
            TargetDirectory = folder;
            TargetListStrings = FileHandler.LoadListRdDcmList(folder);
            return TargetListStrings.Length;
        }



        public void Run(bool runDoseComparisons, bool runPDDComparisons, string SaveDirectory, ref System.Windows.Forms.ProgressBar progressBar)
        {
            #region safetyChecks




            progressBar.PerformStep();
            try
            {
                SourceDirectory.IsNormalized();
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Source directory cannot be null");
            }

            try
            {
                TargetDirectory.IsNormalized();
            }
            catch (NullReferenceException)
            {

                throw new NullReferenceException("Target directory cannot be null"); ;
            }

            if (SourceListStrings.Length <= 0)
                throw new InvalidOperationException("There are no Dose files in the source directory Tree");
            if (TargetListStrings.Length <= 0)
                throw new InvalidOperationException("There are no Dose files in the Target directory Tree");
            #endregion
            System.Threading.Thread sourceDoseProcess = new System.Threading.Thread(() => 
            {
            SourceDosesList = FileHandler.DoseFiles(SourceListStrings);

            });

            System.Threading.Thread sourcePlanProcess = new System.Threading.Thread(() =>
            {
            SourcePlanList = FileHandler.PlanFiles(SourceListStrings);

            });

            System.Threading.Thread targetDoseProcess = new System.Threading.Thread(() =>
            {
            TargetDosesList = FileHandler.DoseFiles(TargetListStrings);

            });

            System.Threading.Thread targetPlanProcess = new System.Threading.Thread(() =>
            {
            TargetPlanList = FileHandler.PlanFiles(TargetListStrings);

            });
            progressBar.PerformStep();
            sourceDoseProcess.Start();
            targetDoseProcess.Start();
            sourcePlanProcess.Start();
            targetPlanProcess.Start();

            sourceDoseProcess.Join();
            targetDoseProcess.Join();
            sourcePlanProcess.Join();
            targetPlanProcess.Join();
            progressBar.PerformStep();

            DosePairsList = new List<MatchedDosePair>();
            ResultMessage = "Tight Tolerance, " + (100 * TightTol).ToString();
            ResultMessage += "\nMain Tolerance, " + (100 * MainTol).ToString();
            ResultMessage += "\nThreshold, " + (100 * ThresholdTol).ToString() + "\n";
            ResultMessage += MatchedDosePair.ResultHeader;

            System.Threading.Thread t1 = new System.Threading.Thread(() =>
            {
                foreach (var doseFile in SourceDosesList)
                {
                    doseFile.SetFieldName(SourcePlanList);
                }
            });

            System.Threading.Thread t2 = new System.Threading.Thread(() =>
            {
                foreach (var doseFile in TargetDosesList)
            {
                doseFile.SetFieldName(TargetPlanList);
            } });

            progressBar.PerformStep();
            t1.Start();
            t2.Start();
            t2.Join();
            t1.Join();
            progressBar.PerformStep();

            // match each pair for analysis
            Parallel.ForEach(TargetDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (dose) =>
            {
                foreach (var sourceDose in SourceDosesList)
                {
                    if (dose.MatchIdentifier == sourceDose.MatchIdentifier)
                    {
                        Debug.WriteLine("matched " + dose.FileName + " and " + sourceDose.FileName);
                        DosePairsList.Add(new MatchedDosePair(sourceDose, dose, this.ThresholdTol, this.TightTol,
                            this.MainTol));
                    }
                }
            });
            progressBar.PerformStep();

            //fix for memory abuse is to limit the number of cores, Arbitrarily I have hard coded it to half the logical cores of the system.
            if (DosePairsList.Count > 0 && runDoseComparisons)
            {
                Parallel.ForEach(DosePairsList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount/2 }, pair =>
                {
                    try
                    {
                        pair.Evaluate();
                        ResultMessage += pair.ResultString + '\n';
                        Debug.WriteLine(pair.ResultString);

                    }
                    // Will catch array misalignment problems
                    catch (Exception)
                    {
                        ResultMessage += pair.Name + ",Was not Evaluated ,\n";

                    }


                });
            }
            progressBar.PerformStep();
            if (runPDDComparisons)
            {
                Parallel.ForEach(DosePairsList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, pair =>
                {
                    pair.GeneratePDD();
                    Debug.WriteLine("Saving " + pair.ChartTitle + " to " + SaveDirectory);
                    SaveFile saveFile = new SaveFile(pair.ChartTitle, SaveDirectory);
                    saveFile.Save(pair.SourcePDD, pair.TargetPDD, pair.ChartFileName, SaveDirectory, pair.ChartTitle);

                });
            }


            
        }

        

    }


    /// <summary>
    /// Will contain the evaluation of any stored pair of DoseFiles
    /// </summary>
    class MatchedDosePair
    {
        public int TotalCount { get; private set; }
        public int TotalComparedTightTol { get; private set; } = 0;
        public int TotalComparedMainTol { get; private set; } = 0;

        public int TotalFailedEpsilonTol { get; private set; }
        //public double PercentFailedEpsilonTol => PercentCalculator(TotalCompared, TotalFailedEpsilonTol);
        public int TotalFailedTightTol { get; private set; }
        public double PercentFailedTightTol => PercentCalculator(TotalComparedTightTol, TotalFailedTightTol);
        public int TotalFailedMainTol { get; private set; }
        public double PercentFailedMainTol => PercentCalculator(TotalComparedMainTol, TotalFailedMainTol);
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
        public string Name => _source.ShortFileName + ',' + _target.ShortFileName;

        public string ResultString => Name + ',' + TotalCount.ToString() + ',' + TotalComparedTightTol.ToString() + ',' + TotalFailedTightTol + ',' + PercentFailedTightTol.ToString("0.0000") + ',' + TotalFailedMainTol.ToString() + ',' + PercentFailedMainTol.ToString("0.0000");

        public static string ResultHeader => "Source Name,Target Name,Voxels,Voxels above Threshold,Failed Tight,Percent, Failed Main,Percent\n";

        private DoseFile _source;
        private DoseFile _target;

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


        public MatchedDosePair(DoseFile source, DoseFile target, double epsilonTol, double tightTol, double mainTol)
        {
            _source = source;
            _target = target;
            ThreshholdTol = epsilonTol;
            TightTol = tightTol;
            MainTol = mainTol;
        }

        public void GeneratePDD()
        {
            var sourceMatrix = _source.DoseMatrix();
            var targetMatrix = _target.DoseMatrix();

            var xMin = (sourceMatrix.X0 > targetMatrix.X0) ? sourceMatrix.X0 : targetMatrix.X0;
            var xMax = (sourceMatrix.XMax < targetMatrix.XMax) ? sourceMatrix.XMax : targetMatrix.XMax;
            var xRes = (sourceMatrix.XRes > targetMatrix.XRes) ? sourceMatrix.XRes : targetMatrix.XRes;
            var yMin = (sourceMatrix.Y0 > targetMatrix.Y0) ? sourceMatrix.Y0 : targetMatrix.Y0;
            var yMax = (sourceMatrix.YMax < targetMatrix.YMax) ? sourceMatrix.YMax : targetMatrix.YMax;
            var yRes = sourceMatrix.YRes;

            var zMin = (sourceMatrix.Z0 > targetMatrix.Z0) ? sourceMatrix.Z0 : targetMatrix.Z0;
            var zMax = (sourceMatrix.ZMax < targetMatrix.ZMax) ? sourceMatrix.ZMax : targetMatrix.ZMax;
            var zRes = sourceMatrix.ZRes;


            var startPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMin, 0 );
            var endPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMax, 0);

            SourcePDD = sourceMatrix.GetLineDose(startPoint, endPoint, yRes);
            TargetPDD = targetMatrix.GetLineDose(startPoint, endPoint, yRes);

            ChartTitle = "PDD of " + _source.PlanID + @" " + _source.FieldName;
            ChartFileName = _source.PlanID + @"\" + _source.FieldName;
        }

        /// <summary>
        /// Performs the actual work not the most efficient way but I'll work it out
        /// </summary>
        public void Evaluate()
        {
            var sourceDose = _source.DoseValues().ToArray();
            var targetDose = _target.DoseValues().ToArray();
            TotalCount = targetDose.Length;
            if (_source.X == _target.X && _source.Y == _target.Y && _source.Z == _target.Z)
            {
                Debug.WriteLine("\n\n\nEvaluating " + _source.FileName + " and " + _target.FileName);
                var tightRet = Evaluate(sourceDose, targetDose, TightTol);
                TotalFailedTightTol = tightRet.Item1;
                TotalComparedTightTol = tightRet.Item2;
                var mainRet = Evaluate(sourceDose, targetDose, MainTol);
                TotalFailedMainTol = mainRet.Item1;
                TotalComparedMainTol = mainRet.Item2;
                IsEvaluated = true;
            }
            else
            {
                Debug.WriteLine("\n\n\nEvaluating " + _source.FileName + " and " + _target.FileName + " Dimensions disagree");
                var tightRet = Evaluate(_source.DoseMatrix(), _target.DoseMatrix(), TightTol);
                TotalFailedTightTol = tightRet.Item1;
                TotalComparedTightTol = tightRet.Item2;
                var mainRet = Evaluate(_source.DoseMatrix(), _target.DoseMatrix(), MainTol);
                TotalFailedMainTol = mainRet.Item1;
                TotalComparedMainTol = mainRet.Item2;
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
        private Tuple<int,int> Evaluate( double[] source,  double[] target, double tol)
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

        private int EvaluateParallel( double[] source, double[] target, double tol)
        {
            int[] failedList = new int[source.Count()];
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * ThreshholdTol;
            Parallel.For(0, failedList.Count(), i =>
            {
                double sourcei = source[i];
                double targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    var sourceLow = (1.0 - tol) * sourcei;
                    var sourceHigh = (1.0 + tol) * sourcei;
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failedList[i] = 1;
                }
                else
                {
                    failedList[i] = 0;
                }
            });
            return failedList.AsParallel().Sum();
        }


        private Tuple<int, int> Evaluate( DoseMatrix source, DoseMatrix target, double tol)
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

    }


}
