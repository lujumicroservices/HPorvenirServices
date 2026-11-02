using HPorvenir.Parser;
using Spire.Pdf;
using Spire.Pdf.General.Find;
using Spire.Pdf.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;


namespace HPorvenir.Document
{
    public class PDFDocument
    {
        private const string WatermarkFileName = "marca_agua.png";

        private static string ResolveWatermarkPath()
        {
            var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, "assets", WatermarkFileName);
            if (File.Exists(baseDirectoryPath))
            {
                return baseDirectoryPath;
            }

            var workingDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", WatermarkFileName);
            if (File.Exists(workingDirectoryPath))
            {
                return workingDirectoryPath;
            }

            throw new FileNotFoundException(
                "Watermark asset not found. Expected assets/marca_agua.png next to the application.",
                baseDirectoryPath);
        }

        private static void ApplyWatermark(PdfDocument doc, PdfImage watermark, bool removeWatermark)
        {
            if (removeWatermark)
            {
                return;
            }

            for (int pageIndex = 0; pageIndex < doc.Pages.Count; pageIndex++)
            {
                var page = doc.Pages[pageIndex];
                page.Canvas.SetTransparency(0.3f);

                bool offsetRow = true;
                int verticalPositions = (int)Math.Ceiling(page.Size.Height / watermark.Height);
                int horizontalPositions = (int)Math.Ceiling(page.Size.Width / watermark.Width);

                for (int i = 0; i < verticalPositions; i++)
                {
                    for (int j = 0; j < horizontalPositions; j++)
                    {
                        float x = watermark.Width * j - (offsetRow ? watermark.Width / 2 : 0);
                        float y = watermark.Height * i;
                        page.Canvas.DrawImage(watermark, x, y);
                    }
                    offsetRow = !offsetRow;
                }

                page.Canvas.SetTransparency(1f);
            }
        }

        public Stream ProcessFile(Stream fileStram, List<Paragraph> hits, string[] terms,bool isPDF, bool removeWhaterMark) {

            Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument();

            if (isPDF)
            {
                doc.LoadFromStream(fileStram);
                
            }
            else {
                fileStram.Position = 0;
                PdfImage pdfImg = PdfImage.FromStream(fileStram);

                PdfPageBase pageT = doc.Pages.Add(new SizeF(pdfImg.PhysicalDimension.Width + (pdfImg.PhysicalDimension.Width * .10F), pdfImg.PhysicalDimension.Height + (pdfImg.PhysicalDimension.Height * .10F)));
                          
                float fitWidth = pdfImg.PhysicalDimension.Width;
                float fitHeight = pdfImg.PhysicalDimension.Height;
                pageT.Canvas.DrawImage(pdfImg, 0, 0, fitWidth, fitHeight);
            }

            PdfImage pima = PdfImage.FromFile(ResolveWatermarkPath());
            ApplyWatermark(doc, pima, removeWhaterMark);

            var page = doc.Pages[0];

            if (isPDF && terms != null)
            {
                HighlightPdfTerms(page, terms);
            }
            else if (hits != null)
            {
                HighlightCoordHits(page, hits);
            }
            





            var streamResult = new System.IO.MemoryStream();
            doc.SaveToStream(streamResult,Spire.Pdf.FileFormat.PDF);
            streamResult.Position = 0;
            return streamResult;
        }

        private const float HighlightOpacity = 0.35f;

        private static void HighlightPdfTerms(PdfPageBase page, string[] terms)
        {
            var rectangles = new List<Rectangle>();
            foreach (var term in terms)
            {
                if (string.IsNullOrWhiteSpace(term))
                {
                    continue;
                }

                var finds = page.FindText(term, TextFindParameter.IgnoreCase).Finds;
                if (finds == null)
                {
                    continue;
                }

                foreach (PdfTextFind find in finds)
                {
                    rectangles.Add(Rectangle.Round(find.Bounds));
                }
            }

            DrawTransparentHighlights(page, rectangles);
        }

        private static void HighlightCoordHits(PdfPageBase page, List<Paragraph> hits)
        {
            var rectangles = new List<Rectangle>(hits.Count);
            foreach (var hit in hits)
            {
                rectangles.Add(ParseCoordsRectangle(hit.Coords));
            }

            DrawTransparentHighlights(page, rectangles);
        }

        private static Rectangle ParseCoordsRectangle(string coords)
        {
            var parts = coords.Split(',');
            var x0 = int.Parse(parts[0]) * 72 / 300;
            var y0 = int.Parse(parts[1]) * 72 / 300;
            var x1 = int.Parse(parts[2]) * 72 / 300;
            var y1 = int.Parse(parts[3]) * 72 / 300;
            return new Rectangle(new Point(x0, y0), new Size(x1 - x0, y1 - y0));
        }

        private static void DrawTransparentHighlights(PdfPageBase page, IList<Rectangle> rectangles)
        {
            if (rectangles == null || rectangles.Count == 0)
            {
                return;
            }

            page.Canvas.SetTransparency(HighlightOpacity);
            var brush = new PdfSolidBrush(new PdfRGBColor(255, 255, 0));
            var pen = new PdfPen(PdfBrushes.Transparent, 0f);

            foreach (var rect in rectangles)
            {
                page.Canvas.DrawRectangle(pen, brush, rect);
            }

            page.Canvas.SetTransparency(1f);
        }

    }
}
