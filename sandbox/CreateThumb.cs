using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Serilog;
using Spire.Pdf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sandbox
{
    public class CreateThumb
    {

        public async Task ExecuteAsync(string stagePath, int threads, int start, int end)
        {


            BlobContainerClient _container = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hemerotecav2");

            

            var startDate = new DateTime(start, 01, 01);

            List<DateTime> datesToProcess = new List<DateTime>();
            while (startDate.Year < end) {
                datesToProcess.Add(startDate);
                startDate = startDate.AddDays(1);
                Log.Information("adding Date {0}" ,startDate);
            }


            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 32;

            ConcurrentDictionary<DateTime, int> missingDate = new System.Collections.Concurrent.ConcurrentDictionary<DateTime,int>();

            Log.Information("missing dates : {count} ", missingDate.Count);

            Parallel.ForEach(datesToProcess, options, date =>
             {

                
                 try
                 {
                     
                     var tfilter = $"{date.Year}/{date.Month.ToString("0#")}/{date.Day.ToString("0#")}/thumb/";
                     var tblobs = _container.GetBlobs(prefix: tfilter).ToList();
                     var filter = $"{date.Year}/{date.Month.ToString("0#")}/{date.Day.ToString("0#")}/";
                     var blobs = _container.GetBlobs(prefix: filter).ToList();

                     //Log.Information("message: {date}", date.ToShortDateString());

                     if (tblobs.Count < blobs.Where(x => x.Name.Contains(".pdf")).Count()) {
                         Log.Information("message: {date}  pdfs {pdf}  thumb {thumb}", date.ToShortDateString(), blobs.Where(x => x.Name.Contains(".pdf")).Count(), tblobs.Count);

                         missingDate.TryAdd(date, blobs.Where(x => x.Name.Contains(".pdf")).Count() - tblobs.Count);
                         Log.Information("missing dates : {count} ", missingDate.Count);
                         //Log.Information($"pdfs: {blobs.Where(x => x.Name.Contains(".pdf")).Count()}");
                         //Log.Information($"thumbs: {tblobs.Count}");
                     }
                     


                     return;

                     if (tblobs.Count < blobs.Where(x => x.Name.Contains(".pdf")).Count())
                     {                         
                         foreach (BlobItem blob in blobs)
                         {
                             var blobnewname = blob.Name.Replace(filter, tfilter).Replace(".pdf", ".jpg");


                             if (blob.Name.Contains(".pdf") && !tblobs.Any(x=>x.Name == blobnewname))
                             {
                                                                  
                                 var bcli = _container.GetBlobClient(blob.Name);

                                 MemoryStream bstream = new MemoryStream();
                                 try
                                 {
                                     var response = bcli.DownloadToAsync(bstream).Result;
                                     Log.Information($"donwload response : {response.Status}");
                                     Log.Information($"donwload reason : {response.ReasonPhrase}");


                                    //response.con
                                }
                                 catch (Exception ex)
                                 {
                                     Log.Error("fetching file from storage {@file}", blob.Name);
                                 }


                                 Log.Information($"loading pdf : {blob.Name}");
                                 PdfDocument doc = new PdfDocument();

                                 
                                 doc.LoadFromStream(bstream);
                                 Stream image;
                                 try
                                 {
                                     Log.Information($"saving image : {blob.Name}");
                                     image = doc.SaveAsImage(0);
                                     Log.Information($"save complete : {blob.Name}");

                                 }
                                 catch (Exception ex)
                                 {
                                     Log.Information($"save error : {blob.Name}");
                                     Log.Error(ex, $"invalid PDF {blob.Name} stream {bstream.Position}  sl {bstream.Length}");
                                     continue;
                                 }


                                 Log.Information($"create bitmap: {blob.Name}");
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

                                     thumb.Save(stagePath + "\\" + blobnewname.Replace("/", "@"));
                                     //thumb.Save(thumbStream, ImageFormat.Jpeg);
                                     //thumbStream.Position = 0;
                                 }



                                 
                                 bstream.Close();
                                 thumbStream.Close();
                                 bitmap.Dispose();
                                 image.Close();
                                 doc.Close();
                                 continue;




                                 //Log.Information($"uploading jpg : {blobnewname}");
                                 //try
                                 //{
                                 //    Log.Information($"thumb name: {blobnewname}");
                                 //    var response =  _container.UploadBlobAsync(blobnewname, thumbStream).Result;
                                 //}
                                 //catch (Exception ex)
                                 //{
                                 //    Log.Error($"error procesing image i guess already exists : {blobnewname}");
                                 //}


                                 //bstream.Dispose();
                                 //thumbStream.Dispose();
                                 //bitmap.Dispose();
                                 //image.Dispose();
                                 //doc.Dispose();

                             }
                         }

                     }
                 }
                 catch (Exception e)
                 {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    //exceptions.Add(e);
                 }
             });


            


        }


        public async Task ExecuteAsyncUp(string stagePath, int threads) {

            BlobContainerClient _container = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hemerotecav2");


            var thumbs = Directory.GetFiles(stagePath);

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = threads;
            

            Parallel.ForEach(thumbs, options, thumbName => {
                //thumbName.Replace("@", "/")
                MemoryStream destination = new MemoryStream();
                using (FileStream source = File.Open(thumbName, FileMode.Open))
                {
                    source.CopyTo(destination);
                }


                try
                {
                    destination.Position = 0;
                    var tname = thumbName.Replace(stagePath + "\\", "").Replace("@", "/");
                    Log.Information("uploading {thimb}", tname);
                    _container.UploadBlob(thumbName.Replace(stagePath + "\\", "").Replace("@", "/"), destination);
                    destination.Close();
                    Log.Information("deleting", tname);
                    File.Delete(thumbName);
                }
                catch(Exception ex) {
                    Log.Error(ex,"may be  duplicatged again");
                }
                

                
            });
        }
    
    }
}
