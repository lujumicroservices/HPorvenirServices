using Azure.Storage.Blobs;
using HPorvenir.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sandbox
{
    public class ProcessStage
    {

        string stageContainer = "hemerotecav2";
        string prodContainer = "hporvenir";

        BlobContainerClient _container = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hporvenir");
        BlobContainerClient _missingData = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "metadata");


        public async Task ExecuteAsync()
        {
            var pending_blobs = _container.GetBlobs();
            var missingData = _missingData.GetBlobClient("missingDatesv2.json");


            MissingDataModel missing = null;
            using (MemoryStream stream = new MemoryStream()) {
                missingData.DownloadTo(stream);
                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream)) {
                    var content = reader.ReadToEnd();
                    missing = JsonConvert.DeserializeObject<MissingDataModel>(content);
                }

                
            }


            ConcurrentBag<string> daysProcessed = new ConcurrentBag<string>();

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;
            //foreach (var b in pending_blobs)
            Parallel.ForEach(pending_blobs, options, b =>
            {
                bool success = true;
                try
                {
                    if (b.Name.EndsWith(".xml"))
                    {
                        ProcessXML xml = new ProcessXML();
                        success = xml.ExecuteAsync(b.Name).Result;
                    }
                    else if (b.Name.EndsWith(".tif"))
                    {
                        ProcessTIF tif = new ProcessTIF();
                        success = tif.ExecuteAsync(b.Name).Result;
                    }
                    else if (b.Name.EndsWith(".pdf"))
                    {
                        ProcessPDF pdf = new ProcessPDF();
                        success = pdf.ExecuteAsync(b.Name).Result;
                    }
                    else
                    {
                        _container.DeleteBlob(b.Name);
                    }
                }
                catch (Exception ex)
                {

                }


                //if (success) {
                //    var pathParts = b.Name.Split('/');
                //    if (pathParts.Length == 2 && pathParts[0].Length == 8)
                //    {
                //        var name = @$"{pathParts[0].Substring(0, 4)}{pathParts[0].Substring(4, 2)}{pathParts[0].Substring(6, 2)}";
                //        if (!daysProcessed.Any(x => x == name)) {
                //            daysProcessed.Add(name);
                //        }
                //    }
                //}

            });


            //foreach (var pday in daysProcessed)
            //{
            //    int year = int.Parse(pday.Substring(0, 4));
            //    int month = int.Parse(pday.Substring(4, 2));
            //    int day = int.Parse(pday.Substring(6, 2));

            //    if (missing.missingDate.ContainsKey(year))
            //    {
            //        if (missing.missingDate[year].ContainsKey(month))
            //        {
            //            if (missing.missingDate[year][month].Count == 0) {
            //                var curr = new DateTime(year, month, day);
            //                var start = new DateTime(year, month, 1);
            //                var end = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

            //                while (start.CompareTo(end) < 0)
            //                {
            //                    if (start.CompareTo(curr) != 0)
            //                    {
            //                        missing.missingDate[year][month].Add(start.Day);
            //                    }
            //                }
            //            }
            //            else if (missing.missingDate[year][month].Contains(day))
            //            {
            //                missing.missingDate[year][month].Remove(day);
            //            }
            //        }
            //        else
            //        {
            //            if (int.Parse(pday.Substring(0,6)) > int.Parse(missing.endDate.Substring(0, 6))){
            //                var curr = new DateTime(int.Parse(missing.endDate.Substring(0,4)), int.Parse(missing.endDate.Substring(4, 2)), int.Parse(missing.endDate.Substring(6, 2)));
            //                var start = new DateTime(int.Parse(missing.endDate.Substring(0, 4)), int.Parse(missing.endDate.Substring(4, 2)), int.Parse(missing.endDate.Substring(6, 2)));
            //                var end = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).AddDays(-1);

            //                while (end.CompareTo(curr) > 0) { 

                                



            //                }
                                                        
            //                missing.missingDate[year].Add(month, new List<int>());
            //            }
                        
                        
            //        }
            //    }
            //}

        }


    }
}
