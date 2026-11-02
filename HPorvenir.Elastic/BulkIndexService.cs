using HPorvenir.Parser;
using Nest;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    public class BulkIndexService
    {
        private readonly ElasticClient _client;
        private readonly string _indexName;
        private readonly IndexerOptions _options;
        private readonly IndexCheckpoint _checkpoint;
        private readonly object _bufferLock = new object();
        private readonly List<BufferedParagraph> _buffer = new List<BufferedParagraph>();

        public BulkIndexService(
            ElasticClient client,
            string indexName,
            IndexerOptions options,
            IndexCheckpoint checkpoint)
        {
            _client = client;
            _indexName = indexName;
            _options = options;
            _checkpoint = checkpoint;
        }

        public int BufferedCount
        {
            get
            {
                lock (_bufferLock)
                {
                    return _buffer.Count;
                }
            }
        }

        public int CompletedSourceCount => _checkpoint.CompletedCount;

        public bool IsSourceCompleted(string sourceKey) => _checkpoint.IsCompleted(sourceKey);

        public void MarkSourceCompleted(string sourceKey) => _checkpoint.MarkCompleted(sourceKey);

        public void FlushCheckpoint() => _checkpoint.Flush();

        public string CheckpointPath => _checkpoint.CheckpointPath;

        public string LastCompletedSource => _checkpoint.LastCompletedSource;

        public void RecordFailure(string sourceKey, string error) => _checkpoint.RecordFailure(sourceKey, error);

        public void Enqueue(string sourceKey, IEnumerable<Paragraph> paragraphs)
        {
            var shouldFlush = false;

            lock (_bufferLock)
            {
                foreach (var paragraph in paragraphs)
                {
                    _buffer.Add(new BufferedParagraph
                    {
                        SourceKey = sourceKey,
                        Paragraph = paragraph
                    });
                }

                shouldFlush = _buffer.Count >= _options.BatchSize;
            }

            if (shouldFlush)
            {
                FlushAsync().GetAwaiter().GetResult();
            }
        }

        public async Task EnqueueAndFlushIfNeededAsync(
            string sourceKey,
            IEnumerable<Paragraph> paragraphs,
            bool forceFlush = false,
            CancellationToken cancellationToken = default)
        {
            Enqueue(sourceKey, paragraphs);

            if (forceFlush || BufferedCount >= _options.BatchSize)
            {
                await FlushAsync(cancellationToken);
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            List<BufferedParagraph> batch;

            lock (_bufferLock)
            {
                if (_buffer.Count == 0)
                {
                    return;
                }

                batch = new List<BufferedParagraph>(_buffer);
                _buffer.Clear();
            }

            try
            {
                await IndexBatchWithRetryAsync(batch, cancellationToken);
            }
            catch
            {
                lock (_bufferLock)
                {
                    _buffer.InsertRange(0, batch);
                }

                throw;
            }
        }

        public async Task IndexSourceAsync(
            string sourceKey,
            IEnumerable<Paragraph> paragraphs,
            CancellationToken cancellationToken = default)
        {
            var items = paragraphs.ToList();
            if (items.Count == 0)
            {
                Log.Warning("No paragraphs parsed for {SourceKey}", sourceKey);
                return;
            }

            await IndexBatchWithRetryAsync(
                items.Select(p => new BufferedParagraph { SourceKey = sourceKey, Paragraph = p }).ToList(),
                cancellationToken);
        }

        private async Task IndexBatchWithRetryAsync(
            List<BufferedParagraph> batch,
            CancellationToken cancellationToken)
        {
            var pending = batch;
            var delayMs = _options.InitialRetryDelayMs;

            for (var attempt = 1; attempt <= _options.MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var response = await _client.BulkAsync(b => BuildBulkDescriptor(b, pending), cancellationToken);

                    if (response.OriginalException != null)
                    {
                        throw new Exception(
                            response.OriginalException.Message,
                            response.OriginalException);
                    }

                    var failedItems = response.ItemsWithErrors?.ToList() ?? new List<BulkResponseItemBase>();
                    if (failedItems.Count == 0 && (response.IsValid || response.Items != null))
                    {
                        var completedSources = pending.Select(x => x.SourceKey).Distinct();
                        _checkpoint.MarkCompleted(completedSources);
                        Log.Debug(
                            "Indexed {DocumentCount} documents from {SourceCount} sources (attempt {Attempt})",
                            pending.Count,
                            completedSources.Count(),
                            attempt);
                        return;
                    }

                    if (!response.IsValid && failedItems.Count == 0)
                    {
                        throw new Exception(
                            response.ServerError?.Error?.Reason
                            ?? response.DebugInformation
                            ?? "Bulk request invalid");
                    }

                    if (!response.Errors)
                    {
                        var completedSources = pending.Select(x => x.SourceKey).Distinct();
                        _checkpoint.MarkCompleted(completedSources);
                        Log.Debug(
                            "Indexed {DocumentCount} documents from {SourceCount} sources (attempt {Attempt})",
                            pending.Count,
                            completedSources.Count(),
                            attempt);
                        return;
                    }

                    pending = CollectFailedItems(pending, response);
                    if (pending.Count == 0)
                    {
                        var completedSources = batch.Select(x => x.SourceKey).Distinct();
                        _checkpoint.MarkCompleted(completedSources);
                        Log.Debug(
                            "Indexed {DocumentCount} documents from {SourceCount} sources with per-item retries",
                            batch.Count,
                            completedSources.Count());
                        return;
                    }

                    Log.Warning(
                        "Bulk attempt {Attempt}/{MaxRetries} had {FailedCount} failed documents",
                        attempt,
                        _options.MaxRetries,
                        pending.Count);
                }
                catch (Exception ex)
                {
                    Log.Warning(
                        ex,
                        "Bulk attempt {Attempt}/{MaxRetries} failed for {DocumentCount} documents",
                        attempt,
                        _options.MaxRetries,
                        pending.Count);
                }

                if (attempt < _options.MaxRetries)
                {
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, 30000);
                }
            }

            foreach (var sourceKey in pending.Select(x => x.SourceKey).Distinct())
            {
                _checkpoint.RecordFailure(sourceKey, $"Failed after {_options.MaxRetries} bulk attempts");
            }

            throw new Exception(
                $"Failed to index {pending.Count} documents after {_options.MaxRetries} attempts. See {_options.FailedSourcesPath}.");
        }

        private BulkDescriptor BuildBulkDescriptor(BulkDescriptor descriptor, List<BufferedParagraph> batch)
        {
            foreach (var item in batch)
            {
                descriptor.Index<Paragraph>(op => op
                    .Index(_indexName)
                    .Id(item.Paragraph.Id)
                    .Document(item.Paragraph));
            }

            return descriptor;
        }

        private static List<BufferedParagraph> CollectFailedItems(
            List<BufferedParagraph> attempted,
            BulkResponse response)
        {
            if (response.ItemsWithErrors == null)
            {
                return attempted;
            }

            var failedIds = new HashSet<string>(
                response.ItemsWithErrors
                    .Where(item => item.Error != null)
                    .Select(item => item.Id),
                StringComparer.Ordinal);

            return attempted.Where(item => failedIds.Contains(item.Paragraph.Id)).ToList();
        }

        private class BufferedParagraph
        {
            public string SourceKey { get; set; }
            public Paragraph Paragraph { get; set; }
        }
    }
}
