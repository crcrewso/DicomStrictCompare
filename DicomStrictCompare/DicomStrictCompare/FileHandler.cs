using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
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

    }

    /// <summary>
    /// Will contain the evaluation of any stored pair of DoseFiles
    /// </summary>
    class MatchedDosePair
    {
        public int TotalCount { get; private set; }
        public int TotalFailedEpsilonTol { get; private set; }
        public double PercentFailedEpsilonTol => PercentCalculator(TotalCount, TotalFailedEpsilonTol);
        public int TotalFailedTightTol { get; private set; }
        public double PercentFailedTightTol => PercentCalculator(TotalCount, TotalFailedTightTol);
        public int TotalFailedMainTol { get; private set; }
        public double PercentFailedMainTol => PercentCalculator(TotalCount, TotalFailedEpsilonTol);
        /// <summary>
        /// The matched pair has been evaluated to measure results
        /// </summary>
        public bool IsEvaluated { get; private set; } = false;
        /// <summary>
        /// Machine Tol for smallest difference between two doses
        /// </summary>
        public float EpsilonTol { get; }
        /// <summary>
        /// User controlled tight tolerance
        /// </summary>
        public float TightTol { get; }
        /// <summary>
        /// User controlled Main tolerance
        /// </summary>
        public float MainTol { get; }


        private DoseFile Source;
        private DoseFile Target;

        private static double PercentCalculator(int total, int failed)
        {
            return (((double)total - (double)failed) / (double)total) * 100.0;
        }


        public MatchedDosePair(DoseFile source, DoseFile target, float epsilonTol, float tightTol, float mainTol)
        {
            Source = source;
            Target = target;
            EpsilonTol = epsilonTol;
            TightTol = tightTol;
            MainTol = mainTol;
        }



        /// <summary>
        /// Performs the actual work not the most efficient way but I'll work it out
        /// </summary>
        public void Evaluate()
        {
            var sourceDose = Source.DoseValues();
            var targetDose = Target.DoseValues();
            if (sourceDose.Count != targetDose.Count)
            { throw new DataMisalignedException("The Array Sizes don't match"); }
            if (Source.X != Target.X || Source.Y != Target.Y || Source.Z != Target.Z)
            { throw new DataMisalignedException("The Array Dimensions don't match"); }
            TotalFailedEpsilonTol = Evaluate(ref sourceDose, ref targetDose, EpsilonTol);
            TotalFailedTightTol = Evaluate(ref sourceDose, ref targetDose, TightTol);
            TotalFailedMainTol = Evaluate(ref sourceDose, ref targetDose, MainTol);
            IsEvaluated = true;
        }

        /// <summary>
        /// Calculates the actual # of failed comparisons given the tolerance
        /// TODO Optimize this it's bad, slowest possible implimentation here. 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="tol">maximum allowable percent difference between two dose values for the voxels to be considered equal</param>
        /// <returns></returns>
        private int Evaluate(ref List<double> source, ref List<double> target, float tol)
        {
            int failed = 0;
            for (int i = 0; i < target.Count; i++)
            {
                var temp = Math.Abs(source[i] - target[i]);
                temp = temp / source[i];
                temp = temp * 100;
                if (temp > tol)
                    failed++;
            }
            return failed;

        }

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
        public string PlanName { get; }
        /// <summary>
        /// Field Identifier
        /// </summary>
        public string FieldName { get; }
        /// <summary>
        /// Compound of other metaData to match against
        /// </summary>
        public string MatchIdentifier { get; }
        /// <summary>
        /// Beam number 
        /// </summary>
        public string BeamNumber { get; }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }


        public DoseFile(string fileName)
        {
            var dcm1 = DICOMObject.Read(fileName);
            FileName = fileName;
            Name = dcm1.FindFirst(TagHelper.SeriesDescription).ToString();
            PatientId = dcm1.FindFirst(TagHelper.PatientID).ToString();
            BeamNumber = dcm1.FindFirst(TagHelper.ReferencedBeamNumber).ToString();
            if (dcm1.FindFirst(TagHelper.Modality).ToString() == "RTDOSE")
            {
                IsDoseFile = true;
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
    }
}
