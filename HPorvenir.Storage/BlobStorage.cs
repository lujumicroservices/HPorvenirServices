using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BitMiracle.LibTiff.Classic;
using BitMiracle.Tiff2Pdf;
using HPorvenir.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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

             

            for (var x = 0; x< 10;) {
                var d = 0;
            }
        }

        public bool Read()
        {
            throw new NotImplementedException();
        }

        public bool Save(bool overwrite = false)
        {
            throw new NotImplementedException();
        }


        public Stream tiffToPdf(FileInfo file) {


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

            Stream thumbStream = null;

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


        
    



        public DayResult ListDay(int year, int month, int day) {


            var result = new DayResult {
                ShareKey = AuthKey,
                URLPrefix = URLPrefix,
                Container = ContainerName
            };

            List<string> files = new List<string>();
            List<string> files_thumb = new List<string>();

            var filter = $"{year}/{month.ToString("0#")}/{day.ToString("0#")}/";
            var blobs = _container.GetBlobs(prefix: filter);
            bool existPDF = false;

            foreach (BlobItem blob in blobs)
            {
                if (blob.Name.Contains(".pdf"))
                {
                    files.Add(blob.Name);
                }
                else if (blob.Name.Contains(".jpg")) {
                    files_thumb.Add(blob.Name);
                }
            }

            
            result.Pages = files;
            result.Thumb = files_thumb;
             
            return result;
        
        }
    }
}
