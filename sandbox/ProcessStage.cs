using Azure.Storage.Blobs;
using HPorvenir.Elastic;
using HPorvenir.Model;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace sandbox
{
    /// <summary>
    /// Moves pending files from staging container <c>hporvenir</c> → <c>hemerotecav2</c>,
    /// indexes them, deletes staging copies, then refreshes navegacion libre metadata.
    /// </summary>
    public class ProcessStage
    {
        private readonly BlobContainerClient _container;
        private readonly BlobContainerClient _missingData;
        private readonly IndexerSettings _settings;
        private readonly int _parallelism;

        public ProcessStage(IndexerSettings settings = null, int parallelism = 8)
        {
            const string connectionString =
                "DefaultEndpointsProtocol=https;AccountName=hemerotecaporvenir;AccountKey=bNsoZn/JEWvP3pqSlD5p9tTQTzowNlWkXaMtKLa0MPppSnRK4QrLMvTGeyQcTh7b/x7cMTLMm/DoNqJ6bMFDDA==;EndpointSuffix=core.windows.net";

            _container = new BlobContainerClient(connectionString, "hporvenir");
            _missingData = new BlobContainerClient(connectionString, "metadata");
            _settings = settings ?? IndexerSettingsLoader.Load();
            _parallelism = Math.Max(1, parallelism);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var pendingBlobs = _container.GetBlobs()
                .Select(b => b.Name)
                .Where(n => n.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
                            n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n)
                .ToList();

            Log.Information("ProcessStage found {BlobCount} indexable blobs in staging container hporvenir", pendingBlobs.Count);
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Staging pending: {pendingBlobs.Count} files");

            if (pendingBlobs.Count == 0)
            {
                return;
            }

            await LoadMissingMetadataAsync();

            var byYear = pendingBlobs
                .GroupBy(GetYear)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var yearGroup in byYear)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessYearAsync(yearGroup.Key, yearGroup.ToList(), cancellationToken);
            }

            Log.Information("ProcessStage completed processing {BlobCount} blobs", pendingBlobs.Count);
        }

        private async Task ProcessYearAsync(int year, List<string> blobNames, CancellationToken cancellationToken)
        {
            Console.WriteLine();
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] === Year {year}: {blobNames.Count} staging files ===");

            var yearSettings = CloneSettings(_settings);
            yearSettings.Indexer.StartYear = year;
            yearSettings.Indexer.EndYear = year;
            yearSettings.Indexer.Parallelism = _parallelism;

            var indexName = $"hporvenirv2_{year}";
            var elasticClient = ElasticClientFactory.Create(yearSettings.Elastic, indexName);
            var indexManager = new ElasticIndexManager(elasticClient, yearSettings.Elastic);
            await indexManager.EnsureIndexAsync(indexName);

            var indexer = new HPorvenir.Elastic.Index(yearSettings, indexName);
            var done = 0;
            var failed = 0;
            var semaphore = new SemaphoreSlim(_parallelism);
            var start = DateTime.UtcNow;

            using var heartbeat = new Timer(_ =>
            {
                var elapsed = DateTime.UtcNow - start;
                var rate = done > 0 && elapsed.TotalSeconds > 0 ? done / elapsed.TotalSeconds : 0;
                Console.WriteLine(
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] stage {year} {done + failed}/{blobNames.Count} | ok {done} fail {failed} | {rate:F1}/s");
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            var tasks = blobNames.Select(async blobName =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var success = await ProcessBlobAsync(blobName, indexer, cancellationToken);
                    if (success)
                    {
                        Interlocked.Increment(ref done);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            await indexer.FlushAsync();
            indexer.FlushCheckpoint();

            if (yearSettings.Indexer.SwapAliasOnCompletion)
            {
                await indexManager.PublishYearToSearchAliasAsync(
                    yearSettings.Elastic.SearchAlias,
                    indexName,
                    yearSettings.Indexer.IndexNamePrefix,
                    year);
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Alias {yearSettings.Elastic.SearchAlias} includes {indexName}");
            }

            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Year {year} done — {done} ok, {failed} failed");
        }

        private async Task LoadMissingMetadataAsync()
        {
            try
            {
                var missingData = _missingData.GetBlobClient("missingDatesv3.json");
                if (!await missingData.ExistsAsync())
                {
                    Log.Warning("missingDatesv3.json not found in metadata container");
                    return;
                }

                using (var stream = new MemoryStream())
                {
                    await missingData.DownloadToAsync(stream);
                    stream.Position = 0;
                    using (var reader = new StreamReader(stream))
                    {
                        var content = await reader.ReadToEndAsync();
                        JsonConvert.DeserializeObject<MissingDataModel>(content);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load missingDatesv3.json; continuing without metadata");
            }
        }

        private async Task<bool> ProcessBlobAsync(
            string blobName,
            HPorvenir.Elastic.Index elasticIndex,
            CancellationToken cancellationToken)
        {
            try
            {
                if (blobName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    return await new ProcessXML(elasticIndex).ExecuteAsync(blobName);
                }

                if (blobName.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                    blobName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                {
                    return await new ProcessTIF(elasticIndex).ExecuteAsync(blobName);
                }

                if (blobName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return await new ProcessPDF(elasticIndex).ExecuteAsync(blobName);
                }

                await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);
                Log.Information("Deleted unsupported staging blob {BlobName}", blobName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process staging blob {BlobName}", blobName);
                return false;
            }
        }

        private static int GetYear(string blobName)
        {
            var dayFolder = blobName.Replace('\\', '/').Split('/')[0];
            if (dayFolder.Length >= 4 && int.TryParse(dayFolder.Substring(0, 4), out var year))
            {
                return year;
            }

            return 0;
        }

        private static IndexerSettings CloneSettings(IndexerSettings template)
        {
            var json = JsonConvert.SerializeObject(template);
            return JsonConvert.DeserializeObject<IndexerSettings>(json);
        }
    }
}
