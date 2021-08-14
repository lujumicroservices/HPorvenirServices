using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Hporvenir.Indexer
{
    public class IndexManager
    {


        int _hilos;

        public void Execute() { 
        
        }


        public async Task Navigate(DirectoryInfo directory)
        {

            if (directory.GetFiles().Length > 0)
            {
                ProcessFolder(directory);
            }

            if (directory.GetDirectories().Length > 0)
            {
                foreach (var dir in directory.GetDirectories())
                {
                    Console.WriteLine($"navigating folder {dir.FullName}");
                    Navigate(dir);
                }
            }
        }


        public async Task ProcessFolder(DirectoryInfo directory)
        {
            var files = directory.GetFiles();

            var validDir = checkfilestructure(directory, out string year);

            if (!validDir || int.Parse(year) < 1940)
                return;

            Console.WriteLine($"PROCESSING FOLDER {directory.FullName}");

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _hilos;

            Parallel.ForEach(files, options, file =>
            {
                if ((file.Extension == ".xml" || file.Extension == ".pdf"))
                {
                    Console.WriteLine($"uploading data {file.FullName}");
                    //uploadBlob(file);
                }
            });
        }


        private bool checkfilestructure(DirectoryInfo dir, out string year)
        {
            try
            {
                year = dir.Parent.Parent.Name;
                return dir.Name.Length == 2 && dir.Parent.Name.Length == 2 && dir.Parent.Parent.Name.Length == 4;
            }
            catch
            {
                year = "0";
                return false;
            }
        }


    }
}
