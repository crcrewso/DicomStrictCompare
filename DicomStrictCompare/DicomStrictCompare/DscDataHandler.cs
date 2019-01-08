using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            foreach (var pair in DosePairsList)
            {
                pair.Evaluate();
                ResultMessage += pair.ResultString + '\n';
                Debug.WriteLine(pair.ResultString);

            }



            
        }

        

    }


    /// <summary>
    /// Will contain the evaluation of any stored pair of DoseFiles
    /// </summary>
    class MatchedDosePair
    {
        public int TotalCount { get; private set; }
        public int TotalFailedEpsilonTol { get; private set; }
        public double PercentFailedEpsilonTol => PercentCalculator(TotalCount, TotalFailedEpsilonTol);
        public int TotalFailedTightTol { get; private set; }
        public double PercentFailedTightTol => PercentCalculator(TotalCount, TotalFailedTightTol);
        public int TotalFailedMainTol { get; private set; }
        public double PercentFailedMainTol => PercentCalculator(TotalCount, TotalFailedEpsilonTol);
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

        public string ResultString => Name + '\t' + TotalCount.ToString() + '\t' + TotalFailedEpsilonTol + '\t' + PercentFailedEpsilonTol.ToString("0.00") +'\t' + PercentFailedTightTol.ToString("0.00") + '\t' + PercentFailedMainTol.ToString("0.00");


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
            if (sourceDose.Count != targetDose.Count)
            { throw new DataMisalignedException("The Array Sizes don't match"); }
            if (_source.X != _target.X || _source.Y != _target.Y || _source.Z != _target.Z)
            { throw new DataMisalignedException("The Array Dimensions don't match"); }
            TotalCount = targetDose.Count;
            TotalFailedEpsilonTol = Evaluate(ref sourceDose, ref targetDose, EpsilonTol);
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
            for (int i = 0; i < target.Count; i++)
            {
                var temp = Math.Abs(source[i] - target[i]);
                temp = temp / source[i];
                temp = temp * 100;
                if (temp > tol)
                    failed++;
            }
            return failed;

        }

    }


}
