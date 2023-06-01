using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSClibrary
{
    public class ParseSNC
    {
        public ParseSNC(string file, Device device)
        {
            Device = device;
            if (string.IsNullOrEmpty(file))
                throw new ArgumentNullException();
            if (Device != Device.ICProfiler)
            {
                throw new ArgumentException("Only IC Profiler is Implimented");
            }
            Filename = Path.GetFileName(file);
            FilePath = Path.GetFullPath(file);
            string[] lines = System.IO.File.ReadAllLines(file);
            if (lines.Length < 2)
                throw new FileLoadException("File is either empty or unreadable");
            if (!lines[lines.Length - 2].StartsWith("Data:"))
                throw new ArgumentException("File is not a IC Profiler file");
            switch (Device)
            {
                case Device.ICProfiler:
                    Profile = new SNCICProfiles(lines[lines.Length - 2]);
                    FileFormat = FileExtension.prm;
                    break;
                default:
                    throw new ArgumentException("File is not supported format");

            }

        }


        public Device Device { get; }
        public string Filename { get; init; }
        public string FilePath { get; init; }
        public FileExtension FileFormat { get; init; }
        public SNCICProfiles Profile { get; init; }


    }

    public record SNCICProfiles
    {
        public int CaxCount { get; init; }
        List<int> xAxis;
        List<int> yAxis;
        List<int> posDiag;
        List<int> negDiag;

        public double[] XAxis { get; init; }
        public double[] YAxis { get; init; }
        public double[] PosDiag { get; init; }
        public double[] NegDiag { get; init; }


        public SNCICProfiles(string data)
        {

            int xstart = 5;
            int ystart = 68;
            int posDiagStart = 132;
            int negDiagStart = 196;
            int end = 259;
            string[] array = data.Split('\t');
            xAxis = new List<int>();
            yAxis = new List<int>();
            posDiag = new List<int>();
            negDiag = new List<int>();
            CaxCount = Int32.Parse(array[36]);
            for (int i = xstart; i < ystart; i++)
                xAxis.Add(Int32.Parse(array[i]));
            for (int i = ystart; i < posDiagStart; i++)
                yAxis.Add(Int32.Parse(array[i]));
            for (int i = posDiagStart; i < negDiagStart; i++)
                posDiag.Add(Int32.Parse(array[i]));
            for (int i = negDiagStart; i < end; i++)
                negDiag.Add(Int32.Parse(array[i]));
            XAxis = Normalize(xAxis);
            YAxis = Normalize(yAxis);
            PosDiag = Normalize(posDiag);
            NegDiag = Normalize(negDiag);
        }

        /// <summary>
        /// Converts the parsed array to normalized profile
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        internal double[] Normalize(List<int> list)
        {
            double[] ret = new double[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ret[i] = (double)list[i] / (double)CaxCount;
            }


            return ret;
        }

    }

}
