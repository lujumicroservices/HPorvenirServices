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
    /// <summary>
    /// Copies METS XML from FTP into Azure Blob under <see cref="BlobStorageOptions.XmlBlobPrefix"/>.
    /// Index with <c>blob</c> mode afterward. Skips files already in blob with matching size.
    /// </summary>
    public class FtpXmlSyncRunner
    {
        private readonly IndexerSettings _settings;
        private readonly FtpOptions _ftpOptions;
        private readonly BlobContainerClient _container;
        private readonly string _xmlBlobPrefix;

        public FtpXmlSyncRunner(IndexerSettings settings, FtpOptions ftpOptions)
        {
            _settings = settings;
            _ftpOptions = ftpOptions;
            _container = new BlobContainerClient(
                settings.BlobStorage.ConnectionString,
                settings.BlobStorage.ContainerName);
            _xmlBlobPrefix = settings.BlobStorage.XmlBlobPrefix;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var start = _settings.Indexer.StartYear;
            var end = _settings.Indexer.EndYear;
            var fileParallelism = Math.Max(4, Math.Min(8, _settings.Indexer.Parallelism));
            var maxYears = Math.Max(1, Math.Min(2, _settings.Indexer.MaxConcurrentYears));
            var blobFolder = XmlBlobPathHelper.NormalizePrefix(_xmlBlobPrefix);

            Log.Information(
                "FTP → Blob XML sync years {StartYear}-{EndYear} → {Container}/{Prefix} | {FileParallelism} workers × {YearParallelism} years | heartbeat 5s",
                start,
                end,
                _settings.BlobStorage.ContainerName,
                string.IsNullOrEmpty(blobFolder) ? "(same tree as TIFF/PDF)" : blobFolder.TrimEnd('/'),
                fileParallelism,
                maxYears);

            IndexingProgressReporter.Emit(
                $"FTP sync: {fileParallelism} download threads (4–8), {maxYears} year(s) in parallel, status every 5s");

            var exists = await _container.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                throw new InvalidOperationException(
                    $"Blob container '{_settings.BlobStorage.ContainerName}' was not found.");
            }

            var years = Enumerable.Range(start, end - start + 1).ToList();
            var yearSemaphore = new SemaphoreSlim(maxYears);
            var failures = new List<(int Year, string Error)>();
            var failureLock = new object();

            var tasks = years.Select(async year =>
            {
                await yearSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await SyncYearAsync(year, fileParallelism, cancellationToken);
                }
                catch (Exception ex)
                {
                    lock (failureLock)
                    {
                        failures.Add((year, ex.Message));
                    }

                    IndexingProgressReporter.Emit($"[FAIL] FTP→Blob {year}: {ex.Message}");
                    Log.Error(ex, "FTP→Blob sync failed for year {Year}", year);
                }
                finally
                {
                    yearSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"FTP→Blob sync finished with {failures.Count} failed year(s): " +
                    string.Join("; ", failures.Select(f => $"{f.Year} ({f.Error})")));
            }
        }

        private async Task SyncYearAsync(int year, int fileParallelism, CancellationToken cancellationToken)
        {
            var ftp = new FtpXmlClient(_ftpOptions);
            var progress = new IndexingProgressReporter();
            var checkpointPath = IndexerSettingsLoader.BuildFtpSyncCheckpointPath(_settings, year);
            var failuresPath = IndexerSettingsLoader.BuildFtpSyncFailuresPath(_settings, year);
            var checkpoint = new IndexCheckpoint(checkpointPath, failuresPath);

            IndexingProgressReporter.Emit($"ftp-sync {year}: listing XML on FTP...");

            Timer scanTimer = null;
            if (fileParallelism > 0)
            {
                scanTimer = new Timer(
                    _ => IndexingProgressReporter.Emit($"ftp-sync {year}: still scanning FTP..."),
                    null,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(5));
            }

            IReadOnlyList<FtpXmlEntry> all;
            try
            {
                all = await ftp.DiscoverXmlForYearAsync(year, cancellationToken);
            }
            finally
            {
                scanTimer?.Dispose();
            }

            IndexingProgressReporter.Emit($"ftp-sync {year}: found {all.Count} XML files, checking blob...");

            var pending = await BuildPendingListAsync(all, checkpoint, fileParallelism, cancellationToken, year);

            var alreadyCompleted = all.Count - pending.Count;

            IndexingProgressReporter.Emit(
                $"--- FTP→Blob XML {year}: {pending.Count} to upload, {alreadyCompleted} already synced ---");

            if (pending.Count == 0)
            {
                checkpoint.Flush();
                return;
            }

            progress.Begin($"ftp-sync {year}", pending.Count, alreadyCompleted, heartbeatIntervalSeconds: 5);

            var uploaded = 0;
            var failed = 0;
            var fileSemaphore = new SemaphoreSlim(fileParallelism);
            var tasks = pending.Select(async entry =>
            {
                await fileSemaphore.WaitAsync(cancellationToken);
                progress.WorkerStarted();
                try
                {
                    progress.SetCurrentFile(entry.IndexPath);
                    var success = await UploadEntryAsync(ftp, entry, checkpoint, cancellationToken);
                    if (success)
                    {
                        Interlocked.Increment(ref uploaded);
                        progress.RecordSuccess();
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        progress.RecordFailure();
                    }
                }
                finally
                {
                    progress.WorkerFinished();
                    fileSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            checkpoint.Flush();
            progress.Complete();
            progress.Dispose();

            Log.Information(
                "FTP→Blob year {Year}: {Uploaded} uploaded, {Failed} failed, {Skipped} skipped",
                year,
                uploaded,
                failed,
                alreadyCompleted);

            if (failed > 0)
            {
                Log.Warning(
                    "FTP→Blob year {Year}: {Failed} failure(s) recorded in {Path}; continuing",
                    year,
                    failed,
                    failuresPath);
            }
        }

        private async Task<List<FtpXmlEntry>> BuildPendingListAsync(
            IReadOnlyList<FtpXmlEntry> all,
            IndexCheckpoint checkpoint,
            int fileParallelism,
            CancellationToken cancellationToken,
            int year)
        {
            var pending = new List<FtpXmlEntry>();
            var checkParallelism = Math.Max(4, Math.Min(8, fileParallelism));
            var checkedCount = 0;
            var checkLock = new object();

            Timer checkTimer = null;
            checkTimer = new Timer(
                _ =>
                {
                    int done;
                    lock (checkLock)
                    {
                        done = checkedCount;
                    }

                    IndexingProgressReporter.Emit(
                        $"ftp-sync {year}: blob check {done}/{all.Count}...");
                },
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

            try
            {
            var checkSemaphore = new SemaphoreSlim(checkParallelism);
            var tasks = all.Select(async entry =>
            {
                if (checkpoint.IsCompleted(entry.IndexPath))
                {
                    lock (checkLock)
                    {
                        checkedCount++;
                    }

                    return;
                }

                await checkSemaphore.WaitAsync(cancellationToken);
                try
                {
                    if (await BlobMatchesFtpSizeAsync(entry, cancellationToken))
                    {
                        checkpoint.MarkCompleted(entry.IndexPath);
                    }
                    else
                    {
                        lock (pending)
                        {
                            pending.Add(entry);
                        }
                    }
                }
                finally
                {
                    lock (checkLock)
                    {
                        checkedCount++;
                    }

                    checkSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            }
            finally
            {
                checkTimer?.Dispose();
            }

            return pending;
        }

        private async Task<bool> BlobMatchesFtpSizeAsync(FtpXmlEntry entry, CancellationToken cancellationToken)
        {
            if (entry.SizeBytes <= 0)
            {
                return false;
            }

            var blob = _container.GetBlobClient(ToBlobPath(entry));
            if (!await blob.ExistsAsync(cancellationToken))
            {
                return false;
            }

            var props = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            return props.Value.ContentLength == entry.SizeBytes;
        }

        private string ToBlobPath(FtpXmlEntry entry) =>
            XmlBlobPathHelper.ToBlobPath(entry.IndexPath, _xmlBlobPrefix);

        private async Task<bool> UploadEntryAsync(
            FtpXmlClient ftp,
            FtpXmlEntry entry,
            IndexCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var stream = await ftp.DownloadToMemoryAsync(entry.RemotePath, cancellationToken))
                {
                    var blobPath = ToBlobPath(entry);
                    var blob = _container.GetBlobClient(blobPath);
                    await blob.UploadAsync(stream, overwrite: true, cancellationToken);
                }

                checkpoint.MarkCompleted(entry.IndexPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FTP→Blob failed {RemotePath} → {BlobPath}", entry.RemotePath, ToBlobPath(entry));
                checkpoint.RecordFailure(entry.IndexPath, ex.Message);
                return false;
            }
        }
    }
}
