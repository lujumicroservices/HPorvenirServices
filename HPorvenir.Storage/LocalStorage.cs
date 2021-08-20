using HPorvenir.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HPorvenir.Storage
{
    class LocalStorage : IStorage
    {
        public void Delete()
        {
            throw new NotImplementedException();
        }

        public DayResult ListDay(int year, int month, int day)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> ReadAsync(string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> ReadPathAsync(string fileName)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> ReadPathFromIndexAsync(string fileName)
        {
            throw new NotImplementedException();
        }

        public bool Save(bool overwrite = false)
        {
            throw new NotImplementedException();
        }
    }
}
