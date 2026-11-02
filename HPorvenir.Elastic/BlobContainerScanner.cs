using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    /// <summary>
    /// Lists PDF and XML blobs for the configured year range.
    /// A modified-since instant keeps the listing to blobs uploaded or overwritten after that time.
    /// </summary>
    public class BlobContainerScanner
    {
        private readonly IndexerSettings _settings;
        private readonly BlobContainerClient _container;
        private readonly string _xmlBlobPrefix;

        public BlobContainerScanner(IndexerSettings settings)
        {
            _settings = settings;
            _container = new BlobContainerClient(
                settings.BlobStorage.ConnectionString,
                settings.BlobStorage.ContainerName);
            _xmlBlobPrefix = settings.BlobStorage.XmlBlobPrefix;
        }

        public async Task<List<string>> ListIndexableNamesAsync(
            DateTimeOffset? modifiedSince,
            CancellationToken cancellationToken = default)
        {
            var exists = await _container.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                throw new InvalidOperationException(
                    $"Blob container '{_settings.BlobStorage.ContainerName}' was not found.");
            }

            var names = new List<string>();
            for (var year = _settings.Indexer.StartYear; year <= _settings.Indexer.EndYear; year++)
            {
                await ScanPrefixAsync($"{year}/", xmlOnly: false, modifiedSince, names, cancellationToken);

                var xmlPrefix = XmlBlobPathHelper.NormalizePrefix(_xmlBlobPrefix);
                if (!string.IsNullOrEmpty(xmlPrefix))
                {
                    await ScanPrefixAsync(
                        XmlBlobPathHelper.YearScanPrefix(year, _xmlBlobPrefix),
                        xmlOnly: true,
                        modifiedSince,
                        names,
                        cancellationToken);
                }
            }

            Log.Information(
                "Blob listing {StartYear}-{EndYear}: {Count} indexable blobs modified since {ModifiedSince}",
                _settings.Indexer.StartYear,
                _settings.Indexer.EndYear,
                names.Count,
                modifiedSince);

            return names;
        }

        private async Task ScanPrefixAsync(
            string prefix,
            bool xmlOnly,
            DateTimeOffset? modifiedSince,
            List<string> names,
            CancellationToken cancellationToken)
        {
            Log.Debug("Scanning blob prefix {Prefix}", prefix);

            await foreach (var page in _container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken).AsPages())
            {
                foreach (var blob in page.Values)
                {
                    if (!IsIndexable(blob, xmlOnly) || !IsModifiedSince(blob, modifiedSince))
                    {
                        continue;
                    }

                    names.Add(blob.Name);
                }
            }
        }

        private bool IsIndexable(BlobItem blob, bool xmlOnly)
        {
            if (xmlOnly)
            {
                return blob.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
            }

            if (blob.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(XmlBlobPathHelper.NormalizePrefix(_xmlBlobPrefix)))
            {
                return false;
            }

            return blob.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                   blob.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsModifiedSince(BlobItem blob, DateTimeOffset? modifiedSince)
        {
            if (!modifiedSince.HasValue)
            {
                return true;
            }

            var lastModified = blob.Properties?.LastModified;
            if (lastModified == null)
            {
                return true;
            }

            return lastModified.Value >= modifiedSince.Value;
        }
    }
}
