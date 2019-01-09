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

        public List<DoseFile> SourceDosesList { get; private set; }
        public List<DoseFile> TargetDosesList { get; private set; }
        public List<MatchedDosePair> DosePairsList { get; private set; }

        public double EpsilonTol { get; set; }
        public double TightTol { get; set; }
        public double MainTol { get; set; }

        public string ResultMessage { get; private set; }

        public DscDataHandler()
        {
            DosePairsList = new List<MatchedDosePair>();
            ResultMessage = "";
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



        public void Run()
        {
            #region safetyChecks

            


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

            if (SourceListStrings.Length <=0 )
                throw new InvalidOperationException("There are no Dose files in the source directory Tree");
            if (TargetListStrings.Length <=0 )
                throw new InvalidOperationException("There are no Dose files in the Target directory Tree");
            #endregion

            SourceDosesList = FileHandler.DoseFiles(SourceListStrings);
            TargetDosesList = FileHandler.DoseFiles(TargetListStrings);
            ResultMessage = "";

            // match each pair for analysis
            Parallel.ForEach(TargetDosesList, (dose) =>
            {
                foreach (var sourceDose in SourceDosesList)
                {
                    if (dose.MatchIdentifier == sourceDose.MatchIdentifier)
                    {
                        Debug.WriteLine("matched " + dose.FileName + " and " + sourceDose.FileName);
                        DosePairsList.Add(new MatchedDosePair(sourceDose, dose, this.EpsilonTol, this.TightTol,
                            this.MainTol));
                    }
                }
            });

            //Do not try to Parallel.ForEach this, it will break, it will run out of memory
            if (DosePairsList.Count > 0)
            {
                foreach (var pair in DosePairsList)
                {
                    try
                    {
                        pair.Evaluate();
                        ResultMessage += pair.ResultString + '\n';
                        Debug.WriteLine(pair.ResultString);
                    }
                    // Will catch array misalignment problems
                    catch (DataMisalignedException)
                    {
                        ResultMessage += pair.Name + " was not evaluated";
                        
                    }


                }
            }


            
        }

        

    }


    /// <summary>
    /// Will contain the evaluation of any stored pair of DoseFiles
    /// </summary>
    class MatchedDosePair
    {
        public int TotalCount { get; private set; }
        public int TotalCompared { get; private set; } = 0;
        public int TotalFailedEpsilonTol { get; private set; }
        //public double PercentFailedEpsilonTol => PercentCalculator(TotalCompared, TotalFailedEpsilonTol);
        public int TotalFailedTightTol { get; private set; }
        public double PercentFailedTightTol => PercentCalculator(TotalCompared, TotalFailedTightTol);
        public int TotalFailedMainTol { get; private set; }
        public double PercentFailedMainTol => PercentCalculator(TotalCompared, TotalFailedEpsilonTol);
        /// <summary>
        /// The matched pair has been evaluated to measure results
        /// </summary>
        public bool IsEvaluated { get; private set; } = false;
        /// <summary>
        /// Machine Tol for smallest difference between two doses
        /// </summary>
        public double EpsilonTol { get; }
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
        public string Name => _source.FileName + '\t' + _target.FileName;

        public string ResultString => Name + '\t' + TotalCount.ToString() + '\t' + TotalCompared.ToString() + '\t' + TotalFailedEpsilonTol + '\t' + TotalFailedTightTol +'\t' + TotalFailedMainTol + '\t' + PercentFailedMainTol.ToString("0.00");


        private DoseFile _source;
        private DoseFile _target;

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
            EpsilonTol = epsilonTol;
            TightTol = tightTol;
            MainTol = mainTol;
        }



        /// <summary>
        /// Performs the actual work not the most efficient way but I'll work it out
        /// </summary>
        public void Evaluate()
        {
            var sourceDose = _source.DoseValues();
            var targetDose = _target.DoseValues();
            TotalCount = targetDose.Count;
            //var sourceDose = _source.DoseMatrix();
            //var targetDose = _target.DoseMatrix();
            //TotalCount = targetDose.DoseValues.Count;
            if (_source.X != _target.X || _source.Y != _target.Y || _source.Z != _target.Z)
            { throw new DataMisalignedException("The Array Dimensions don't match"); }
            Debug.WriteLine("\n\n\nEvaluating " + _source.FileName + " and " + _target.FileName); 
            TotalFailedTightTol = Evaluate(ref sourceDose, ref targetDose, TightTol);
            TotalFailedMainTol = Evaluate(ref sourceDose, ref targetDose, MainTol);
            IsEvaluated = true;
        }

        /// <summary>
        /// Calculates the actual # of failed comparisons given the tolerance
        /// TODO Optimize this it's bad, slowest possible implimentation here. 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="tol">maximum allowable percent difference between two dose values for the voxels to be considered equal</param>
        /// <returns></returns>
        private int Evaluate(ref List<double> source, ref List<double> target, double tol)
        {
            int failed = 0;
            TotalCompared = 0;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * EpsilonTol;
            for (int i = 0; i < target.Count; i++)
            {
                var sourcei = source[i];
                var targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    TotalCompared++;
                    var temp = Math.Abs(sourcei - targeti);
                    temp = temp / sourcei;
                    temp = temp * 100;
                    if (temp > tol)
                        failed++;
                }

            }
            Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            return failed;

        }

        private int Evaluate(ref DoseMatrix source, ref DoseMatrix target, double tol)
        {
            TotalCompared = 0;
            int failed = 0;
            double MaxSource = source.MaxPointDose.Dose;
            double MinDoseEvaluated = MaxSource * EpsilonTol;
            int debugCounter = 0;
            for (var i = target.X0; i <= target.XMax; i += target.XRes)
            {
                if (debugCounter % 10 == 0)
                {
                    Debug.Write(" " + debugCounter);
                }
                debugCounter++;
                for (var j = target.Y0; j <= target.YMax; j += target.YRes)
                {
                    for (var k = target.Z0; k <= target.ZMax; k += target.ZRes)
                    {
                        var targeti = target.GetPointDose(i, j, k);
                        if (targeti.Dose < MinDoseEvaluated) { continue; }
                        try
                        {
                            var sourcei = source.GetPointDose(targeti.X, targeti.Y, targeti.Z);
                            if (sourcei.Dose > MinDoseEvaluated && targeti.Dose > MinDoseEvaluated)
                            {   
                                TotalCompared++;
                                var temp = Math.Abs(sourcei.Dose - targeti.Dose);
                                temp = temp / sourcei.Dose;
                                temp = temp * 100;
                                if (temp > tol)
                                    failed++;
                            }
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                        }
                        
                    }
                }
            }
            Debug.WriteLine("");
            Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            return failed; 
        }

    }


}
