﻿using DicomStrictCompare.Model;
using System.Linq;

namespace DicomStrictCompare
{
    public abstract class IMathematics
    {

        public enum Type { relative, absolute };
        /// <summary>
        /// Compares the two provided dose arrays voxel to voxel. Dose difference is calculated as % of source dose
        /// </summary>
        /// <param name="source">Reference dose array</param>
        /// <param name="target">Dose Array being verified</param>
        /// <param name="tolerance">Less than this percent difference is a pass</param>
        /// <param name="epsilon">Threshold percent of max dose below which comparison will not be evaluated</param>
        /// <returns> The total failed voxels and total number of voxels compared</returns>
        /// 
        public abstract SingleComparison CompareAbsolute(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);
        public abstract SingleComparison CompareRelative(in Model.DoseMatrixOptimal source, in Model.DoseMatrixOptimal target, Model.Dta dta);
        public abstract SingleComparison CompareParallel( Model.DoseMatrixOptimal source,  Model.DoseMatrixOptimal target, Model.Dta dta, int cpuParallel, Type type);
    }



}
