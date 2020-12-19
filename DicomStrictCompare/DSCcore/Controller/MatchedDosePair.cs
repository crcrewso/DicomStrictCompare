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

        /// <summary>
        /// Total voxels in source
        /// </summary>
        public int TotalCount { get; private set; }
        /// <summary>
        /// Total compared, as in total comparisons above threshhold 
        /// </summary>
        public int TotalCompared { get; private set; } = 0;
        /// <summary>
        /// Comparisons that have failed
        /// </summary>
        public int TotalFailed { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public double PercentFailed => PercentCalculator(TotalCompared, TotalFailed);
        /// <summary>
        /// Summary of results of PDD Comparison
        /// TODO: Replace this functionality 
        /// </summary>
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

        /// <summary>
        /// TODO Fix this
        /// </summary>
        public string ResultString => String.Join(",", ResultArray());
        /// <summary>
        /// TODO Fix this
        /// </summary>
        public string ResultHeader => String.Join(",", ResultArrayHeaderRow0()) + "\n" + String.Join(",", ResultArrayHeaderRow1()) + "\n";
        /// <summary>
        /// TODO fix this
        /// </summary>
        /// <param name="dtas">DTA settings for the comparisons </param>
        /// <returns></returns>
        public static string StaticResultHeader(Model.Dta[] dtas)
        {
            if (dtas == null) throw new ArgumentNullException(nameof(dtas));
            return String.Join(",", ResultArrayHeaderRow0(dtas)) + "\n" + String.Join(",", ResultArrayHeaderRow1(dtas)) + "\n";
        }

        private readonly DoseFile _source;
        private readonly DoseFile _target;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public List<DoseValue> SourcePDD { get; private set; }
        public List<DoseValue> TargetPDD { get; private set; }
        public string ChartTitle { get; private set; }
        public string ChartFileName { get; private set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member


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
            for (int i = 0; i < 3 * _dtas.Length; i++)
            {
                ret.Add(_dtas[i % _dtas.Length].ShortToString());
            }
            ret.Add("Source File Name,Target File Name");
            ret.Add("Source MUs");
            ret.Add("Target MUs");
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


        /// <summary>
        /// Complex Constructor
        /// </summary>
        /// <param name="source"> source or reference RT Dose file</param>
        /// <param name="target">Target RT Dose file</param>
        /// <param name="settings"> global settings</param>
        public MatchedDosePair(DoseFile source, DoseFile target, Controller.DSCUserSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _dtas = settings.Dtas;
            _comparisons = new SingleComparison[_dtas.Length];
            PDDoutString = "PDD's not run";
            ChartTitle = "PDD of " + _source.PlanID + @" " + _source.FieldName;
            ChartFileName = _source.PlanID + @"\" + _source.FieldName;
        }

        /// <summary>
        /// Generates PDD profile datasets for comparison and plotting
        /// </summary>
        public void GeneratePDD()
        {
            RTDose sourceMatrix = _source.DoseMatrix();
            RTDose targetMatrix = _target.DoseMatrix();
            double yMin = (sourceMatrix.Y0 > targetMatrix.Y0) ? sourceMatrix.Y0 : targetMatrix.Y0;
            double yMax = (sourceMatrix.YMax < targetMatrix.YMax) ? sourceMatrix.YMax : targetMatrix.YMax;
            double yRes = sourceMatrix.YRes;
            EvilDICOM.Core.Helpers.Vector3 startPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMin, 0);
            EvilDICOM.Core.Helpers.Vector3 endPoint = new EvilDICOM.Core.Helpers.Vector3(0, yMax, 0);
            try
            {
                SourcePDD = sourceMatrix.GetLineDose(startPoint, endPoint, yRes);
                TargetPDD = targetMatrix.GetLineDose(startPoint, endPoint, yRes);
            }
            catch (Exception)
            {

                double xMidSource = (sourceMatrix.XMax - sourceMatrix.X0) / 2;
                double zMidSource = (sourceMatrix.ZMax - sourceMatrix.Z0) / 2;

                if ((targetMatrix.XMax > xMidSource && xMidSource > targetMatrix.X0) && (targetMatrix.ZMax > zMidSource && zMidSource > targetMatrix.Z0))
                {
                    startPoint = new EvilDICOM.Core.Helpers.Vector3(xMidSource, yMin, zMidSource);
                    endPoint = new EvilDICOM.Core.Helpers.Vector3(xMidSource, yMax, zMidSource);
                    SourcePDD = sourceMatrix.GetLineDose(startPoint, endPoint, yRes);
                    TargetPDD = targetMatrix.GetLineDose(startPoint, endPoint, yRes);
                }
                else
                {
                    TargetPDD = new List<DoseValue>();
                    SourcePDD = new List<DoseValue>();
                    for (double pt = sourceMatrix.Y0; pt <= sourceMatrix.YMax; pt += sourceMatrix.YRes)
                    {
                        var tempDose = new DoseValue(0, pt, 0, 0);
                        TargetPDD.Add(tempDose);
                        SourcePDD.Add(tempDose);
                    }

                }
            }
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
