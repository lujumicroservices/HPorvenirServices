using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace sandbox
{
    public abstract class ProcessDocument
    {
        protected readonly HPorvenir.Elastic.Index ElasticIndex;

        protected ProcessDocument(HPorvenir.Elastic.Index elasticIndex = null)
        {
            ElasticIndex = elasticIndex ?? new HPorvenir.Elastic.Index();
        }

        public abstract Task<bool> ExecuteAsync(string fileName);

        /// <summary>
        /// Staging path YYYYMMDD/file.pdf → hemerotecav2 path YYYY/MM/DD/YYYY_MM_DD_file.pdf
        /// </summary>
        protected string CalculateBlobName(string path)
        {
            var pathParts = path.Replace('\\', '/').Split('/');

            if (pathParts.Length == 2 && pathParts[0].Length == 8)
            {
                var dayFolder = pathParts[0];
                var year = dayFolder.Substring(0, 4);
                var month = dayFolder.Substring(4, 2);
                var day = dayFolder.Substring(6, 2);
                return $"{year}/{month}/{day}/{year}_{month}_{day}_{pathParts[1]}";
            }

            Log.Error("invalid path structure {path}", path);
            throw new Exception($"invalid path structure {path}");
        }

        protected string CalculateThumbBlobName(string path, string ext)
        {
            var pathParts = path.Replace('\\', '/').Split('/');

            if (pathParts.Length == 2 && pathParts[0].Length == 8)
            {
                var dayFolder = pathParts[0];
                var year = dayFolder.Substring(0, 4);
                var month = dayFolder.Substring(4, 2);
                var day = dayFolder.Substring(6, 2);
                var thumbName = Path.ChangeExtension(pathParts[1], ".jpg");
                return $"{year}/{month}/{day}/thumb/{year}_{month}_{day}_{thumbName}";
            }

            Log.Error("invalid path structure {path}", path);
            throw new Exception($"invalid path structure {path}");
        }
    }
}
