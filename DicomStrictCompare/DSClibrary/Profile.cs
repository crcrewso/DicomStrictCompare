using EvilDICOM.RT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSClibrary
{
    public record Profile
    {
        public List<DoseValue> Doses { get; init; }
        public ProfileType ProfileType { get; init; } 
        public NormalizationType NormalizationType { get; init; }

        public Profile(List<DoseValue> doseValues, ProfileType profileType, NormalizationType normalizationType) {
            Doses = doseValues;
            ProfileType = profileType;
            NormalizationType = normalizationType;
        }



    }
}
