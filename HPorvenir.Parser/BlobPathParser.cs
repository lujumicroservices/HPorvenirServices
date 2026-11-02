namespace HPorvenir.Parser
{
    using System;
    public static class BlobPathParser
    {
        public static bool TryParse(string blobPath, out int fileDate, out string fileName)
        {
            fileDate = 0;
            fileName = null;

            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return false;
            }

            var parts = blobPath.Replace('\\', '/').Trim('/').Split('/');

            if (parts.Length >= 4 &&
                parts[0].Length == 4 &&
                int.TryParse(parts[0], out var year) &&
                int.TryParse(parts[1], out var month) &&
                int.TryParse(parts[2], out var day))
            {
                fileName = parts[parts.Length - 1];
                fileDate = year * 10000 + month * 100 + day;
                return true;
            }

            if (parts.Length == 2 && parts[0].Length == 8 && int.TryParse(parts[0], out fileDate))
            {
                fileName = parts[1];
                return true;
            }

            return false;
        }

        public static string BuildDocumentName(int fileDate, string fileName) => $"{fileDate}{fileName}";

        public static bool TryResolveBlobPathFromDocumentName(string documentName, out string blobPath)
        {
            blobPath = null;

            if (string.IsNullOrWhiteSpace(documentName))
            {
                return false;
            }

            if (documentName.Contains("/"))
            {
                blobPath = documentName.Replace('\\', '/').TrimStart('/');
                return true;
            }

            if (documentName.Length <= 8 || !int.TryParse(documentName.Substring(0, 8), out var fileDate))
            {
                return false;
            }

            var year = fileDate / 10000;
            var month = (fileDate / 100) % 100;
            var day = fileDate % 100;
            var filePart = documentName.Substring(8);

            if (filePart.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                filePart.StartsWith($"{year}_", StringComparison.Ordinal))
            {
                blobPath = $"{year}/{month:D2}/{day:D2}/{filePart}";
                return true;
            }

            var legacyFile = filePart.Replace(".xml", ".tif", StringComparison.OrdinalIgnoreCase);
            blobPath = $"{year}/{month:D2}/{day:D2}/{year}_{month:D2}_{day:D2}_{legacyFile}";
            return true;
        }
    }
}
