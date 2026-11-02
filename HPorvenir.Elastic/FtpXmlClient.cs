using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    public sealed class FtpXmlEntry
    {
        public string RemotePath { get; set; }
        public string IndexPath { get; set; }
        public int Year { get; set; }
        public string SourceKey { get; set; }
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// Lists and downloads XML from FTP. Files are streamed in memory — nothing is written to Azure Blob.
    /// TIFF/PDF on FTP are ignored; images/PDF for viewing come from Azure Blob.
    /// </summary>
    public class FtpXmlClient
    {
        private readonly FtpOptions _options;

        public FtpXmlClient(FtpOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Counts XML under /{year}/ only (ignores TIFF/PDF on FTP).</summary>
        public async Task<YearStorageStats> ScanYearAsync(int year, CancellationToken cancellationToken = default)
        {
            var stats = new YearStorageStats
            {
                Year = year,
                Source = "ftp"
            };

            var queue = new Queue<string>();
            queue.Enqueue($"/{year}");

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = queue.Dequeue();
                var entries = await ListDirectoryAsync(directory, cancellationToken);

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.IsDirectory)
                    {
                        queue.Enqueue(entry.FullPath);
                        continue;
                    }

                    if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    stats.XmlCount++;
                    stats.XmlBytes += entry.SizeBytes;
                }
            }

            return stats;
        }

        public async Task<IReadOnlyList<FtpXmlEntry>> DiscoverXmlAsync(
            int startYear,
            int endYear,
            CancellationToken cancellationToken = default)
        {
            if (startYear == endYear)
            {
                return await DiscoverXmlForYearAsync(startYear, cancellationToken);
            }

            var discovered = new List<FtpXmlEntry>();
            for (var year = startYear; year <= endYear; year++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var yearFiles = await DiscoverXmlForYearAsync(year, cancellationToken);
                discovered.AddRange(yearFiles);
            }

            Log.Information(
                "FTP discovery: {Count} XML files for years {StartYear}-{EndYear}",
                discovered.Count,
                startYear,
                endYear);

            return discovered;
        }

        /// <summary>Lists XML under /{year}/ only (used for per-year indexing).</summary>
        public async Task<IReadOnlyList<FtpXmlEntry>> DiscoverXmlForYearAsync(
            int year,
            CancellationToken cancellationToken = default)
        {
            var discovered = new List<FtpXmlEntry>();
            var queue = new Queue<string>();
            queue.Enqueue($"/{year}");

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = queue.Dequeue();
                var entries = await ListDirectoryAsync(directory, cancellationToken);

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.IsDirectory)
                    {
                        queue.Enqueue(entry.FullPath);
                        continue;
                    }

                    if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!FtpPathMapper.TryMapToIndexPath(entry.FullPath, out var indexPath))
                    {
                        continue;
                    }

                    discovered.Add(new FtpXmlEntry
                    {
                        RemotePath = entry.FullPath,
                        IndexPath = indexPath,
                        Year = year,
                        SourceKey = FtpPathMapper.ToSourceKey(indexPath),
                        SizeBytes = entry.SizeBytes
                    });
                }
            }

            Log.Information("FTP year {Year}: {Count} XML files", year, discovered.Count);
            return discovered;
        }

        /// <summary>Downloads XML into a memory stream (disposed by caller after upload/index).</summary>
        public async Task<MemoryStream> DownloadToMemoryAsync(string remotePath, CancellationToken cancellationToken = default)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await Task.Run(() => DownloadToMemoryCore(remotePath, cancellationToken), cancellationToken);
                }
                catch (Exception ex) when (attempt < maxAttempts && IsRetryableFtpError(ex))
                {
                    var delayMs = 1500 * attempt;
                    Log.Debug("FTP download retry {Attempt}/{Max} for {Path}: {Message}", attempt, maxAttempts, remotePath, ex.Message);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            throw new InvalidOperationException($"FTP download failed for {remotePath}");
        }

        private MemoryStream DownloadToMemoryCore(string remotePath, CancellationToken cancellationToken)
        {
            var uri = BuildUri(remotePath);
            var request = CreateRequest(uri, WebRequestMethods.Ftp.DownloadFile);

            using (var response = (FtpWebResponse)request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var buffer = new MemoryStream();
                responseStream.CopyTo(buffer);
                buffer.Position = 0;
                return buffer;
            }
        }

        private async Task<IReadOnlyList<FtpListEntry>> ListDirectoryAsync(
            string directory,
            CancellationToken cancellationToken)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await Task.Run(() => ListDirectoryCore(directory, cancellationToken), cancellationToken);
                }
                catch (Exception ex) when (attempt < maxAttempts && IsRetryableFtpError(ex))
                {
                    var delayMs = 1500 * attempt;
                    Log.Debug("FTP list retry {Attempt}/{Max} for {Dir}: {Message}", attempt, maxAttempts, directory, ex.Message);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            throw new InvalidOperationException($"FTP list failed for {directory}");
        }

        private static bool IsRetryableFtpError(Exception ex)
        {
            var msg = ex.ToString();
            return ex is System.Net.WebException ||
                   ex is System.IO.IOException ||
                   msg.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("connection was closed", StringComparison.OrdinalIgnoreCase);
        }

        private List<FtpListEntry> ListDirectoryCore(string directory, CancellationToken cancellationToken)
        {
            var uri = BuildUri(directory);
            var request = CreateRequest(uri, WebRequestMethods.Ftp.ListDirectoryDetails);

            var entries = new List<FtpListEntry>();
            using (var response = (FtpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (TryParseListLine(line, directory, out var entry))
                    {
                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private FtpWebRequest CreateRequest(Uri uri, string method)
        {
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = method;
            request.Credentials = new NetworkCredential(_options.Username, _options.Password);
            request.UseBinary = true;
            request.UsePassive = _options.UsePassive;
            request.EnableSsl = _options.UseSsl;
            request.KeepAlive = false;
            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;
            return request;
        }

        private Uri BuildUri(string remotePath)
        {
            var path = remotePath.Replace('\\', '/');
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            return new Uri($"ftp://{_options.Host}:{_options.Port}{path}");
        }

        private static string NormalizeDirectory(string path)
        {
            var normalized = (path ?? "/").Replace('\\', '/').Trim();
            if (normalized.Length == 0)
            {
                return "/";
            }

            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            return normalized.TrimEnd('/');
        }

        private static bool TryParseListLine(string line, string parentDirectory, out FtpListEntry entry)
        {
            entry = null;
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            // Unix-style: drwxr-xr-x ... name
            if (trimmed.Length > 10 && (trimmed[0] == 'd' || trimmed[0] == '-'))
            {
                var isDirectory = trimmed[0] == 'd';
                var name = trimmed.Substring(trimmed.LastIndexOf(' ') + 1).Trim();
                if (name == "." || name == "..")
                {
                    return false;
                }

                long sizeBytes = 0;
                if (!isDirectory)
                {
                    var unixParts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (unixParts.Length >= 5)
                    {
                        long.TryParse(unixParts[4], out sizeBytes);
                    }
                }

                entry = new FtpListEntry
                {
                    Name = name,
                    IsDirectory = isDirectory,
                    FullPath = CombineRemotePath(parentDirectory, name),
                    SizeBytes = sizeBytes
                };
                return true;
            }

            // Windows/IIS-style: 02-03-24  05:46PM       <DIR>          folder
            var dirIndex = trimmed.IndexOf("<DIR>", StringComparison.OrdinalIgnoreCase);
            if (dirIndex >= 0)
            {
                var name = trimmed.Substring(dirIndex + 5).Trim();
                if (name == "." || name == "..")
                {
                    return false;
                }

                entry = new FtpListEntry
                {
                    Name = name,
                    IsDirectory = true,
                    FullPath = CombineRemotePath(parentDirectory, name)
                };
                return true;
            }

            // Fallback: treat as file name (ListDirectory plain mode)
            var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var fileName = parts[parts.Length - 1];
            if (fileName == "." || fileName == "..")
            {
                return false;
            }

            entry = new FtpListEntry
            {
                Name = fileName,
                IsDirectory = false,
                FullPath = CombineRemotePath(parentDirectory, fileName)
            };
            return true;
        }

        private static string CombineRemotePath(string parent, string name)
        {
            var basePath = NormalizeDirectory(parent);
            return $"{basePath}/{name}".Replace("//", "/");
        }

        private sealed class FtpListEntry
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public bool IsDirectory { get; set; }
            public long SizeBytes { get; set; }
        }
    }
}
