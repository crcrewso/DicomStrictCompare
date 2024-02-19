using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using DCSCore.Model;

namespace DCSCore
{
    /// <summary>
    /// Holds the user entered data and builds the file list for the file handler
    /// </summary>
    public class DscDataHandler
    {
        public Controller.Settings Settings { get; set; }
        /// <summary>
        /// results table 
        /// </summary>
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
        public ConcurrentBag<MatchedDosePair> DosePairsList { get; private set; }

        private IMathematics mathematics;


        public DscDataHandler()
        {
            DosePairsList = new ConcurrentBag<MatchedDosePair>();
            mathematics = new X86Mathematics();
        }


        public DscDataHandler(Controller.Settings settings)
        {
            this.Settings = settings;
            DosePairsList = new ConcurrentBag<MatchedDosePair>();
            mathematics = new X86Mathematics();
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



        public Controller.Results Run(bool runDoseComparisons, bool runPDDComparisons, string SaveDirectory, object sender)
        {
            // Maximum number of CPU threads
            ParallelOptions cpuParallel = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            //ParallelOptions cpuParallel = new ParallelOptions { MaxDegreeOfParallelism = 1 };

            ParallelOptions parallel = cpuParallel;
            var resultsStrings = new ConcurrentBag<string>();

            // Sets the system to use the correct resources without doubling down on GPU

            mathematics = new X86Mathematics();
            parallel = cpuParallel;


            double progress = 0;
            #region safetyChecks
            try
            {
                _ = SourceDirectory.IsNormalized();
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Source directory cannot be null");
            }

            try
            {
                _ = TargetDirectory.IsNormalized();
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

            #region setup
            ((BackgroundWorker)sender).ReportProgress((int)progress, "Setup: Scanning Source Doses ");
            SourceDosesList = FileHandler.DoseFiles(SourceListStrings);
            ((BackgroundWorker)sender).ReportProgress((int)progress, "Setup: Scanning Source Plans ");
            SourcePlanList = FileHandler.PlanFiles(SourceListStrings);
            ((BackgroundWorker)sender).ReportProgress((int)progress, "Setup: Scanning Target Doses ");
            TargetDosesList = FileHandler.DoseFiles(TargetListStrings);
            ((BackgroundWorker)sender).ReportProgress((int)progress, "Setup: Scanning Target Plans ");
            TargetPlanList = FileHandler.PlanFiles(TargetListStrings);


            #endregion


            DosePairsList = new ConcurrentBag<MatchedDosePair>();

            progress += 5;

            ((BackgroundWorker)sender).ReportProgress((int)progress, "Scanning Source Folder");
            _ = Parallel.ForEach(SourceDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, doseFile =>
              {
                  doseFile.SetFieldName(SourcePlanList);

              });

            progress += 5;
            ((BackgroundWorker)sender).ReportProgress((int)progress, "Scanning Target Folder");
            _ = Parallel.ForEach(TargetDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, doseFile =>
              {
                  doseFile.SetFieldName(TargetPlanList);
              });


            double ProgressIncrimentor = 10.0 / TargetDosesList.Count;
            ((BackgroundWorker)sender).ReportProgress((int)progress, "Matching");
            // match each pair for analysis
            _ = Parallel.ForEach(TargetDosesList, cpuParallel, (dose) =>
              {
                  progress += ProgressIncrimentor;
                  progress %= 100;
                  ((BackgroundWorker)sender).ReportProgress((int)progress, "Matching");
                  var sourceDose = SourceDosesList.Find(x => x.MatchIdentifier == dose.MatchIdentifier);
                  if (sourceDose != null)
                  {
                      Debug.WriteLine("matched " + dose.FileName + " and " + sourceDose.FileName);
                      DosePairsList.Add(new MatchedDosePair(sourceDose, dose, Settings));
                  }

              });
            if (DosePairsList.Count <= 0)
                return null;
            progress = 39;
            ProgressIncrimentor = 30.0 / DosePairsList.Count;
            progress %= 100;
            ((BackgroundWorker)sender).ReportProgress((int)progress, "PDD Production");
            //if (runPDDComparisons)
            if (true)
            {
                _ = Parallel.ForEach(DosePairsList, cpuParallel, pair =>
                {
                    progress += ProgressIncrimentor;
                    progress %= 100;
                    ((BackgroundWorker)sender).ReportProgress((int)progress, "PDD Production");
                    pair.GeneratePDD();
                    Debug.WriteLine("Saving " + pair.ChartTitle + " to " + SaveDirectory);
                    try
                    {
                        SaveFile saveFile = new SaveFile(pair.ChartTitle, SaveDirectory);
                        if (pair.IsReasonablyCentered())
                            pair.PDDoutString = saveFile.Save(pair.SourcePDD, pair.TargetPDD, pair.ChartFileName, SaveDirectory, pair.ChartTitle, SourceAliasName, TargetAliasName);
                        else
                            pair.PDDoutString = "did not run";
                    }
                    catch (Exception)
                    {

                        Debug.WriteLine("Failed to save " + pair.ChartTitle);
                    }
                });
                progress = 69;
            }
            //fix for memory abuse is to limit the number of cores, Arbitrarily I have hard coded it to half the logical cores of the system.
            if (runDoseComparisons)
            {

                ProgressIncrimentor = 30.0 / DosePairsList.Count;
                _ = Parallel.ForEach(DosePairsList, parallel, pair =>
                  {
                      progress += ProgressIncrimentor;
                      progress %= 100;
                      ((BackgroundWorker)sender).ReportProgress((int)progress, "Comparing " + progress);
                      try
                      {
                          pair.Evaluate(mathematics);
                          resultsStrings.Add(pair.ResultString);
                      }
                      // Will catch array misalignment problems
                      catch (Exception e)
                      {
                          string temp = pair.Name + ",Was not Evaluated ,\n";
                          resultsStrings.Add(temp);
                          Debug.WriteLine(temp);
                          Debug.WriteLine(e.Message.ToString() ?? "\n");
                          Debug.Write(e.StackTrace.ToString() ?? "\n");

                      }


                  });
            }

            

            return new Controller.Results(SourceAliasName, TargetAliasName, resultsStrings.ToArray(), MatchedDosePair.StaticResultHeader(Settings.Dtas));

        }


    }


}
