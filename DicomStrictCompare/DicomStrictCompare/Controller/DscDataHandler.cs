using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace DicomStrictCompare
{
    /// <summary>
    /// Holds the user entered data and builds the file list for the file handler
    /// </summary>
    class DscDataHandler
    {


        public string SourceDirectory { get; private set; }
        public string SourceAliasName { get; set; }
        public string TargetAliasName { get; set; }
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



        public void Run(bool runDoseComparisons, bool runPDDComparisons, string SaveDirectory, object sender)
        {
            double progress = 0;
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

            if (SourceListStrings.Length <= 0)
                throw new InvalidOperationException("There are no Dose files in the source directory Tree");
            if (TargetListStrings.Length <= 0)
                throw new InvalidOperationException("There are no Dose files in the Target directory Tree");
            #endregion

            (sender as BackgroundWorker).ReportProgress((int)progress, "Setup");
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
            sourceDoseProcess.Start();
            targetDoseProcess.Start();
            sourcePlanProcess.Start();
            targetPlanProcess.Start();
            sourceDoseProcess.Join();
            targetDoseProcess.Join();
            sourcePlanProcess.Join();
            targetPlanProcess.Join();

            DosePairsList = new List<MatchedDosePair>();
            ResultMessage = "Tight Tolerance, " + (100 * TightTol).ToString();
            ResultMessage += "\nMain Tolerance, " + (100 * MainTol).ToString();
            ResultMessage += "\nThreshold, " + (100 * ThresholdTol).ToString() + "\n";
            ResultMessage += MatchedDosePair.ResultHeader;

            progress += 5;

            (sender as BackgroundWorker).ReportProgress((int)progress, "Scanning Source Folder");
            Parallel.ForEach(SourceDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, doseFile =>

            {
                    doseFile.SetFieldName(SourcePlanList);
                
            });

            progress += 5;
            (sender as BackgroundWorker).ReportProgress((int)progress, "Scanning Target Folder");
            Parallel.ForEach(TargetDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, doseFile =>
            {
                doseFile.SetFieldName(TargetPlanList);
            } );


            double ProgressIncrimentor = 10.0 / TargetDosesList.Count;
            (sender as BackgroundWorker).ReportProgress((int)progress, "Matching");
            // match each pair for analysis
            Parallel.ForEach(TargetDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (dose) =>
            {
                progress += ProgressIncrimentor;
                progress = progress % 100;
                (sender as BackgroundWorker).ReportProgress((int)progress, "Matching");
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
            if (DosePairsList.Count <= 0)
                return;


            //fix for memory abuse is to limit the number of cores, Arbitrarily I have hard coded it to half the logical cores of the system.
            if (runDoseComparisons)
            {


                ProgressIncrimentor = 50.0 / DosePairsList.Count;
                Parallel.ForEach(DosePairsList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, pair =>
                {
                    progress += ProgressIncrimentor;
                    progress = progress % 100;
                    (sender as BackgroundWorker).ReportProgress((int)progress, "Comparing");
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
            progress = 70;
            ProgressIncrimentor = 30 / DosePairsList.Count;
            progress = progress % 100;
            (sender as BackgroundWorker).ReportProgress((int)progress, "PDD Production");
            if (runPDDComparisons)
            {
                Parallel.ForEach(DosePairsList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, pair =>
                {
                    progress += ProgressIncrimentor;
                    progress = progress % 100;
                    (sender as BackgroundWorker).ReportProgress((int)progress, "PDD Production");
                    pair.GeneratePDD();
                    Debug.WriteLine("Saving " + pair.ChartTitle + " to " + SaveDirectory);
                    SaveFile saveFile = new SaveFile(pair.ChartTitle, SaveDirectory);
                    saveFile.Save(pair.SourcePDD, pair.TargetPDD, pair.ChartFileName, SaveDirectory, pair.ChartTitle, SourceAliasName, TargetAliasName);

                });
            }

        }

        

    }


}
