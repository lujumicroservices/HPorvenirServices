using System;

namespace HPorvenir.Elastic
{
    /// <summary>
    /// FTP-synced XML lives under a separate blob prefix; indexing still uses canonical YYYY/MM/DD paths.
    /// </summary>
    public static class XmlBlobPathHelper
    {
        public static string NormalizePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return string.Empty;
            }

            return prefix.Replace('\\', '/').Trim('/').ToLowerInvariant() + "/";
        }

        public static string ToBlobPath(string indexPath, string xmlBlobPrefix)
        {
            var normalized = indexPath.Replace('\\', '/').TrimStart('/');
            var prefix = NormalizePrefix(xmlBlobPrefix);
            return string.IsNullOrEmpty(prefix) ? normalized : prefix + normalized;
        }

        public static bool TryToIndexPath(string blobPath, string xmlBlobPrefix, out string indexPath)
        {
            indexPath = null;
            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return false;
            }

            var normalized = blobPath.Replace('\\', '/').TrimStart('/');
            var prefix = NormalizePrefix(xmlBlobPrefix);

            if (!string.IsNullOrEmpty(prefix))
            {
                if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                indexPath = normalized.Substring(prefix.Length);
                return true;
            }

            indexPath = normalized;
            return true;
        }

        public static string YearScanPrefix(int year, string xmlBlobPrefix) =>
            NormalizePrefix(xmlBlobPrefix) + $"{year}/";
    }
}
