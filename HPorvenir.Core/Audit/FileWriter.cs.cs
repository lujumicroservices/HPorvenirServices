using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HPorvenir.Core.Audit
{
    public class FileWriter
    {

        string _filePath;
        //StreamWriter writer;
        public FileWriter(string filePath, string name)
        {
            _filePath = @$"{filePath}{name}.txt";
            //writer = new StreamWriter(@$"{filePath}{name}.txt");
        }



        public void Write(string message)
        {
            using (StreamWriter writer = new StreamWriter(_filePath,true))
            {
                writer.WriteLine(message);
            }
        }
    }
}
