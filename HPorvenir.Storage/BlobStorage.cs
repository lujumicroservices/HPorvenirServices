using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HPorvenir.Storage
{
    public class BlobStorage : IStorage
    {

        private readonly IConfiguration _configuration;
        private readonly BlobContainerClient _container;
        private string URLPrefix;
        private string ContainerName;
        private string AuthKey;


        public BlobStorage(IConfiguration configuration) {
            _configuration = configuration;

            string connectionString = _configuration.GetSection("BlobStorage:ConnectionString").Value;
            ContainerName = _configuration.GetSection("BlobStorage:RootContainer").Value;
            URLPrefix = _configuration.GetSection("BlobStorage:URLPrefix").Value;
            AuthKey = _configuration.GetSection("BlobStorage:AuthKey").Value;
            _container = new BlobContainerClient(connectionString, ContainerName);
        }


        public void Delete()
        {
            throw new NotImplementedException();
        }

        public bool Read()
        {
            throw new NotImplementedException();
        }

        public bool Save(bool overwrite = false)
        {
            throw new NotImplementedException();
        }

        public List<string> ListDay(int year, int month, int day) {
            
            List<string> files = new List<string>();

            var filter = $"{year}/{month.ToString("0#")}_{day.ToString("0#")}";
                        
            foreach (BlobItem blob in _container.GetBlobs( prefix:filter))
            {
                files.Add($"{URLPrefix}/{ContainerName}/{blob.Name}{AuthKey}");                
            }

            return files;
        
        }
    }
}
