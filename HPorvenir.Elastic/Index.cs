using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Hporvenir.Indexer;
using HPorvenir.Parser;
using Nest;
using Serilog;

namespace HPorvenir.Elastic
{
    public class Index
    {
        int _hilos = 1;
        ElasticClient client;
        string _indexPath;
        string _indexName = "hporvenirv2v2_2005-2021";
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

        public Index()
        {
            var settings = new ConnectionSettings(new Uri("https://hporvenir-elastic.es.westus2.azure.elastic-cloud.com:9243")).DefaultIndex(_indexName).ApiKeyAuthentication("TAvQfXoBALKbRWliRmnL", "SoEZ9e7HQZO8gOXF_qHbZg");
            client = new ElasticClient(settings);
        }

        public Index(string indexPath, string indexName)
        {
            _indexName = indexName;
            _indexPath = indexPath;
            _client = new IndexClient(_indexPath, indexName);
        }


        public async Task ExecuteAsync(string basePath)
        {
            DirectoryInfo directory = new DirectoryInfo(basePath);
            await Navigate(directory);
        }

        public async Task Navigate(DirectoryInfo directory)
        {
            Log.Information("navigate processing folder {dirname}", directory.Name);
            if (directory.Name.Length == 4 && (int.Parse(directory.Name) < _start || int.Parse(directory.Name) > _end))
                return;


            Log.Information("folder with files {count}", directory.GetFiles().Length);
            if (directory.GetFiles().Length > 0)
            {
                
                await ProcessFolder(directory);
            }


            Log.Information("folder with folders {count}", directory.GetDirectories().Length);
            if (directory.GetDirectories().Length > 0)
            {
                
                foreach (var dir in directory.GetDirectories())
                {
                    Console.WriteLine($"navigating folder {dir.FullName}");
                    await Navigate(dir);
                }
            }
        }


        public async Task ProcessFolder(DirectoryInfo directory)
        {            
            
            
            Log.Information($"PROCESSING FOLDER {directory.FullName}  with {_hilos}");

            var validDir = checkfilestructure(directory, out string year);
            var files = directory.GetFiles("*.pdf");

            if (files.Length == 0) { 
             files = directory.GetFiles("*.xml");
            }


            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _hilos;


            Console.WriteLine($"validDir {validDir}");
            if (validDir)
            {
                Log.Information("processing folder {dirname}", directory.FullName);
                Log.Information("files {count}", files.Length);

                //foreach (var file in files)
                Parallel.ForEach(files, options, file =>
                {
                    Console.WriteLine($"file: {file.FullName}");

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
                            while (retry)
                            {
                                var response = client.IndexMany<Paragraph>(document);
                                if (!response.Errors)
                                {
                                    retry = false;
                                }
                                else
                                {
                                    Console.WriteLine("ORIGINAL EXCEPTIONS");
                                    Console.WriteLine(response.OriginalException.Message);
                                    Console.WriteLine("ITEMS WITH ERRORS");
                                    foreach (var r in response.ItemsWithErrors)
                                    {
                                        Console.WriteLine(r.Error.Reason);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error {ex.Message}  inner {ex.InnerException?.Message}");
                            throw new Exception("index error", ex);
                        }
                    }



                    if (file.Extension == ".pdf")
                    {
                        Console.WriteLine($"uploading data {file.FullName}");

                        Console.WriteLine($"Parse file {file.Name}");
                        Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument(file.FullName);
                        var page = doc.Pages[0];
                        var content = page.ExtractText();

                        Console.WriteLine($"Parse file {file.Name}");
                        List<Paragraph> document = new List<Paragraph>();
                        int fileDate = int.Parse(file.Directory.Parent.Parent.Name + file.Directory.Parent.Name + file.Directory.Name);
                        document.Add(
                            new Paragraph
                            {
                                Content = content,
                                Date = fileDate,
                                Id = $"{fileDate}{file.Name.Replace(file.Extension, "").Replace("-", "")}0",
                                Name = $"{fileDate}{file.Name}"
                            }
                        );

                        Console.WriteLine($"Index file {file.Name}");
                        try
                        {
                            bool retry = true;
                            while (retry)
                            {
                                var response = client.IndexMany<Paragraph>(document);
                                if (!response.Errors)
                                {
                                    retry = false;
                                }
                                else
                                {
                                    Console.WriteLine("ORIGINAL EXCEPTIONS");
                                    Console.WriteLine(response.OriginalException.Message);
                                    Console.WriteLine("ITEMS WITH ERRORS");
                                    foreach (var r in response.ItemsWithErrors)
                                    {
                                        Console.WriteLine(r.Error.Reason);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error {ex.Message}  inner {ex.InnerException?.Message}");
                            throw new Exception("index error", ex);
                        }
                    }


                 });
                //}
            }
            else {
                Log.Information("skip folder {dirname}", directory.FullName);
            }
        }


        private bool checkfilestructure(DirectoryInfo dir, out string year)
        {
            try
            {
                if ("El Porvenir" != dir.Parent.Parent.Parent.Name) {
                    throw new Exception("invalid file structure");
                }
                

                year = dir.Parent.Parent.Name;
                return dir.Name.Length == 2 && dir.Parent.Name.Length == 2 && dir.Parent.Parent.Name.Length == 4;
            }
            catch
            {
                Log.Information("error {dirname}", dir.FullName);
                year = "0";
                return false;
            }
        }



        public async Task IndexPDF(MemoryStream fileStream, string blobName) 
        {

            var path = blobName.Split("/");
            var nameNoExtension = path[1].Substring(0, path[1].Length - 4);

            Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument(fileStream);
            var page = doc.Pages[0];
            var content = page.ExtractText();

            List<Paragraph> document = new List<Paragraph>();
            int fileDate = int.Parse(path[0]);
            document.Add(
                new Paragraph
                {
                    Content = content,
                    Date = fileDate,
                    Id = $"{fileDate}{nameNoExtension.Replace("-", "")}0",
                    Name = $"{fileDate}{path[1]}"
                }
            );

            Console.WriteLine($"Index file {path[1]}");
            try
            {
                bool retry = true;
                while (retry)
                {
                    var response = client.IndexMany<Paragraph>(document);
                    if (!response.Errors)
                    {
                        retry = false;
                    }
                    else
                    {
                        Console.WriteLine("ORIGINAL EXCEPTIONS");
                        Console.WriteLine(response.OriginalException.Message);
                        Console.WriteLine("ITEMS WITH ERRORS");
                        foreach (var r in response.ItemsWithErrors)
                        {
                            Console.WriteLine(r.Error.Reason);
                        }
                        throw new Exception("index  response with error");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {ex.Message}  inner {ex.InnerException?.Message}");
                throw new Exception("index error", ex);
            }


        }

        public async Task IndexXML(MemoryStream fileStream, string blobName)
        {
            Parser.XmlParser parser = new Parser.XmlParser();
            var document = parser.ParseXml(fileStream, blobName);            

            try
            {
                bool retry = true;
                while (retry)
                {
                    var response = client.IndexMany(document);
                    if (!response.Errors)
                    {
                        retry = false;
                    }
                    else
                    {
                        Console.WriteLine("ORIGINAL EXCEPTIONS");
                        Console.WriteLine(response.OriginalException.Message);
                        Console.WriteLine("ITEMS WITH ERRORS");
                        foreach (var r in response.ItemsWithErrors)
                        {
                            Console.WriteLine(r.Error.Reason);
                        }
                        throw new Exception("index  response with error");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {ex.Message}  inner {ex.InnerException?.Message}");
                throw new Exception("index error", ex);
            }

        }



    }
}

