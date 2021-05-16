using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HPorvenir.Storage
{
    interface IStorage
    {
        File Read();
        File Save(bool overwrite = false);
        void Delete();
    }
}
 