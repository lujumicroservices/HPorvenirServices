using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration
{
    [ElasticsearchType(IdProperty = "Id")]
    public class Doc
    {
        public string? Id { get; set; }
        public long? Date { get; set; }
        public string? Coords { get; set; }
        public string? Name { get; set; }
        public string? Content { get; set; }

       
    }
}
