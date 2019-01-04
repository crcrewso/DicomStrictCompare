using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicomStrictCompare
{
    class DscDataHandler
    {
        public string SourceDirectory { get; private set; }
        public string TargetDirectory { get; private set; }
        public string[] SourceListStrings { get; private set; }
        public string[] TargetListStrings { get; private set; }


        public int CreateSourcelist(string folder)
        {
            SourceDirectory = folder;
            SourceListStrings = LoadListRDDcmList(folder);
            return SourceListStrings.Length;
        }

        public int CreateTargetList(string folder)
        {
            TargetDirectory = folder;
            TargetListStrings = LoadListRDDcmList(folder);
            return TargetListStrings.Length;
        }

        public static string[] LoadListRDDcmList(string folder)
        {
            return Directory.GetFiles(folder, "RD*.dcm", SearchOption.AllDirectories);
        }

        public void run()
        {
            try
            {
                SourceDirectory.IsNormalized();
            }
            catch (NullReferenceException)
            {
                throw new NullReferenceException("Source directory cannot be null");
            }

            try
            {
                TargetDirectory.IsNormalized();
            }
            catch (NullReferenceException)
            {

                throw new NullReferenceException("Target directory cannot be null"); ;
            }

            if (SourceListStrings.Length <=0 )
                throw new InvalidOperationException("There are no Dose files in the source directory Tree");
            if (TargetListStrings.Length <=0 )
                throw new InvalidOperationException("There are no Dose files in the Target directory Tree");

        }

    }


}
