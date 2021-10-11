using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace sandbox
{
    public class ProcessXML : ProcessDocument
    {

        public override async Task<bool> ExecuteAsync(string fileName)
        {
            bool result = false;
            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.InstrumentationKey = "af471157-d0a3-4a20-b7e7-e9c479852bb2";

            TelemetryClient telemetry = new TelemetryClient(configuration);

            BlobContainerClient _hporvenir = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hporvenir");            
            var stageClient = _hporvenir.GetBlobClient(fileName);

            telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "start" } });
            Log.Information("Process {fileName} {step}", fileName, "start");

            MemoryStream stream = new MemoryStream();
            try
            {
                var response = await stageClient.DownloadToAsync(stream);
                stream.Position = 0;
                HPorvenir.Elastic.Index index = new HPorvenir.Elastic.Index();
                await index.IndexXML(stream, fileName);
                telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "index" } });
                Log.Information("Process {fileName} {step}", fileName, "index");

                stageClient.DeleteIfExists();
                telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "deleteIndex" } });
                Log.Information("Process {fileName} {step}", fileName, "deleteIndex");

                result = true;
            }
            catch (Exception ex)
            {
                Log.Error("fetching file from storage {@file}", stageClient.Name);
            }
            finally {
                stream.Close();
            }

            return result;
        }
    }
}
