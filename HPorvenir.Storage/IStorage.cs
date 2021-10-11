using HPorvenir.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HPorvenir.Storage
{
    public interface IStorage
    {
        Task<Stream> ReadPathFromIndexAsync(string fileName);
        Task<Stream> ReadPathAsync(string fileName);
        Stream GetMetadata();
        bool Save(bool overwrite = false);
        void Delete();
        DayResult ListDay(int year, int month, int day);
    }
}
 