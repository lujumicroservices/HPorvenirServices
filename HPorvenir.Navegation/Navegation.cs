using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace HPorvenir.Navegation
{
    public class Navegation
    {
        public Dictionary<int, Dictionary<int, List<int>>> LoadNavigation() {
            using (StreamReader r = new StreamReader("missingDates.json"))
            {
                string json = r.ReadToEnd();
                var items = JsonConvert.DeserializeObject<Dictionary<int,Dictionary<int,List<int>>>>(json);
                return items;
            }
        }

        
    }
}
