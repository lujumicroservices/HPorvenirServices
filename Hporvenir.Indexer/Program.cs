using System;

namespace Hporvenir.Indexer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start Program!");


            string startyear = args[0];
            string endyear = args[1];
            string mergename = args[2];

            //IndexClient ind = new IndexClient();
            //ind.MergeIndex(int.Parse(startyear), int.Parse(endyear), mergename);
        }
    }
}
