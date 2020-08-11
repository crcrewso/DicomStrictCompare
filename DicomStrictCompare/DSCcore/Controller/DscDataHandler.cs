using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using DicomStrictCompare.Model;

namespace DicomStrictCompare
{
    /// <summary>
    /// Holds the user entered data and builds the file list for the file handler
    /// </summary>
    public class DscDataHandler
    {
        /// <summary>
        /// Global immutable object specifying all settings parameters the user entered. 
        /// </summary>
        public Controller.DSCUserSettings Settings { get; set; }
        /// <summary>
        /// Directory containing reference or source files. Subdirectories will be searched
        /// </summary>
        public string SourceDirectory { get; private set; }
        /// <summary>
        /// Human Readable identifier user specifies for outputs
        /// </summary>
        public string SourceAliasName { get; set; }
        /// <summary>
        /// Human Readable identifier user specifies for outputs
        /// </summary>
        public string TargetAliasName { get; set; }
        /// <summary>
        /// Directory containing target files. Subdirectories will be searched
        /// </summary>
        public string TargetDirectory { get; private set; }
        /// <summary>
        /// List of all Dicom Files in source director
        /// </summary>
        public string[] SourceListStrings { get; private set; }

        /// <summary>
        /// List of all Dicom Files in target directory
        /// </summary>
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

        /// <summary>
        /// Increasingly inaccurate name for the csv separated results from the analysis, 
        /// includes error messages for matches that didn't work. 
        /// </summary>
        public string ResultMessage { get; private set; }

        /// <summary>
        /// Argument Free constructor
        /// </summary>
        public DscDataHandler()
        {
            DosePairsList = new ConcurrentBag<MatchedDosePair>();
            mathematics = new X86Mathematics();
        }

        /// <summary>
        /// Constructor that accepts settings file and prepares accordingly
        /// </summary>
        /// <param name="settings"></param>
        public DscDataHandler(DicomStrictCompare.Controller.DSCUserSettings settings)
        {
            this.Settings = settings;
            DosePairsList = new ConcurrentBag<MatchedDosePair>();
            mathematics = new X86Mathematics();
        }



        /// <summary>
        /// Generates the list of DICOM files to examine from the 'Source' location
        /// </summary>
        /// <param name="folder">Global location, all subfolders will be examined</param>
        /// <returns>The number of Files successfully identified</returns>
        public int CreateSourceList(string folder)
        {
            SourceDirectory = folder;
            SourceListStrings = FileHandler.LoadListRdDcmList(folder);
            return SourceListStrings.Length;
        }


        /// <summary>
        /// Generates the list of DICOM files to examine from the 'Target' location
        /// </summary>
        /// <param name="folder">Global location, all subfolders will be examined</param>
        /// <returns>The number of Files successfully identified</returns>
        public int CreateTargetList(string folder)
        {
            TargetDirectory = folder;
            TargetListStrings = FileHandler.LoadListRdDcmList(folder);
            return TargetListStrings.Length;
        }


        /// <summary>
        /// Runner function
        /// </summary>
        /// <param name="runDoseComparisons">True if User would like 3D Dose comparisons</param>
        /// <param name="runPDDComparisons">True if User would like PDD plots with comparisons</param>
        /// <param name="SaveDirectory">Location of all output files, some results will be nested into subfolders, data will be overwritten</param>
        /// <param name="sender"></param>
        public void Run(bool runDoseComparisons, bool runPDDComparisons, string SaveDirectory, object sender)
        {
            if (SaveDirectory == null)
                throw new ArgumentNullException(nameof(SaveDirectory));
            if (sender == null)
                throw new ArgumentNullException(nameof(sender));

            // Maximum number of CPU threads
            ParallelOptions cpuParallel = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            //ParallelOptions cpuParallel = new ParallelOptions { MaxDegreeOfParallelism = 1 };

            ParallelOptions parallel = cpuParallel;
            ResultMessage = "";

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
            (sender as BackgroundWorker).ReportProgress((int)progress, "Setup: Scanning Source Doses ");
            SourceDosesList = FileHandler.DoseFiles(SourceListStrings);
            (sender as BackgroundWorker).ReportProgress((int)progress, "Setup: Scanning Source Plans ");
            SourcePlanList = FileHandler.PlanFiles(SourceListStrings);
            (sender as BackgroundWorker).ReportProgress((int)progress, "Setup: Scanning Target Doses ");
            TargetDosesList = FileHandler.DoseFiles(TargetListStrings);
            (sender as BackgroundWorker).ReportProgress((int)progress, "Setup: Scanning Target Plans ");
            TargetPlanList = FileHandler.PlanFiles(TargetListStrings);


            #endregion


            DosePairsList = new ConcurrentBag<MatchedDosePair>();

            progress += 5;

            (sender as BackgroundWorker).ReportProgress((int)progress, "Scanning Source Folder");
            _ = Parallel.ForEach(SourceDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, doseFile =>
              {
                  doseFile.SetFieldName(SourcePlanList);

              });

            progress += 5;
            (sender as BackgroundWorker).ReportProgress((int)progress, "Scanning Target Folder");
            _ = Parallel.ForEach(TargetDosesList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, doseFile =>
              {
                  doseFile.SetFieldName(TargetPlanList);
              });


            double ProgressIncrimentor = 10.0 / TargetDosesList.Count;
            (sender as BackgroundWorker).ReportProgress((int)progress, "Matching");
            // match each pair for analysis
            _ = Parallel.ForEach(TargetDosesList, cpuParallel, (dose) =>
              {
                  progress += ProgressIncrimentor;
                  progress %= 100;
                  (sender as BackgroundWorker).ReportProgress((int)progress, "Matching");
                  var sourceDose = SourceDosesList.Find(x => x.MatchIdentifier == dose.MatchIdentifier);
                  if (sourceDose != null)
                  {
                      Debug.WriteLine("matched " + dose.FileName + " and " + sourceDose.FileName);
                      DosePairsList.Add(new MatchedDosePair(sourceDose, dose, Settings));
                  }

              });
            if (DosePairsList.Count <= 0)
                return;
            progress = 39;
            ProgressIncrimentor = 30.0 / DosePairsList.Count;
            progress %= 100;
            (sender as BackgroundWorker).ReportProgress((int)progress, "PDD Production");
            if (runPDDComparisons)
            {
                _ = Parallel.ForEach(DosePairsList, cpuParallel, pair =>
                {
                    progress += ProgressIncrimentor;
                    progress %= 100;
                    (sender as BackgroundWorker).ReportProgress((int)progress, "PDD Production");
                    pair.GeneratePDD();
                    Debug.WriteLine("Saving " + pair.ChartTitle + " to " + SaveDirectory);
                    try
                    {
                        //SaveFile saveFile = new SaveFile(pair.ChartTitle, SaveDirectory);
                        pair.PDDoutString = SaveFile.Save(pair.SourcePDD, pair.TargetPDD, pair.ChartFileName, SaveDirectory, pair.ChartTitle, SourceAliasName, TargetAliasName);

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
                ResultMessage += MatchedDosePair.StaticResultHeader(Settings.Dtas);
                _ = Parallel.ForEach(DosePairsList, parallel, pair =>
                  {
                      progress += ProgressIncrimentor;
                      progress %= 100;
                      (sender as BackgroundWorker).ReportProgress((int)progress, "Comparing " + progress);
                      try
                      {
                          pair.Evaluate(mathematics);
                          ResultMessage += pair.ResultString + '\n';
                      }
                      // Will catch array misalignment problems
                      catch (Exception e)
                      {
                          string temp = pair.Name + ",Was not Evaluated ,\n";
                          ResultMessage += temp;
                          Debug.WriteLine(temp);
                          Debug.WriteLine(e.Message.ToString());
                          Debug.Write(e.StackTrace.ToString());

                      }


                  });
            }

        }


    }


}
