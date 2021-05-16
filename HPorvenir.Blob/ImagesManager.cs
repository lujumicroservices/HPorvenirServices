using HPorvenir.Core.Audit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;

namespace HPorvenir.Blob
{
    public class ImagesManager
    {

        FileWriter _fWriter;
        FileWriter _eWriter;
        DateTime _startDate;
        DateTime _endDate;
        string _basePath;
        string _outputPath;


        Dictionary<int, Dictionary<int, List<int>>> missingDates;
        
        public ImagesManager(string basePath, string ePath, DateTime startDate,  DateTime endDate) {
            _fWriter = new FileWriter(ePath, "errores");
            _eWriter = new FileWriter(ePath, "exceptions");
            _basePath = basePath;
            _outputPath = ePath;

            _startDate = startDate;
            _endDate = endDate;

            missingDates = new Dictionary<int, Dictionary<int, List<int>>>();
        }


        public void MapData(string basePath) {
            var baseDirectory = new DirectoryInfo(basePath);
            EvaluateFolder(baseDirectory, 0);

        }


        public void MapData() {

            
            while (DateTime.Compare(_startDate, _endDate) != 0) {

                if (ExistDay(_startDate))
                {
                    _startDate = _startDate.AddDays(1);
                    continue;
                }
                else if (ExistMonth(_startDate))
                {
                    //add day exception
                    AddException(_startDate, ExceptionType.DAY);

                    _startDate = _startDate.AddDays(1);
                    continue;                    
                }
                else if (ExistYear(_startDate))
                {
                    //add month exception
                    AddException(_startDate, ExceptionType.MONTH);

                    var tempDate = _startDate.AddMonths(1);
                    _startDate = new DateTime(tempDate.Year, tempDate.Month, 1);                    
                }
                else {
                    //add year exception
                    AddException(_startDate, ExceptionType.YEAR);

                    _startDate = new DateTime(_startDate.Year+1, 1, 1);
                }
            }

            var  missingDatesString = JsonConvert.SerializeObject(missingDates);
            using (StreamWriter writer = new StreamWriter(@$"{_outputPath}\missingDates.json", false))
            {
                writer.Write(missingDatesString);
            }




        }

        enum ExceptionType { 
            DAY,
            MONTH,
            YEAR
        }

        private void AddException(DateTime date, ExceptionType exType) {

            if (!missingDates.ContainsKey(date.Year))
            {
                missingDates.Add(date.Year, new Dictionary<int, List<int>>());
            }

            switch (exType)
            {
                case ExceptionType.YEAR:                    
                    _eWriter.Write(DateToYearPath(date));
                    break;
                case ExceptionType.MONTH:
                    
                    if (!missingDates[date.Year].ContainsKey(date.Month))
                    {
                        missingDates[date.Year].Add(date.Month, new List<int>());
                    }

                    _eWriter.Write(DateToMonthPath(date));
                    break;
                case ExceptionType.DAY:
                    
                    if (!missingDates[date.Year].ContainsKey(date.Month))
                    {
                        missingDates[date.Year].Add(date.Month, new List<int>());
                    }

                    missingDates[date.Year][date.Month].Add(date.Day);

                    _eWriter.Write(DateToDayPath(date));
                    break;
            }
        }

        private bool ExistYear(DateTime date) {

            return Directory.Exists($@"{_basePath}{DateToYearPath(date)}");
        }

        private bool ExistMonth(DateTime date)
        {
            return Directory.Exists($@"{_basePath}{DateToMonthPath(date)}");
        }

        private bool ExistDay(DateTime date)
        {
            if (Directory.Exists($@"{_basePath}{DateToDayPath(date)}")) {
                var files = Directory.GetFiles($@"{_basePath}{DateToDayPath(date)}");
                return files.Any(f => f.ToLower().EndsWith(".tiff") || f.ToLower().EndsWith(".tif") || f.ToLower().EndsWith(".pdf"));
            }

            return false;
        }


        private string DateToDayPath(DateTime date) {
            return $@"\{date.Year}\{date.Month.ToString("00")}\{date.Day.ToString("00")}";
        }

        private string DateToMonthPath(DateTime date)
        {
            return $@"\{date.Year}\{date.Month.ToString("00")}";
        }

        private string DateToYearPath(DateTime date)
        {
            return $@"\{date.Year}";
        }






        private void EvaluateFolder(DirectoryInfo dir, int level) {
            var files = dir.GetFiles();
            var folders = dir.GetDirectories();
            
            switch (level) {
                case 0:                    
                case 1:                    
                case 2:                                    
                    if (files.Length > 0) {
                        _fWriter.Write(@$"{dir.FullName} - Archivos en ruta incorrecta");
                    }
                    DateExceptions(folders,level);

                    foreach (var folder in folders)
                        EvaluateFolder(folder, level+1);
                    break;                                                            
                case 3:
                    if (folders.Length > 0)
                    {
                        _fWriter.Write(@$"{dir.FullName} - Folders en ruta incorrecta");
                    }                        
                    //EvaluateFolder(folder, level + 1);
                    break;                
            }            
        }

        private void DateExceptions(DirectoryInfo[] folders, int level) {
            
            int baseIndex = -1;
            foreach (var folder in folders) {
                if (int.TryParse(folder.Name, out int number))
                {
                    if (baseIndex == -1) {
                        baseIndex = number;
                        continue;
                    }
                    if (number == baseIndex + 1)
                    {
                        baseIndex = baseIndex + 1;
                    }
                    else {
                        //add exception
                        switch (level) {
                            case 0:
                                _eWriter.Write(folder.Name);                                
                                break;
                            case 1:
                                _eWriter.Write($@"{folder.Parent.Name}{folder.Name}");
                                break;
                            case 2:
                                _eWriter.Write($@"{folder.Parent.Parent.Name}{folder.Parent.Name}{folder.Name}");                                
                                break;
                        }
                        
                        baseIndex = baseIndex + 1;
                    }

                }
                else {
                    switch (level)
                    {
                        case 0:
                            _fWriter.Write(@$"{folder.FullName} - Nombre de Folders invalido para Año");
                            break;
                        case 1:
                            _fWriter.Write(@$"{folder.FullName} - Nombre de Folders invalido para Mes");
                            break;
                        case 2:
                            _fWriter.Write(@$"{folder.FullName} - Nombre de Folders invalido para Dia");
                            break;
                    }
                    
                    //invalid folder name
                    continue;

                }
            }
        }

        private void MonthExceptions(DirectoryInfo root) { 
        
        }

        private void DayExceptions(DirectoryInfo root) { 
        
        }
    }
}
