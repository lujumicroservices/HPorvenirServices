using HPorvenir.Blob;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Threading.Tasks;

namespace sandbox
{
    class Program
    {
        static async Task Main(string[] args)
        {

            // Create the DI container.
            IServiceCollection services = new ServiceCollection();

            // Being a regular console app, there is no appsettings.json or configuration providers enabled by default.
            // Hence instrumentation key and any changes to default logging level must be specified here.
            services.AddApplicationInsightsTelemetryWorkerService("af471157-d0a3-4a20-b7e7-e9c479852bb2");

            // Build ServiceProvider.
            IServiceProvider serviceProvider = services.BuildServiceProvider();

            
            // Obtain TelemetryClient instance from DI, for additional manual tracking or to flush.
            var telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();


            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);

            string path = args[0];
            int hilos = int.Parse(args[1]);
            int start = 2005;
            int end = 2010;

            if (args.Length > 2)
            {
                Console.WriteLine(args[2]);
                start = int.Parse(args[2]);
            }

            if (args.Length > 3)
            {
                Console.WriteLine(args[3]);
                end = int.Parse(args[3]);
            }


            

           Console.WriteLine("Start the magic");


            var configuration = TelemetryConfiguration.CreateDefault();
            configuration.InstrumentationKey = "af471157-d0a3-4a20-b7e7-e9c479852bb2";

           Log.Logger = new LoggerConfiguration()
              .WriteTo.Console()
              .WriteTo.File($"{start}_{end}-.txt", rollingInterval: RollingInterval.Day)
              .WriteTo
                .ApplicationInsights(configuration, TelemetryConverter.Events)
              .CreateLogger();
            
            //CreateThumb t = new CreateThumb();
            //await t.ExecuteAsync(path, hilos, start, end);
            //await t.ExecuteAsyncUp(path, hilos);
            //BlobManager manager = n ew BlobManager(path, hilos, start, end);
            //await manager.MigrateData();

            ProcessStage process = new ProcessStage();
            await process.ExecuteAsync();





        }


        static DependencyTrackingTelemetryModule InitializeDependencyTracking(TelemetryConfiguration configuration)
        {
            var module = new DependencyTrackingTelemetryModule();

            // prevent Correlation Id to be sent to certain endpoints. You may add other domains as needed.
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.chinacloudapi.cn");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.cloudapi.de");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.usgovcloudapi.net");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("localhost");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("127.0.0.1");

            // enable known dependency tracking, note that in future versions, we will extend this list. 
            // please check default settings in https://github.com/microsoft/ApplicationInsights-dotnet-server/blob/develop/WEB/Src/DependencyCollector/DependencyCollector/ApplicationInsights.config.install.xdt

            module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.ServiceBus");
            module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");

            // initialize the module
            module.Initialize(configuration);

            return module;
        }
    }
}
