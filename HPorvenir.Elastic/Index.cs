using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hporvenir.Indexer;
using HPorvenir.Parser;
using Serilog;
using Spire.Pdf;

namespace HPorvenir.Elastic
{
    public class Index
    {
        private IndexerSettings _settings;
        private string _indexName;
        private BulkIndexService _bulkService;
        private readonly IndexClient _luceneClient;

        public Index()
        {
            var settings = IndexerSettingsLoader.Load();
            Initialize(settings, settings.Elastic.DefaultIndex);
        }

        public Index(IndexerSettings settings, string indexName)
        {
            Initialize(settings, indexName);
        }

        public Index(int start, int end, string indexNamePrefix, int parallelism)
            : this(
                BuildLegacySettings(start, end, indexNamePrefix, parallelism),
                start == end ? $"{indexNamePrefix}v2_{start}" : $"{indexNamePrefix}v2_{start}-{end}")
        {
        }

        public Index(string indexPath, string indexName)
        {
            _indexName = indexName;
            _luceneClient = new IndexClient(indexPath, indexName);
        }

        public async Task ExecuteAsync(string basePath)
        {
            Log.Information(
                "Starting bulk index {IndexName} from {SourcePath} ({StartYear}-{EndYear})",
                _indexName,
                basePath,
                _settings.Indexer.StartYear,
                _settings.Indexer.EndYear);

            var directory = new DirectoryInfo(basePath);
            await Navigate(directory);
            await _bulkService.FlushAsync();

            Log.Information(
                "Bulk index completed for {IndexName}. Checkpointed sources: {CompletedCount}. Buffered remaining: {BufferedCount}",
                _indexName,
                _bulkService.CompletedSourceCount,
                _bulkService.BufferedCount);
        }

        public async Task Navigate(DirectoryInfo directory)
        {
            Log.Information("Navigate folder {DirectoryName}", directory.Name);

            if (directory.Name.Length == 4)
            {
                if (!int.TryParse(directory.Name, out var year) ||
                    year < _settings.Indexer.StartYear ||
                    year > _settings.Indexer.EndYear)
                {
                    return;
                }
            }

            if (directory.GetFiles().Length > 0)
            {
                await ProcessFolder(directory);
            }

            foreach (var child in directory.GetDirectories())
            {
                Log.Information("Navigating child folder {ChildPath}", child.FullName);
                await Navigate(child);
            }
        }

        public async Task ProcessFolder(DirectoryInfo directory)
        {
            Log.Information("Processing folder {FolderPath}", directory.FullName);

            if (!TryValidateFolderStructure(directory, out _))
            {
                Log.Information("Skipping invalid folder structure {FolderPath}", directory.FullName);
                return;
            }

            var files = directory.GetFiles("*.pdf");
            if (files.Length == 0)
            {
                files = directory.GetFiles("*.xml");
            }

            var pendingFiles = files
                .Where(file => !_bulkService.IsSourceCompleted(file.FullName))
                .ToArray();

            Log.Information(
                "Folder {FolderPath} has {PendingCount}/{TotalCount} files to index",
                directory.FullName,
                pendingFiles.Length,
                files.Length);

            if (pendingFiles.Length == 0)
            {
                return;
            }

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.Indexer.Parallelism
            };

            Parallel.ForEach(pendingFiles, options, file =>
            {
                try
                {
                    if (file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        IndexXmlFile(file);
                    }
                    else if (file.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        IndexPdfFile(file);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to index file {FilePath}", file.FullName);
                    _bulkService.RecordFailure(file.FullName, ex.Message);
                }
            });

            await _bulkService.FlushAsync();
        }

        public async Task IndexPDF(MemoryStream fileStream, string blobName)
        {
            var paragraphs = ParsePdfFromStream(fileStream, blobName);
            if (paragraphs.Count == 0)
            {
                Log.Debug("No indexable PDF content extracted for {BlobName}", blobName);
                _bulkService.MarkSourceCompleted(blobName);
                return;
            }

            await _bulkService.EnqueueAndFlushIfNeededAsync(blobName, paragraphs);
        }

        public Task IndexXML(MemoryStream fileStream, string blobName) =>
            IndexXML(fileStream, blobName, blobName);

        /// <param name="blobName">Virtual path for <see cref="BlobPathParser"/> (document name / TIFF resolution).</param>
        /// <param name="sourceKey">Checkpoint key; use <see cref="FtpPathMapper.ToSourceKey"/> for FTP sources.</param>
        public async Task IndexXML(MemoryStream fileStream, string blobName, string sourceKey)
        {
            var parser = new XmlParser();
            var paragraphs = parser.ParseXml(fileStream, blobName);
            if (paragraphs.Count == 0)
            {
                Log.Debug("No paragraphs parsed from {SourceKey}", sourceKey);
                _bulkService.MarkSourceCompleted(sourceKey);
                return;
            }

            await _bulkService.EnqueueAndFlushIfNeededAsync(sourceKey, paragraphs);
        }

        private void IndexXmlFile(FileInfo file)
        {
            Log.Information("Parsing XML {FileName}", file.Name);
            var parser = new XmlParser();
            var paragraphs = parser.ParseXml(file);
            _bulkService.Enqueue(file.FullName, paragraphs);
        }

        private void IndexPdfFile(FileInfo file)
        {
            Log.Information("Parsing PDF {FileName}", file.Name);

            var fileDate = int.Parse(
                file.Directory.Parent.Parent.Name +
                file.Directory.Parent.Name +
                file.Directory.Name);

            var paragraphs = PdfTextExtractor.ExtractFromFile(fileDate, file.Name, file.FullName);
            if (paragraphs.Count > 0)
            {
                _bulkService.Enqueue(file.FullName, paragraphs);
            }
        }

        private static List<Paragraph> ParsePdfFromStream(MemoryStream fileStream, string blobName)
        {
            if (!BlobPathParser.TryParse(blobName, out var fileDate, out var fileName))
            {
                throw new InvalidOperationException($"Unsupported blob path format: {blobName}");
            }

            return PdfTextExtractor.ExtractFromStream(fileDate, fileName, fileStream);
        }

        public Task FlushAsync() => _bulkService.FlushAsync();

        public void FlushCheckpoint() => _bulkService.FlushCheckpoint();

        public int CompletedSourceCount => _bulkService.CompletedSourceCount;

        public string CheckpointPath => _bulkService.CheckpointPath;

        public string LastCompletedSource => _bulkService.LastCompletedSource;

        public bool IsSourceCompleted(string sourceKey) => _bulkService.IsSourceCompleted(sourceKey);

        public void MarkSourceCompleted(string sourceKey) => _bulkService.MarkSourceCompleted(sourceKey);

        public void RecordFailure(string sourceKey, string error) => _bulkService.RecordFailure(sourceKey, error);

        private static bool TryValidateFolderStructure(DirectoryInfo directory, out string year)
        {
            try
            {
                if (directory.Parent?.Parent?.Parent?.Name != "El Porvenir")
                {
                    throw new InvalidOperationException("invalid file structure");
                }

                year = directory.Parent.Parent.Name;
                return directory.Name.Length == 2 &&
                       directory.Parent.Name.Length == 2 &&
                       directory.Parent.Parent.Name.Length == 4;
            }
            catch
            {
                Log.Information("Invalid folder structure {FolderPath}", directory.FullName);
                year = "0";
                return false;
            }
        }

        private static IndexerSettings BuildLegacySettings(int start, int end, string indexNamePrefix, int parallelism)
        {
            var settings = IndexerSettingsLoader.Load();
            settings.Indexer.StartYear = start;
            settings.Indexer.EndYear = end;
            settings.Indexer.IndexNamePrefix = indexNamePrefix;
            settings.Indexer.Parallelism = parallelism;
            return settings;
        }

        private void Initialize(IndexerSettings settings, string indexName)
        {
            _settings = settings;
            _indexName = indexName;

            var elasticClient = ElasticClientFactory.Create(settings.Elastic, indexName);
            var checkpointPath = IndexerSettingsLoader.BuildCheckpointPath(settings, indexName);
            var failuresPath = IndexerSettingsLoader.BuildFailuresPath(settings, indexName);
            var checkpoint = new IndexCheckpoint(checkpointPath, failuresPath);
            _bulkService = new BulkIndexService(elasticClient, indexName, settings.Indexer, checkpoint);
        }
    }
}
