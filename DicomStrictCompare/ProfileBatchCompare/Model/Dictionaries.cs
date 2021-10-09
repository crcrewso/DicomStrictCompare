using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfileBatchCompare.Model
{
    internal static class Dictionaries
    {
        internal enum textFileType { SNCprofile, PTWprofile, DICOMDose, EGSnrcDose}
        internal static textFileType GetFileType(string extension)
        {
            string lowExtension = extension.ToLower();
            if (string.IsNullOrEmpty(extension))
                throw new ArgumentNullException();
            switch (extension)
            {
                case "prm":
                    return textFileType.SNCprofile;
                case "mcc":
                    return textFileType.PTWprofile;
                case "dcm":
                    return textFileType.DICOMDose;
                case "3ddose":
                    return textFileType.EGSnrcDose;
                default:
                    throw new FileLoadException(message: "File type not supported - " + extension);

            }

        }

        internal enum direction { InPlane, CrossPlane, Diagonal_Plus, Diagonal_Minus}
        internal enum doseType { Relative, Absolute}

    }

}
