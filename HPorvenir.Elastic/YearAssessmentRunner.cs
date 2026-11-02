using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    public class YearAssessmentRunner
    {
        private readonly IndexerSettings _settings;
        private readonly FtpOptions _ftpOptions;

        public YearAssessmentRunner(IndexerSettings settings, FtpOptions ftpOptions = null)
        {
            _settings = settings;
            _ftpOptions = ftpOptions;
        }

        public async Task<YearAssessmentReport> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var start = _settings.Indexer.StartYear;
            var end = _settings.Indexer.EndYear;
            var ftpLast = _settings.Indexer.FtpXmlLastYear;
            var parallel = Math.Min(_settings.Indexer.AssessmentParallelism, 3);

            Console.WriteLine($"Scanning storage for years {start}-{end} ({parallel} parallel scans)...");
            Console.WriteLine($"FTP XML years: {start}-{Math.Min(end, ftpLast)} | Blob PDF years: {Math.Max(start, ftpLast + 1)}-{end}");
            Console.Out.Flush();

            BlobContainerClient blobContainer = null;
            if (end > ftpLast &&
                !string.IsNullOrWhiteSpace(_settings.BlobStorage.ConnectionString))
            {
                blobContainer = new BlobContainerClient(
                    _settings.BlobStorage.ConnectionString,
                    _settings.BlobStorage.ContainerName);
            }

            FtpXmlClient ftpClient = null;
            if (start <= ftpLast && _ftpOptions != null)
            {
                ftpClient = new FtpXmlClient(_ftpOptions);
            }

            var years = Enumerable.Range(start, end - start + 1).ToList();
            var results = new YearStorageStats[years.Count];
            var semaphore = new SemaphoreSlim(parallel);
            var tasks = years.Select(async year =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var index = year - start;
                    results[index] = await ScanOneYearAsync(
                        year,
                        ftpClient,
                        blobContainer,
                        ftpLast,
                        _settings.BlobStorage.XmlBlobPrefix,
                        cancellationToken);
                    PrintYearLine(results[index]);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            var report = new YearAssessmentReport
            {
                StartYear = start,
                EndYear = end,
                FtpXmlLastYear = ftpLast,
                Years = results
            };

            var outPath = Path.Combine(Directory.GetCurrentDirectory(), "indexer-state", "year-size-assessment.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
            File.WriteAllText(outPath, JsonConvert.SerializeObject(report, Formatting.Indented));

            PrintSummary(report, outPath);
            return report;
        }

        private static async Task<YearStorageStats> ScanOneYearAsync(
            int year,
            FtpXmlClient ftpClient,
            BlobContainerClient blobContainer,
            int ftpLast,
            string xmlBlobPrefix,
            CancellationToken cancellationToken)
        {
            try
            {
                if (YearSourceCatalog.UsesFtp(year, ftpLast))
                {
                    if (ftpClient == null)
                    {
                        return new YearStorageStats
                        {
                            Year = year,
                            Source = "ftp",
                            ScanError = "FTP settings not configured"
                        };
                    }

                    return await ftpClient.ScanYearAsync(year, cancellationToken);
                }

                if (blobContainer == null)
                {
                    return new YearStorageStats
                    {
                        Year = year,
                        Source = "blob",
                        ScanError = "Blob storage not configured"
                    };
                }

                return await BlobYearScanner.ScanYearAsync(
                    blobContainer,
                    year,
                    xmlBlobPrefix,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Assessment failed for year {Year}", year);
                return new YearStorageStats
                {
                    Year = year,
                    Source = YearSourceCatalog.UsesFtp(year, ftpLast) ? "ftp" : "blob",
                    ScanError = ex.Message
                };
            }
        }

        private static void PrintYearLine(YearStorageStats s)
        {
            if (!string.IsNullOrEmpty(s.ScanError))
            {
                Console.WriteLine($"  {s.Year}  ERROR  {s.ScanError}");
                return;
            }

            if (s.Source == "ftp")
            {
                Console.WriteLine(
                    $"  {s.Year}  FTP   xml={s.XmlCount,6} ({FormatGiB(s.XmlBytes),8})");
            }
            else
            {
                Console.WriteLine(
                    $"  {s.Year}  BLOB  pdf={s.PdfCount,6} ({FormatGiB(s.PdfBytes),8})  xml={s.XmlCount,5}");
            }

            Console.Out.Flush();
        }

        private static void PrintSummary(YearAssessmentReport report, string outPath)
        {
            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Indexable files: {report.TotalIndexableFiles:N0}");
            Console.WriteLine($"Indexable size:  {report.TotalIndexableGiB:F2} GiB ({report.TotalIndexableBytes:N0} bytes)");
            Console.WriteLine();

            var ftpYears = report.Years.Where(y => y.Source == "ftp" && string.IsNullOrEmpty(y.ScanError)).ToList();
            var blobYears = report.Years.Where(y => y.Source == "blob" && string.IsNullOrEmpty(y.ScanError)).ToList();

            if (ftpYears.Count > 0)
            {
                var ftpXml = ftpYears.Sum(y => y.XmlBytes);
                var ftpFiles = ftpYears.Sum(y => y.XmlCount);
                Console.WriteLine($"FTP XML ({ftpYears.Count} years): {ftpFiles:N0} files, {ftpXml / (1024.0 * 1024.0 * 1024.0):F2} GiB");
            }

            if (blobYears.Count > 0)
            {
                var blobPdf = blobYears.Sum(y => y.PdfBytes);
                var blobFiles = blobYears.Sum(y => y.PdfCount);
                Console.WriteLine($"Blob PDF ({blobYears.Count} years): {blobFiles:N0} files, {blobPdf / (1024.0 * 1024.0 * 1024.0):F2} GiB");
            }

            Console.WriteLine();
            Console.WriteLine("=== Merge guidance (~5 GiB indexable payload per index) ===");
            PrintMergeSuggestions(report.Years.Where(y => string.IsNullOrEmpty(y.ScanError)).ToList());

            Console.WriteLine();
            Console.WriteLine($"Full report: {outPath}");
        }

        private static void PrintMergeSuggestions(IReadOnlyList<YearStorageStats> years)
        {
            const double targetGiB = 5.0;
            var sb = new StringBuilder();
            double bucketGiB = 0;
            int bucketStart = years[0].Year;
            var bucketYears = new List<int>();

            void FlushBucket()
            {
                if (bucketYears.Count == 0)
                {
                    return;
                }

                var label = bucketYears.Count == 1
                    ? $"hporvenirv2_{bucketStart}"
                    : $"hporvenirv2_{bucketStart}-{bucketYears[bucketYears.Count - 1]}";
                sb.AppendLine($"  ~{bucketGiB:F2} GiB  →  {label}  ({string.Join(", ", bucketYears)})");
                bucketYears.Clear();
                bucketGiB = 0;
            }

            foreach (var y in years.OrderBy(x => x.Year))
            {
                var g = y.IndexableGiB;
                if (bucketYears.Count > 0 && bucketGiB + g > targetGiB && bucketYears.Count >= 1)
                {
                    FlushBucket();
                    bucketStart = y.Year;
                }

                if (bucketYears.Count == 0)
                {
                    bucketStart = y.Year;
                }

                bucketYears.Add(y.Year);
                bucketGiB += g;
            }

            FlushBucket();
            Console.WriteLine(sb.ToString());
        }

        private static string FormatGiB(long bytes) => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GiB";
    }
}
