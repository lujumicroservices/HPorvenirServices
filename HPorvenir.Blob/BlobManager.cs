using Azure.Storage.Blobs;
using BitMiracle.LibTiff.Classic;
using BitMiracle.Tiff2Pdf;
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
        int _hilos;


        public BlobManager(string path, int hilos) {
            connectionString = "DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net";
            blobServiceClient = new BlobServiceClient(connectionString);
            // Create the container and return a container client object
            _path = path;
            _hilos = hilos;
            containerClient = blobServiceClient.GetBlobContainerClient("hemeroteca");
            containerClient.CreateIfNotExists();
        }


        
        public async Task MigrateData() {

            //string basepath = @"D:\HPorvenir\";
            string basepath = _path;
            DirectoryInfo directory = new DirectoryInfo(basepath);

            await Navigate(directory);

        }



        public async Task ProcessFolder(DirectoryInfo directory) {
            var files = directory.GetFiles();

            var validDir = checkfilestructure(directory, out string year);

            if (!validDir || int.Parse(year) < 1940)
                return;

            Console.WriteLine($"PROCESSING FOLDER {directory.FullName}");
           

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _hilos;


            //foreach (var file in files) {
            //    if ((file.Extension == ".tiff" || file.Extension == ".tif" || file.Extension == ".pdf"))
            //    {
            //        Console.WriteLine($"uploading data {file.FullName}");
            //        uploadBlob(file);
            //    }
            //}

            Parallel.ForEach(files, options, file =>
            {



                if ((file.Extension == ".tiff" || file.Extension == ".tif" || file.Extension == ".pdf"))
                {
                    Console.WriteLine($"uploading data {file.FullName}");
                    uploadBlob(file);
                }

            });


        }

        public bool uploadBlob(FileInfo file) {
            try
            {
                //BlobClient blobClient = containerClient.GetBlobClient(CalculateBlobName(file));
                //using FileStream uploadFileStream = File.OpenRead(file.FullName);


                var pdfStream = tiffToPdf(file);
                containerClient.UploadBlob(CalculateBlobName(file), pdfStream);
                var thumbStream = tiffTothumb(file);
                containerClient.UploadBlob(CalculateThumbBlobName(file), thumbStream);

                pdfStream.Dispose();
                thumbStream.Dispose();

                return true;
            }
            catch(Exception ex) {
                Console.WriteLine($"error uploading image {file.Name}");
                Console.WriteLine($"waiting 3");
                //Task.Delay(3000);
                
                //uploadBlob(file);
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


            var name = @$"{file.Directory.Parent.Parent.Name}\{file.Directory.Parent.Name}\{file.Directory.Name}\{file.Directory.Parent.Parent.Name}_{file.Directory.Parent.Name}_{file.Directory.Name}_{file.Name.Replace($"{file.Extension}",".pdf")}";
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
                year = dir.Parent.Parent.Name;
                return dir.Name.Length == 2 && dir.Parent.Name.Length == 2 && dir.Parent.Parent.Name.Length == 4;
            }
            catch {
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


    }
}
