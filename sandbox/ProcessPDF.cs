using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using Spire.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace sandbox
{
    public class ProcessPDF: ProcessDocument
    {

        public override async Task ExecuteAsync(string fileName)
        {

            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.InstrumentationKey = "af471157-d0a3-4a20-b7e7-e9c479852bb2";

            TelemetryClient telemetry = new TelemetryClient(configuration);

            BlobContainerClient _hporvenir = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hporvenir");
            BlobContainerClient _hemerotecav2 = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hemerotecav2");

            var stageClient = _hporvenir.GetBlobClient(fileName);

            telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "start" } });
            Log.Information("Process {fileName} {step}", fileName, "start");

            MemoryStream stream = new MemoryStream();
            try
            {
                var response = await stageClient.DownloadToAsync(stream);
                Log.Information($"donwload response : {response.Status}");
                Log.Information($"donwload reason : {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                Log.Error("fetching file from storage {@file}", stageClient.Name);
            }

            var tumbStream = PDFToThumb(stream,fileName);

            var targetClient = _hemerotecav2.GetBlobClient(CalculateBlobName(fileName));
            var targetTClient = _hemerotecav2.GetBlobClient(CalculateThumbBlobName(fileName, ".pdf"));

            try
            {

                if (!targetClient.Exists()) {
                    stream.Position = 0;
                    await targetClient.UploadAsync(stream);
                }


                HPorvenir.Elastic.Index index = new HPorvenir.Elastic.Index();
                stream.Position = 0;
                index.IndexPDF(stream, fileName);

                telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "copy" } });
                Log.Information("Process {fileName} {step}", fileName, "copy");

                if (!targetTClient.Exists())
                    await targetTClient.UploadAsync(tumbStream);


                telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "thumb" } });
                Log.Information("Process {fileName} {step}", fileName, "thumb");


                if (targetClient.Exists() && targetTClient.Exists())
                    stageClient.DeleteIfExists();

                telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "delete" } });
                Log.Information("Process {fileName} {step}", fileName, "delete");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Processing {path}", fileName);
            }
            finally
            {
                tumbStream.Close();
                stream.Close();
            }

        }

        public Stream PDFToThumb(MemoryStream stream, string fileName) {
            
            PdfDocument doc = new PdfDocument();
            doc.LoadFromStream(stream);
            Stream image;
            try
            {
                Log.Information($"saving image : {fileName}");
                image = doc.SaveAsImage(0);
                Log.Information($"save complete : {fileName}");

            }
            catch (Exception ex)
            {
                Log.Information($"save error : {fileName}");
                Log.Error(ex, $"invalid PDF {fileName} stream {stream.Position}  sl {stream.Length}");
                return null;
            }


            Log.Information($"create bitmap: {fileName}");
            Bitmap bitmap = new Bitmap(image);

            const int thumbnailSize = 150;
            var imageHeight = bitmap.Height;
            var imageWidth = bitmap.Width;
            if (imageHeight > imageWidth)
            {
                imageWidth = (int)(((float)imageWidth / (float)imageHeight) * thumbnailSize);
                imageHeight = thumbnailSize;
            }
            else
            {
                imageHeight = (int)(((float)imageHeight / (float)imageWidth) * thumbnailSize);
                imageWidth = thumbnailSize;
            }

            Stream thumbStream = new MemoryStream();
            using (var thumb = bitmap.GetThumbnailImage(imageWidth, imageHeight, () => false, IntPtr.Zero))
            {

                thumb.Save(thumbStream, ImageFormat.Jpeg);
                thumbStream.Position = 0;
            }

            doc.Dispose();            
            bitmap.Dispose();

            return thumbStream;

        }

    }
}
