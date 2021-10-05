using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace sandbox
{
    public class ProcessStage
    {

        string stageContainer = "hemerotecav2";
        string prodContainer = "hporvenir";

        BlobContainerClient _container = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hporvenir");

        public async Task ExecuteAsync()
        {
            var pending_blobs = _container.GetBlobs();


            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;
            //foreach (var b in pending_blobs)
            Parallel.ForEach(pending_blobs, options, b =>
            {
                try
                {
                    if (b.Name.EndsWith(".xml"))
                    {
                        ProcessXML xml = new ProcessXML();
                        xml.ExecuteAsync(b.Name).Wait();
                    }
                    else if (b.Name.EndsWith(".tif"))
                    {
                        ProcessTIF tif = new ProcessTIF();
                        tif.ExecuteAsync(b.Name).Wait();
                    }
                    else if (b.Name.EndsWith(".pdf"))
                    {
                        ProcessPDF pdf = new ProcessPDF();
                        pdf.ExecuteAsync(b.Name).Wait();
                    }
                    else
                    {
                        _container.DeleteBlob(b.Name);
                    }
                }
                catch (Exception ex) { 
                    
                }

                
            });

        }


    }
}
