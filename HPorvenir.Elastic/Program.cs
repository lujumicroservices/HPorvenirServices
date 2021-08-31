using Serilog;
using System;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    class Program
    {
        static async Task Main(string[] args)
        {

            int startyear = int.Parse(args[0]);
            int endyear = int.Parse(args[1]);
            string indexname = $"{args[2]}v2_{startyear}-{endyear}";
            int hilos = int.Parse(args[3]);

            Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File($"{indexname}-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();


            Log.Information("Start_index");

            //Console.WriteLine("Hello World!");
            //new Index(@"E:\IndicesV2", "hporvenir").Execute(@"E:\Porvenir\Hemeroteca\Diario\Mexico\Nuevo Leon\Monterrey\El Porvenir");

            await new Index(startyear, endyear, indexname, hilos).ExecuteAsync(@"E:\Porvenir\Hemeroteca\Diario\Mexico\Nuevo Leon\Monterrey\El Porvenir");

            //String[] terms = new String[] { "respuestas" };
            
            //var result = new Searcher("hporvenir*").Search(terms,false);
            //var result = new Searcher("hporvenir*").FileDetails("1971070300460-03.xml", terms, false);

            
        }
    }
}
