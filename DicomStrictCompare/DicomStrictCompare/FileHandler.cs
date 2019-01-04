using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;
using EvilDICOM.RT;

namespace DicomStrictCompare
{
    class FileHandler
    {

    }

    class DoseFile
    {
        public string FileName { get; }
        public bool IsDoseFile { get; } = false;
        public string Name { get; }
        public string PatientId { get; }
        public string PlanName { get; }
        public string FieldName { get; }
        public string MatchIdentifier { get; }
        public List<double> DoseValues { get; }
        public int Count => DoseValues.Count;

        public DoseFile(string fileName)
        {
            var dcm1 = DICOMObject.Read(fileName);
            DoseMatrix dcmMatrix = new DoseMatrix(dcm1);

            FileName = fileName;
            Name = dcm1.FindFirst(TagHelper.SeriesDescription).ToString();
            PatientId = dcm1.FindFirst(TagHelper.PatientID).ToString();
            if (dcm1.FindFirst(TagHelper.Modality).ToString() == "RTDOSE")
            {
                IsDoseFile = true;
                DoseValues = dcmMatrix.DoseValues;
            }
        }

    }
}
