using Azure.Storage.Blobs;
using System;

namespace HPorvenir.Blob
{
    class Program
    {


        static async System.Threading.Tasks.Task Main(string[] args)
        {
            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);


            EvaluateFiles(args);         
        }

        static async System.Threading.Tasks.Task Migrate(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);



            BlobManager manager = new BlobManager(args[0],int.Parse(args[1]));
            await manager.MigrateData();
            Console.WriteLine("container created");

        }


        static void EvaluateFiles(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);



            ImagesManager manager = new ImagesManager(args[0], args[1], DateTime.Parse(args[2]), DateTime.Parse(args[3]));
            manager.MapData();
            Console.WriteLine("container created");

        }
    }
}
