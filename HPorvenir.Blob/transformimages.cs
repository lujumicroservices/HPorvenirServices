using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HPorvenir.Blob
{
    public class TransformImages
    {
        private readonly IConfiguration _configuration;
        private readonly BlobContainerClient _container;
        private string URLPrefix;
        private string ContainerName;
        private string AuthKey;

        public TransformImages()
        {
            //_configuration = configuration;

            string connectionString = "DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net";
            ContainerName = "hemeroteca";
            URLPrefix = "https://hemerotecaporvenir.blob.core.windows.net";
            AuthKey = "?sv=2020-02-10&ss=bf&srt=o&sp=rl&se=2022-06-11T08:00:36Z&st=2021-06-11T00:00:36Z&spr=https&sig=YfFVSUacatSS9F67uvV%2BkrhAwyZ%2BnWU%2F5ciPiRE7JSk%3D";
            _container = new BlobContainerClient(connectionString, ContainerName);



        }


        public void Execute() {

            List<string> data = new List<string>();
            var index = 0;
            var blobs = _container.GetBlobs();
            foreach (var b in blobs) {
                data.Add(b.Name);
                index++;

                if (index % 1000 == 0) {
                    Console.WriteLine("processed files: " + index);
                }
            }

            File.WriteAllLines("c:\\dev2\\totalimages.csv", data);
            
        }

    }
}
