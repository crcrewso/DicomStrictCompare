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


    }

}
