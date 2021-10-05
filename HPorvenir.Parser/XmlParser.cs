using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Globalization;

namespace HPorvenir.Parser
{
    public class XmlParser
    {


        public void Execute(string basePath) {
            var xmlFolder = new DirectoryInfo(basePath);
            var files = xmlFolder.GetFiles();
            foreach (var file in files) {
                if (file.Extension.ToLower() == ".xml") {
                    ParseXml(file);
                }
            }        
        }


        public List<Paragraph> ParseXml(FileInfo file) {

            List<Paragraph> paragraphs = new List<Paragraph>();

            int fileDate = int.Parse(file.Directory.Parent.Parent.Name + file.Directory.Parent.Name + file.Directory.Name);
            
            //page.Path = file.FullName;
            //page.Name = file.Name;

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            using (var fileStream = file.OpenRead()) {
                XPathDocument xPath = new XPathDocument(fileStream);
                var navigator = xPath.CreateNavigator();

                //Compile the query with a namespace prefix. 
                XPathExpression query = navigator.Compile("METS:mets/METS:dmdSec/METS:amdSec/METS:mdWrap/METS:xmlData/hiddentext/pagecolumn/region/paragraph");

                //Do some BS to get the default namespace to actually be called . 
                var nameSpace = new XmlNamespaceManager(navigator.NameTable);
                nameSpace.AddNamespace("METS", "http://www.loc.gov/METS/");
                query.SetContext(nameSpace);

                var paragraph = navigator.Select(query);

                int paraindex = 0;
                while (paragraph.MoveNext()) {

                    StringBuilder content = new StringBuilder();

                    var words = paragraph.Current.Select("line/word");
                    while (words.MoveNext())
                    {
                        content.Append(ConvertWesternEuropeanToASCII(words.Current.Value.ToLower()) + " ");                         
                    }

                    paragraph.Current.MoveToFirstAttribute();

                    paragraphs.Add( new Paragraph {Id = $"{fileDate}{file.Name.Replace(file.Extension,"").Replace("-","")}{paraindex.ToString()}",  Date = fileDate, Name = $"{fileDate}{file.Name}",  Coords = paragraph.Current.Value, Content = content.ToString() }  );
                    paraindex++;
                }                
            }


            return paragraphs;
        }


        public List<Paragraph> ParseXml(MemoryStream fileStream, string blobName)
        {

            var path = blobName.Split("/");
            var nameNoExtension = path[1].Substring(0, path[1].Length-4);

            List<Paragraph> paragraphs = new List<Paragraph>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            
            XPathDocument xPath = new XPathDocument(fileStream);
            var navigator = xPath.CreateNavigator();

            //Compile the query with a namespace prefix. 
            XPathExpression query = navigator.Compile("METS:mets/METS:dmdSec/METS:amdSec/METS:mdWrap/METS:xmlData/hiddentext/pagecolumn/region/paragraph");

            //Do some BS to get the default namespace to actually be called . 
            var nameSpace = new XmlNamespaceManager(navigator.NameTable);
            nameSpace.AddNamespace("METS", "http://www.loc.gov/METS/");
            query.SetContext(nameSpace);

            var paragraph = navigator.Select(query);

            int paraindex = 0;
            while (paragraph.MoveNext())
            {

                StringBuilder content = new StringBuilder();

                var words = paragraph.Current.Select("line/word");
                while (words.MoveNext())
                {
                    content.Append(ConvertWesternEuropeanToASCII(words.Current.Value.ToLower()) + " ");
                }

                paragraph.Current.MoveToFirstAttribute();

                paragraphs.Add(new Paragraph { Id = $"{path[0]}{nameNoExtension.Replace("-", "")}{paraindex.ToString()}", Date = int.Parse(path[0]), Name = $"{path[0]}{path[1]}", Coords = paragraph.Current.Value, Content = content.ToString() });
                paraindex++;
            }
            

            return paragraphs;
        }




        public string ConvertWesternEuropeanToASCII(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

    }
}
