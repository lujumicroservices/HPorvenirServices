using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    /// <summary>
    /// Indexes METS XML directly from FTP into Elasticsearch.
    /// XML is read into memory (or temp buffers) only — FTP remains the permanent source; nothing is stored in Blob.
    /// </summary>
    public class FtpIndexRunner
    {
        private readonly IndexerSettings _settings;
        private readonly Index _index;
        private readonly FtpXmlClient _ftp;
        private readonly IndexingProgressReporter _progress = new IndexingProgressReporter();

        public FtpIndexRunner(IndexerSettings settings, Index index, FtpOptions ftpOptions)
        {
            _settings = settings;
            _index = index;
            _ftp = new FtpXmlClient(ftpOptions);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Log.Information(
                "FTP XML index years {StartYear}-{EndYear} (no blob upload; FTP is permanent source)",
                _settings.Indexer.StartYear,
                _settings.Indexer.EndYear);

            var all = await _ftp.DiscoverXmlForYearAsync(
                _settings.Indexer.StartYear,
                cancellationToken);

            var pending = all
                .Where(entry => !_index.IsSourceCompleted(entry.SourceKey))
                .ToList();

            var alreadyCompleted = all.Count - pending.Count;

            if (pending.Count == 0)
            {
                Log.Information(
                    "Year {Year} FTP XML already complete — {CompletedCount} sources checkpointed",
                    _settings.Indexer.StartYear,
                    alreadyCompleted);
                return;
            }

            var label = $"hporvenir ftp {_settings.Indexer.StartYear}";
            _progress.Begin(label, pending.Count, alreadyCompleted);

            var processed = 0;
            var failed = 0;
            var semaphore = new SemaphoreSlim(_settings.Indexer.Parallelism);
            var tasks = pending.Select(async entry =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    _progress.SetCurrentFile(entry.IndexPath);
                    var success = await IndexEntryAsync(entry, cancellationToken);
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
                "Year {Year} FTP done — {Processed} indexed, {Failed} failed, {Total} in this run",
                _settings.Indexer.StartYear,
                processed,
                failed,
                pending.Count);
        }

        private async Task<bool> IndexEntryAsync(FtpXmlEntry entry, CancellationToken cancellationToken)
        {
            try
            {
                using (var stream = await _ftp.DownloadToMemoryAsync(entry.RemotePath, cancellationToken))
                {
                    await _index.IndexXML(stream, entry.IndexPath, entry.SourceKey);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to index FTP XML {RemotePath} ({IndexPath})", entry.RemotePath, entry.IndexPath);
                _index.RecordFailure(entry.SourceKey, ex.Message);
                return false;
            }
        }
    }
}
