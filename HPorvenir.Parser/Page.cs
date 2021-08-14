using System;
using System.Collections.Generic;
using System.Text;

namespace HPorvenir.Parser
{
    public class Page
    {
        public Page(int date) {
            Date = date;
            Paragraphs = new List<Paragraph>();
        }

        public int Date { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public List<Paragraph> Paragraphs { get; set; }
    }


    public class Paragraph {
        
        public string Id { get; set; }
        public int Date { get; set; }
        public string Name { get; set; }      
        public string Coords { get; set; }
        public string Content { get; set; }    
    }
}
