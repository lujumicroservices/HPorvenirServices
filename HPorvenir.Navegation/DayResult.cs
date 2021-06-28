using System.Collections.Generic;

namespace HPorvenir.Navegation
{
    public class DayResult
    {
        public string URLPrefix {
            get; set;
        }

        public string ShareKey
        {
            get; set;
        }

        public List<string> Pages {
            get; set;
        }

        public List<string> Thumb
        {
            get; set;
        }
    }
}
