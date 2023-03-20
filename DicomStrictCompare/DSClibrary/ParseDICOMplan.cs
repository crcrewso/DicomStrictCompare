using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EvilDICOM;
using EvilDICOM.Core;
using EvilDICOM.Core.Helpers;

namespace DSClibrary
{
    public class ParseDICOMplan : List<DICOMdose>
    {
        public UUID SOPInstanceID { get; init; }
        public string PatientiD { get; init; }
        public string RTPlanLabel { get; init; }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="filename">A DICOM Plan file</param>
        public ParseDICOMplan(DICOMObject dcm)
        {


            if (!dcm.IsPlanFile())
            {
                throw new ArgumentException("This is not a plan file", nameof(dcm));
            }

            if (dcm == null)
                throw new ArgumentNullException("I cannot parse a null plan", nameof(dcm));




            #region Define Parameters

            // Suppressing Null warnings since FindFirst will not return null, it may throw exception
            SOPInstanceID = dcm.FindFirst(TagHelper.SOPInstanceUID).ToString() ?? throw new ArgumentNullException(nameof(dcm), "SOP Instance ID cannot be null, malformed Dose File");
            PatientiD = dcm.FindFirst(TagHelper.PatientID).ToString() ?? throw new ArgumentNullException(nameof(dcm), "Patient ID cannot be null, malformed Dose File");
            RTPlanLabel = dcm.FindFirst(TagHelper.RTPlanLabel).ToString() ?? throw new ArgumentNullException(nameof(dcm), "Unsupported format: RT Plan name cannot be null");





            #endregion


        }

    }
}
