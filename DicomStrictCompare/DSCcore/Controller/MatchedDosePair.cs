using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DicomStrictCompare.Model;
using EvilDICOM.RT;

namespace DicomStrictCompare
{
    /// <summary>
    /// Will contain the evaluation of any stored pair of DoseFiles
    /// </summary>
    public class MatchedDosePair
    {
        readonly Model.Dta[] _dtas;
        readonly SingleComparison[] _comparisons;
        public int TotalCount { get; private set; }
        public int TotalCompared { get; private set; } = 0;
        public int TotalFailed { get; private set; }
        public double PercentFailed => PercentCalculator(TotalCompared, TotalFailed);
 
        public string PDDoutString { get; set; } = "null";
        /// <summary>
        /// The matched pair has been evaluated to measure results
        /// </summary>
        public bool IsEvaluated { get; private set; } = false;



        /// <summary>
        /// Name of the pair evaluated
        /// </summary>
        public string Name => _source.PlanID + ',' + _source.FieldName;
            
        /// <summary>
        /// Filenames of the pair of files
        /// </summary>
        public string FileNames => _source.ShortFileName + ',' + _target.ShortFileName;

        public string ResultString => String.Join(",", ResultArray());
        public string ResultHeader => String.Join(",", ResultArrayHeaderRow0()) + "\n" + String.Join(",", ResultArrayHeaderRow1()) + "\n";

        public static string StaticResultHeader(Model.Dta[] dtas)
        {
            if (dtas == null) throw new ArgumentNullException(nameof(dtas));
            return String.Join(",", ResultArrayHeaderRow0(dtas)) + "\n" + String.Join(",", ResultArrayHeaderRow1(dtas)) + "\n";
        }

        private readonly DoseFile _source;
        private readonly DoseFile _target;

        public List<DoseValue> SourcePDD { get; private set; }
        public List<DoseValue> TargetPDD { get; private set; }
        public string ChartTitle { get; private set; }
        public string ChartFileName { get; private set; }

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

        string[] ResultArray()
        {
            List<string> ret = new List<string>
            {
                Name
            };
            for (int i = 0; i < _dtas.Length; i++)
            {
                ret.Add(_comparisons[i].PercentFailed.ToString("0.000"));
            }
            for (int i = 0; i < _dtas.Length; i++)
            {
                ret.Add(_comparisons[i].TotalCompared.ToString("0"));
            }
            for (int i = 0; i < _dtas.Length; i++)
            {
                ret.Add(_comparisons[i].TotalFailed.ToString("0.000"));
            }
            ret.Add(FileNames);
            ret.Add(_source.FieldMUs);
            ret.Add(_target.FieldMUs);
            ret.Add(PDDoutString);
            return ret.ToArray();
        }
        string[] ResultArrayHeaderRow0()
        {
            List<string> ret = new List<string>();
            ret.AddRange(Enumerable.Repeat(" ", 2));
            ret.Add("Percent Failed");
            ret.AddRange(Enumerable.Repeat(" ", _dtas.Length - 1));
            ret.Add("Total Compared");
            ret.AddRange(Enumerable.Repeat(" ", _dtas.Length - 1));
            ret.Add("Total Failed");
            ret.AddRange(Enumerable.Repeat(" ", _dtas.Length - 1));
            ret.AddRange(Enumerable.Repeat(" ", 4));

            return ret.ToArray();
        }
        static string[] ResultArrayHeaderRow0(Model.Dta[] dtas)
        {
            List<string> ret = new List<string>();
            ret.AddRange(Enumerable.Repeat(" ", 2));
            ret.Add("Percent Failed");
            ret.AddRange(Enumerable.Repeat(" ", dtas.Length - 1));
            ret.Add("Total Compared");
            ret.AddRange(Enumerable.Repeat(" ", dtas.Length - 1));
            ret.Add("Total Failed");
            ret.AddRange(Enumerable.Repeat(" ", dtas.Length - 1));
            ret.AddRange(Enumerable.Repeat(" ", 4));

            return ret.ToArray();
        }
        string[] ResultArrayHeaderRow1()
        {
            List<string> ret = new List<string>
            {
                "Plan Name",
                "Field Name"
            };
            for (int i = 0; i < 3*_dtas.Length; i++)
            {
                ret.Add(_dtas[i%_dtas.Length].ShortToString());
            }
            ret.Add("Source File Name,Target File Name");
            ret.Add("Source MUs");
            ret.Add( "Target MUs");
            return ret.ToArray();
        }

        static string[] ResultArrayHeaderRow1(Model.Dta[] dtas)
        {
            List<string> ret = new List<string>
            {
                "Plan Name",
                "Field Name"
            };
            for (int i = 0; i < 3 * dtas.Length; i++)
            {
                ret.Add(dtas[i % dtas.Length].ShortToString());
            }
            ret.Add("Source File Name,Target File Name");
            ret.Add("Source MUs");
            ret.Add("Target MUs");
            return ret.ToArray();
        }

        public MatchedDosePair(DoseFile source, DoseFile target, Controller.Settings settings)
        {
            if(settings == null) throw new ArgumentNullException(nameof(settings));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _dtas = settings.Dtas; 
            _comparisons = new SingleComparison[_dtas.Length];
            PDDoutString = "PDD's not run";
            ChartTitle = "PDD of " + _source.PlanID + @" " + _source.FieldName;
            ChartFileName = _source.PlanID + @"\" + _source.FieldName;
        }

        public void GeneratePDD()
        {
            RTDose sourceMatrix = _source.DoseMatrix();
            RTDose targetMatrix = _target.DoseMatrix();
            double yMin = (sourceMatrix.Y0 > targetMatrix.Y0) ? sourceMatrix.Y0 : targetMatrix.Y0;
            double yMax = (sourceMatrix.YMax < targetMatrix.YMax) ? sourceMatrix.YMax : targetMatrix.YMax;
            double yRes = sourceMatrix.YRes;
            EvilDICOM.Core.Helpers.Vector3 startPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMin, 0 );
            EvilDICOM.Core.Helpers.Vector3 endPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMax, 0);
            SourcePDD = sourceMatrix.GetLineDose(startPoint, endPoint, yRes);
            TargetPDD = targetMatrix.GetLineDose(startPoint, endPoint, yRes);


        }

        /// <summary>
        /// Performs the actual work not the most efficient way but I'll work it out
        /// </summary>
        public void Evaluate(IMathematics mathematics)
        {
            if (mathematics == null) throw new ArgumentNullException(nameof(mathematics));
            Model.DoseMatrixOptimal sourceDose = new Model.DoseMatrixOptimal(_source.DoseMatrix());
            Model.DoseMatrixOptimal targetDose = new Model.DoseMatrixOptimal(_target.DoseMatrix());
            TotalCount = targetDose.Length;
            Debug.WriteLine("\n\n\nEvaluating " + _source.FileName + " and " + _target.FileName + " Dimensions disagree");
            Debug.WriteLine("Max dose: Source - " + sourceDose.MaxPointDose.Dose + " Target - " + targetDose.MaxPointDose.Dose);
            Model.SingleComparison ret;
            for (int i = 0; i < _dtas.Length; i++)
            {
                if (_dtas[i].Relative)
                {
                    ret = mathematics.CompareRelative(sourceDose, targetDose, _dtas[i]);
                }
                else
                    ret = mathematics.CompareAbsolute(sourceDose, targetDose, _dtas[i]);
                _comparisons[i] = ret;
            }
            IsEvaluated = true;



        }




    }


}
