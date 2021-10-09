using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ProfileBatchCompare.Controller
{
    internal record TextFileImport
    {
        public string[] Contents { get; init; }

        public string FileName { get; init; }
        public string Name { get; init; }
        public string Extension { get; init; }
        /// <summary>
        /// write once enum of file type 
        /// </summary>
        public Model.Dictionaries.textFileType FileType { get; init; }



/// <summary>
/// Imports the raw text file 
/// </summary>
/// <param name="path"></param>
/// <exception cref="FileNotFoundException"></exception>
/// <exception cref="FileLoadException"></exception>
        public TextFileImport(string path)
        {
            Exception exception = null;
            FileName = path;
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(message: "File not found", fileName: FileName);
            }
            Name = Path.GetFileName(path);
            Extension = Path.GetExtension(path);
            FileType = Model.Dictionaries.GetFileType(Extension);
            List<string> lines = new List<string>();
            // starting from docs.microsoft.com example
            String line;
            //Pass the file path and file name to the StreamReader constructor
            StreamReader sr = new StreamReader(path);
            //Read the first line of text
            try
            {
                line = sr.ReadLine();
                //Continue to read until you reach end of file
                while (line != null)
                {
                    lines.Add(line);
                    line = sr.ReadLine();
                }
                //close the file
                sr.Close();
            }
            catch (Exception e)
            {
                exception = e;
            }
            if (lines.Count == 0)
            {
                throw new FileLoadException(message: "File is empty", fileName: FileName, inner: exception);
            }
            Contents = lines.ToArray();
        }
    }
}
