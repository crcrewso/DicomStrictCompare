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
using EvilDICOM.Core;
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
                var temp = new DoseFile(file);
                if (temp.IsDoseFile)
                {
                    Debug.WriteLine("Found Dose File " + temp.FileName);
                    doseFiles.Add(temp);
                }
            }
            return doseFiles;
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
        public string MatchIdentifier => PatientId + BeamNumber + SopInstanceId;
        /// <summary>
        /// Beam number 
        /// </summary>
        public string BeamNumber { get; }
        /// <summary>
        /// SOP Instance Identifier, hopefully this is the plan ID
        /// </summary>
        public string SopInstanceId { get;  }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }


        public DoseFile(string fileName)
        {
            var dcm1 = DICOMObject.Read(fileName);
            FileName = fileName;
            Name = dcm1.FindFirst(TagHelper.SeriesDescription).ToString();
            PatientId = dcm1.FindFirst(TagHelper.PatientID).ToString();

            SopInstanceId = SopInstanceIdClean(dcm1.FindFirst(TagHelper.ReferencedSOPInstanceUID ).ToString());
            if (dcm1.FindFirst(TagHelper.Modality).ToString().Contains("RTDOSE"))
            {
                IsDoseFile = true;
                BeamNumber = dcm1.FindFirst(TagHelper.ReferencedBeamNumber).ToString();
            }
        }

        /// <summary>
        /// Cleans the Reference SOP Instance Id as read in the dose file, of the date string at the end of the string. 
        /// </summary>
        /// <param name="sopInstanceId">The sop instance identifier.</param>
        /// <returns></returns>
         private string SopInstanceIdClean(string sopInstanceId)
        {
            int lastDecimal = sopInstanceId.LastIndexOf('.'); //index of decimal 
            return sopInstanceId.Substring(0, lastDecimal - 1); //substring before the decimal
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
}
