using Azure;
using Azure.Storage.Blobs;
using BitMiracle.LibTiff.Classic;
using BitMiracle.Tiff2Pdf;
using Serilog;
using Spire.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
        
        int _hilos = 1;        
        int _start;
        int _end;

        public BlobManager() { 
        }

        public BlobManager(string path, int hilos, int start = 0, int end = 3000) {
            
            connectionString = "DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net";
            blobServiceClient = new BlobServiceClient(connectionString);
            
            // Create the container and return a container client object
            _path = path;
            _hilos = hilos;
            _start = start;
            _end = end;

            containerClient = blobServiceClient.GetBlobContainerClient("hemerotecav2");
            containerClient.CreateIfNotExists();
        }

        
        public async Task MigrateData() {
            //string basepath = @"D:\HPorvenir\";
            string basepath = _path;
            DirectoryInfo directory = new DirectoryInfo(basepath);
            await Navigate(directory);
        }



        public Task ProcessFolder(DirectoryInfo directory)
        {
            var validDir = checkfilestructure(directory, out string year);


            if (!validDir) {
                Log.Error("INVALID {directory}", directory.FullName);
                return Task.CompletedTask;
            }
                

            var files = directory.GetFiles("*.pdf");

            if (files.Length == 0)
            {
                files = directory.GetFiles("*.tif");
            }

            Console.WriteLine($"PROCESSING FOLDER {directory.FullName}");
           
            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _hilos;
       
            Parallel.ForEach(files, options, file =>
            {                
                Console.WriteLine($"uploading data {file.FullName}");
                var uploadresult =  uploadBlobAsync(file).Result;                                
            });
            return Task.CompletedTask;
        }

        public async Task<bool> uploadBlobAsync(FileInfo file) {
            try
            {

                using FileStream uploadFileStream = File.OpenRead(file.FullName);
                var upresult = await containerClient.UploadBlobAsync(CalculateBlobName(file), uploadFileStream);



                if (file.Extension == ".tif")
                {
                    var thumbStream = tiffTothumb(file);
                    containerClient.UploadBlob(CalculateThumbBlobName(file), thumbStream);
                }

                if (file.Extension == ".pdf")
                {
                    PdfDocument doc = new PdfDocument();
                    doc.LoadFromFile(file.FullName);
                    Stream image = doc.SaveAsImage(0);
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
                    containerClient.UploadBlob(CalculateThumbBlobName(file), thumbStream);
                }



                ////BlobClient blobClient = containerClient.GetBlobClient(CalculateBlobName(file));
                ////using FileStream uploadFileStream = File.OpenRead(file.FullName);

                //var pdfStream = tiffToPdf(file);
                //containerClient.UploadBlob(CalculateBlobName(file), pdfStream);
                //var thumbStream = tiffTothumb(file);
                //containerClient.UploadBlob(CalculateThumbBlobName(file), thumbStream);

                //pdfStream.Dispose();
                //thumbStream.Dispose();

                return true;
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == 409)
                    Log.Error("UPLOAD DUPLICATED ERROR {file}", file.Name);
                else {
                    Log.Error("UPLOAD ERROR {status} {file}",ex.Status,  file.Name);
                }
                return false;

            }
            catch (Exception ex) {
                Log.Error("UPLOAD ERROR {file}", file.Name);
                return false;
            }
            

        }

       
        public async Task Navigate(DirectoryInfo directory) {

            if (directory.Name.Length == 4 && (int.Parse(directory.Name) < _start || int.Parse(directory.Name) > _end))
                return;


            if (directory.GetFiles().Length > 0)
            {
                await ProcessFolder(directory);
            }


            if (directory.GetDirectories().Length > 0)
            {

                foreach (var dir in directory.GetDirectories())
                {
                    Console.WriteLine($"navigating folder {dir.FullName}");
                    await Navigate(dir);
                }
            }
            
        }


        private string CalculateBlobName(FileInfo file) {


            var name = @$"{file.Directory.Parent.Parent.Name}\{file.Directory.Parent.Name}\{file.Directory.Name}\{file.Directory.Parent.Parent.Name}_{file.Directory.Parent.Name}_{file.Directory.Name}_{file.Name}";
            return name;
        }

        private string CalculateThumbBlobName(FileInfo file)
        {


            var name = @$"{file.Directory.Parent.Parent.Name}\{file.Directory.Parent.Name}\{file.Directory.Name}\thumb\{file.Directory.Parent.Parent.Name}_{file.Directory.Parent.Name}_{file.Directory.Name}_{file.Name.Replace($"{file.Extension}", ".jpg")}";
            return name;
        }

        private bool checkfilestructure(DirectoryInfo dir, out string year) {


            try
            {
                if ("El Porvenir" != dir.Parent.Parent.Parent.Name)
                {
                    throw new Exception("invalid file structure");
                }


                year = dir.Parent.Parent.Name;
                return dir.Name.Length == 2 && dir.Parent.Name.Length == 2 && dir.Parent.Parent.Name.Length == 4;
            }
            catch
            {
                //Log.Information("error {dirname}", dir.FullName);
                year = "0";
                return false;
            }

          
            

        }

        public Stream tiffToPdf(FileInfo file)
        {


            Stream pdfStream = null;

            using (Tiff image = Tiff.ClientOpen("in-memory", "r", file.OpenRead(), new TiffStream()))
            {
                PDFConverter converter = new PDFConverter();
                pdfStream = converter.Convert(image, "name");

            }

            return pdfStream;

        }

        public Stream tiffTothumb(FileInfo file)
        {

            Stream thumbStream = new MemoryStream();

            using (Tiff tif = Tiff.ClientOpen("in-memory", "r", file.OpenRead(), new TiffStream()))
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

                    //_container.UploadBlob(blob.Name.Replace(".tif", "_thumb.jpg"), tstream);
                    //files_thumb.Add(blob.Name.Replace(".tif", "_thumb.jpg"));

                }

            }

            return thumbStream;


        }

        public void exportIndex() {

            string basepath = @"E:\Indices";
            var baseIndexFolder = new System.IO.DirectoryInfo(basepath);
            var existingIndexes = baseIndexFolder.GetDirectories();
            foreach (var indexfolder in existingIndexes) {
                Console.WriteLine($"procesing index {indexfolder.Name}");
                uploadIndex(indexfolder);
            }
        }

        private bool uploadIndex(DirectoryInfo index) {

            containerClient = blobServiceClient.GetBlobContainerClient(index.Name.Replace("Porvenir_",""));
            containerClient.CreateIfNotExists();
            var indexFiles = index.GetFiles();
            foreach (var file in indexFiles) {
                using (FileStream fs = file.OpenRead())
                {
                    Console.WriteLine($"uploading file {file.Name}");
                    containerClient.UploadBlob(file.Name, fs);
                }                
            }

            return true;
        }

        

    }
}
