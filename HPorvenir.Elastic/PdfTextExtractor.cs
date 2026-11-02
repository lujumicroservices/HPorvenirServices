using HPorvenir.Parser;
using Serilog;
using Spire.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HPorvenir.Elastic
{
    public static class PdfTextExtractor
    {
        public static List<Paragraph> ExtractFromFile(int fileDate, string fileName, string filePath)
        {
            using (var document = new PdfDocument(filePath))
            {
                return ExtractFromDocument(fileDate, fileName, document);
            }
        }

        public static List<Paragraph> ExtractFromStream(int fileDate, string fileName, MemoryStream fileStream)
        {
            fileStream.Position = 0;
            using (var document = new PdfDocument(fileStream))
            {
                return ExtractFromDocument(fileDate, fileName, document);
            }
        }

        public static List<Paragraph> ExtractFromDocument(int fileDate, string fileName, PdfDocument document)
        {
            var nameNoExtension = Path.GetFileNameWithoutExtension(fileName).Replace("-", "");
            var documentName = BlobPathParser.BuildDocumentName(fileDate, fileName);
            var contentBuilder = new StringBuilder();
            var pagesWithText = 0;

            for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
            {
                string pageText;
                try
                {
                    pageText = document.Pages[pageIndex].ExtractText();
                }
                catch (Exception ex)
                {
                    Log.Debug(
                        ex,
                        "Skipping page {PageNumber}/{TotalPages} in {FileName}",
                        pageIndex + 1,
                        document.Pages.Count,
                        fileName);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(pageText))
                {
                    continue;
                }

                pagesWithText++;
                if (contentBuilder.Length > 0)
                {
                    contentBuilder.AppendLine();
                    contentBuilder.AppendLine();
                }

                contentBuilder.Append(pageText.Trim());
            }

            if (contentBuilder.Length == 0)
            {
                Log.Debug(
                    "PDF {FileName} produced no extractable text across {PageCount} pages",
                    fileName,
                    document.Pages.Count);
                return new List<Paragraph>();
            }

            Log.Debug(
                "Extracted {PagesWithText}/{TotalPages} pages ({CharacterCount} chars) from {FileName}",
                pagesWithText,
                document.Pages.Count,
                contentBuilder.Length,
                fileName);

            return new List<Paragraph>
            {
                new Paragraph
                {
                    Content = contentBuilder.ToString(),
                    Date = fileDate,
                    Id = $"{fileDate}{nameNoExtension}0",
                    Name = documentName
                }
            };
        }
    }
}
