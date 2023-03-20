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

    }

    public class ProfileDescription : Description
    {
        public double depth { get; init; }

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

    }


}
