using HPorvenir.Parser;
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

        public Stream ProcessFile(Stream fileStram, List<Paragraph> hits, bool removeWhaterMark) {

            Spire.Pdf.PdfDocument doc = new Spire.Pdf.PdfDocument(fileStram);
            Image img = Image.FromFile(@"./assets/marca_agua.gif");
            PdfImage pima = PdfImage.FromFile(@"./assets/marca_agua.gif");
            var stream = new System.IO.MemoryStream();
                        
            PdfPen pen = new PdfPen(PdfBrushes.Yellow, 1f);
            PdfBrush brush1 = PdfBrushes.Yellow;
            var page = doc.Pages[0];            

            img.Save(stream, ImageFormat.Gif);
            //page.BackgroundImage = stream;
            page.Canvas.SetTransparency(.5f);

            if (hits != null)
            foreach (var hit in hits) {
                var coords = hit.Coords.Split(',');
                page.Canvas.DrawRectangle(pen, brush1, new Rectangle(new Point(int.Parse(coords[0]) * 72 / 300, int.Parse(coords[1]) * 72 / 300), new Size((int.Parse(coords[2]) - int.Parse(coords[0])) * 72 / 300, (int.Parse(coords[3]) - int.Parse(coords[1])) * 72 / 300)));
            }

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

            var streamResult = new System.IO.MemoryStream();
            doc.SaveToStream(streamResult,Spire.Pdf.FileFormat.PDF);
            streamResult.Position = 0;
            return streamResult;
        }

    }
}
