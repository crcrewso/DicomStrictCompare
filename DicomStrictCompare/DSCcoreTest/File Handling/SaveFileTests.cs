using Microsoft.VisualStudio.TestTools.UnitTesting;
using DicomStrictCompare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScottPlot;

namespace DicomStrictCompare.Tests
{
    [TestClass()]
    public class SaveFileTests
    {
        [TestMethod()]
        public void SaveScottPlotTestFromWebsite()
        {
            string saveFileLocation = "C:\\Temp\\Quickstart_Quickstart_Scatter.png";
            var plt = new ScottPlot.Plot(600, 400);

            int pointCount = 51;
            double[] xs = DataGen.Consecutive(pointCount);
            double[] sin = DataGen.Sin(pointCount);
            double[] cos = DataGen.Cos(pointCount);

            plt.PlotScatter(xs, sin, label: "sin");
            plt.PlotScatter(xs, cos, label: "cos");
            plt.Legend();

            plt.Title("Scatter Plot Quickstart");
            plt.YLabel("Vertical Units");
            plt.XLabel("Horizontal Units");

            plt.SaveFig(saveFileLocation);

            Assert.IsTrue(System.IO.File.Exists(saveFileLocation));

            System.IO.File.Delete(saveFileLocation);
        }

        [TestMethod()]
        public void SaveScottPlotMyData()
        {
            string saveFileLocation = "C:\\Temp";
            string saveFileName = "Figone";
            string saveFileLongName = saveFileLocation + @"\" + saveFileName + @".png";
            int pointCount = 51;
            double[] xs = DataGen.Consecutive(pointCount);
            double[] sin = DataGen.Sin(pointCount);
            double[] cos = DataGen.Cos(pointCount);

            DicomStrictCompare.SaveFile.SaveScottPlot(xs, 1, sin, "sin", cos, "cos", "title", "subtitle", saveFileName, saveFileLocation);

            Assert.IsTrue(System.IO.File.Exists(saveFileLongName));

            //System.IO.File.Delete(saveFileLongName);

        }


    }
}