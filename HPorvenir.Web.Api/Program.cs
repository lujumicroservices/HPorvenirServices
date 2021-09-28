using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CS_AES_CTR;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace HPorvenir.Web.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {           
            Log.Logger = new LoggerConfiguration()                                    
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(LogEventLevel.Debug)
            .CreateLogger();
            CreateHostBuilder(args).Build().Run();            
        }


        

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
