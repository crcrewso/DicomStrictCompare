namespace DicomStrictCompare
{
    interface IMathematics
    {
        /// <summary>
        /// Compares the two provided dose arrays voxel to voxel. Dose difference is calculated as % of source dose
        /// </summary>
        /// <param name="source">Reference dose array</param>
        /// <param name="target">Dose Array being verified</param>
        /// <param name="tolerance">Less than this percent difference is a pass</param>
        /// <param name="epsilon">Threshold percent of max dose below which comparison will not be evaluated</param>
        /// <returns></returns>
        int CompareAbsolute(double[] source, double[] target, double tolerance, double epsilon);
        int CompareRelative(double[] source, double[] target, double tolerance, double epsilon);
    }

}
