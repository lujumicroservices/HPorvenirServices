﻿using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BitMiracle.LibTiff.Classic;
using BitMiracle.Tiff2Pdf;
using HPorvenir.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HPorvenir.Storage
{
    public class BlobStorage : IStorage
    {

        private readonly IConfiguration _configuration;
        private readonly BlobContainerClient _container;
        private string URLPrefix;
        private string ContainerName;
        private string AuthKey;
        private readonly ILogger<BlobStorage> _logger;



        public BlobStorage(IConfiguration configuration, ILogger<BlobStorage> logger) {
            _configuration = configuration;
            _logger = logger;

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

        public async System.Threading.Tasks.Task<Stream> ReadPathFromIndexAsync(string pathId)
        {
            string stringDate = pathId.Substring(0, 8);
            string year = stringDate.Substring(0, 4);
            string month = stringDate.Substring(4, 2);
            string day = stringDate.Substring(6, 2);
            string filename = pathId.Substring(8, pathId.Length - 8);

            string path = $"{year}/{month}/{day}/{year}_{month}_{day}_{filename.Replace(".xml",".tif")}";            
            var bclient = _container.GetBlobClient(path);

            MemoryStream bstream = new MemoryStream();
            try
            {
                await bclient.DownloadToAsync(bstream);
            }
            catch (Exception ex) {
                _logger.LogError("fetching file from storage {@file}", path);
                throw new Exception($"Error occurs trying to fetch the file {path} ",ex);
            }
            
            bstream.Position = 0;
            return bstream;                     
        }

        public async System.Threading.Tasks.Task<Stream> ReadPathAsync(string pathId)
        {
            
            var bclient = _container.GetBlobClient(pathId);

            MemoryStream bstream = new MemoryStream();
            try
            {
                await bclient.DownloadToAsync(bstream);
            }
            catch (Exception ex)
            {
                _logger.LogError("fetching file from storage {@file}", pathId);
                throw new Exception($"Error occurs trying to fetch the file {pathId} ", ex);
            }

            bstream.Position = 0;
            return bstream;
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

            foreach (BlobItem blob in blobs)
            {
                if (blob.Name.Contains(".pdf") || blob.Name.Contains(".tif"))
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

        public Stream GetMetadata()
        {
            BlobContainerClient _missingData = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "metadata");
            var metadataClient = _missingData.GetBlobClient("missingDatesv3.json");
            MemoryStream stream = new MemoryStream();
            metadataClient.DownloadTo(stream);
            return stream;
        }
    }
}
