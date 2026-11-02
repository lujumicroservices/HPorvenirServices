using HPorvenir.Parser;
using System;

namespace HPorvenir.Elastic
{
    /// <summary>
    /// Maps remote FTP paths to the same virtual blob paths used by <see cref="BlobPathParser"/>
    /// so indexed document names resolve to TIFFs already in Azure Blob.
    /// </summary>
    public static class FtpPathMapper
    {
        /// <summary>
        /// Converts a remote FTP path to an index source key (e.g. 1919/04/25/page.xml).
        /// </summary>
        public static bool TryMapToIndexPath(string remotePath, out string indexPath)
        {
            indexPath = null;

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                return false;
            }

            var normalized = remotePath.Replace('\\', '/').Trim('/');
            if (!normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parts = normalized.Split('/');
            var fileName = parts[parts.Length - 1];

            for (var i = 0; i <= parts.Length - 4; i++)
            {
                if (parts[i].Length != 4 ||
                    !int.TryParse(parts[i], out var year) ||
                    !int.TryParse(parts[i + 1], out var month) ||
                    !int.TryParse(parts[i + 2], out var day))
                {
                    continue;
                }

                indexPath = $"{year}/{month:D2}/{day:D2}/{fileName}";
                return BlobPathParser.TryParse(indexPath, out _, out _);
            }

            return false;
        }

        public static bool TryGetYear(string indexPath, out int year)
        {
            year = 0;
            if (!BlobPathParser.TryParse(indexPath, out var fileDate, out _))
            {
                return false;
            }

            year = fileDate / 10000;
            return year > 0;
        }

        /// <summary>Stable checkpoint / failure key: ftp-scheme avoids colliding with blob source keys.</summary>
        public static string ToSourceKey(string indexPath) => $"ftp:{indexPath}";
    }
}
