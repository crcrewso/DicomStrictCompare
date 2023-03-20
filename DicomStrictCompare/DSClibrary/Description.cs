using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EvilDICOM.RT;

namespace DSClibrary
{
    /// <summary>
    /// all conditions that might be useful to compare against
    /// this is intended as being a key for a search
    /// Units - cm and cGy
    /// </summary>
    public class Description
    {
        public double GantryAngle { get; init; }
        public double CollAngle { get; init; }
        public double CollX1Jaw { get; init; }
        public double CollY1Jaw { get; init; }
        public double CollX2Jaw { get; init; }
        public double CollY2Jaw { get; init; }
        public double CollX => CollX1Jaw + CollX2Jaw;
        public double CollY => CollY1Jaw + CollY2Jaw;
        public double SSD { get; init; }
        public double MUs { get; init; }
        public DoseType DoseType { get; init; }




        public bool Equals(Description other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (DoseType != other.DoseType) return false;
            if (GantryAngle != other.GantryAngle) return false;
            if (CollAngle != other.CollAngle) return false;
            if (CollX1Jaw != other.CollX1Jaw) return false;
            if (CollX2Jaw != other.CollX2Jaw) return false;
            if (CollY1Jaw != other.CollY1Jaw) return false;
            if (CollY2Jaw != other.CollY2Jaw) return false;
            if (SSD != other.SSD) return false;
            if (MUs != other.MUs) return false;
            return true;
        }



    }

    public class ProfileDescription : Description
    {
        public double depth { get; init; }

        public bool Equals(DoseVolumeDescription doseVolumeDescription)
        {
            return base.Equals(doseVolumeDescription);
        }

        public ProfileDescription()
        {
            DoseType = DoseType.Relative;
        }

    }

    public class DoseVolumeDescription : Description
    {
        public double Xmin { get; init; }
        public double Ymin { get; init; }
        public double Zmin { get; init; }
        public double Xmax { get; init; }
        public double Ymax { get; init; }
        public double Zmax { get; init; }
        public double Xres { get; init; }
        public double Yres { get; init; }
        public double Zres { get; init; }

        public double X => Xmax - Xmin;
        public double Y => Ymax - Ymin;
        public double Z => Zmax - Zmin;

        public DoseVolumeDescription()
        {
            DoseType = DoseType.Absolute;
        }

        public bool Equals(ProfileDescription profileDescription)
        {
            return base.Equals((Description) profileDescription);
        }

    }




}
