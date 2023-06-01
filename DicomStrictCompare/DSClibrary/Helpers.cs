using EvilDICOM.Core.Helpers;
using EvilDICOM.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;

namespace DSClibrary
{

    public enum DoseType { Relative, Absolute}
    public enum Device { Profiler2, ICProfiler, LA48 }
    public enum FileExtension { prm, prs, mcc}
    public enum DetectorType { IonChamber, Diode, ICArray, DiodeArray}



    public class UUID :IEquatable<UUID>
    {
        private readonly string _uuid;

        public UUID(string uuid)
        {
            _uuid = uuid;
        }

        public static implicit operator UUID(string? uuid)
        {
            if (ReferenceEquals(null, uuid))
                return uuid;

            return new UUID(uuid);
        }

        public static implicit operator string(UUID? uuid)
        {
            if (ReferenceEquals(null, uuid))
                return uuid;
            return uuid._uuid;
        }
        public static bool operator ==(UUID left, UUID right)
        {
            return Equals(left, right);
        }
        public static bool operator !=(UUID left, UUID right)
        {
            return !Equals(left, right);
        }
        public bool Equals(UUID? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _uuid == other._uuid;
        }
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((UUID)obj);
        }

        public override int GetHashCode()
        {
            return (_uuid != null ? _uuid.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return _uuid.ToString();
        }
    }

    /// <summary>
    /// This class is filled with Static helper functions for comparison purposes, mostly extension functions
    /// </summary>
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
