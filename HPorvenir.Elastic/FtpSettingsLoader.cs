using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace HPorvenir.Elastic
{
    public static class FtpSettingsLoader
    {
        public static FtpOptions Load(string basePath = null)
        {
            var root = basePath ?? AppContext.BaseDirectory;
            var path = Path.Combine(root, "ftp.settings.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "ftp.settings.json");
            }

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Missing ftp.settings.json. Copy ftp.settings.template.json to ftp.settings.json and configure FTP access.");
            }

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false, reloadOnChange: false)
                .Build();

            var options = new FtpOptions();
            configuration.GetSection("Ftp").Bind(options);

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                throw new InvalidOperationException("Ftp:Host is required in ftp.settings.json.");
            }

            if (string.IsNullOrWhiteSpace(options.Username))
            {
                throw new InvalidOperationException("Ftp:Username is required in ftp.settings.json.");
            }

            return options;
        }
    }
}
