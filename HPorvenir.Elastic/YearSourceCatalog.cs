using System;

namespace HPorvenir.Elastic
{
    /// <summary>
    /// Chooses FTP (METS XML) vs Azure Blob (PDF) per year based on archive layout.
    /// </summary>
    public static class YearSourceCatalog
    {
        /// <summary>Last year with OCR XML on FTP (sync to blob, then index as XML).</summary>
        public const int DefaultFtpXmlLastYear = 2010;

        /// <summary>Indexing source after XML has been synced to blob (Option B).</summary>
        public static string ResolveIndexSourceMode(int year, int ftpXmlLastYear = DefaultFtpXmlLastYear) => "blob";

        /// <summary>Years that need FTP→Blob XML sync before indexing.</summary>
        public static bool NeedsFtpXmlSync(int year, int ftpXmlLastYear = DefaultFtpXmlLastYear) =>
            year <= ftpXmlLastYear;

        [Obsolete("Use NeedsFtpXmlSync + blob indexing (Option B).")]
        public static string ResolveSourceMode(int year, int ftpXmlLastYear = DefaultFtpXmlLastYear) =>
            ResolveIndexSourceMode(year, ftpXmlLastYear);

        public static bool UsesFtp(int year, int ftpXmlLastYear = DefaultFtpXmlLastYear) =>
            year <= ftpXmlLastYear;

        public static bool UsesBlob(int year, int ftpXmlLastYear = DefaultFtpXmlLastYear) =>
            year > ftpXmlLastYear;
    }
}
