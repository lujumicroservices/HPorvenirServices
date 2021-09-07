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

        public Stream ProcessFile(Stream fileStram, List<Paragraph> hits, string[] terms,bool isPDF, bool removeWhaterMark) {

            Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument();

            if (isPDF)
            {
                doc.LoadFromStream(fileStram);
                
            }
            else {
                Image tiffImage = Image.FromStream(fileStram);
                PdfImage pdfImg = PdfImage.FromStream(fileStram);

                float difw = pdfImg.PhysicalDimension.Width + (pdfImg.PhysicalDimension.Width*.10F);
                PdfPageBase pageT = doc.Pages.Add(new SizeF(pdfImg.PhysicalDimension.Width + (pdfImg.PhysicalDimension.Width * .10F), pdfImg.PhysicalDimension.Height + (pdfImg.PhysicalDimension.Height * .10F)));
                          
                float fitWidth = pdfImg.PhysicalDimension.Width;
                float fitHeight = pdfImg.PhysicalDimension.Height;
                pageT.Canvas.DrawImage(pdfImg, 0, 0, fitWidth, fitHeight);
            }

            PdfImage pima = PdfImage.FromFile(@"./assets/marca_agua.gif");
            PdfPen pen = new PdfPen(PdfBrushes.Yellow, 1f);
            PdfBrush brush1 = PdfBrushes.Yellow;
            var page = doc.Pages[0];            

            page.Canvas.SetTransparency(.5f);
            
            bool bitLine = true;
            double vpositions =  page.Size.Height / pima.Height;
            double hpositions = page.Size.Width / pima.Width;
            double wmh = pima.Height;
            double dh = page.Size.Height;

            for (int i = 0; i < vpositions; i++) {                
                for (int j = 0; j < hpositions; j++) {
                    float x = pima.Width * j - (bitLine? pima.Width/2 : 0);
                    float y = pima.Height * i;
                    if(!removeWhaterMark)
                        page.Canvas.DrawImage(pima, x, y);
                }
                bitLine = !bitLine;
            }


            if (isPDF && terms != null)
            {
                //doc.Pages[0].

                foreach (var term in terms) {
                    PdfTextFind[] result1 = null;
                    result1 = doc.Pages[0].FindText(term, TextFindParameter.IgnoreCase).Finds;
                    foreach (PdfTextFind find in result1)
                    {
                        find.ApplyHighLight(Color.Yellow);
                    }
                }
                                    
            }
            else {
                if (hits != null)
                    foreach (var hit in hits)
                    {
                        var coords = hit.Coords.Split(',');
                        page.Canvas.DrawRectangle(pen, brush1, new Rectangle(new Point(int.Parse(coords[0]) * 72 / 300, int.Parse(coords[1]) * 72 / 300), new Size((int.Parse(coords[2]) - int.Parse(coords[0])) * 72 / 300, (int.Parse(coords[3]) - int.Parse(coords[1])) * 72 / 300)));
                    }
            }
            





            var streamResult = new System.IO.MemoryStream();
            doc.SaveToStream(streamResult,Spire.Pdf.FileFormat.PDF);
            streamResult.Position = 0;
            return streamResult;
        }

    }
}
