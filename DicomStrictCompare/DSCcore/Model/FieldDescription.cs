using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvilDICOM.Core.Helpers;
using EvilDICOM.Core.Interfaces;

namespace DSCcore.Model
{
    record FieldDescriptions
    {
        public List<FieldDescription> fields;
        public string PlanName; 
        public FieldDescriptions(DicomStrictCompare.PlanFile planFile)
        {
            var dcm1 = planFile.rawDicomPlan;
            PlanName = dcm1.FindFirst(TagHelper.RTPlanLabel).ToString();
            List<IDICOMElement> beamNumbers = dcm1.FindAll(TagHelper.BeamNumber);
            List<IDICOMElement> beamNames = dcm1.FindAll(TagHelper.BeamName);
            List < IDICOMElement > jawAndColls = dcm1.FindAll(TagHelper.BeamLimitingDeviceSequence);
            foreach (var setset in dcm1.FindFirst(TagHelper.BeamSequence)
            foreach (var set in jawAndColls)
            {
                dcm1.Elements
            }    
            List < IDICOMElement > mus = dcm1.FindAll(TagHelper.BeamMeterset);
            List < IDICOMElement >  = dcm1.FindAll(TagHelper.);
            List < IDICOMElement >  = dcm1.FindAll(TagHelper.);
            List < IDICOMElement >  = dcm1.FindAll(TagHelper.);
            List < IDICOMElement >  = dcm1.FindAll(TagHelper.);
            List < IDICOMElement >  = dcm1.FindAll(TagHelper.);
            List < IDICOMElement >  = dcm1.FindAll(TagHelper.);


        }
    }




    record FieldDescription
    {
        public string PlanName;
        public string FieldName;

        public float JawX1;
        public float JawX2;
        public float JawY1;
        public float JawY2; 





    }
}
