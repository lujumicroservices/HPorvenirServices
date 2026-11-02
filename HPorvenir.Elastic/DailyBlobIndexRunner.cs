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
    /// <summary>
    /// Indexes only blobs created or overwritten since the last successful run.
    /// The watermark is stored in the container so the job can run from any machine.
    /// </summary>
    public class DailyBlobIndexRunner
    {
        private readonly IndexerSettings _settings;
        private readonly BlobContainerClient _container;

        public DailyBlobIndexRunner(IndexerSettings settings)
        {
            _settings = settings;
            _container = new BlobContainerClient(
                settings.BlobStorage.ConnectionString,
                settings.BlobStorage.ContainerName);
        }

        public async Task ExecuteAsync(
            Func<IndexerSettings, int, Task> indexYearAsync,
            CancellationToken cancellationToken = default)
        {
            if (indexYearAsync == null)
            {
                throw new ArgumentNullException(nameof(indexYearAsync));
            }

            var runStarted = DateTimeOffset.UtcNow;
            var storedWatermark = await TryReadWatermarkAsync(cancellationToken);
            var watermark = storedWatermark ?? runStarted.AddDays(-Math.Max(1, _settings.Indexer.DailyInitialLookbackDays));
            var overlap = TimeSpan.FromHours(Math.Max(0, _settings.Indexer.DailyOverlapHours));
            var modifiedSince = watermark - overlap;

            Console.WriteLine(
                storedWatermark.HasValue
                    ? $"Daily index since {modifiedSince:u} (watermark {watermark:u}, overlap {_settings.Indexer.DailyOverlapHours}h)"
                    : $"No watermark yet. First run indexes blobs modified since {modifiedSince:u}");
            Console.Out.Flush();

            Log.Information(
                "Daily blob index. Watermark {Watermark}, modified since {ModifiedSince}, years {StartYear}-{EndYear}",
                watermark,
                modifiedSince,
                _settings.Indexer.StartYear,
                _settings.Indexer.EndYear);

            var scanner = new BlobContainerScanner(_settings);
            var names = await scanner.ListIndexableNamesAsync(modifiedSince, cancellationToken);

            var byYear = new SortedDictionary<int, List<string>>();
            var unrecognized = 0;
            foreach (var name in names)
            {
                if (!TryGetYear(name, out var year))
                {
                    unrecognized++;
                    Log.Warning("Skipping blob with no year in the path: {BlobName}", name);
                    continue;
                }

                if (!byYear.TryGetValue(year, out var yearNames))
                {
                    yearNames = new List<string>();
                    byYear[year] = yearNames;
                }

                yearNames.Add(name);
            }

            if (unrecognized > 0)
            {
                Console.WriteLine($"Skipped {unrecognized} blobs whose path does not start with a year.");
            }

            if (byYear.Count == 0)
            {
                Console.WriteLine("No new PDF or XML blobs to index.");
                await SaveWatermarkAsync(runStarted, processed: 0, failed: 0, cancellationToken);
                return;
            }

            Console.WriteLine($"New blobs: {names.Count - unrecognized} across years {string.Join(", ", byYear.Keys)}");
            Console.Out.Flush();

            var processed = 0;
            var failed = 0;
            _settings.Indexer.SourceMode = "blob";

            foreach (var year in byYear)
            {
                Console.WriteLine($"--- {year.Key}: {year.Value.Count} new blobs ---");
                Console.Out.Flush();
                _settings.Indexer.OnlyBlobNames = year.Value;

                try
                {
                    await indexYearAsync(_settings, year.Key);
                    processed += _settings.Indexer.LastRunProcessed;
                    failed += _settings.Indexer.LastRunFailed;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Error(ex, "Daily index failed for year {Year}", year.Key);
                    Console.WriteLine($"Year {year.Key} failed: {ex.Message}");
                }
            }

            _settings.Indexer.OnlyBlobNames = null;

            Console.WriteLine($"Daily run indexed {processed}, failed {failed}.");

            if (failed > 0)
            {
                throw new InvalidOperationException(
                    $"Daily index finished with {failed} failure(s). Watermark stays at {watermark:u} so the next run retries them.");
            }

            await SaveWatermarkAsync(runStarted, processed, failed, cancellationToken);
            Console.WriteLine($"Watermark saved at {runStarted:u}.");
        }

        private async Task<DateTimeOffset?> TryReadWatermarkAsync(CancellationToken cancellationToken)
        {
            var blob = _container.GetBlobClient(_settings.Indexer.DailyStateBlobName);
            if (!await blob.ExistsAsync(cancellationToken))
            {
                return null;
            }

            using var stream = new MemoryStream();
            await blob.DownloadToAsync(stream, cancellationToken);
            var json = Encoding.UTF8.GetString(stream.ToArray());
            var state = JsonConvert.DeserializeObject<DailyIndexState>(json);
            if (state == null || state.WatermarkUtc == default)
            {
                throw new InvalidOperationException(
                    $"Daily watermark blob '{_settings.Indexer.DailyStateBlobName}' is empty or invalid.");
            }

            return state.WatermarkUtc;
        }

        private async Task SaveWatermarkAsync(
            DateTimeOffset watermarkUtc,
            int processed,
            int failed,
            CancellationToken cancellationToken)
        {
            var current = await TryReadWatermarkAsync(cancellationToken);
            if (current.HasValue && current.Value > watermarkUtc)
            {
                Log.Information(
                    "Keeping newer watermark {Current} instead of {Watermark}",
                    current.Value,
                    watermarkUtc);
                return;
            }

            var state = new DailyIndexState
            {
                WatermarkUtc = watermarkUtc,
                UpdatedUtc = DateTimeOffset.UtcNow,
                LastProcessed = processed,
                LastFailed = failed
            };

            var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state, Formatting.Indented));
            using var stream = new MemoryStream(payload);
            var blob = _container.GetBlobClient(_settings.Indexer.DailyStateBlobName);
            await blob.UploadAsync(stream, overwrite: true, cancellationToken);
            Log.Information("Saved daily watermark {Watermark} to {BlobName}", watermarkUtc, blob.Name);
        }

        private bool TryGetYear(string blobName, out int year)
        {
            year = 0;
            var path = blobName.Replace('\\', '/').TrimStart('/');
            var prefix = XmlBlobPathHelper.NormalizePrefix(_settings.BlobStorage.XmlBlobPrefix);
            if (!string.IsNullOrEmpty(prefix) &&
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(prefix.Length);
            }

            var slash = path.IndexOf('/');
            var yearText = slash < 0 ? path : path.Substring(0, slash);
            return yearText.Length == 4 && int.TryParse(yearText, out year);
        }

        private class DailyIndexState
        {
            public DateTimeOffset WatermarkUtc { get; set; }
            public DateTimeOffset UpdatedUtc { get; set; }
            public int LastProcessed { get; set; }
            public int LastFailed { get; set; }
        }
    }
}
