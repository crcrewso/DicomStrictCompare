using EvilDICOM.Core.Helpers;
using EvilDICOM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSClibrary
{
    public static class Helpers
    {
        public static bool IsPlanFile(this DICOMObject dcm)
        {
            if ((dcm.FindFirst(TagHelper.Modality).ToString() ?? "").Contains("RTPLAN"))
                return true;
            return false;
        }

        public static bool IsDoseFile(this DICOMObject dcm)
        {
            if ((dcm.FindFirst(TagHelper.Modality).ToString() ?? "").Contains("RTDOSE"))
                return true;
            return false;
        }


        /// <summary>
        /// Test for plan files 
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="dose"></param>
        /// <returns></returns>
        public static bool IsMemberOf(this DICOMObject plan, DICOMObject dose) 
        {
            var DoseReferenceuUID = dose.FindFirst(TagHelper.ReferencedSOPInstanceUID).ToString();
            var planReferenceUID = plan.FindFirst(TagHelper.SOPInstanceUID).ToString();
            if (DoseReferenceuUID == null || planReferenceUID == null)
                return false;
            if (DoseReferenceuUID.Equals(planReferenceUID)) return true;
            return false;

        }

    }
}
