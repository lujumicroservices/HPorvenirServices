using System;
using System.Collections.Generic;
using System.Text;

namespace HPorvenir.Model
{
    public class AggResult
    {
        public string Name { get; set; }

        public string Date { 
            get {
                return Name.Substring(0,8);
            } 
        }

        public string FileName {
            get {
                return Name.Substring(7, Name.Length - 8);
            }
        }
    }
}
