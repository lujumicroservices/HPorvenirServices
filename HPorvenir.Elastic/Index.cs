using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Hporvenir.Indexer;
using HPorvenir.Parser;
using Nest;

namespace HPorvenir.Elastic
{
    public class Index
    {
        int _hilos = 1;
        ElasticClient client;
        string _indexPath;
        string _indexName;
        int _start;
        int _end;

        IndexClient _client;

        public Index(int start, int end, string indexname, int hilos) {
            

            _start = start;
            _end = end;
            _indexName = indexname;
            _hilos = hilos;

            var settings = new ConnectionSettings(new Uri("https://hporvenir-elastic.es.westus2.azure.elastic-cloud.com:9243")).DefaultIndex(_indexName).ApiKeyAuthentication("TAvQfXoBALKbRWliRmnL", "SoEZ9e7HQZO8gOXF_qHbZg");
            client = new ElasticClient(settings);

        }

        public Index(string indexPath, string indexName)
        {
            _indexName = indexName;
            _indexPath = indexPath;
            _client = new IndexClient(_indexPath, indexName);
        }


        public void Execute(string basePath)
        {
            DirectoryInfo directory = new DirectoryInfo(basePath);
            Navigate(directory);
        }

        public async Task Navigate(DirectoryInfo directory)
        {



            if (directory.Name.Length == 4 && (int.Parse(directory.Name) < _start || int.Parse(directory.Name) > _end))
                return;


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

            
         
            Console.WriteLine($"PROCESSING FOLDER {directory.FullName}");

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _hilos;

            Parallel.ForEach(files, options, file =>
            {
                if ((file.Extension == ".xml"))
                {
                    Console.WriteLine($"uploading data {file.FullName}");
                    Parser.XmlParser parser = new Parser.XmlParser();
                    Console.WriteLine($"Parse file {file.Name}");
                    var document = parser.ParseXml(file);
                    Console.WriteLine($"Index file {file.Name}");


                    try
                    {
                        bool retry = true;
                        while (retry) {
                            var response = client.IndexMany<Paragraph>(document);
                            if (!response.Errors)
                            {
                                retry = false;
                            }
                            else {
                                Console.WriteLine("ORIGINAL EXCEPTIONS");
                                Console.WriteLine(response.OriginalException.Message);
                                Console.WriteLine("ITEMS WITH ERRORS");
                                foreach (var r in response.ItemsWithErrors) {
                                    Console.WriteLine(r.Error.Reason);
                                }
                                
                            }


                        }
                        
                                                                             
                    }
                    catch(Exception ex) {
                            Console.WriteLine($"Error {ex.Message}  inner {ex.InnerException?.Message}");
                            throw new Exception("index error",ex);
                    }


                    //try
                    //{
                    //    _client.IndexMany(document);
                    //}
                    //catch (Exception ex)
                    //{
                    //    Console.WriteLine($"Error {ex.Message}  inner {ex.InnerException?.Message}");
                    //    throw new Exception("index error",ex);
                    //}



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

