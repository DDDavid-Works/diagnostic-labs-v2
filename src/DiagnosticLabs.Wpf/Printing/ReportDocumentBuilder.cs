using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;

namespace DiagnosticLabs.Wpf.Printing;

/// <summary>
/// Lays a <see cref="PrintableReport"/> out on a US Letter page (816 x 1056 pixels at 96 dpi). Each result type has its own page design,
/// placed at the coordinates measured from the client's printed forms, so the paper matches what they use today.
/// Everything that appears on the page comes from the report, which is worked out and tested in the Application layer.
/// </summary>
public static class ReportDocumentBuilder
{
    public const double PageWidth = 816;
    public const double PageHeight = 1056;

    public static FixedDocument Build(PrintableReport report)
    {
        var canvas = new Canvas { Width = PageWidth, Height = PageHeight, Background = Brushes.White };

        switch (report.Layout)
        {
            case ReportLayout.StoolFecalysis:
                StoolFecalysisLayout.Draw(canvas, report);
                break;
            default:
                throw new NotSupportedException($"There is no page design for {report.Layout}.");
        }

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(PageWidth, PageHeight);

        var page = new FixedPage { Width = PageWidth, Height = PageHeight, Background = Brushes.White };
        page.Children.Add(canvas);

        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);
        document.Pages.Add(pageContent);
        return document;
    }
}

/// <summary>Small drawing helpers shared by the page designs. All positions are page pixels; text is placed by its baseline, like the originals.</summary>
internal static class PageDrawing
{
    public const double Body = 13;
    private static readonly FontFamily Font = new("Arial");

    public static TextBlock Text(string? value, double size, bool bold, double? width = null, TextAlignment alignment = TextAlignment.Left)
    {
        var block = new TextBlock
        {
            Text = value ?? string.Empty,
            FontFamily = Font,
            FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = alignment,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Black,
        };
        if (width is { } w)
            block.Width = w;

        block.Measure(new Size(width ?? double.PositiveInfinity, double.PositiveInfinity));
        return block;
    }

    /// <summary>Places text so its first baseline sits on <paramref name="baseline"/>.</summary>
    public static TextBlock PutText(Canvas canvas, string? value, double x, double baseline, double size, bool bold, double? width = null, TextAlignment alignment = TextAlignment.Left)
    {
        var block = Text(value, size, bold, width, alignment);
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, baseline - block.BaselineOffset);
        canvas.Children.Add(block);
        return block;
    }

    /// <summary>A bold label followed by its colon, as printed ("Patient Code :"); returns where the colon ended.</summary>
    public static void PutLabel(Canvas canvas, string label, double x, double baseline)
    {
        var text = PutText(canvas, label, x, baseline, Body, bold: true);
        PutText(canvas, ":", x + text.DesiredSize.Width, baseline, Body, bold: true);
    }

    public static void PutRule(Canvas canvas, double x, double y, double width, double thickness = 1.33) =>
        PutRect(canvas, x, y, width, thickness, Brushes.Black, null, 0);

    public static void PutVerticalRule(Canvas canvas, double x, double y, double height, double thickness = 1.33) =>
        PutRect(canvas, x, y, thickness, height, Brushes.Black, null, 0);

    /// <summary>An outlined (and optionally filled) rectangle; the border is drawn inside the given bounds.</summary>
    public static void PutRect(Canvas canvas, double x, double y, double width, double height, Brush? fill, Brush? border, double thickness)
    {
        var rect = new Border
        {
            Width = width,
            Height = height,
            Background = fill,
            BorderBrush = border,
            BorderThickness = new Thickness(thickness),
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);
    }

    public static string Line(PrintableReport report, string label) =>
        report.PatientLines.FirstOrDefault(l => l.Label == label)?.Value ?? string.Empty;

    /// <summary>The company block (logo, name, branch, address, contacts, e-mail) and the photo box, identical on every form.</summary>
    public static void PutHeader(Canvas canvas, ReportLetterhead letterhead)
    {
        if (ImageLoader.FromBytes(letterhead.Logo) is { } logo)
        {
            var image = new Image { Source = logo, Width = 92, Height = 100, Stretch = Stretch.Uniform };
            Canvas.SetLeft(image, 52.7);
            Canvas.SetTop(image, 32);
            canvas.Children.Add(image);
        }

        PutText(canvas, letterhead.CompanyName, 150.6, 45.6, 18, bold: true);
        PutText(canvas, letterhead.SubCompanyName, 150.6, 68.0, Body, bold: false);
        PutText(canvas, letterhead.Address, 150.6, 86.0, Body, bold: false);
        PutText(canvas, letterhead.ContactNumbers, 150.6, 103.6, Body, bold: false);
        PutText(canvas, letterhead.Email, 150.6, 121.2, Body, bold: false);

        // The box at the top right is for a photo; it is printed empty, as on the old forms.
        PutRect(canvas, 640.7, 30, 112.6, 109.7, null, Brushes.Black, 1.33);
    }

    /// <summary>The yellow title bar with the form's name centred in it.</summary>
    public static void PutTitleBar(Canvas canvas, string title)
    {
        PutRect(canvas, 50.3, 214.9, 712.4, 21.4, new SolidColorBrush(Color.FromRgb(225, 225, 0)), Brushes.Black, 1.4);
        PutText(canvas, title, 50.3, 230.0, Body, bold: true, width: 712.4, alignment: TextAlignment.Center);
    }

    /// <summary>The patient block: three rows, the first two split into a left and a right half, exactly as on the old forms.</summary>
    public static void PutPatientBlock(Canvas canvas, PrintableReport report)
    {
        PutLabel(canvas, "Patient Code", 52.7, 161.7);
        PutText(canvas, Line(report, "Patient Code"), 185.3, 161.3, Body, bold: false);
        PutLabel(canvas, "Patient Name", 52.7, 182.3);
        PutText(canvas, Line(report, "Patient Name"), 185.3, 182.0, Body, bold: false);
        PutLabel(canvas, "Company/Physician", 52.7, 204.3);
        PutText(canvas, Line(report, "Company/Physician"), 185.3, 204.0, Body, bold: false);

        PutLabel(canvas, "Age", 453.3, 161.7);
        PutText(canvas, Line(report, "Age"), 516.0, 161.3, Body, bold: false);
        PutLabel(canvas, "Sex", 629.3, 161.7);
        PutText(canvas, Line(report, "Sex"), 692.0, 161.3, Body, bold: false);
        PutLabel(canvas, "Date Requested", 453.3, 182.3);
        PutText(canvas, Line(report, "Date Requested"), 564.0, 182.0, Body, bold: false);
    }

    /// <summary>The "computer generated" note and the two signatories, laid out below <paramref name="top"/> (the bottom of the last box).</summary>
    public static void PutFooter(Canvas canvas, PrintableReport report, double top)
    {
        var note = top + 21.0;
        PutText(canvas, report.FooterNote, 50.3, note, Body, bold: false, width: 712.4, alignment: TextAlignment.Center);

        double[] left = [89.0, 448.7];
        for (var i = 0; i < report.Signatories.Count && i < left.Length; i++)
        {
            const double width = 284.9;
            PutText(canvas, report.Signatories[i].Name, left[i], note + 27.7, Body, bold: true, width: width, alignment: TextAlignment.Center);
            PutRule(canvas, left[i], note + 33.4, width);
            PutText(canvas, report.Signatories[i].Role, left[i], note + 47.9, Body, bold: false, width: width, alignment: TextAlignment.Center);
        }
    }
}

/// <summary>Stool/Fecalysis, copied from the client's printed form.</summary>
internal static class StoolFecalysisLayout
{
    private const double Left = 51.3;
    private const double Right = 763.3;

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        PageDrawing.PutHeader(canvas, report.Letterhead);
        PageDrawing.PutTitleBar(canvas, report.Title);
        PageDrawing.PutPatientBlock(canvas, report);

        // Color and Consistency share the top row of the result box, in four cells.
        PageDrawing.PutLabel(canvas, "Color", 54.7, 260.3);
        PageDrawing.PutText(canvas, report.ResultLines.FirstOrDefault(l => l.Label == "Color")?.Value, 218.0, 260.0, PageDrawing.Body, bold: false, width: 185);
        PageDrawing.PutLabel(canvas, "Consistency", 412.0, 260.3);
        PageDrawing.PutText(canvas, report.ResultLines.FirstOrDefault(l => l.Label == "Consistency")?.Value, 603.3, 260.0, PageDrawing.Body, bold: false, width: 158);

        // The Result text grows the box when it is long, and everything below moves down with it.
        PageDrawing.PutLabel(canvas, "Result", 54.7, 281.7);
        var result = report.ResultTexts.FirstOrDefault(t => t.Label == "Result")?.Text;
        var resultText = Wrapped(result, 54.7, 287.9, 700);
        canvas.Children.Add(resultText);
        var boxBottom = Math.Max(371.5, Canvas.GetTop(resultText) + resultText.DesiredSize.Height + 6);

        PageDrawing.PutRect(canvas, Left, 246.5, Right - Left, boxBottom - 246.5, null, Brushes.Black, 1.33);
        PageDrawing.PutRule(canvas, 51.7, 264.9, 711.0);
        foreach (var x in new[] { 214.5, 407.0, 598.5 })
            PageDrawing.PutVerticalRule(canvas, x, 246.5, 18.7);

        var shift = boxBottom - 371.5;
        PageDrawing.PutLabel(canvas, "Remarks", 52.0, 395.7 + shift);
        var remarks = report.ResultTexts.FirstOrDefault(t => t.Label == "Remarks")?.Text;
        var remarksText = Wrapped(remarks, 54.7, 404.2 + shift, 700);
        canvas.Children.Add(remarksText);
        var remarksBottom = Math.Max(443.3 + shift, Canvas.GetTop(remarksText) + remarksText.DesiredSize.Height + 6);
        PageDrawing.PutRect(canvas, 51.7, 401.2 + shift, 712.0, remarksBottom - (401.2 + shift), null, Brushes.Black, 1.4);

        PageDrawing.PutFooter(canvas, report, remarksBottom);
    }

    private static TextBlock Wrapped(string? text, double x, double top, double width)
    {
        var block = PageDrawing.Text(text, PageDrawing.Body, bold: false, width: width);
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, top);
        return block;
    }
}
