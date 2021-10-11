using Azure.Storage.Blobs;
using BitMiracle.LibTiff.Classic;
using HPorvenir.Blob;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace sandbox
{
    public class ProcessTIF : ProcessDocument
    {
        
        public ProcessTIF() { 
        
        }

        public override async Task<bool> ExecuteAsync(string fileName) {

            bool result = false;
            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.InstrumentationKey = "af471157-d0a3-4a20-b7e7-e9c479852bb2";

            TelemetryClient telemetry = new TelemetryClient(configuration);

            BlobContainerClient _hporvenir = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hporvenir");
            BlobContainerClient _hemerotecav2 = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hemerotecav2");
            
            var stageClient = _hporvenir.GetBlobClient(fileName);


            telemetry.TrackEvent(fileName, new Dictionary<string, string>() { { "step", "start" } });
            Log.Information("Process {fileName} {step}",fileName, "start" );

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
           
            var tumbStream = TiffTothumb(stream);

            var targetClient = _hemerotecav2.GetBlobClient(CalculateBlobName(fileName));
            var targetTClient = _hemerotecav2.GetBlobClient(CalculateThumbBlobName(fileName,".tif"));

            try
            {

                if (!targetClient.Exists()) {
                    stream.Position = 0;
                    await targetClient.UploadAsync(stream);
                }
                    

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

                result = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Processing {path}", fileName);
            }
            finally {
                tumbStream.Close();


            }

            return result;
        }

        public Stream TiffTothumb(MemoryStream file)
        {

            Stream thumbStream = new MemoryStream();
            file.Position = 0;

            using (Tiff tif = Tiff.ClientOpen("in-memory", "r", file, new TiffStream()))
            {
                // Find the width and height of the image
                FieldValue[] value = tif.GetField(TiffTag.IMAGEWIDTH);
                int width = value[0].ToInt();

                value = tif.GetField(TiffTag.IMAGELENGTH);
                int height = value[0].ToInt();

                // Read the image into the memory buffer
                int[] raster = new int[height * width];
                if (!tif.ReadRGBAImage(width, height, raster))
                {
                    return null;
                }

                using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppRgb))
                {
                    Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    BitmapData bmpdata = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
                    byte[] bits = new byte[bmpdata.Stride * bmpdata.Height];

                    for (int y = 0; y < bmp.Height; y++)
                    {
                        int rasterOffset = y * bmp.Width;
                        int bitsOffset = (bmp.Height - y - 1) * bmpdata.Stride;

                        for (int x = 0; x < bmp.Width; x++)
                        {
                            int rgba = raster[rasterOffset++];
                            bits[bitsOffset++] = (byte)((rgba >> 16) & 0xff);
                            bits[bitsOffset++] = (byte)((rgba >> 8) & 0xff);
                            bits[bitsOffset++] = (byte)(rgba & 0xff);
                            bits[bitsOffset++] = (byte)((rgba >> 24) & 0xff);
                        }
                    }

                    System.Runtime.InteropServices.Marshal.Copy(bits, 0, bmpdata.Scan0, bits.Length);
                    bmp.UnlockBits(bmpdata);

                    const int thumbnailSize = 150;
                    var imageHeight = bmp.Height;
                    var imageWidth = bmp.Width;
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

                    using (var thumb = bmp.GetThumbnailImage(imageWidth, imageHeight, () => false, IntPtr.Zero))
                    {

                        thumb.Save(thumbStream, ImageFormat.Jpeg);
                        thumbStream.Position = 0;
                    }                   
                }

            }
           
            return thumbStream;


        }

    }
}
