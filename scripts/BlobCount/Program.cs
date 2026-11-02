using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "HPorvenir.Elastic")))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var conn = config["BlobStorage:ConnectionString"];
        var containerName = config["BlobStorage:ContainerName"] ?? "hemerotecav2";
        var container = new BlobContainerClient(conn, containerName);

        var byYear = new Dictionary<int, (int Tif, int Pdf, int Xml, int Other)>();
        var totalTif = 0;
        var totalPdf = 0;
        var totalXml = 0;
        var totalOther = 0;
        var scanned = 0;

        await foreach (var page in container.GetBlobsAsync().AsPages())
        {
            foreach (var blob in page.Values)
            {
                scanned++;
                var name = blob.Name.ToLowerInvariant();
                var year = 0;
                var slash = name.IndexOf('/');
                if (slash >= 4 && int.TryParse(name.Substring(0, slash), out var y) && y >= 1900 && y <= 2100)
                {
                    year = y;
                }

                if (!byYear.ContainsKey(year))
                {
                    byYear[year] = (0, 0, 0, 0);
                }

                var row = byYear[year];
                if (name.EndsWith(".tif") || name.EndsWith(".tiff"))
                {
                    totalTif++;
                    byYear[year] = (row.Tif + 1, row.Pdf, row.Xml, row.Other);
                }
                else if (name.EndsWith(".pdf"))
                {
                    totalPdf++;
                    byYear[year] = (row.Tif, row.Pdf + 1, row.Xml, row.Other);
                }
                else if (name.EndsWith(".xml"))
                {
                    totalXml++;
                    byYear[year] = (row.Tif, row.Pdf, row.Xml + 1, row.Other);
                }
                else
                {
                    totalOther++;
                    byYear[year] = (row.Tif, row.Pdf, row.Xml, row.Other + 1);
                }

                if (scanned % 50000 == 0)
                {
                    Console.WriteLine($"Scanned {scanned}... tif={totalTif} pdf={totalPdf}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Container: {containerName}");
        Console.WriteLine($"Scanned: {scanned}");
        Console.WriteLine($"TIFF: {totalTif}");
        Console.WriteLine($"PDF:  {totalPdf}");
        Console.WriteLine($"XML:  {totalXml}");
        Console.WriteLine($"Other:{totalOther}");
        Console.WriteLine();
        Console.WriteLine("Year   TIF      PDF      XML");
        foreach (var kv in byYear)
        {
            if (kv.Key == 0) continue;
            Console.WriteLine($"{kv.Key}  {kv.Value.Tif,7}  {kv.Value.Pdf,7}  {kv.Value.Xml,7}");
        }

        var cost1 = totalTif * 1.50 / 1000.0;
        var cost2 = totalTif > 1_000_000
            ? (1_000_000 * 1.50 / 1000.0) + ((totalTif - 1_000_000) * 0.60 / 1000.0)
            : cost1;
        Console.WriteLine();
        Console.WriteLine($"Azure DI Read estimate (1 page/TIF):");
        Console.WriteLine($"  Tier 1 ($1.50/1k): ${cost1:N0}");
        Console.WriteLine($"  Tiered (1M+ @ $0.60/1k): ${cost2:N0}");
    }
}
