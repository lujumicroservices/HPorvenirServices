using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    /// <summary>
    /// Option B: sync FTP XML to blob, then index all years from blob in parallel.
    /// </summary>
    public class UnifiedIndexOrchestrator
    {
        private readonly IndexerSettings _settings;
        private readonly Func<IndexerSettings, int, Task> _indexYearAsync;

        private readonly bool _syncFtpXmlFirst;

        public UnifiedIndexOrchestrator(
            IndexerSettings settings,
            Func<IndexerSettings, int, Task> indexYearAsync,
            bool syncFtpXmlFirst = true)
        {
            _settings = settings;
            _indexYearAsync = indexYearAsync;
            _syncFtpXmlFirst = syncFtpXmlFirst;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var start = _settings.Indexer.StartYear;
            var end = _settings.Indexer.EndYear;
            var ftpLast = _settings.Indexer.FtpXmlLastYear;
            var maxYears = _settings.Indexer.MaxConcurrentYears;

            var syncEnd = Math.Min(end, ftpLast);
            if (_syncFtpXmlFirst && start <= syncEnd)
            {
                Console.WriteLine();
                Console.WriteLine($"=== Phase 1: FTP → Blob XML sync ({start}-{syncEnd}) ===");
                Console.Out.Flush();

                var syncSettings = CloneForRange(_settings, start, syncEnd);
                var ftpOptions = FtpSettingsLoader.Load();
                var syncRunner = new FtpXmlSyncRunner(syncSettings, ftpOptions);
                await syncRunner.ExecuteAsync(cancellationToken);
            }

            var years = Enumerable.Range(start, end - start + 1).ToList();

            Console.WriteLine();
            Console.WriteLine(_syncFtpXmlFirst
                ? "=== Phase 2: Index from Blob ==="
                : "=== Index from Blob (XML: hporvenirxml/, PDF: year folders) ===");
            Console.WriteLine(
                $"Years {start}-{end} | {maxYears} concurrent years | {_settings.Indexer.Parallelism} threads/year");
            Console.Out.Flush();

            var semaphore = new SemaphoreSlim(maxYears);
            var failures = new List<(int Year, string Error)>();
            var lockObj = new object();

            var tasks = years.Select(async year =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    Console.WriteLine($"[START] {year} (BLOB)");
                    Console.Out.Flush();

                    var yearSettings = CloneForYear(_settings, year, "blob");
                    await _indexYearAsync(yearSettings, year);

                    Console.WriteLine($"[DONE]  {year}");
                    Console.Out.Flush();
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        failures.Add((year, ex.Message));
                    }

                    Console.WriteLine($"[FAIL]  {year}: {ex.Message}");
                    Log.Error(ex, "Year {Year} failed", year);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Indexing finished with {failures.Count} failed year(s): " +
                    string.Join("; ", failures.Select(f => $"{f.Year} ({f.Error})")));
            }
        }

        private static IndexerSettings CloneForYear(IndexerSettings template, int year, string sourceMode)
        {
            var clone = CloneForRange(template, year, year);
            clone.Indexer.SourceMode = sourceMode;
            return clone;
        }

        private static IndexerSettings CloneForRange(IndexerSettings template, int startYear, int endYear)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(template);
            var clone = Newtonsoft.Json.JsonConvert.DeserializeObject<IndexerSettings>(json);
            clone.Indexer.StartYear = startYear;
            clone.Indexer.EndYear = endYear;
            return clone;
        }
    }
}
