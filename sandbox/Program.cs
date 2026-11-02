using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace sandbox
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var parallelism = 8;
            var skipNavUpdate = false;
            var navOnly = false;

            if (args.Length > 0)
            {
                if (string.Equals(args[0], "nav-only", StringComparison.OrdinalIgnoreCase))
                {
                    navOnly = true;
                }
                else
                {
                    parallelism = int.Parse(args[0]);
                }
            }

            if (args.Length > 1 &&
                string.Equals(args[1], "skip-nav", StringComparison.OrdinalIgnoreCase))
            {
                skipNavUpdate = true;
            }

            Directory.CreateDirectory("indexer-state/logs");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine("indexer-state", "logs", "process-staging-.txt"),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Console.WriteLine(navOnly
                ? "UpdateIndex only: rebuild metadata/missingDatesv3.json for navegacion libre"
                : $"ProcessStage: hporvenir → hemerotecav2 + index | parallelism={parallelism}");

            try
            {
                if (!navOnly)
                {
                    var settings = HPorvenir.Elastic.IndexerSettingsLoader.Load();
                    var process = new ProcessStage(settings, parallelism);
                    await process.ExecuteAsync();
                }

                if (!skipNavUpdate)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Updating navegacion libre (missingDatesv3.json)...");
                    var nav = new UpdateIndex();
                    nav.ExecuteAsync();
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] missingDatesv3.json updated in metadata container");
                }

                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "ProcessStage failed");
                Console.WriteLine($"FAILED: {ex.Message}");
                Environment.ExitCode = 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
