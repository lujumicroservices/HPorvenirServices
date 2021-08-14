using System;

namespace HPorvenir.Parser
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            XmlParser parser = new XmlParser();
            parser.Execute(@"D:\DEV\PorvenirImages\2020\10\02");
        }
    }
}
