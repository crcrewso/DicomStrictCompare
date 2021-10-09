using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfileBatchCompare.Model
{
    internal class SNCProfile
    {
        string startOfScanString;
        string endOfScanString;
        Controller.TextFileImport sourceFile;
        List<string[]> rawData;
        internal SNCProfile(Controller.TextFileImport textFile)
        {
            if (textFile == null)
                throw new ArgumentNullException("text");
            if (textFile.FileType != Model.Dictionaries.textFileType.SNCprofile)
                throw new ArgumentException(message: textFile.FileType.ToString(), "Not of supported file type");
            
        }

       
    }
}
