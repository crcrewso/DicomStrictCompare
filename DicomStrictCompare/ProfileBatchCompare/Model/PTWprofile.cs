using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfileBatchCompare.Model
{
    internal class PTWprofile
    {
        string startOfScanString = "BEGIN_SCAN";
        string endOfScanString = "END_SCAN";
        Controller.TextFileImport sourceFile;
        List<string[]> rawData;
        List<Profile> Profiles; 

        public PTWprofile(Controller.TextFileImport textFile)
        {
            if (textFile == null)
                throw new ArgumentNullException("text");
            if (textFile.FileType != Model.Dictionaries.textFileType.PTWprofile)
                throw new ArgumentException(message: textFile.FileType.ToString(), "Not of supported file type");
            sourceFile = textFile;

            int i = 0;
            int startOfScan = -1;
            int endOfScan = -2;
            int scanNumber = -1;
            while (i < sourceFile.Contents.Length)
            {
                string line = textFile.Contents[i];
                if (line.Contains(startOfScanString))
                {
                    int index = line.IndexOf(startOfScanString);
                    string indexStr = (index < 0)
                        ? line
                        : line.Remove(index, startOfScanString.Length);
                    scanNumber = Convert.ToInt32(indexStr);
                }
                if (line.Contains(endOfScanString))
                    endOfScan = i;
                if (endOfScan > startOfScan && scanNumber > 0)
                    extractScan(sourceFile.Contents, startOfScan, endOfScan, scanNumber);
            }
        }
         private void extractScan(string[] contents, int startOfScan, int endOfScan, int scanNumber)
        {
            int length = endOfScan - startOfScan;
            string[] tempRawData = contents.Skip(startOfScan).Take(length).ToArray();
            rawData.Add(tempRawData);

            throw new NotImplementedException();
        }
    }
}
