using System;

namespace HPorvenir.Elastic
{
    public sealed class YearStorageStats
    {
        public int Year { get; set; }
        public string Source { get; set; }
        public int XmlCount { get; set; }
        public long XmlBytes { get; set; }
        public int TifCount { get; set; }
        public long TifBytes { get; set; }
        public int PdfCount { get; set; }
        public long PdfBytes { get; set; }

        /// <summary>Primary payload indexed (XML on FTP years, PDF on blob years).</summary>
        public long IndexableBytes => Source == "ftp" ? XmlBytes : PdfBytes;

        public int IndexableCount => Source == "ftp" ? XmlCount : PdfCount;

        public double IndexableGiB => IndexableBytes / (1024.0 * 1024.0 * 1024.0);

        public string ScanError { get; set; }
    }

    public sealed class YearAssessmentReport
    {
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public int StartYear { get; set; }
        public int EndYear { get; set; }
        public int FtpXmlLastYear { get; set; } = YearSourceCatalog.DefaultFtpXmlLastYear;
        public YearStorageStats[] Years { get; set; } = Array.Empty<YearStorageStats>();

        public long TotalIndexableBytes
        {
            get
            {
                long total = 0;
                foreach (var y in Years)
                {
                    total += y.IndexableBytes;
                }

                return total;
            }
        }

        public int TotalIndexableFiles
        {
            get
            {
                var total = 0;
                foreach (var y in Years)
                {
                    total += y.IndexableCount;
                }

                return total;
            }
        }

        public double TotalIndexableGiB => TotalIndexableBytes / (1024.0 * 1024.0 * 1024.0);
    }
}
