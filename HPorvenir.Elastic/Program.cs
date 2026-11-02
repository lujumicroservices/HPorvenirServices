using Serilog;

using Serilog.Events;

using System;

using System.IO;

using System.Threading.Tasks;



namespace HPorvenir.Elastic

{

    class Program

    {

        static async Task Main(string[] args)

        {

            Console.WriteLine("HPorvenir Elastic Indexer");

            Console.Out.Flush();



            var settings = IndexerSettingsLoader.Load(args);

            var mode = settings.Indexer.SourceMode ?? "blob";

            var runLabel = mode == "assess"
                ? "assess"
                : mode == "ftp-sync"
                    ? $"ftp-sync-{settings.Indexer.StartYear}-{settings.Indexer.EndYear}"
                    : mode == "daily"
                        ? "daily"
                        : $"{settings.Indexer.IndexNamePrefix}v2_{settings.Indexer.StartYear}-{settings.Indexer.EndYear}";

            var consoleMinLevel = mode == "ftp-sync" || mode == "daily"
                ? LogEventLevel.Information
                : LogEventLevel.Warning;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.Console(
                    restrictedToMinimumLevel: consoleMinLevel,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine("indexer-state", "logs", $"{runLabel}-.txt"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();



            Console.WriteLine($"Mode: {mode} | Years {settings.Indexer.StartYear}-{settings.Indexer.EndYear}");

            if (mode == "all" || mode == "ftp-sync" || mode == "blob")

            {

                var detail = mode == "blob"
                    ? $" | XML folder: {settings.BlobStorage.XmlBlobPrefix}"
                    : $" | alias {settings.Elastic.SearchAlias}";

                Console.WriteLine(

                    $"Parallelism: {settings.Indexer.Parallelism}/year × {settings.Indexer.MaxConcurrentYears} years{detail}");

            }

            else if (mode != "assess")

            {

                Console.WriteLine($"Parallelism: {settings.Indexer.Parallelism} | alias {settings.Elastic.SearchAlias}");

            }



            Console.WriteLine("Detailed logs: indexer-state/logs/");

            Console.Out.Flush();



            try

            {

                ValidateSettings(settings, mode);



                if (string.Equals(mode, "assess", StringComparison.OrdinalIgnoreCase))

                {

                    FtpOptions ftp = null;

                    try

                    {

                        ftp = FtpSettingsLoader.Load();

                    }

                    catch

                    {

                        // FTP optional if range is blob-only

                    }



                    var assessor = new YearAssessmentRunner(settings, ftp);

                    await assessor.ExecuteAsync();

                    return;

                }



                if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))

                {

                    var orchestrator = new UnifiedIndexOrchestrator(settings, IndexYearAsync);

                    await orchestrator.ExecuteAsync();

                    Console.WriteLine();

                    Console.WriteLine(

                        $"All years {settings.Indexer.StartYear}-{settings.Indexer.EndYear} complete. Alias: {settings.Elastic.SearchAlias}");

                    return;

                }



                if (string.Equals(mode, "ftp-sync", StringComparison.OrdinalIgnoreCase))

                {

                    var ftpOptions = FtpSettingsLoader.Load();

                    var syncRunner = new FtpXmlSyncRunner(settings, ftpOptions);

                    await syncRunner.ExecuteAsync();

                    Console.WriteLine();

                    Console.WriteLine(

                        $"FTP→Blob XML sync complete for {settings.Indexer.StartYear}-{settings.Indexer.EndYear}.");

                    return;

                }



                if (string.Equals(mode, "daily", StringComparison.OrdinalIgnoreCase))

                {

                    var dailyRunner = new DailyBlobIndexRunner(settings);

                    await dailyRunner.ExecuteAsync(IndexYearAsync);

                    Console.WriteLine();

                    Console.WriteLine(

                        $"Daily blob index complete. Alias: {settings.Elastic.SearchAlias}");

                    return;

                }



                var startYear = settings.Indexer.StartYear;

                var endYear = settings.Indexer.EndYear;



                if (string.Equals(mode, "blob", StringComparison.OrdinalIgnoreCase) && startYear != endYear)

                {

                    var orchestrator = new UnifiedIndexOrchestrator(settings, IndexYearAsync, syncFtpXmlFirst: false);

                    await orchestrator.ExecuteAsync();

                    Console.WriteLine();

                    Console.WriteLine($"Blob index years {startYear}-{endYear} complete. Alias: {settings.Elastic.SearchAlias}");

                    return;

                }



                for (var year = startYear; year <= endYear; year++)

                {

                    await IndexYearAsync(settings, year);

                }



                Console.WriteLine();

                Console.WriteLine($"All years {startYear}-{endYear} complete. Alias: {settings.Elastic.SearchAlias}");

            }

            catch (Exception ex)

            {

                Console.WriteLine();

                Console.WriteLine($"Indexer failed: {ex.Message}");

                Log.Fatal(ex, "Indexer failed for years {StartYear}-{EndYear}", settings.Indexer.StartYear, settings.Indexer.EndYear);

                Environment.ExitCode = 1;

            }

            finally

            {

                Log.CloseAndFlush();

            }

        }



        private static async Task IndexYearAsync(IndexerSettings settings, int year)

        {

            settings.Indexer.StartYear = year;

            settings.Indexer.EndYear = year;



            var indexName = IndexerSettingsLoader.BuildIndexName(settings);

            var checkpointPath = IndexerSettingsLoader.BuildCheckpointPath(settings, indexName);



            Console.WriteLine();

            Console.WriteLine($"--- {indexName} ({settings.Indexer.SourceMode}) ---");

            if (File.Exists(checkpointPath))

            {

                Console.WriteLine($"Checkpoint: {checkpointPath}");

            }



            Log.Information("=== Year {Year}: index {IndexName} source {Source} ===", year, indexName, settings.Indexer.SourceMode);



            var elasticClient = ElasticClientFactory.Create(settings.Elastic, indexName);

            var indexManager = new ElasticIndexManager(elasticClient, settings.Elastic);

            await indexManager.EnsureIndexAsync(indexName);



            var indexer = new Index(settings, indexName);



            var sourceMode = settings.Indexer.SourceMode ?? "blob";



            if (string.Equals(sourceMode, "blob", StringComparison.OrdinalIgnoreCase))

            {

                var blobRunner = new BlobIndexRunner(settings, indexer);

                await blobRunner.ExecuteAsync();

            }

            else if (string.Equals(sourceMode, "ftp", StringComparison.OrdinalIgnoreCase))

            {

                var ftpOptions = FtpSettingsLoader.Load();

                var ftpRunner = new FtpIndexRunner(settings, indexer, ftpOptions);

                await ftpRunner.ExecuteAsync();

            }

            else

            {

                await indexer.ExecuteAsync(settings.Indexer.SourcePath);

            }



            indexer.FlushCheckpoint();



            if (settings.Indexer.SwapAliasOnCompletion)

            {

                await indexManager.PublishYearToSearchAliasAsync(

                    settings.Elastic.SearchAlias,

                    indexName,

                    settings.Indexer.IndexNamePrefix,

                    year);

                Console.WriteLine($"Alias {settings.Elastic.SearchAlias} includes {indexName}");

            }

        }



        private static void ValidateSettings(IndexerSettings settings, string mode)

        {

            if (string.Equals(mode, "assess", StringComparison.OrdinalIgnoreCase))

            {

                return;

            }



            if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))

            {

                FtpSettingsLoader.Load();

                if (string.IsNullOrWhiteSpace(settings.BlobStorage.ConnectionString))

                {

                    throw new InvalidOperationException(

                        "BlobStorage:ConnectionString is required for all-mode (sync + blob index).");

                }

            }



            if (string.Equals(mode, "ftp-sync", StringComparison.OrdinalIgnoreCase))

            {

                FtpSettingsLoader.Load();

                if (string.IsNullOrWhiteSpace(settings.BlobStorage.ConnectionString))

                {

                    throw new InvalidOperationException(

                        "BlobStorage:ConnectionString is required for ftp-sync.");

                }

            }



            var sourceMode = settings.Indexer.SourceMode ?? "blob";



            if ((string.Equals(mode, "daily", StringComparison.OrdinalIgnoreCase) ||

                 string.Equals(sourceMode, "blob", StringComparison.OrdinalIgnoreCase)) &&

                string.IsNullOrWhiteSpace(settings.BlobStorage.ConnectionString))

            {

                throw new InvalidOperationException(

                    "BlobStorage:ConnectionString is required for blob indexing and the daily run. Set it in appsettings.json or environment variables.");

            }



            if (string.Equals(sourceMode, "ftp", StringComparison.OrdinalIgnoreCase))

            {

                FtpSettingsLoader.Load();

            }



            if (settings.Elastic.UseElasticCloud &&

                string.IsNullOrWhiteSpace(settings.Elastic.CloudId) &&

                string.IsNullOrWhiteSpace(settings.Elastic.Url))

            {

                throw new InvalidOperationException(

                    "Elastic:Url or Elastic:CloudId is required when UseElasticCloud is true.");

            }

        }

    }

}


