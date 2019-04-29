using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using EvilDICOM.RT;

namespace DicomStrictCompare
{
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
        /// TODO increase font 
        /// </summary>
        /// <param name="sourcePDD"></param>
        /// <param name="targetPDD"></param>
        /// <param name="filename"></param>
        /// <param name="title"></param>
        /// <param name="note"></param>
        public void Save(List<DoseValue> sourcePDD, List<DoseValue> targetPDD, string filename, string location, string chartTitleString, string SourceAlias = "Reference", string TargetAlias = "New Model")
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


            var titleFont = new Font("Consolas", 36, FontStyle.Regular);
            var axesFont = new Font("Consolas", 20, FontStyle.Regular);
            var subtitleFont = new Font("Consolas", 24, FontStyle.Regular);
            var chart = new Chart();
            chart.Size = new Size(3200, 1800);
            var chartTitle = new Title(chartTitleString, Docking.Top, titleFont, Color.Black);
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
            chartArea.AxisY2.Maximum = 6;
            chartArea.AxisY2.Interval = 1;

            chartArea.AxisX.Title = "Position [mm]";
            chartArea.AxisY.Title = "Dose ";
            chartArea.AxisY2.Title = "Percent of Max dose Diff";

            chart.ChartAreas.Add(chartArea);
            // font style and size declarations must be after the add Chart Area or else they are ignored. 
            chartArea.AxisX.TitleFont = axesFont;
            chartArea.AxisY.TitleFont = axesFont;
            chartArea.AxisY2.TitleFont = axesFont;
            chartArea.AxisX.LabelStyle.Font = axesFont;
            chartArea.AxisY.LabelStyle.Font = axesFont;
            chartArea.AxisY2.LabelStyle.Font = axesFont;

            var title2 = new Title("Results", Docking.Bottom, subtitleFont, Color.DarkBlue);
            title2.Text = "Pixels outside 1%/1mm = " + Math.Round(oneOne, 1) + " %\nRaw " + oneOneRaw + " of " + sourcePDD.Count;
            chart.Titles.Add(title2);
            /*
             /// Old logic for when the dose diff could be large. not necessary for this project
            double doseDiffMax = 0;
            for (int i = 5; i < doseDiff.Count; i++)
            {
                doseDiffMax = (doseDiff[i] > doseDiffMax) ? doseDiff[i] : doseDiffMax;
            }

            if (doseDiffMax > 10)
            {
                chartArea.AxisY2.Maximum = 120;
                chartArea.AxisY2.Interval = 20;
                chartArea.AxisY2.LabelStyle.Font = new Font("Consolas", 20, FontStyle.Italic);
            }
            */


            var series = new Series();
            series.Name = SourceAlias;
            series.ChartType = SeriesChartType.Line;
            series.XValueType = ChartValueType.Double;
            series.Color = Color.Blue;
            series.MarkerSize = 5;
            series.Points.DataBindXY(z, doses0);
            series.YAxisType = AxisType.Primary;
            chart.Series.Add(series);
            chart.ChartAreas[0].RecalculateAxesScale();
            var series1 = new Series();
            series1.Name = TargetAlias;
            series1.ChartType = SeriesChartType.Point;
            series1.MarkerSize = 4;
            series1.Color = Color.DarkGreen;
            series1.XValueType = ChartValueType.Double;
            series1.Points.DataBindXY(z, doses1);
            series1.YAxisType = AxisType.Primary;
            chart.Series.Add(series1);
            chart.ChartAreas[0].RecalculateAxesScale();
            var series2 = new Series();
            series2.Name = "Dose Difference (%)";
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
            chart.Legends[0].Font = subtitleFont;

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
