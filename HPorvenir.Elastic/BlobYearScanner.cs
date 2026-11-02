using Azure.Storage.Blobs;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    public static class BlobYearScanner
    {
        public static async Task<YearStorageStats> ScanYearAsync(
            BlobContainerClient container,
            int year,
            string xmlBlobPrefix = null,
            CancellationToken cancellationToken = default)
        {
            var stats = new YearStorageStats
            {
                Year = year,
                Source = "blob"
            };

            await ScanPrefixAsync(container, $"{year}/", xmlBlobPrefix, stats, cancellationToken);

            var normalizedXmlPrefix = XmlBlobPathHelper.NormalizePrefix(xmlBlobPrefix);
            if (!string.IsNullOrEmpty(normalizedXmlPrefix))
            {
                await ScanPrefixAsync(
                    container,
                    XmlBlobPathHelper.YearScanPrefix(year, xmlBlobPrefix),
                    xmlBlobPrefix,
                    stats,
                    cancellationToken);
            }

            Log.Debug(
                "Blob year {Year}: {PdfCount} PDF ({PdfGiB:F2} GiB)",
                year,
                stats.PdfCount,
                stats.PdfBytes / (1024.0 * 1024.0 * 1024.0));

            return stats;
        }

        private static async Task ScanPrefixAsync(
            BlobContainerClient container,
            string prefix,
            string xmlBlobPrefix,
            YearStorageStats stats,
            CancellationToken cancellationToken)
        {
            var xmlPrefixFolder = XmlBlobPathHelper.NormalizePrefix(xmlBlobPrefix);

            await foreach (var page in container
                               .GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken)
                               .AsPages(pageSizeHint: 5000))
            {
                foreach (var blob in page.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = blob.Name;
                    if (name.IndexOf("/thumb/", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    var size = blob.Properties.ContentLength ?? 0;
                    if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        stats.PdfCount++;
                        stats.PdfBytes += size;
                    }
                    else if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(xmlPrefixFolder) &&
                            !name.StartsWith(xmlPrefixFolder, StringComparison.OrdinalIgnoreCase) &&
                            prefix.StartsWith($"{stats.Year}/", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        stats.XmlCount++;
                        stats.XmlBytes += size;
                    }
                    else if (name.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                             name.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                    {
                        stats.TifCount++;
                        stats.TifBytes += size;
                    }
                }
            }
        }
    }
}
