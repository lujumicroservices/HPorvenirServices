using Azure.Storage.Blobs;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    public class BlobIndexRunner
    {
        private readonly IndexerSettings _settings;
        private readonly Index _index;
        private readonly BlobContainerClient _container;
        private readonly string _xmlBlobPrefix;
        private readonly IndexingProgressReporter _progress = new IndexingProgressReporter();

        public BlobIndexRunner(IndexerSettings settings, Index index)
        {
            _settings = settings;
            _index = index;
            _container = new BlobContainerClient(
                settings.BlobStorage.ConnectionString,
                settings.BlobStorage.ContainerName);
            _xmlBlobPrefix = settings.BlobStorage.XmlBlobPrefix;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Log.Information(
                "Blob container {Container}, years {StartYear}-{EndYear}",
                _settings.BlobStorage.ContainerName,
                _settings.Indexer.StartYear,
                _settings.Indexer.EndYear);

            var exists = await _container.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                throw new InvalidOperationException(
                    $"Blob container '{_settings.BlobStorage.ContainerName}' was not found.");
            }

            var discovery = await DiscoverBlobsAsync(cancellationToken);
            if (discovery.Pending.Count == 0)
            {
                Log.Information(
                    "Year {Year} already complete — {CompletedCount} sources checkpointed, nothing to resume",
                    _settings.Indexer.StartYear,
                    discovery.AlreadyCompleted);
                RememberRun(processed: 0, failed: 0);
                return;
            }

            var label = $"hporvenir {_settings.Indexer.StartYear}";
            _progress.Begin(label, discovery.Pending.Count, discovery.AlreadyCompleted);

            var processed = 0;
            var failed = 0;
            var semaphore = new SemaphoreSlim(_settings.Indexer.Parallelism);
            var tasks = discovery.Pending.Select(async blobName =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    _progress.SetCurrentFile(blobName);
                    var success = await IndexBlobAsync(blobName, cancellationToken);
                    if (success)
                    {
                        Interlocked.Increment(ref processed);
                        _progress.RecordSuccess();
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        _progress.RecordFailure();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            await _index.FlushAsync();
            _index.FlushCheckpoint();
            _progress.Complete();

            Log.Information(
                "Year {Year} done — {Processed} indexed, {Failed} failed, {Total} in this run",
                _settings.Indexer.StartYear,
                processed,
                failed,
                discovery.Pending.Count);
            RememberRun(processed, failed);
        }

        private void RememberRun(int processed, int failed)
        {
            _settings.Indexer.LastRunProcessed = processed;
            _settings.Indexer.LastRunFailed = failed;
        }

        private async Task<BlobDiscoveryResult> DiscoverBlobsAsync(CancellationToken cancellationToken)
        {
            List<string> indexable;
            if (_settings.Indexer.OnlyBlobNames != null)
            {
                indexable = _settings.Indexer.OnlyBlobNames.ToList();
            }
            else
            {
                var scanner = new BlobContainerScanner(_settings);
                indexable = await scanner.ListIndexableNamesAsync(modifiedSince: null, cancellationToken);
            }

            var pending = indexable
                .Where(name => !_index.IsSourceCompleted(name))
                .ToList();

            var alreadyCompleted = indexable.Count - pending.Count;

            Log.Information(
                "Year {Year}: {PendingCount} to index, {CompletedCount} already checkpointed ({TotalCount} total)",
                _settings.Indexer.StartYear,
                pending.Count,
                alreadyCompleted,
                indexable.Count);

            if (!string.IsNullOrWhiteSpace(_index.LastCompletedSource))
            {
                Log.Information("Resume after: {LastSource}", _index.LastCompletedSource);
            }

            return new BlobDiscoveryResult
            {
                Pending = pending,
                AlreadyCompleted = alreadyCompleted,
                Total = indexable.Count
            };
        }

        private async Task<bool> IndexBlobAsync(string blobName, CancellationToken cancellationToken)
        {
            try
            {
                var client = _container.GetBlobClient(blobName);
                using var stream = new MemoryStream();
                await client.DownloadToAsync(stream, cancellationToken);
                stream.Position = 0;

                if (blobName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    if (!XmlBlobPathHelper.TryToIndexPath(blobName, _xmlBlobPrefix, out var indexPath))
                    {
                        throw new InvalidOperationException($"Unsupported XML blob path: {blobName}");
                    }

                    await _index.IndexXML(stream, indexPath, blobName);
                }
                else if (blobName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    await _index.IndexPDF(stream, blobName);
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to index blob {BlobName}", blobName);
                _index.RecordFailure(blobName, ex.Message);
                return false;
            }
        }

        private class BlobDiscoveryResult
        {
            public List<string> Pending { get; set; } = new List<string>();
            public int AlreadyCompleted { get; set; }
            public int Total { get; set; }
        }
    }
}
