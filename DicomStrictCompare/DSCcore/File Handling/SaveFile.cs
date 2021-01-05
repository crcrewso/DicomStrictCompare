using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing;
using EvilDICOM.RT;

namespace DicomStrictCompare
{
    /// <summary>
    /// Produces the 
    /// </summary>
    public class SaveFile
    {
        public string SaveFileName { get; }
        public string SaveFileDir { get; } 

        public SaveFile(string FileName, string FileDirectory)
        {
            if (Directory.Exists(FileDirectory) )
            {
                SaveFileDir = FileDirectory;
            }
            else
            {
                Directory.CreateDirectory(FileDirectory);
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
                throw new ArgumentNullException(nameof(csvMessage));
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
        /// <param name="chartTitleString"></param>
        /// <param name="SourceAlias"></param>
        /// <param name="TargetAlias"></param>
        /// <param name="location"></param>
        public static string Save(List<DoseValue> sourcePDD, List<DoseValue> targetPDD, string filename, string location, string chartTitleString, string SourceAlias = "Reference", string TargetAlias = "New Model")
        {

            #region Safety Check
            if (sourcePDD.Count != targetPDD.Count)
            {
                throw new ArgumentOutOfRangeException("The two lists don't have the same length!!!!\n" + chartTitleString);
            }
            if (sourcePDD.Count == 0 || targetPDD.Count == 0)
            {
                throw new ArgumentException("the lists are empty");
            }
            #endregion

            //Calculates Max Dose
            double maxDose = 0;
            foreach (DoseValue dose in sourcePDD) { maxDose = (dose.Dose > maxDose) ? dose.Dose : maxDose; }

            // Percent of PDD matching 1%/1mm
            double oneOne = ProfileTools.Comparison(sourcePDD, targetPDD, 1, 1);
            // Number of comparisions matching 1%/1mm
            int oneOneRaw = ProfileTools.ComparisonRaw(sourcePDD, targetPDD, 1, 1);


            List<double> z = new List<double>(); //List of plot positions
            List<double> doses0 = new List<double>(); // the list of doubles to plot for dose0
            List<double> doses1 = new List<double>(); // the list of doubles to plot for dose1

            // index of z to use to report the percent of peak metrics 
            var sourcePercent80 = ProfileTools.DepthToPercentOfPeak(sourcePDD, 80).Y - sourcePDD[0].Y;
            var sourcePercent50 = ProfileTools.DepthToPercentOfPeak(sourcePDD, 50).Y - sourcePDD[0].Y;
            var targetPercent80 = ProfileTools.DepthToPercentOfPeak(targetPDD, 80).Y - targetPDD[0].Y;
            var targetPercent50 = ProfileTools.DepthToPercentOfPeak(targetPDD, 50).Y - targetPDD[0].Y;
            
            //converts List<DoseValue> to List<double> for plotting
            // sets the x locations of each data point
            foreach (DoseValue item in sourcePDD)
            {
                z.Add(item.Y - sourcePDD[0].Y);
                doses0.Add(item.Dose);
            }

            foreach (DoseValue item in targetPDD)
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
            string strFormat = "0.00";
            string titleText = "";
            titleText += "         80%       50%";
            titleText += "\n" + SourceAlias + " \t"+sourcePercent80.ToString(strFormat) + " \t" + sourcePercent50.ToString(strFormat);
            titleText += "\n" + TargetAlias + " \t"+targetPercent80.ToString(strFormat) + " \t" + targetPercent50.ToString(strFormat);

            string analysis = "Pixels outside 1%/1mm," + Math.Round(oneOne, 1) + ",Raw, " + oneOneRaw + ",of," + sourcePDD.Count;


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

            SaveScottPlot(z.ToArray(), maxDose, doses0.ToArray(), SourceAlias, doses1.ToArray(), TargetAlias, titleText + '\n'+ analysis.Replace(',', ' ').Replace("Raw", "  Points:") , filename, location) ;
            return analysis;
        }

        public static void SaveScottPlot(double[] xIndexValues, double maxDose, double[] sourceDoses, string sourceAlias, double[] targetDoses, string targetAlias, string titleText, string filename, string location)
        {
            string longFileName = location + @"\" + filename;
            string longDirectory = longFileName.Substring(0, longFileName.LastIndexOf(@"\"));
            if (!(System.IO.Directory.Exists(longDirectory)))
                System.IO.Directory.CreateDirectory(longDirectory);

            var plt = new ScottPlot.Plot(1440, 900);

            plt.PlotScatter(xIndexValues, sourceDoses, label: sourceAlias);
            plt.PlotScatter(xIndexValues, targetDoses, label: targetAlias);

            plt.Legend(fixedLineWidth: false);
            plt.Title(filename + '\n' + titleText);
            plt.XLabel(@"Depth (mm)");
            plt.YLabel(@"Dose (Gy)");
            if (maxDose < 1.5 && maxDose > 0.1)
                plt.Axis(y1: 0, y2: 1.2);
            plt.SaveFig(longFileName + @".png");


        }
    }

}
