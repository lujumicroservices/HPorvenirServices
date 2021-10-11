using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace sandbox
{
    public class UpdateIndex
    {

        const int startnumber = 1919;         
        string stageContainer = "hemerotecav2";        

        BlobContainerClient _container = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "hemerotecav2");
        BlobContainerClient _missingData = new BlobContainerClient("DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net", "metadata");


        DateTime start = new DateTime(startnumber, 1, 1);
        DateTime end = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(1).AddDays(-1);
        DateTime globalDate = new DateTime(startnumber, 1, 1);

        Dictionary<int, Dictionary<int, List<int>>> missingDates;

        public void ExecuteAsync() {

            

            missingDates = new Dictionary<int, Dictionary<int, List<int>>>();

            
            var pending_blobs = _container.GetBlobsByHierarchy(prefix: "", delimiter:"/").ToList();
            foreach (var item in pending_blobs) {
                if (int.Parse(item.Prefix.Substring(0,4)) >= startnumber)
                {
                    var months = _container.GetBlobsByHierarchy(prefix: item.Prefix, delimiter: "/").ToList();
                    foreach (var month in months)
                    {
                        var days = _container.GetBlobsByHierarchy(prefix: month.Prefix, delimiter: "/").ToList();
                        foreach (var day in days)
                        {

                            var daySplit = day.Prefix.Split("/");
                            if (daySplit.Length == 4)
                            {
                                var curr = new DateTime(int.Parse(daySplit[0]), int.Parse(daySplit[1]), int.Parse(daySplit[2]));
                                globalDate = new DateTime(int.Parse(daySplit[0]), int.Parse(daySplit[1]), int.Parse(daySplit[2]));

                                evaluateDate(curr);

                                start = start.AddDays(1);
                            }
                        }
                    }
                }                            
            }

            evaluateDate(end.AddDays(1));

            var resut = new
            {
                startDate = $"{startnumber}0101",
                endDate = $"{end.Year}{end.Month.ToString("0#")}{end.Day.ToString("0#")}",
                missingDate = missingDates
            };

            var missingDatesString = JsonConvert.SerializeObject(resut);

            using (MemoryStream stream = new MemoryStream()) {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(missingDatesString);
                    writer.Flush();
                    stream.Position = 0;
                    _missingData.DeleteBlobIfExists("missingDatesv2.json");
                    _missingData.UploadBlob("missingDatesv2.json", stream);
                }

                
            }
            
        }


        private void  evaluateDate(DateTime curr) {
            var diff = start.CompareTo(curr);
            
            while (diff != 0)
            {
                //Console.WriteLine($"{start.ToLongDateString()} - {curr.ToLongDateString()}");

                if (diff < 0)
                {
                    if (!missingDates.ContainsKey(start.Year))
                    {
                        missingDates.Add(start.Year, new Dictionary<int, List<int>>());
                    }

                    if (curr.Year > start.Year && start.Month == 1)
                    {
                        Console.WriteLine($"year added {start.ToLongDateString()}");
                        //add year
                        start = new DateTime(start.Year + 1, 1, 1);
                    }
                    else if ((curr.Month > start.Month && start.Day == 1) || curr.Year > start.Year)
                    {
                        if (!missingDates[start.Year].ContainsKey(start.Month))
                        {
                            missingDates[start.Year].Add(start.Month, new List<int>());
                        }
                        Console.WriteLine($"month added {start.ToLongDateString()}");
                        //add month
                        var tempDate = start.AddMonths(1);
                        start = new DateTime(tempDate.Year, tempDate.Month, 1);
                    }
                    else
                    {
                        if (!missingDates[start.Year].ContainsKey(start.Month))
                        {
                            missingDates[start.Year].Add(start.Month, new List<int>());
                        }
                        missingDates[start.Year][start.Month].Add(start.Day);
                        Console.WriteLine($"day added {start.ToLongDateString()}");
                        //add day
                        start = start.AddDays(1);
                    }
                    diff = start.CompareTo(curr);
                }

            }
        }

    }
}
