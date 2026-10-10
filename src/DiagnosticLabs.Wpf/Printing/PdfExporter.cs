using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiagnosticLabs.Wpf.Printing;

/// <summary>
/// Saves a printed document as a PDF. Each page is drawn as a picture (200 dpi) on a Letter-sized PDF page, so the file looks exactly like the
/// preview and needs no PDF library or printer driver; the text in it cannot be selected.
/// </summary>
public static class PdfExporter
{
    private const double Dpi = 200;

    public static void Save(FixedDocument document, string path)
    {
        var paginator = document.DocumentPaginator;
        var pages = new List<(byte[] Jpeg, int Width, int Height)>();
        for (var i = 0; i < paginator.PageCount; i++)
        {
            using var page = paginator.GetPage(i);
            var size = page.Size;
            var width = (int)Math.Round(size.Width * Dpi / 96);
            var height = (int)Math.Round(size.Height * Dpi / 96);
            var bitmap = new RenderTargetBitmap(width, height, Dpi, Dpi, PixelFormats.Pbgra32);

            // White paper first, so the transparent parts of the page do not turn black in the JPEG.
            var paper = new DrawingVisual();
            using (var context = paper.RenderOpen())
                context.DrawRectangle(Brushes.White, null, new Rect(0, 0, size.Width, size.Height));
            bitmap.Render(paper);
            bitmap.Render(page.Visual);

            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            pages.Add((stream.ToArray(), width, height));
        }

        File.WriteAllBytes(path, Build(pages));
    }

    private static byte[] Build(List<(byte[] Jpeg, int Width, int Height)> pages)
    {
        // Object numbers: 1 catalog, 2 page tree, then per page: page, content, image.
        using var output = new MemoryStream();
        var offsets = new List<long>();

        void Write(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        void Begin(int number)
        {
            offsets.Add(output.Position);
            Write($"{number} 0 obj\n");
        }

        Write("%PDF-1.4\n");

        Begin(1);
        Write("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        Begin(2);
        var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + (i * 3)} 0 R"));
        Write($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>\nendobj\n");

        for (var i = 0; i < pages.Count; i++)
        {
            var (jpeg, width, height) = pages[i];
            var pageNumber = 3 + (i * 3);
            var pointsWide = Math.Round(width * 72 / Dpi, 2);
            var pointsHigh = Math.Round(height * 72 / Dpi, 2);
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            Begin(pageNumber);
            Write(string.Create(inv, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pointsWide} {pointsHigh}] /Resources << /XObject << /Im0 {pageNumber + 2} 0 R >> >> /Contents {pageNumber + 1} 0 R >>\nendobj\n"));

            var content = string.Create(inv, $"q {pointsWide} 0 0 {pointsHigh} 0 0 cm /Im0 Do Q");
            Begin(pageNumber + 1);
            Write($"<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n");

            Begin(pageNumber + 2);
            Write($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            output.Write(jpeg);
            Write("\nendstream\nendobj\n");
        }

        var xref = output.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            Write($"{offset:D10} 00000 n \n");

        Write($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
