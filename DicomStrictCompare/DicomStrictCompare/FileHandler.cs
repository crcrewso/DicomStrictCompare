using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using EvilDICOM.Core;
using EvilDICOM.Core.Element;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Modules;
using EvilDICOM.RT;

namespace DicomStrictCompare
{
    /// <summary>
    /// Take the list of source and target dicom files
    /// clean list removing non-dose files
    /// compare files to find matches
    /// report target files with no known source match
    /// provide results back to view 
    /// 
    /// 
    /// </summary>
    class FileHandler
    {

        public static List<DoseFile> DoseFiles(string[] listOfFiles)
        {
            List<DoseFile> doseFiles = new List<DoseFile>();
            foreach (var file in listOfFiles)
            {
                var temp = new DicomFile(file);
                if (temp.IsDoseFile)
                {
                    var tempDose = new DoseFile(file);
                    Debug.WriteLine("Found Dose File " + tempDose.FileName);
                    doseFiles.Add(tempDose);
                }
            }
            return doseFiles;
        }

        public static List<PlanFile> PlanFiles(string[] listOfFiles)
        {
            List<PlanFile> planFiles = new List<PlanFile>();
            foreach (var file in listOfFiles)
            {
                var temp = new DicomFile(file);
                if (temp.IsPlanFile)
                {
                    var tempPlan = new PlanFile(file);
                    Debug.WriteLine("Found Plan File " + tempPlan.FileName);
                    planFiles.Add(tempPlan);
                }
            }
            return planFiles;
        }


        public static string[] LoadListRdDcmList(string folder) => Directory.GetFiles(folder, "*.dcm", SearchOption.AllDirectories);
    }


    /// <summary>
    /// Parsses all of the MetaData from a dicom file, and if a dose file, allows access to the dose information through methods
    /// </summary>
    class DoseFile
    {
        /// <summary>
        /// full filename including address
        /// </summary>
        public string FileName { get; }
        public string ShortFileName { get; }
        /// <summary>
        /// bool, true when modality is RTDOSE
        /// </summary>
        public bool IsDoseFile { get; } = false;
        /// <summary>
        /// Series Description
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Hospital Patient ID
        /// </summary>
        public string PatientId { get; }
        /// <summary>
        /// Plan Identifier
        /// </summary>
        public string PlanID { get; private set; }
        /// <summary>
        /// Field Identifier
        /// </summary>
        public string FieldName { get; private set; }

        /// <summary>
        /// Compound of other metaData to match against
        /// </summary>
        //public string MatchIdentifier => PatientId + BeamNumber + SopInstanceId;
        public string MatchIdentifier => PatientId + PlanID + FieldName;
        /// <summary>
        /// Beam number 
        /// </summary>
        public string BeamNumber { get; }
        /// <summary>
        /// SOP Instance Identifier of the source plan ID. 
        /// </summary>
        public string SopInstanceId { get; }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }


        public DoseFile(string fileName)
        {
            var dcm1 = DICOMObject.Read(fileName);
            FileName = fileName;
            var slashindex = FileName.LastIndexOf(@"\");
            slashindex = FileName.Substring(0, slashindex - 1).LastIndexOf(@"\");
            slashindex = FileName.Substring(0, slashindex - 1).LastIndexOf(@"\");
            ShortFileName = FileName.Substring(slashindex + 1);
            Name = dcm1.FindFirst(TagHelper.SeriesDescription).ToString();
            PatientId = dcm1.FindFirst(TagHelper.PatientID).DData.ToString();

            var tempSopInstanceId = dcm1.FindFirst(TagHelper.ReferencedSOPInstanceUID);
            SopInstanceId = tempSopInstanceId.DData.ToString();
            if (dcm1.FindFirst(TagHelper.Modality).ToString().Contains("RTDOSE"))
            {
                IsDoseFile = true;

                try
                {
                    BeamNumber = dcm1.FindFirst(TagHelper.ReferencedBeamNumber).DData.ToString();

                }
                catch (NullReferenceException)
                {

                    BeamNumber = "0";
                }
            }
        }

        public void SetFieldName(List<PlanFile> planFiles)
        {
            foreach (var plan in planFiles)
            {
                if (plan.SopInstanceId == SopInstanceId)
                {
                    PlanID = plan.PlanID;
                    foreach (var beam in plan.FieldNumberToNameList)
                    {
                        if (beam.Item1 == BeamNumber)
                        {
                            FieldName = beam.Item2;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// If the file is a Dicom Dose file then the list of doses is returned.
        /// This is implemented as a method as a mitigation against memory abuse. 
        /// </summary>
        /// <returns>List of Doses from the dicom file</returns>
        /// <exception cref="InvalidOperationException"> Thrown if the file is not a dose file</exception>
        public List<double> DoseValues()
        {
            if (IsDoseFile)
            {
                var dcm1 = DICOMObject.Read(FileName);
                DoseMatrix dcmMatrix = new DoseMatrix(dcm1);
                X = dcmMatrix.DimensionX;
                Y = dcmMatrix.DimensionY;
                Z = dcmMatrix.DimensionZ;
                return dcmMatrix.DoseValues;
            }
            else
            {
                throw new InvalidOperationException("Cannot call for dose on a Dicom file that is not a dose file");
            }
        }

        /// <summary>
        /// Returns the Dose Matrix object for advanced evaluation
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Cannot call for dose on a Dicom file that is not a dose file</exception>
        public DoseMatrix DoseMatrix()
        {
            if (IsDoseFile)
            {
                var dcm1 = DICOMObject.Read(FileName);
                DoseMatrix dcmMatrix = new DoseMatrix(dcm1);
                X = dcmMatrix.DimensionX;
                Y = dcmMatrix.DimensionY;
                Z = dcmMatrix.DimensionZ;
                return dcmMatrix;
            }
            else
            {
                throw new InvalidOperationException("Cannot call for dose on a Dicom file that is not a dose file");
            }
        }

    }

    class DicomFile
    {
        public bool IsPlanFile { get; }
        public bool IsDoseFile { get; }

        public DicomFile(string fileName)
        {

            var dcm1 = DICOMObject.Read(fileName);
            if (dcm1.FindFirst(TagHelper.Modality).ToString().Contains("RTDOSE"))
            {
                IsDoseFile = true;
            }
            else if (dcm1.FindFirst(TagHelper.Modality).ToString().Contains("RTPLAN"))
            {
                IsPlanFile = true;
            }
            else
            {
                return;
            }
        }
    }

    class PlanFile
    {
        public List<Tuple<string, string>> FieldNumberToNameList { get; }
        public string FileName { get; }
        public string ShortFileName { get; }
        /// <summary>
        /// Plan Label is the Dicom Equivalent of the Plan ID in Eclipse
        /// </summary>
        public string PlanID { get; }
        public string SopInstanceId { get; }
        public string PatientID { get; }
        public bool IsPlanFile { get; }

        public PlanFile(string fileName)
        {
            FieldNumberToNameList = new List<Tuple<string, string>>();
            FileName = fileName;
            ShortFileName = FileName.Substring(FileName.LastIndexOf(@"\"));
            var dcm1 = DICOMObject.Read(fileName);
            SopInstanceId = dcm1.FindFirst(TagHelper.SOPInstanceUID).DData.ToString();
            PatientID = dcm1.FindFirst(TagHelper.PatientID).DData.ToString();
            if (dcm1.FindFirst(TagHelper.Modality).ToString().Contains("RTPLAN"))
            {
                IsPlanFile = true;
                PlanID = dcm1.FindFirst(TagHelper.RTPlanLabel).DData.ToString();
                var beamNumbers = dcm1.FindAll(TagHelper.BeamNumber);
                var beamNames = dcm1.FindAll(TagHelper.BeamName);
                if (beamNames.Count == beamNumbers.Count)
                {
                    for (int i = 0; i < beamNames.Count; i++)
                    {
                        FieldNumberToNameList.Add(new Tuple<string, string>(beamNumbers[i].DData.ToString(), beamNames[i].DData.ToString()));
                    }
                }
            }

        }
    }

    /// <summary>
    /// Produces the 
    /// </summary>
    class SaveFile
    {
        public string SaveFileName { get; } = null;
        public string SaveFileDir { get; } = null;

        public SaveFile(string FileName, string FileDirectory)
        {
            if (Directory.Exists(FileDirectory))
            {
                SaveFileDir = FileDirectory;
            }

            if (!String.IsNullOrEmpty(SaveFileDir))
            {
                SaveFileName = FileDirectory + '/' + FileName + ".csv";
            }
        }

        /// <summary>
        /// Adds the provided csv Message to the existing file. 
        /// </summary>
        /// <param name="csvMessage">Comma separated value message to be saved </param>
        /// <returns>true iff the save was successful</returns>
        public void Save(string csvMessage)
        {

            if (String.IsNullOrEmpty(SaveFileName) || String.IsNullOrEmpty(SaveFileDir))
            {
                throw new FileLoadException("No Valid Location to open");
            }
            if (String.IsNullOrEmpty(csvMessage))
            {
                throw new ArgumentNullException("I have no data to save");
            }
            StreamWriter outfile = new StreamWriter(SaveFileName);
            outfile.Write(csvMessage);
            outfile.Close();
        }


        /// <summary>
        /// This will only save PDD's 
        /// </summary>
        /// <param name="sourcePDD"></param>
        /// <param name="targetPDD"></param>
        /// <param name="filename"></param>
        /// <param name="title"></param>
        /// <param name="note"></param>
        public void Save(List<DoseValue> sourcePDD, List<DoseValue> targetPDD, string filename, string location, string chartTitleString)
        {
            double maxDose = 0;
            foreach (var dose in sourcePDD) { maxDose = (dose.Dose > maxDose) ? dose.Dose : maxDose; }

            if (sourcePDD.Count != targetPDD.Count)
            {
                throw new ArgumentOutOfRangeException("The two lists don't have the same length!!!!");
            }
            if (sourcePDD.Count == 0 || targetPDD.Count == 0)
            {
                throw new ArgumentException("the lists are empty");
            }




            double oneOne = ProfileTools.Comparison(sourcePDD, targetPDD, 1, 1);
            int oneOneRaw = ProfileTools.ComparisonRaw(sourcePDD, targetPDD, 1, 1);

            List<double> z = new List<double>();
            List<double> doses0 = new List<double>(); // the list of doubles to plot for dose0
            List<double> doses1 = new List<double>(); // the list of doubles to plot for dose1


            //converts List<DoseValue> to List<double> for plotting
            // sets the x locations of each data point
            foreach (var item in sourcePDD)
            {
                z.Add(item.Y - sourcePDD[0].Y);
                doses0.Add(item.Dose);
            }

            foreach (var item in targetPDD)
            {
                doses1.Add(item.Dose);
            }

            // removes negative doses
            for (int i = 0; i < sourcePDD.Count; i++)
            {
                if (doses0[i] < 0)
                {
                    doses0[i] = 0;
                }
                if (doses1[i] < 0)
                {
                    doses1[i] = 0;
                }
            }


            //produces the list of differences to plot
            List<double> doseDiff = new List<double>();
            for (int i = 0; i < sourcePDD.Count; i++)
            {
                if (doses0[i] < 0.01) { doseDiff.Add(0); continue; } // cleans up the graphs
                double temp = doses1[i] - doses0[i];
                temp /= maxDose;
                temp *= 100;
                temp = Math.Abs(temp);
                if (temp > 1000) { temp = 1000; } // needed so the variable doesn't overload chart
                if (temp < 0.1) { temp = 0; } // so the floor isn't so noisy
                doseDiff.Add(temp);
            }



            var chart = new Chart();
            chart.Size = new Size(3200, 1800);
            var chartTitle = new Title(chartTitleString, Docking.Top, new Font("Consolas", 36, FontStyle.Regular), Color.Black);
            chart.Titles.Add(chartTitle);
            var chartArea = new ChartArea();
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.Black;
            chartArea.AxisY2.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorTickMark.Interval = 0.1;
            chartArea.AxisX.IntervalType = System.Windows.Forms.DataVisualization.Charting.DateTimeIntervalType.Number;
            chartArea.AxisY.IntervalType = System.Windows.Forms.DataVisualization.Charting.DateTimeIntervalType.Number;
            chartArea.AxisX.Minimum = 0;
            chartArea.AxisX.Maximum = (300.0 < z.Last()) ? 300 : z.Last();

            chartArea.AxisY.Minimum = 0;
            chartArea.AxisY.Maximum = 1.20;
            chartArea.AxisY.Interval = 0.20;

            chartArea.AxisY2.Minimum = 0;
            chartArea.AxisY2.Maximum = 12;
            chartArea.AxisY2.Interval = 2;

            chartArea.AxisX.Title = "Position [mm]";
            chartArea.AxisY.Title = "Dose ";
            chartArea.AxisY2.Title = "Percent of Max dose Diff";

            chart.ChartAreas.Add(chartArea);
            // font style and size declarations must be after the add Chart Area or else they are ignored. 
            chartArea.AxisX.TitleFont = new Font("Consolas", 20, FontStyle.Regular);
            chartArea.AxisY.TitleFont = new Font("Consolas", 20, FontStyle.Regular);
            chartArea.AxisY2.TitleFont = new Font("Consolas", 20, FontStyle.Regular);
            chartArea.AxisX.LabelStyle.Font = new Font("Consolas", 20, FontStyle.Regular);
            chartArea.AxisY.LabelStyle.Font = new Font("Consolas", 20, FontStyle.Regular);
            chartArea.AxisY2.LabelStyle.Font = new Font("Consolas", 20, FontStyle.Regular);

            var title2 = new Title("Results", Docking.Bottom, new Font("Consolas", 20, FontStyle.Regular), Color.DarkBlue);
            title2.Text = "Pixels outside 1%/1mm = " + Math.Round(oneOne, 1) + " %\nRaw " + oneOneRaw + " of " + sourcePDD.Count;
            chart.Titles.Add(title2);

            double doseDiffMax = 0;
            for (int i = 3; i < doseDiff.Count; i++)
            {
                doseDiffMax = (doseDiff[i] > doseDiffMax) ? doseDiff[i] : doseDiffMax;
            }

            if (doseDiffMax > 20)
            {
                chartArea.AxisY2.Maximum = 120;
                chartArea.AxisY2.Interval = 20;
                chartArea.AxisY2.LabelStyle.Font = new Font("Consolas", 20, FontStyle.Italic);
            }



            var series = new Series();
            series.Name = "Reference";
            series.ChartType = SeriesChartType.Line;
            series.XValueType = ChartValueType.Double;
            series.Color = Color.Blue;
            series.MarkerSize = 5;
            series.Points.DataBindXY(z, doses0);
            series.YAxisType = AxisType.Primary;
            chart.Series.Add(series);
            chart.ChartAreas[0].RecalculateAxesScale();
            var series1 = new Series();
            series1.Name = "New Model";
            series1.ChartType = SeriesChartType.Point;
            series1.MarkerSize = 4;
            series1.Color = Color.DarkGreen;
            series1.XValueType = ChartValueType.Double;
            series1.Points.DataBindXY(z, doses1);
            series1.YAxisType = AxisType.Primary;
            chart.Series.Add(series1);
            chart.ChartAreas[0].RecalculateAxesScale();
            var series2 = new Series();
            series2.Name = "DoseDiff %";
            series2.ChartType = SeriesChartType.Line;
            series2.XValueType = ChartValueType.Double;
            series2.Color = Color.DarkRed;
            series2.Points.DataBindXY(z, doseDiff);
            series2.YAxisType = AxisType.Secondary;
            chart.Series.Add(series2);
            chart.ChartAreas[0].RecalculateAxesScale();
            chart.Legends.Add(new Legend("Legend"));
            chart.Legends[0].Docking = Docking.Bottom;
            chart.Legends[0].LegendStyle = LegendStyle.Row;
            chart.Legends[0].TitleAlignment = StringAlignment.Center;
            chart.Invalidate();
            chart.Update();
            string longFileName = location + @"\\" + filename;
            string longDirectory = longFileName.Substring(0, longFileName.LastIndexOf(@"\"));
            System.IO.Directory.CreateDirectory(longDirectory);
            chart.SaveImage(longFileName + ".emf", format: ChartImageFormat.EmfPlus);
            chart.SaveImage(longFileName + ".png", format: ChartImageFormat.Png);
            Debug.WriteLine("Finished saving " + filename);
        }


        


    }

}
