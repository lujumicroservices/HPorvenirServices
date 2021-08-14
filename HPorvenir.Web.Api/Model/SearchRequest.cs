using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Model
{
    public class SearchRequest
    {

        public String[] Terms { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public String FileName { get; set; }

        public bool IsPhrase { get; set; }

    }
}
