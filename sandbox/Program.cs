using HPorvenir.Blob;
using Serilog;
using System;
using System.Threading.Tasks;

namespace sandbox
{
    class Program
    {
        static async Task Main(string[] args)
        {

            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);

            string path = args[0];
            int hilos = int.Parse(args[1]);
            int start = 0;
            int end = 3000;

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


            

           Console.WriteLine("Hello World!");
           Log.Logger = new LoggerConfiguration()
          .WriteTo.Console()
          .WriteTo.File($"{start}_{end}-.txt", rollingInterval: RollingInterval.Day)
          .CreateLogger();

            Log.Information("Start_index");

            BlobManager manager = new BlobManager(path, hilos, start, end);
            await manager.MigrateData();


            


        }
    }
}
