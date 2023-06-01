using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using EvilDICOM.RT;

namespace DCSCore
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
    public class FileHandler
    {

        

        public static List<DoseFile> DoseFiles(string[] listOfFiles)
        {
            ConcurrentBag<DoseFile> doseFiles = new ConcurrentBag<DoseFile>();
            _ = Parallel.ForEach(listOfFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
              {
                  DicomFile temp = new DicomFile(file);
                  if (temp.IsDoseFile)
                  {
                      DoseFile tempDose = new DoseFile(file);
                      Debug.WriteLine("Found Dose File " + tempDose.FileName);
                      doseFiles.Add(tempDose);
                  }
              });
            return new List<DoseFile>( doseFiles);
        }

        public static List<PlanFile> PlanFiles(string[] listOfFiles)
        {
            ConcurrentBag<PlanFile> planFiles = new ConcurrentBag<PlanFile>();
            _ = Parallel.ForEach(listOfFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
              {
                  DicomFile temp = new DicomFile(file);
                  if (temp.IsPlanFile)
                  {
                      PlanFile tempPlan = new PlanFile(file);
                      Debug.WriteLine("Found Plan File " + tempPlan.FileName);
                      planFiles.Add(tempPlan);
                  }
              });
            return new List<PlanFile>(planFiles);
        }


        public static string[] LoadListRdDcmList(string folder) => Directory.GetFiles(folder, "*.dcm", SearchOption.AllDirectories);
    }


    /// <summary>
    /// Parsses all of the MetaData from a dicom file, and if a dose file, allows access to the dose information through methods
    /// </summary>
    public class DoseFile
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

        public string FieldMUs { get; private set; }

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public DoseFile(string fileName)
        {
            if (fileName == null || fileName.Length < 1)
                throw new ArgumentNullException(nameof(fileName));
            DICOMObject dcm1 = DICOMObject.Read(fileName);
            FileName = fileName;
            int slashindex = FileName.LastIndexOf(@"\");
            slashindex = FileName.Substring(0, slashindex - 1).LastIndexOf(@"\");
            slashindex = FileName.Substring(0, slashindex - 1).LastIndexOf(@"\");
            ShortFileName = FileName.Substring(slashindex + 1);
            Name = dcm1.FindFirst(TagHelper.SeriesDescription).ToString();
            PatientId = dcm1.FindFirst(TagHelper.PatientID).DData.ToString();

            EvilDICOM.Core.Interfaces.IDICOMElement tempSopInstanceId = dcm1.FindFirst(TagHelper.ReferencedSOPInstanceUID);
            SopInstanceId = tempSopInstanceId.DData.ToString();
            if ((dcm1.FindFirst(TagHelper.Modality).ToString() ?? "").Contains("RTDOSE"))
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
            if (planFiles == null)
                throw new ArgumentNullException(nameof(planFiles));
            PlanFile match = planFiles.Find(X => X.SopInstanceId == SopInstanceId);
            if (match != null)
            {
                Tuple<string, string, string> beam = match.FieldNumberToNameList.Find(x => x.Item1 == BeamNumber);
                if (beam != null)
                {
                    PlanID = match.PlanID;
                    FieldName = beam.Item2;
                    FieldMUs = beam.Item3;
                }
                else
                {
                    var random = new Random();
                    PlanID = "FieldNotFound" + random.Next();
                    FieldName = "FieldNotFound" + random.Next();
                    FieldMUs = "-1";
                }
            }
            else
            {
                var random = new Random();
                PlanID = "FieldNotFound" + random.Next();
                FieldName = "FieldNotFound" + random.Next();
                FieldMUs = "-1";
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
                DICOMObject dcm1 = DICOMObject.Read(FileName);
                RTDose dcmMatrix = new RTDose(dcm1);
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
        public EvilDICOM.RT.RTDose DoseMatrix()
        {
            if (IsDoseFile)
            {
                DICOMObject dcm1 = DICOMObject.Read(FileName);
                EvilDICOM.RT.RTDose dcmMatrix = new EvilDICOM.RT.RTDose(dcm1);
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

    public class DicomFile
    {
        public bool IsPlanFile { get; }
        public bool IsDoseFile { get; }

        public DicomFile(string fileName)
        {

            DICOMObject dcm1 = DICOMObject.Read(fileName);
            if ((dcm1.FindFirst(TagHelper.Modality).ToString()?? "").Contains("RTDOSE"))
            {
                IsDoseFile = true;
            }
            else if ((dcm1.FindFirst(TagHelper.Modality).ToString()?? "").Contains("RTPLAN"))
            {
                IsPlanFile = true;
            }
            else
            {
                return;
            }
        }
    }

    public class PlanFile
    {
        /// <summary>
        /// Contains the field name, field number and number of MU's 
        /// </summary>
        public List<Tuple<string, string, string>> FieldNumberToNameList { get; }
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
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            FieldNumberToNameList = new List<Tuple<string, string, string>>();
            FileName = fileName;
            ShortFileName = FileName.Substring(FileName.LastIndexOf(@"\"));
            DICOMObject dcm1 = DICOMObject.Read(fileName);
            SopInstanceId = dcm1.FindFirst(TagHelper.SOPInstanceUID).DData.ToString();
            PatientID = dcm1.FindFirst(TagHelper.PatientID).DData.ToString();
            if ((dcm1.FindFirst(TagHelper.Modality).ToString() ?? "").Contains("RTPLAN"))
            {
                IsPlanFile = true;
                PlanID = dcm1.FindFirst(TagHelper.RTPlanLabel).DData.ToString();
                List<EvilDICOM.Core.Interfaces.IDICOMElement> beamNumbers = dcm1.FindAll(TagHelper.BeamNumber);
                List<EvilDICOM.Core.Interfaces.IDICOMElement> beamNames = dcm1.FindAll(TagHelper.BeamName);
                List<EvilDICOM.Core.Interfaces.IDICOMElement> beamMUs = dcm1.FindAll(TagHelper.BeamMeterset);
                if (beamNames.Count == beamNumbers.Count && beamNames.Count == beamMUs.Count)
                {
                    for (int i = 0; i < beamNames.Count; i++)
                    {
                        try
                        {
                            var temp = new Tuple<string, string, string>(beamNumbers[i].DData.ToString(), beamNames[i].DData.ToString(), beamMUs[i].DData.ToString());
                            FieldNumberToNameList.Add(temp);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            break;
                        }
                    }
                }
            }

        }
    }

}
