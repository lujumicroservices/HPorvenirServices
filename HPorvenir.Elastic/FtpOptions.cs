namespace HPorvenir.Elastic
{
    /// <summary>FTP connection settings (permanent XML source). Loaded from ftp.settings.json.</summary>
    public class FtpOptions
    {
        public string Host { get; set; }
        public int Port { get; set; } = 21;
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UsePassive { get; set; } = true;
        public bool UseSsl { get; set; }
        public string RootPath { get; set; } = "/";
    }
}
