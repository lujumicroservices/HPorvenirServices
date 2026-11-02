using Microsoft.Extensions.Configuration;

using System;

using System.IO;



namespace HPorvenir.Elastic

{

    public static class IndexerSettingsLoader

    {

        public static IndexerSettings Load(string[] cliArgs = null)

        {

            var configuration = new ConfigurationBuilder()

                .SetBasePath(AppContext.BaseDirectory)

                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)

                .AddJsonFile("elastic.cloud.settings.json", optional: true, reloadOnChange: false)

                .AddEnvironmentVariables()

                .Build();



            var settings = new IndexerSettings();

            configuration.Bind(settings);

            ApplyCliOverrides(settings, cliArgs);

            return settings;

        }



        private static void ApplyCliOverrides(IndexerSettings settings, string[] cliArgs)

        {

            if (cliArgs == null || cliArgs.Length == 0)

            {

                return;

            }



            var offset = 0;

            var mode = cliArgs[0].ToLowerInvariant();

            switch (mode)

            {

                case "blob":

                    settings.Indexer.SourceMode = "blob";

                    offset = 1;

                    break;

                case "ftp":

                    settings.Indexer.SourceMode = "ftp";

                    offset = 1;

                    break;

                case "disk":

                    settings.Indexer.SourceMode = "disk";

                    offset = 1;

                    break;

                case "all":

                    settings.Indexer.SourceMode = "all";

                    offset = 1;

                    break;

                case "assess":

                    settings.Indexer.SourceMode = "assess";

                    offset = 1;

                    break;

                case "ftp-sync":

                    settings.Indexer.SourceMode = "ftp-sync";

                    offset = 1;

                    break;

                case "daily":

                    settings.Indexer.SourceMode = "daily";

                    offset = 1;

                    break;

            }



            if (cliArgs.Length < offset + 2)

            {

                return;

            }



            settings.Indexer.StartYear = int.Parse(cliArgs[offset]);

            settings.Indexer.EndYear = int.Parse(cliArgs[offset + 1]);



            if (cliArgs.Length >= offset + 3 && !string.IsNullOrWhiteSpace(cliArgs[offset + 2]))

            {

                settings.Indexer.IndexNamePrefix = cliArgs[offset + 2];

            }



            if (cliArgs.Length >= offset + 4 && !string.IsNullOrWhiteSpace(cliArgs[offset + 3]))

            {

                settings.Indexer.Parallelism = int.Parse(cliArgs[offset + 3]);

            }



            if (cliArgs.Length >= offset + 5 && !string.IsNullOrWhiteSpace(cliArgs[offset + 4]))

            {

                if (string.Equals(settings.Indexer.SourceMode, "all", StringComparison.OrdinalIgnoreCase) ||

                    string.Equals(settings.Indexer.SourceMode, "ftp-sync", StringComparison.OrdinalIgnoreCase) ||

                    string.Equals(settings.Indexer.SourceMode, "blob", StringComparison.OrdinalIgnoreCase))

                {

                    settings.Indexer.MaxConcurrentYears = int.Parse(cliArgs[offset + 4]);

                }

                else if (string.Equals(settings.Indexer.SourceMode, "assess", StringComparison.OrdinalIgnoreCase))

                {

                    settings.Indexer.AssessmentParallelism = int.Parse(cliArgs[offset + 4]);

                }

                else if (string.Equals(settings.Indexer.SourceMode, "disk", StringComparison.OrdinalIgnoreCase))

                {

                    settings.Indexer.SourcePath = cliArgs[offset + 4];

                }

            }

        }



        public static string BuildIndexName(IndexerSettings settings)

        {

            var indexer = settings.Indexer;

            if (indexer.StartYear == indexer.EndYear)

            {

                return $"{indexer.IndexNamePrefix}v2_{indexer.StartYear}";

            }



            return $"{indexer.IndexNamePrefix}v2_{indexer.StartYear}-{indexer.EndYear}";

        }



        public static string BuildLegacyRangeIndexName(string indexNamePrefix, int year) =>

            $"{indexNamePrefix}v2_{year}-{year}";



        public static string BuildCheckpointPath(IndexerSettings settings, string indexName)

        {

            var fileName = Path.GetFileName(settings.Indexer.CheckpointPath);

            var stateDir = Path.Combine(Directory.GetCurrentDirectory(), "indexer-state");

            return Path.Combine(stateDir, $"{indexName}-{fileName}");

        }



        public static string BuildFailuresPath(IndexerSettings settings, string indexName)

        {

            var fileName = Path.GetFileName(settings.Indexer.FailedSourcesPath);

            var stateDir = Path.Combine(Directory.GetCurrentDirectory(), "indexer-state");

            return Path.Combine(stateDir, $"{indexName}-{fileName}");

        }



        public static string BuildFtpSyncCheckpointPath(IndexerSettings settings, int year)

        {

            var stateDir = Path.Combine(Directory.GetCurrentDirectory(), "indexer-state");

            return Path.Combine(stateDir, $"ftp-xml-sync-{year}-checkpoint.json");

        }



        public static string BuildFtpSyncFailuresPath(IndexerSettings settings, int year)

        {

            var stateDir = Path.Combine(Directory.GetCurrentDirectory(), "indexer-state");

            return Path.Combine(stateDir, $"ftp-xml-sync-{year}-failures.jsonl");

        }

    }

}


