using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace HPorvenir.Elastic
{
    public class ElasticOptions
    {
        public bool UseElasticCloud { get; set; }
        public string Url { get; set; } = "https://40.124.185.84:9200/";
        public string CloudId { get; set; }
        public string ApiKeyId { get; set; } = "eeoYHowB5SerKtpAEWzN";
        public string ApiKey { get; set; } = "201YWmlbQaC0rYGD8PeS6g";
        public string DefaultIndex { get; set; } = "hporvenirv_2005-2021";
        public string SearchAlias { get; set; } = "hporvenir-current";
        public string ContentAnalyzer { get; set; } = "spanish_folded";
        public int NumberOfShards { get; set; } = 1;
        public int NumberOfReplicas { get; set; } = 0;
        public int RequestTimeoutSeconds { get; set; } = 120;
    }

    public class BlobStorageOptions
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; } = "hemerotecav2";
        /// <summary>Folder prefix for FTP-synced METS XML (e.g. hporvenirxml/). Empty = same tree as TIFF/PDF.</summary>
        public string XmlBlobPrefix { get; set; } = "hporvenirxml/";
    }

    public class IndexerOptions
    {
        public string SourceMode { get; set; } = "blob";
        public string SourcePath { get; set; } =
            @"E:\Porvenir\Hemeroteca\Diario\Mexico\Nuevo Leon\Monterrey\El Porvenir";

        public int StartYear { get; set; } = 1919;
        public int EndYear { get; set; } = 2026;
        public string IndexNamePrefix { get; set; } = "hporvenir";
        public int Parallelism { get; set; } = 8;
        public int BatchSize { get; set; } = 1000;
        public int MaxRetries { get; set; } = 5;
        public int InitialRetryDelayMs { get; set; } = 1000;
        public string CheckpointPath { get; set; } = "indexer-checkpoint.json";
        public string FailedSourcesPath { get; set; } = "indexer-failures.jsonl";
        public bool SwapAliasOnCompletion { get; set; } = true;
        /// <summary>Last calendar year with METS XML on FTP (2011+ PDF indexed from blob).</summary>
        public int FtpXmlLastYear { get; set; } = YearSourceCatalog.DefaultFtpXmlLastYear;
        /// <summary>How many years to index concurrently in <c>all</c> mode.</summary>
        public int MaxConcurrentYears { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
        /// <summary>Parallel FTP/blob scans during <c>assess</c> mode.</summary>
        public int AssessmentParallelism { get; set; } = Math.Max(4, Environment.ProcessorCount);

        /// <summary>Re-read this many hours before the stored watermark so a file uploaded during a run is not skipped.</summary>
        public int DailyOverlapHours { get; set; } = 6;

        /// <summary>Used only when no watermark exists yet. The first daily run indexes blobs newer than this many days.</summary>
        public int DailyInitialLookbackDays { get; set; } = 2;

        /// <summary>Blob that stores the last successful daily run (UTC).</summary>
        public string DailyStateBlobName { get; set; } = "indexer-state/daily-watermark.json";

        /// <summary>When set, blob indexing processes these names and does not scan the container.</summary>
        [JsonIgnore]
        public IReadOnlyList<string> OnlyBlobNames { get; set; }

        [JsonIgnore]
        public int LastRunProcessed { get; set; }

        [JsonIgnore]
        public int LastRunFailed { get; set; }
    }

    public class IndexerSettings
    {
        public ElasticOptions Elastic { get; set; } = new ElasticOptions();
        public BlobStorageOptions BlobStorage { get; set; } = new BlobStorageOptions();
        public IndexerOptions Indexer { get; set; } = new IndexerOptions();
    }

}
