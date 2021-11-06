using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfileBatchCompare.Model
{
    /// <summary>
    /// Reimplimentation of the SNCProfiler class 
    /// 
    /// This is currently a mix of how to parse IC profiler files and Profiler 2
    /// </summary>
    internal class SNCProfile
    {
        bool is_IC_profiler = false;
        bool is_Profiler2 = false;
        Controller.TextFileImport sourceFile;
        string[] raw_data;
        int detector_row = 107;
        int bias_row = 108;
        int calibration_row = 109;
        int data_row = -1;
        int data_column_start = 5;
        int data_column_end = 259;
        string[] detectors = null;
        double[] biases = null;
        double[] calibrations = null;
        double timetic;
        double integrated_dose;
        double[] doses = null;

        double[] Doses { get { return doses; } }
        string[] Detectors { get { return detectors; } }
        //double[] Data { get { return data; } }

        public double[] X { get { return doseVector("X"); } }
        public double[] Y { get { return doseVector("Y"); } }
        public double[] Positive_Diagonal { get { return doseVector("PD"); } }
        public double[] Negative_Diagonal { get { return doseVector("ND"); } }
        double[] doseVector(string parse_string)
        {
            List<double> ret = new List<double>();
            for (int i = 0; i < doses.Length; i++)
            {
                if (detectors[i].ToLower().Contains(parse_string.ToLower()))
                    ret.Add(doses[i]);
            }
            return ret.ToArray();
        }

        internal SNCProfile(Controller.TextFileImport textFile)
        {
            if (textFile == null)
                throw new ArgumentNullException("text");
            if (textFile.FileType != Model.Dictionaries.textFileType.SNCprofile)
                throw new ArgumentException(message: textFile.FileType.ToString(), "Not of supported file type");
            sourceFile = textFile;
            if (sourceFile.Contents.Contains("IC PROFILER"))
                is_IC_profiler = true;
            else
                is_Profiler2 = true;

            
            raw_data = sourceFile.Contents.ToArray();

            if(is_IC_profiler)
                parse_IC_profiler();
            else if (is_Profiler2)
                parse_profiler2();
        }

        void parse_IC_profiler()
        {
            throw new NotImplementedException();
            /*
            foreach (var row in raw_data)
            {
                double[] temp_row;
                if (row.Contains("Data"))
                {
                    TrimArray(raw_data[data_row].Split('\t').ToArray(), out temp_row, data_column_start, data_column_end);
                    data.Add(temp_row);
                }


            }
            */
        }

        void parse_profiler2()
        {
            List<double[]> data = new List<double[]>();
            TrimArray(raw_data[detector_row].Split('\t').ToArray(), out detectors, data_column_start, data_column_end);
            TrimArray(raw_data[bias_row].Split('\t').ToArray(), out biases, data_column_start, data_column_end);
            TrimArray(raw_data[calibration_row].Split('\t').ToArray(), out calibrations, data_column_start, data_column_end);


            
            timetic = float.Parse(raw_data[bias_row].Split('\t')[2]);
            double[] temp = biases.Select(r => r * timetic).ToArray();
            temp = temp.Zip(data, (x, y) => x - y).ToArray();
            doses = temp.Zip(calibrations, (x, y) => x * y).ToArray();
            integrated_dose = temp.Sum(x => x);
        }

        /// <summary>
        /// Reduces an array of raw data to a usable array of interesting data based upon how SNCPatient rows are structured
        /// 
        /// </summary>
        /// <typeparam name="T">type of data being parsed</typeparam>
        /// <param name="input">source array</param>
        /// <param name="output">data output array</param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <exception cref="System.ArgumentNullException"></exception>
        static void TrimArray(string[] input, out double[] output, int startIndex, int endIndex)
        {
            if (input == null)
                throw new System.ArgumentNullException("input");
            double[] temp_array, outarray = null;
            temp_array = Array.ConvertAll(input, s => double.Parse(s));
            Array.Copy(sourceArray: temp_array, startIndex, destinationArray: outarray, 0, endIndex - startIndex);
            output = outarray;
        }
        static void TrimArray(string[] input, out string[] output, int startIndex, int endIndex)
        {
            if (input == null)
                throw new System.ArgumentNullException("input");
            string[] temp_array, outarray = null;
            temp_array = Array.ConvertAll(input, s => s);
            Array.Copy(sourceArray: temp_array, startIndex, destinationArray: outarray, 0, endIndex - startIndex);
            output = outarray;
        }
    }
}
