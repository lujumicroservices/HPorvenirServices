using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HPorvenir.Blob
{
    public class BlobManager
    {

        string connectionString;
        BlobServiceClient blobServiceClient;
        // Create the container and return a container client object
        BlobContainerClient containerClient;
        string _path;
        int _hilos;


        public BlobManager(string path, int hilos) {
            connectionString = "DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net";
            blobServiceClient = new BlobServiceClient(connectionString);
            // Create the container and return a container client object
            _path = path;
            _hilos = hilos;
            containerClient = blobServiceClient.GetBlobContainerClient("hemeroteca");
        }


        
        public async Task MigrateData() {

            //string basepath = @"D:\HPorvenir\";
            string basepath = _path;
            DirectoryInfo directory = new DirectoryInfo(basepath);

            await Navigate(directory);

        }



        public async Task ProcessFolder(DirectoryInfo directory) {
            var files = directory.GetFiles();


            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _hilos;


            Parallel.ForEach(files, options, file => {

                if (file.Extension == ".tiff" || file.Extension == ".tif" || file.Extension == ".pdf")
                {
                    Console.WriteLine($"uploading data {file.FullName}");
                    uploadBlob(file);
                }
            });

                                
        }

        public bool uploadBlob(FileInfo file) {
            try
            {
                BlobClient blobClient = containerClient.GetBlobClient(CalculateBlobName(file));
                using FileStream uploadFileStream = File.OpenRead(file.FullName);
                blobClient.Upload(uploadFileStream, true);
                uploadFileStream.Close();
                return true;
            }
            catch {
                Console.WriteLine($"error uploading image {file.Name}");
                Console.WriteLine($"waiting 3");
                Task.Delay(3000);
                
                uploadBlob(file);
                return false;
            }
            

        }


        public async Task Navigate(DirectoryInfo directory) {

            if (directory.GetFiles().Length > 0) {
                ProcessFolder(directory);
            }

            if (directory.GetDirectories().Length > 0) {
                foreach (var dir in directory.GetDirectories()) {
                    Console.WriteLine($"navigating folder {dir.FullName}");
                    Navigate(dir);
                }
            }
        }


        private string CalculateBlobName(FileInfo file) {
            var name = @$"{file.Directory.Parent.Parent.Name}\{file.Directory.Parent.Name}_{file.Directory.Name}_{file.Name}";
            return name;
        }



    }
}
