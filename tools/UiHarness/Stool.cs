using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Printing;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

// Stands in for the preview window: keeps the report and renders its page to a PNG so the layout can be looked at.
internal sealed class CapturePreview : IReportPreviewDialog
{
    public static PrintableReport? Last { get; private set; }

    public static string OutputFolder { get; set; } = "";

    public void Show(PrintableReport report)
    {
        Last = report;
        var document = ReportDocumentBuilder.Build(report);
        DiagnosticLabs.Wpf.Printing.PdfExporter.Save(document, Path.Combine(OutputFolder, $"print-{report.Layout}.pdf"));
        var page = document.Pages[0].GetPageRoot(false);
        page.Measure(new Size(ReportDocumentBuilder.PageWidth, ReportDocumentBuilder.PageHeight));
        page.Arrange(new Rect(0, 0, ReportDocumentBuilder.PageWidth, ReportDocumentBuilder.PageHeight));
        page.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)ReportDocumentBuilder.PageWidth, (int)ReportDocumentBuilder.PageHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(page);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var lines = new List<string>();
        foreach (var child in ((Canvas)((FixedPage)page).Children[0]).Children)
            if (child is TextBlock tb)
                lines.Add($"{Canvas.GetLeft(tb):N1}|{Canvas.GetTop(tb) + tb.BaselineOffset:N1}|{tb.Text}");
        File.WriteAllLines(Path.Combine(OutputFolder, $"print-{report.Layout}-text.txt"), lines);
        using var stream = File.Create(Path.Combine(OutputFolder, $"print-{report.Layout}.png"));
        encoder.Save(stream);
    }

    public static FixedDocument? LastDocument { get; private set; }

    public static void Clear() => LastDocument = null;

    // A report that is not a result form (the Sales Report): every page goes to a PNG, and the whole document to a PDF.
    public void Show(FixedDocument document, string title, string fileName)
    {
        LastDocument = document;
        var size = document.DocumentPaginator.PageSize;
        DiagnosticLabs.Wpf.Printing.PdfExporter.Save(document, Path.Combine(OutputFolder, $"{fileName}.pdf"));
        for (var i = 0; i < document.Pages.Count; i++)
        {
            var page = document.Pages[i].GetPageRoot(false);
            page.Measure(size);
            page.Arrange(new Rect(0, 0, size.Width, size.Height));
            page.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(page);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(OutputFolder, $"{fileName}-page{i + 1}.png"));
            encoder.Save(stream);
        }
    }
}

internal static partial class Program
{
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }
}
