﻿using HPorvenir.Document;
using System;

namespace HPorvenir.Elastic
{
    class Program
    {
        static void Main(string[] args)
        {
            //int startyear = int.Parse(args[0]);
            //int endyear = int.Parse(args[1]);
            //string indexname = args[2];
            //int hilos = int.Parse(args[3]);

            //Console.WriteLine("Hello World!");
            // new Index(@"E:\IndicesV2", "hporvenir").Execute(@"E:\Porvenir\Hemeroteca\Diario\Mexico\Nuevo Leon\Monterrey\El Porvenir");

            //new Index(startyear, endyear, indexname, hilos).Execute(@"E:\Porvenir\Hemeroteca\Diario\Mexico\Nuevo Leon\Monterrey\El Porvenir");

            //String[] terms = new String[] { "hola","todos" };

            //var result = new Searcher("hporvenir*").Search(terms,false);
            //var result = new Searcher("hporvenir*").FileDetails("1971070300460-03.xml", terms, false);

            Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument(@"D:\DEV\06_03_2021_14_Escena2.pdf");

            var page = doc.Pages[0];
            var text = page.FindAllText();
            var text2 = page.ExtractText();
            //page.FindText()
            


            //PDFDocument doc = new PDFDocument();
            //doc.ProcessFile();

            
        }
    }
}
