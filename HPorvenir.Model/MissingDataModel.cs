using System;
using System.Collections.Generic;
using System.Text;

namespace HPorvenir.Model
{
    public class MissingDataModel
    {
        public string startDate { get; set; }

        public string endDate { get; set; }

        public Dictionary<int, Dictionary<int, List<int>>> missingDate { get; set; }
    }
}
