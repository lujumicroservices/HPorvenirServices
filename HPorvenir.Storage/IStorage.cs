﻿using HPorvenir.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HPorvenir.Storage
{
    public interface IStorage
    {
        System.Threading.Tasks.Task<Stream> ReadAsync(string fileName);
        bool Save(bool overwrite = false);
        void Delete();

        DayResult ListDay(int year, int month, int day);
    }
}
 