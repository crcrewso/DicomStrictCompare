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
        public string SOPInstanceID { get; init; }
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





            #region Define Parameters
            SOPInstanceID = dcm.FindFirst(TagHelper.SOPInstanceUID).ToString();
            PatientiD = dcm.FindFirst(TagHelper.PatientID).ToString();
            RTPlanLabel = dcm.FindFirst(TagHelper.RTPlanLabel).ToString();



            #endregion


        }

    }
}
