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
            case ReportLayout.Urinalysis:
                UrinalysisLayout.Draw(canvas, report);
                break;
            case ReportLayout.Hematology:
                HematologyLayout.Draw(canvas, report);
                break;
            case ReportLayout.TestResult:
                TestResultLayout.Draw(canvas, report);
                break;
            case ReportLayout.ClinicalChemistry:
                ClinicalChemistryLayout.Draw(canvas, report);
                break;
            case ReportLayout.ClinicalChemistry2:
                ClinicalChemistry2Layout.Draw(canvas, report);
                break;
            case ReportLayout.MedicalExamination:
                MedicalExaminationLayout.Draw(canvas, report);
                break;
            case ReportLayout.AnnualPhysicalExam:
                AnnualPhysicalExamLayout.Draw(canvas, report);
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

/// <summary>How big a page design sets its text and how heavy its lines are (the forms were printed from different report designs).</summary>
internal readonly record struct PageStyle(double Body, double Company, double Line)
{
    public static readonly PageStyle Standard = new(13, 18, 1.33);

    /// <summary>The Serology and Immunology forms come from a smaller, finer report design.</summary>
    public static readonly PageStyle Fine = new(11.7, 16.3, 1.0);
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
    public static void PutLabel(Canvas canvas, string label, double x, double baseline, double size = Body)
    {
        var text = PutText(canvas, label, x, baseline, size, bold: true);
        PutText(canvas, ":", x + text.DesiredSize.Width, baseline, size, bold: true);
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
    public static void PutHeader(Canvas canvas, ReportLetterhead letterhead, PageStyle? style = null)
    {
        var s = style ?? PageStyle.Standard;
        if (ImageLoader.FromBytes(letterhead.Logo) is { } logo)
        {
            var image = new Image { Source = logo, Width = 92, Height = 100, Stretch = Stretch.Uniform };
            Canvas.SetLeft(image, 52.7);
            Canvas.SetTop(image, 32);
            canvas.Children.Add(image);
        }

        PutText(canvas, letterhead.CompanyName, 150.6, 45.6, s.Company, bold: true);
        PutText(canvas, letterhead.SubCompanyName, 150.6, 68.0, s.Body, bold: false);
        PutText(canvas, letterhead.Address, 150.6, 86.0, s.Body, bold: false);
        PutText(canvas, letterhead.ContactNumbers, 150.6, 103.6, s.Body, bold: false);
        PutText(canvas, letterhead.Email, 150.6, 121.2, s.Body, bold: false);

        // The box at the top right is for a photo; it is printed empty, as on the old forms.
        PutRect(canvas, 640.7, 30, 112.6, 109.7, null, Brushes.Black, s.Line);
    }

    /// <summary>The yellow title bar with the form's name centred in it.</summary>
    public static void PutTitleBar(Canvas canvas, string title, PageStyle? style = null)
    {
        var s = style ?? PageStyle.Standard;
        PutRect(canvas, 50.3, 214.9, 712.4, 21.4, new SolidColorBrush(Color.FromRgb(225, 225, 0)), Brushes.Black, 1.4);
        PutText(canvas, title, 50.3, 230.0, s.Body, bold: true, width: 712.4, alignment: TextAlignment.Center);
    }

    /// <summary>Where the values of the patient block start. The labels are the same on every form; the printed forms each start the values a few pixels apart.</summary>
    public readonly record struct PatientValues(double Left, double Age, double Sex, double Date)
    {
        public static readonly PatientValues Standard = new(185.3, 516.0, 692.0, 564.0);
    }

    /// <summary>The patient block: three rows, the first two split into a left and a right half, exactly as on the old forms.</summary>
    public static void PutPatientBlock(Canvas canvas, PrintableReport report, PatientValues? values = null, PageStyle? style = null)
    {
        var v = values ?? PatientValues.Standard;
        var size = (style ?? PageStyle.Standard).Body;
        PutLabel(canvas, "Patient Code", 52.7, 161.7, size);
        PutText(canvas, Line(report, "Patient Code"), v.Left, 161.3, size, bold: false);
        PutLabel(canvas, "Patient Name", 52.7, 182.3, size);
        PutText(canvas, Line(report, "Patient Name"), v.Left, 182.0, size, bold: false);
        PutLabel(canvas, "Company/Physician", 52.7, 204.3, size);
        PutText(canvas, Line(report, "Company/Physician"), v.Left, 204.0, size, bold: false);

        PutLabel(canvas, "Age", 453.3, 161.7, size);
        PutText(canvas, Line(report, "Age"), v.Age, 161.3, size, bold: false);
        PutLabel(canvas, "Sex", 629.3, 161.7, size);
        PutText(canvas, Line(report, "Sex"), v.Sex, 161.3, size, bold: false);
        PutLabel(canvas, "Date Requested", 453.3, 182.3, size);
        PutText(canvas, Line(report, "Date Requested"), v.Date, 182.0, size, bold: false);
    }

    /// <summary>The "computer generated" note and the two signatories, laid out below <paramref name="top"/> (the bottom of the last box).</summary>
    public static void PutFooter(Canvas canvas, PrintableReport report, double top, PageStyle? style = null)
    {
        var s = style ?? PageStyle.Standard;
        var note = top + 21.0;
        PutText(canvas, report.FooterNote, 50.3, note, s.Body, bold: false, width: 712.4, alignment: TextAlignment.Center);

        double[] left = [89.0, 448.7];
        for (var i = 0; i < report.Signatories.Count && i < left.Length; i++)
        {
            const double width = 284.9;
            PutText(canvas, report.Signatories[i].Name, left[i], note + 27.7, s.Body, bold: true, width: width, alignment: TextAlignment.Center);
            PutRule(canvas, left[i], note + 33.4, width, s.Line);
            PutText(canvas, report.Signatories[i].Role, left[i], note + 47.9, s.Body, bold: false, width: width, alignment: TextAlignment.Center);
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

/// <summary>
/// Urinalysis. The header, patient block, title bar and footer are the same as on every form; the body is a table of eight rows in
/// two columns (the right column has six), then an "Others" box and the Remarks box. Positions are measured from the client's printed form.
/// </summary>
internal static class UrinalysisLayout
{
    private const double Left = 51.3;
    private const double Right = 763.3;
    private const double Top = 246.5;

    private static readonly string[] LeftLabels = ["Color", "Appearance", "Reaction", "SP. Gravity", "Albumin", "Sugar", "Pus Cells", "Red Cells"];
    private static readonly string[] RightLabels = ["Mucus Threads", "Epithelial Cells", "Amorphous Urates / PO4", "Bacteria", "Casts", "Crystals"];

    // The rows of the printed form are not evenly spaced, so each rule and baseline is placed as measured.
    private static readonly double[] Rules = [264.9, 286.0, 306.0, 326.0, 345.0, 364.7, 383.3, 403.3];
    private static readonly double[] LabelBaselines = [260.3, 281.0, 301.7, 321.0, 341.0, 360.3, 379.7, 399.0];
    private static readonly double[] LeftValueBaselines = [260.6, 280.5, 301.2, 320.5, 340.5, 359.8, 379.2, 398.5];
    private static readonly double[] RightValueBaselines = [260.0, 280.6, 301.3, 320.6, 340.6, 360.0];
    private const double TableBottom = 404.0;

    private static readonly PageDrawing.PatientValues PatientValues = new(188.7, 516.7, 694.0, 564.7);

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        PageDrawing.PutHeader(canvas, report.Letterhead);
        PageDrawing.PutTitleBar(canvas, report.Title.ToUpperInvariant());
        PageDrawing.PutPatientBlock(canvas, report, PatientValues);

        string? ValueOf(string label) => report.ResultLines.FirstOrDefault(l => l.Label == label)?.Value;

        for (var row = 0; row < LeftLabels.Length; row++)
        {
            PageDrawing.PutLabel(canvas, LeftLabels[row], 54.7, LabelBaselines[row]);
            PageDrawing.PutText(canvas, ValueOf(LeftLabels[row]), 220.0, LeftValueBaselines[row], PageDrawing.Body, bold: false, width: 185);

            if (row < RightLabels.Length)
            {
                PageDrawing.PutLabel(canvas, RightLabels[row], 412.0, LabelBaselines[row]);
                PageDrawing.PutText(canvas, ValueOf(RightLabels[row]), 603.3, RightValueBaselines[row], PageDrawing.Body, bold: false, width: 158);
            }
        }

        // "Others": the label and its text share the first line, as on the old form; a long text grows the box.
        PageDrawing.PutLabel(canvas, "Others", 54.7, 419.3);
        var others = PageDrawing.Text(report.ResultTexts.FirstOrDefault(t => t.Label == "Others")?.Text, PageDrawing.Body, bold: false, width: 578);
        var othersTop = 419.0 - others.BaselineOffset;
        Canvas.SetLeft(others, 183.0);
        Canvas.SetTop(others, othersTop);
        canvas.Children.Add(others);
        var othersBottom = Math.Max(452.3, othersTop + others.DesiredSize.Height + 6);

        PageDrawing.PutRect(canvas, Left, Top, Right - Left, othersBottom - Top, null, Brushes.Black, 1.33);
        foreach (var y in Rules)
            PageDrawing.PutRule(canvas, 51.7, y, 711.0);

        foreach (var x in new[] { 214.5, 407.0, 598.5 })
            PageDrawing.PutVerticalRule(canvas, x, Top, TableBottom - Top);

        // Remarks
        PageDrawing.PutLabel(canvas, "Remarks", 52.0, othersBottom + 15.4);
        var remarksTop = othersBottom + 20.9;
        var remarks = PageDrawing.Text(report.ResultTexts.FirstOrDefault(t => t.Label == "Remarks")?.Text, PageDrawing.Body, bold: false, width: 700);
        Canvas.SetLeft(remarks, 54.5);
        Canvas.SetTop(remarks, remarksTop + 4.1);
        canvas.Children.Add(remarks);
        var remarksBottom = Math.Max(remarksTop + 42.1, remarksTop + 4.1 + remarks.DesiredSize.Height + 6);
        PageDrawing.PutRect(canvas, 51.7, remarksTop, 712.0, remarksBottom - remarksTop, null, Brushes.Black, 1.33);

        PageDrawing.PutFooter(canvas, report, remarksBottom);
    }
}
/// <summary>
/// Serology and Immunology share one page design: a box whose first row names the test, then the Result text, then the Remarks box.
/// It comes from a finer report design than the other forms (smaller type, 1 pixel lines). Positions are measured from the printed form.
/// </summary>
internal static class TestResultLayout
{
    private static readonly PageStyle Style = PageStyle.Fine;
    private static readonly PageDrawing.PatientValues PatientValues = new(188.7, 516.0, 692.7, 566.0);

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        PageDrawing.PutHeader(canvas, report.Letterhead, Style);
        PageDrawing.PutTitleBar(canvas, report.Title, Style);
        PageDrawing.PutPatientBlock(canvas, report, PatientValues, Style);

        var testLine = report.ResultLines.FirstOrDefault(l => l.Label == "Test");
        var test = testLine?.Value;
        var size = Style.Body;

        // Without a test row (the pregnancy test) the Result starts in the first row and the box is one row shorter.
        var up = testLine is null ? 19.0 : 0.0;

        // The Result text grows the box when it is long, and everything below it moves down.
        var result = PageDrawing.Text(report.ResultTexts.FirstOrDefault(t => t.Label == "Result")?.Text, size, bold: false, width: 700);
        var resultTop = 299.9 - up - result.BaselineOffset;
        Canvas.SetLeft(result, 54.7);
        Canvas.SetTop(result, resultTop);
        canvas.Children.Add(result);
        var boxBottom = Math.Max(371.5 - up, resultTop + result.DesiredSize.Height + 6);

        PageDrawing.PutRect(canvas, 51.3, 246.5, 712.0, boxBottom - 246.5, null, Brushes.Black, Style.Line);
        if (testLine is not null)
        {
            PageDrawing.PutRule(canvas, 51.7, 265.0, 711.0, Style.Line);
            PageDrawing.PutText(canvas, test, 47.0, 260.6, size, bold: false, width: 712.6, alignment: TextAlignment.Center);
        }

        PageDrawing.PutLabel(canvas, "Result", 54.7, 281.7 - up, size);

        var shift = boxBottom - 371.5;
        PageDrawing.PutLabel(canvas, "Remarks", 52.0, 395.7 + shift, size);
        var remarksTop = 401.2 + shift;
        var remarks = PageDrawing.Text(report.ResultTexts.FirstOrDefault(t => t.Label == "Remarks")?.Text, size, bold: false, width: 700);
        var remarksTextTop = remarksTop + 15.4 - remarks.BaselineOffset;
        Canvas.SetLeft(remarks, 55.3);
        Canvas.SetTop(remarks, remarksTextTop);
        canvas.Children.Add(remarks);
        var remarksBottom = Math.Max(remarksTop + 42.1, remarksTextTop + remarks.DesiredSize.Height + 6);
        PageDrawing.PutRect(canvas, 51.7, remarksTop, 712.0, remarksBottom - remarksTop, null, Brushes.Black, Style.Line);

        PageDrawing.PutFooter(canvas, report, remarksBottom, Style);
    }
}
/// <summary>
/// Clinical Chemistry: a table of nine tests (test, normal values, result; the last two centred) in the finer report design, with no Remarks.
/// Positions are measured from the client's printed form; the lines are not evenly spaced there, so each is placed on its own.
/// </summary>
internal static class ClinicalChemistryLayout
{
    private static readonly PageStyle Style = PageStyle.Fine;
    private static readonly PageDrawing.PatientValues PatientValues = new(186.5, 513.5, 685.5, 564.0);

    private static readonly double[] Rules = [265.5, 286.7, 306.7, 326.7, 345.7, 365.3, 384.0, 404.0, 423.0];
    private static readonly double[] LabelBaselines = [281.0, 301.7, 321.0, 341.0, 360.3, 379.7, 399.3, 419.3, 438.5];
    private static readonly double[] ValueBaselines = [280.6, 301.3, 320.6, 340.6, 359.9, 379.3, 398.9, 418.9, 438.1];

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        PageDrawing.PutHeader(canvas, report.Letterhead, Style);
        PageDrawing.PutTitleBar(canvas, report.Title.ToUpperInvariant(), Style);
        PageDrawing.PutPatientBlock(canvas, report, PatientValues, Style);

        const double top = 246.5, bottom = 444.3;
        PageDrawing.PutRect(canvas, 51.3, top, 712.0, bottom - top, null, Brushes.Black, Style.Line);
        foreach (var y in Rules)
            PageDrawing.PutRule(canvas, 51.7, y, 711.0, Style.Line);

        foreach (var x in new[] { 261.9, 531.2 })
            PageDrawing.PutVerticalRule(canvas, x, top, bottom - top, Style.Line);

        const double normalLeft = 262.9, normalWidth = 268.3, resultLeft = 532.2, resultWidth = 231.1;
        var size = Style.Body;
        PageDrawing.PutText(canvas, "Test", 54.7, 260.3, size, bold: true);
        PageDrawing.PutText(canvas, "Normal Values", normalLeft, 260.3, size, bold: true, width: normalWidth, alignment: TextAlignment.Center);
        PageDrawing.PutText(canvas, "Result", resultLeft, 260.3, size, bold: true, width: resultWidth, alignment: TextAlignment.Center);

        var fields = report.Fields ?? new Dictionary<string, string?>();
        for (var i = 0; i < ClinicalChemistryService.Tests.Length; i++)
        {
            var (name, label) = ClinicalChemistryService.Tests[i];
            PageDrawing.PutText(canvas, label, 54.7, LabelBaselines[i], size, bold: true);

            fields.TryGetValue(name + "NormalValue", out var normal);
            fields.TryGetValue(name + "Result", out var result);
            PageDrawing.PutText(canvas, normal, normalLeft, ValueBaselines[i], size, bold: false, width: normalWidth, alignment: TextAlignment.Center);
            PageDrawing.PutText(canvas, result, resultLeft, ValueBaselines[i], size, bold: false, width: resultWidth, alignment: TextAlignment.Center);
        }

        PageDrawing.PutFooter(canvas, report, bottom + 1.5, Style);
    }
}
/// <summary>
/// Clinical Chemistry 2: alkaline phosphatase and SGOT, each with normal values, unit and results in conventional and in system units.
/// The client's printed form is titled "CLINICAL CHEMISTRY" (without the 2), so that is what is printed. Positions are measured from it.
/// </summary>
internal static class ClinicalChemistry2Layout
{
    private static readonly PageStyle Style = PageStyle.Fine;
    private static readonly PageDrawing.PatientValues PatientValues = new(186.5, 513.5, 685.5, 564.0);

    // The left edge of the text in each of the six cells (normal value, unit, results; conventional, then system units).
    private static readonly double[] CellX = [203.5, 301.7, 360.9, 485.9, 584.0, 643.2];
    private static readonly string[] Suffix = ["CNValue", "CUnit", "CResults", "SNValue", "SUnit", "SResults"];

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        PageDrawing.PutHeader(canvas, report.Letterhead, Style);
        PageDrawing.PutTitleBar(canvas, "CLINICAL CHEMISTRY", Style);
        PageDrawing.PutPatientBlock(canvas, report, PatientValues, Style);

        const double top = 246.5, bottom = 329.9;
        var line = Style.Line;
        PageDrawing.PutRect(canvas, 51.3, top, 712.0, bottom - top, null, Brushes.Black, line);

        // Rules: under "Conventional" / "System Unit", under the column names, between the two tests.
        PageDrawing.PutRule(canvas, 199.2, 265.5, 564.0, line);
        PageDrawing.PutRule(canvas, 51.7, 285.9, 711.0, line);
        PageDrawing.PutRule(canvas, 51.7, 308.7, 711.0, line);
        PageDrawing.PutVerticalRule(canvas, 198.9, top, bottom - top, line);
        PageDrawing.PutVerticalRule(canvas, 480.7, top, bottom - top, line);
        foreach (var x in new[] { 298.0, 357.2, 581.7, 639.5 })
            PageDrawing.PutVerticalRule(canvas, x, 265.7, bottom - 265.7, line);

        var size = Style.Body;
        PageDrawing.PutText(canvas, "Test", 51.3, 270.4, size, bold: true, width: 147.6, alignment: TextAlignment.Center);
        PageDrawing.PutText(canvas, "Conventional", 198.9, 260.3, size, bold: true, width: 281.8, alignment: TextAlignment.Center);
        PageDrawing.PutText(canvas, "System Unit", 480.7, 260.3, size, bold: true, width: 282.6, alignment: TextAlignment.Center);

        string[] names = ["Normal Values", "Unit", "Results", "Normal Values", "Unit", "Results"];
        for (var i = 0; i < names.Length; i++)
            PageDrawing.PutText(canvas, names[i], CellX[i], 281.5, size, bold: true);

        var fields = report.Fields ?? new Dictionary<string, string?>();
        (string Name, string Label, double LabelBaseline, double ValueBaseline)[] tests =
        [
            ("AlkalinePhosphatase", "Alkaline Phosphatase", 302.0, 301.9),
            ("SGOT", "SGOT", 324.7, 324.3),
        ];
        foreach (var (name, label, labelBaseline, valueBaseline) in tests)
        {
            PageDrawing.PutText(canvas, label, 54.3, labelBaseline, size, bold: true);
            for (var i = 0; i < CellX.Length; i++)
            {
                fields.TryGetValue(name + Suffix[i], out var value);
                var width = i switch { 0 or 3 => 94.0, 1 or 4 => 56.0, _ => 120.0 };
                PageDrawing.PutText(canvas, value, CellX[i], valueBaseline, size, bold: false, width: width);
            }
        }

        PageDrawing.PutFooter(canvas, report, bottom, Style);
    }
}
/// <summary>
/// The Medical Examination Report, printed as Annual Physical Exam Page 2. Like the first APE page it has a centred letterhead and its own
/// person box, and no "computer generated" line; the laboratory/X-ray table and the classification sit side by side, then three text boxes
/// and the physician's signature block. Every position is measured from the client's printed form (one Letter page, Crystal Reports).
/// A long text grows its box and moves everything below it down.
/// </summary>
internal static class MedicalExaminationLayout
{
    private static readonly FontFamily Calibri = new("Calibri");
    private static readonly FontFamily Arial = new("Arial");
    private static readonly FontFamily Wingdings = new("Wingdings");

    private const double Center = 425.7; // the letterhead and the title are centred here
    private static Dictionary<string, string?> _v = [];

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        _v = new Dictionary<string, string?>(report.Fields ?? new Dictionary<string, string?>());

        // ---- letterhead and title -------------------------------------------------------------------
        Centered(canvas, report.Letterhead.CompanyName, 42.9, 14, Arial, bold: true);
        Centered(canvas, report.Letterhead.Address, 59.1, 11.9, Arial, bold: false);
        Centered(canvas, report.Letterhead.ContactNumbers, 76.6, 11.9, Arial, bold: false);
        Centered(canvas, report.Letterhead.Email, 92.9, 11.9, Arial, bold: false);
        Centered(canvas, "MEDICAL EXAMINATION REPORT", 115.7, 12, Calibri, bold: true);

        Text(canvas, "Date:", 681.0, 135.9);
        Text(canvas, Value("Date"), 712.0, 135.9, 11.9, Arial, width: 63);
        Line(canvas, 712.0, 139.9, 775.7);

        // ---- person box ---------------------------------------------------------------------------------
        Box(canvas, 31.5, 142.7, 775.7, 184.7);
        Line(canvas, 31.0, 163.7, 776.5);
        Vertical(canvas, 395.9, 142.5, 184.7);
        Vertical(canvas, 145.5, 164.2, 185.0);
        Vertical(canvas, 255.3, 163.9, 184.7);

        Label(canvas, "Name", 36.2, 157.5, 53.5);
        Text(canvas, Value("Name"), 78.4, 157.5, 11.9, Arial, width: 312);
        Label(canvas, "Contact No.", 398.7, 157.5, 448.3);
        Text(canvas, Value("ContactNo"), 471.9, 157.5, 11.9, Arial, width: 300);
        Label(canvas, "Age", 36.7, 178.3, 53.5);
        Text(canvas, Value("Age"), 60.0, 178.3, 11.9, Arial, width: 84);
        Label(canvas, "Gender", 148.5, 178.3, 184.5);
        Text(canvas, Value("Sex"), 191.0, 178.3, 11.9, Arial, width: 62);
        Label(canvas, "Civil Status", 257.7, 178.3, 305.8);
        Text(canvas, Value("CivilStatus"), 313.0, 178.3, 11.9, Arial, width: 80);
        Label(canvas, "Company Name", 400.7, 178.3, 471.4);
        Text(canvas, Value("CompanyName"), 478.0, 178.3, 11.9, Arial, width: 296);

        // ---- laboratory / X-ray results (left) and classification (right) -----------------------------------
        Text(canvas, "V. LABORATORY / X-RAY RESULTS:", 32.5, 200.9);
        Text(canvas, "VI. CLASSIFICATION:", 493.3, 200.9, 12);

        Box(canvas, 33.7, 208.3, 479.8, 404.7);
        foreach (var y in new[] { 229.0, 251.0, 273.9, 296.3, 319.0, 363.0 })
            Line(canvas, 33.0, y, 479.0);

        foreach (var x in new[] { 159.7, 266.3, 373.3 })
            Vertical(canvas, x, 208.5, 405.0);

        (string Label, string? Label2, string Key, string N, string F, double LabelBaseline, double CheckBaseline)[] rows =
        [
            ("Chest X-Ray", null, "ChestXray", "Normal", "With Findings", 222.3, 222.3),
            ("CBC", null, "CBC", "Normal", "With Findings", 245.0, 245.0),
            ("Urinalysis", null, "Urinalysis", "Normal", "With Findings", 267.3, 267.3),
            ("Fecalysis", null, "Fecalysis", "Normal", "With Findings", 290.3, 290.9),
            ("HBsAg", null, "HBsAg", "Non-Reactive", "Reactive", 312.3, 312.3),
            ("Drug Test: METH/THC", "(2 Panel)", "DrugTest2Panel", "Negative", "Positive", 335.0, 335.0),
            ("Drug Test: COC/PCP", "(4 Panel) OPI, AMP", "DrugTest4Panel", "Negative", "Positive", 378.0, 378.6),
        ];
        foreach (var (label, label2, key, n, f, labelBaseline, checkBaseline) in rows)
        {
            Text(canvas, label, 36.7, labelBaseline);
            if (label2 is not null)
                Text(canvas, label2, 36.7, labelBaseline + 16.2);

            var state = Value(key);
            Tick(canvas, 164.2, checkBaseline, state == "N");
            Text(canvas, n, 184.3, labelBaseline);
            Tick(canvas, 272.3, checkBaseline, state == "F");
            Text(canvas, f, 293.7, labelBaseline);
            Text(canvas, Value(key + "Remarks"), 377.0, labelBaseline, 11.9, Arial, width: 100);
        }

        Box(canvas, 491.3, 208.3, 774.7, 404.7);
        foreach (var y in new[] { 249.3, 288.0, 324.7, 365.3 })
            Line(canvas, 491.0, y, 774.5);

        (string Code, string Line1, string? Line2, double Indent, double Baseline)[] classes =
        [
            ("Fit", "A - Fit for employment.", null, 0, 226.0),
            ("Fit2", "B - Fit for employment;", "with minor ailment/defect.", 534.0, 264.7),
            ("Acceptable", "C - Acceptable employment;", "defect may not be easily corrected.", 532.0, 302.0),
            ("Unfit", "D - Unfit for employment.", null, 0, 340.7),
            ("Pending", "PENDING - Incomplete medical laboratory", "exam.", 520.0, 380.0),
        ];
        foreach (var (code, line1, line2, indent, baseline) in classes)
        {
            Tick(canvas, 496.7, baseline, Value("Classification") == code);
            Text(canvas, line1, 520.0, baseline, 12);
            if (line2 is not null)
                Text(canvas, line2, indent, baseline + 16.2, 12);
        }

        // ---- the three text boxes (each grows with its text) -------------------------------------------------
        var shift = 0.0;
        shift += TextBox(canvas, "Medical/Surgical History:", report, "Medical/Surgical History", 430.2, 488.9, 39.2, shift);
        shift += TextBox(canvas, "Assessment:", report, "Assessment", 511.3, 627.3, 38.7, shift);
        shift += TextBox(canvas, "Remarks:", report, "Remarks", 649.7, 708.3, 37.0, shift);

        // ---- who did the assessment, and the physician -------------------------------------------------------
        var foot = 708.3 + shift + 21.4;
        Text(canvas, "Assessment Done By:", 42.7, foot);
        Text(canvas, Value("AssessmentDoneBy"), 162.1, foot, width: 330);
        Text(canvas, "Noted By:", 511.2, foot);

        const double signLeft = 511.2, signWidth = 262.0;
        TextCentered(canvas, Value("PhysicianName"), signLeft, signWidth, foot + 33.5);
        TextCentered(canvas, Value("PhysicianLicense"), signLeft, signWidth, foot + 52.0);
        Line(canvas, signLeft, foot + 58.2, signLeft + signWidth);
        TextCentered(canvas, "Physician in Charge", signLeft, signWidth, foot + 72.8);
    }

    private static string? Value(string name) => _v.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// One of the text boxes: the label above it and the box with its text. The box is where it is measured, moved down by what the boxes
    /// above it have grown; it grows when its text is long. Returns by how much it grew.
    /// </summary>
    private static double TextBox(Canvas canvas, string label, PrintableReport report, string key, double top, double bottom, double textX, double shift)
    {
        var y = top + shift;
        Text(canvas, label, 35.7, y - 7.5);

        var text = report.ResultTexts.FirstOrDefault(t => t.Label == key)?.Text;
        var block = Block(text, 11.9, Arial, bold: false, italic: false, width: 728);
        var textTop = y + 16.7 - block.BaselineOffset;
        Canvas.SetLeft(block, textX);
        Canvas.SetTop(block, textTop);
        canvas.Children.Add(block);

        var measured = bottom + shift;
        var end = Math.Max(measured, textTop + block.DesiredSize.Height + 8);
        Box(canvas, 33.7, y, 774.7, end);
        return end - measured;
    }
    private static TextBlock Block(string? value, double size, FontFamily family, bool bold, bool italic, double? width)
    {
        var block = new TextBlock
        {
            Text = value ?? string.Empty,
            FontFamily = family,
            FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
            TextWrapping = width is null ? TextWrapping.NoWrap : TextWrapping.Wrap,
            Foreground = Brushes.Black,
        };
        if (width is { } w)
            block.Width = w;

        block.Measure(new Size(width ?? double.PositiveInfinity, double.PositiveInfinity));
        return block;
    }

    private static void Text(Canvas canvas, string? value, double x, double baseline, double size = 10.9, FontFamily? family = null, double? width = null)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var block = Block(value, size, family ?? Calibri, bold: false, italic: false, width);
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, baseline - block.BaselineOffset);
        canvas.Children.Add(block);
    }

    private static void TextCentered(Canvas canvas, string? value, double left, double width, double baseline)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var block = Block(value, 10.9, Calibri, bold: false, italic: false, width: width);
        block.TextAlignment = TextAlignment.Center;
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, baseline - block.BaselineOffset);
        canvas.Children.Add(block);
    }

    private static void Centered(Canvas canvas, string? value, double baseline, double size, FontFamily family, bool bold)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var block = Block(value, size, family, bold, italic: false, width: 560);
        block.TextAlignment = TextAlignment.Center;
        Canvas.SetLeft(block, Center - 280);
        Canvas.SetTop(block, baseline - block.BaselineOffset);
        canvas.Children.Add(block);
    }

    /// <summary>A label with its colon drawn separately, where the form puts it.</summary>
    private static void Label(Canvas canvas, string label, double x, double baseline, double colonX)
    {
        Text(canvas, label, x, baseline);
        Text(canvas, ":", colonX, baseline);
    }

    /// <summary>The form's "[ ]" with a check mark inside when ticked.</summary>
    private static void Tick(Canvas canvas, double x, double baseline, bool ticked)
    {
        Text(canvas, "[", x, baseline, 12);
        Text(canvas, "]", x + 13.3, baseline, 12);
        if (!ticked)
            return;

        var mark = Block("ü", 12, Wingdings, bold: false, italic: false, width: null);
        Canvas.SetLeft(mark, x + 3.9);
        Canvas.SetTop(mark, baseline - 0.6 - mark.BaselineOffset);
        canvas.Children.Add(mark);
    }

    private static void Line(Canvas canvas, double x1, double y, double x2) =>
        PageDrawing.PutRect(canvas, x1, y - 0.5, x2 - x1, 1, Brushes.Black, null, 0);

    private static void Vertical(Canvas canvas, double x, double y1, double y2) =>
        PageDrawing.PutRect(canvas, x - 0.5, y1, 1, y2 - y1, Brushes.Black, null, 0);

    private static void Box(Canvas canvas, double x1, double y1, double x2, double y2) =>
        PageDrawing.PutRect(canvas, x1 - 0.5, y1 - 0.5, x2 - x1 + 1, y2 - y1 + 1, null, Brushes.Black, 1);
}
/// <summary>
/// Hematology: a table of ten lines in three columns (test, normal values, result; the last two are centred), then the Remarks box.
/// Positions are measured from the client's printed form; the lines are not evenly spaced there, so each is placed on its own.
/// </summary>
internal static class HematologyLayout
{
    private const double Left = 51.3;
    private const double Right = 763.3;
    private const double Top = 246.5;
    private const double Bottom = 463.0;

    // Where each row's rule is drawn, and where its text sits (from the printed form).
    private static readonly double[] Rules = [265.0, 286.0, 306.0, 326.0, 345.0, 364.7, 383.3, 403.3, 422.3, 442.3];
    private static readonly double[] LabelBaselines = [281.0, 301.9, 321.3, 341.0, 360.8, 379.7, 399.3, 419.3, 438.5, 457.9];
    private static readonly double[] ValueBaselines = [280.6, 301.5, 321.0, 340.6, 360.4, 379.3, 399.0, 419.0, 438.2, 457.5];
    private static readonly double[] LabelX = [54.7, 55.0, 54.5, 82.7, 82.5, 82.7, 82.5, 83.0, 82.7, 54.5];

    private static readonly PageDrawing.PatientValues PatientValues = new(191.5, 516.0, 688.5, 563.0);

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        PageDrawing.PutHeader(canvas, report.Letterhead);
        PageDrawing.PutTitleBar(canvas, report.Title.ToUpperInvariant());
        PageDrawing.PutPatientBlock(canvas, report, PatientValues);

        PageDrawing.PutRect(canvas, Left, Top, Right - Left, Bottom - Top, null, Brushes.Black, 1.33);
        foreach (var y in Rules)
            PageDrawing.PutRule(canvas, 51.7, y, 711.0);

        foreach (var x in new[] { 261.2, 530.5 })
            PageDrawing.PutVerticalRule(canvas, x, Top, Bottom - Top - 0.6);

        const double normalLeft = 262.5, normalWidth = 268.0, resultLeft = 531.9, resultWidth = 231.4;
        PageDrawing.PutText(canvas, "Complete Blood Count", 54.0, 260.9, PageDrawing.Body, bold: true);
        PageDrawing.PutText(canvas, "Normal Values", normalLeft, 260.3, PageDrawing.Body, bold: true, width: normalWidth, alignment: TextAlignment.Center);
        PageDrawing.PutText(canvas, "Result", resultLeft, 260.3, PageDrawing.Body, bold: true, width: resultWidth, alignment: TextAlignment.Center);

        var fields = report.Fields ?? new Dictionary<string, string?>();
        for (var i = 0; i < HematologyService.Tests.Length; i++)
        {
            var (name, label) = HematologyService.Tests[i];
            PageDrawing.PutText(canvas, label, LabelX[i], LabelBaselines[i], PageDrawing.Body, bold: true);

            fields.TryGetValue(name + "NormalValue", out var normal);
            fields.TryGetValue(name + "Result", out var result);
            PageDrawing.PutText(canvas, normal, normalLeft, ValueBaselines[i], PageDrawing.Body, bold: false, width: normalWidth, alignment: TextAlignment.Center);
            PageDrawing.PutText(canvas, result, resultLeft, ValueBaselines[i], PageDrawing.Body, bold: false, width: resultWidth, alignment: TextAlignment.Center);
        }

        // Remarks: a long text grows the box and everything below it.
        PageDrawing.PutLabel(canvas, "Remarks", 51.5, 482.3);
        const double remarksTop = 487.9;
        var remarks = PageDrawing.Text(report.ResultTexts.FirstOrDefault(t => t.Label == "Remarks")?.Text, PageDrawing.Body, bold: false, width: 700);
        Canvas.SetLeft(remarks, 54.5);
        Canvas.SetTop(remarks, remarksTop + 4.1);
        canvas.Children.Add(remarks);
        var remarksBottom = Math.Max(remarksTop + 42.1, remarksTop + 4.1 + remarks.DesiredSize.Height + 6);
        PageDrawing.PutRect(canvas, 51.7, remarksTop, 712.0, remarksBottom - remarksTop, null, Brushes.Black, 1.33);

        PageDrawing.PutFooter(canvas, report, remarksBottom);
    }
}
/// <summary>
/// The annual physical exam ("Medical Examination Report"). Unlike the other forms it has a centred letterhead, no photo box or title bar,
/// and is built from boxed sections in Calibri. Every position, line and size is measured from the client's printed form (one Letter page).
/// A long text grows its box and moves everything below it down.
/// </summary>
internal static class AnnualPhysicalExamLayout
{
    private static readonly FontFamily Calibri = new("Calibri");
    private static readonly FontFamily Arial = new("Arial");
    private static readonly FontFamily Wingdings = new("Wingdings");

    private const double Size = 13;
    private const double Small = 10;
    private const string Check = "ü"; // a check mark in Wingdings

    private static Dictionary<string, string?> _v = [];

    public static void Draw(Canvas canvas, PrintableReport report)
    {
        _v = new Dictionary<string, string?>(report.Fields ?? new Dictionary<string, string?>());

        Header(canvas, report.Letterhead);
        PersonBox(canvas);

        // ---- past medical history -------------------------------------------------------------------
        Label(canvas, "PAST MEDICAL HISTORY:", 31.7, 231.4);
        Fill(canvas, 30.5, 212.4, 782.5, 215.1);
        Fill(canvas, 30.2, 237.2, 782.5, 238.5);

        string[,] history =
        {
            { "EYES, EARS, NOSE, THROAT", "ENT", "GASTROENTEROLOGY", "Gastroenterology" },
            { "RESPIRATORY", "Respiratory", "INTEGUMENTARY/SKIN", "IntegumentarySkin" },
            { "CARDIOLOGY", "Cardiology", "PSYCHOLOGY", "Psychology" },
            { "ENDOCRINOLOGY", "Endocrinology", "OB-GYNE/UROLOGY", "OBGyneUrology" },
            { "MUSCULOSKELETAL", "Muscoloskeletal", "INFECTIOUS/COMMUNICABLE", "InfectiousCommunicable" },
            { "NEUROLOGY", "Neurological", "SURGICAL", "Surgical" },
        };
        double[] historyBaselines = [253.5, 271.2, 289.7, 308.4, 327.2, 345.5];
        for (var i = 0; i < history.GetLength(0); i++)
        {
            Text(canvas, history[i, 0], 33.5, historyBaselines[i]);
            Text(canvas, Value(history[i, 1]), 216.9, historyBaselines[i] - 0.3, width: 188);
            Text(canvas, history[i, 2], 411.5, historyBaselines[i]);
            Text(canvas, Value(history[i, 3]), 598.3, historyBaselines[i] - 0.3, width: 182);
        }

        foreach (var y in new[] { 256.1, 275.1, 293.5, 311.7, 329.7, 348.1 })
            Fill(canvas, 30.2, y, 782.5, y + 1.3);

        foreach (var x in new[] { 213.3, 408.0, 596.7 })
            Fill(canvas, x, 238.7, x + 1.4, 348.7);

        // Others (a text that can grow the box)
        Text(canvas, "OTHERS", 33.5, 363.1);
        var others = Wrapped(canvas, Value("OthersPast"), 79.3, 362.8, 700, Size);
        var s1 = Math.Max(0, others - 384.2 + 3);
        Fill(canvas, 30.2, 237.9, 31.5, 385.2 + s1);
        Fill(canvas, 781.5, 237.2, 782.9, 384.5 + s1);
        Fill(canvas, 30.2, 384.2 + s1, 782.5, 385.5 + s1);

        // ---- medications / allergies / review of systems ----------------------------------------------
        var top2 = 387.1 + s1;
        Fill(canvas, 30.3, top2, 782.3, top2 + 1.3);
        Text(canvas, "MEDICATIONS", 33.5, 402.2 + s1);
        Text(canvas, "ALLERGIES", 411.5, 402.2 + s1);
        var meds = Wrapped(canvas, Value("Medications"), 33.5, 418.4 + s1, 368, Size);
        var allergies = Wrapped(canvas, Value("Allergies"), 411.5, 418.4 + s1, 366, Size);
        var s2 = Math.Max(0, Math.Max(meds, allergies) - (421.4 + s1) + 3);

        var mid = 421.4 + s1 + s2;
        Fill(canvas, 30.9, mid, 782.0, mid + 1.3);
        Fill(canvas, 408.0, top2 + 0.4, 409.3, mid + 0.1);

        Text(canvas, "REVIEW OF SYSTEMS", 33.5, 437.3 + s1 + s2);
        var ros = Wrapped(canvas, Value("ReviewOfSystems"), 149.3, 437.0 + s1 + s2, 630, Size);
        var s3 = Math.Max(0, ros - (461.7 + s1 + s2) + 3);

        var bottom2 = 461.7 + s1 + s2 + s3;
        Fill(canvas, 30.2, bottom2, 782.3, bottom2 + 1.4);
        Fill(canvas, 30.2, top2 + 0.4, 31.5, bottom2 + 1.2);
        Fill(canvas, 781.5, top2 + 0.4, 782.9, bottom2 + 1.2);

        var S = s1 + s2 + s3;

        // ---- present medical history -------------------------------------------------------------------
        Fill(canvas, 30.2, 466.9 + S, 782.2, 469.5 + S);
        Label(canvas, "PRESENT MEDICAL HISTORY:", 31.7, 483.4 + S);
        Fill(canvas, 30.0, 488.5 + S, 781.8, 489.9 + S);
        Fill(canvas, 30.2, 489.5 + S, 31.5, 527.5 + S);
        Fill(canvas, 781.5, 489.4 + S, 782.9, 527.4 + S);
        Fill(canvas, 30.2, 506.7 + S, 782.0, 508.1 + S);
        Fill(canvas, 30.2, 526.4 + S, 782.0, 527.7 + S);

        Text(canvas, "Smoking:", 32.8, 504.2 + S);
        YesNo(canvas, Value("IsSmoking"), 504.2 + S, 505.9 + S);
        Text(canvas, "Since when:", 284.8, 504.2 + S);
        Text(canvas, Value("SmokingSinceWhen"), 352.0, 503.9 + S, width: 55);
        Text(canvas, "# of Sticks per day:", 413.5, 504.2 + S);
        Text(canvas, Value("NumberOfSticksPerDay"), 516.0, 503.9 + S, width: 60);

        Text(canvas, "Drinking (Alcoholic Beverages):", 32.8, 522.5 + S);
        YesNo(canvas, Value("IsDrinking"), 522.5 + S, 524.3 + S);
        Text(canvas, "Since when:", 284.8, 522.5 + S);
        Text(canvas, Value("DrinkingSinceWhen"), 352.0, 522.2 + S, width: 55);
        Text(canvas, "# of Bottles:", 413.5, 522.5 + S);
        Text(canvas, Value("NumberOfBottles"), 480.0, 522.2 + S, width: 80);
        Option(canvas, "Daily", 578.9, 522.5 + S, 524.3 + S, Value("DrinkingFrequency") == "Daily", 564.4);
        Option(canvas, "Weekly", 649.5, 522.5 + S, 524.3 + S, Value("DrinkingFrequency") == "Weekly", 635.0);
        Option(canvas, "Occasional", 721.5, 522.5 + S, 524.3 + S, Value("DrinkingFrequency") == "Occasional", 707.0);

        Text(canvas, "LMP (First day of last menstrual period):", 31.7, 541.7 + S);
        Text(canvas, Value("LMP"), 251.2, 541.4 + S, width: 138);
        Fill(canvas, 251.2, 545.5 + S, 392.2, 546.9 + S);
        Fill(canvas, 409.0, 545.5 + S, 425.0, 546.9 + S);
        Fill(canvas, 499.5, 545.5 + S, 515.5, 546.9 + S);
        Text(canvas, "Regular", 425.3, 541.7 + S);
        Text(canvas, "Irregular", 516.0, 541.7 + S);
        if (Value("LMPType") == "Regular")
            Mark(canvas, 410.5, 543.4 + S);
        else if (Value("LMPType") == "Irregular")
            Mark(canvas, 501.0, 543.4 + S);

        Fill(canvas, 30.2, 547.4 + S, 782.0, 548.7 + S);

        // ---- vital signs and visual acuity ---------------------------------------------------------------
        Fill(canvas, 30.2, 547.5 + S, 31.5, 630.2 + S);
        foreach (var x in new[] { 130.2, 345.5, 444.2, 781.5 })
            Fill(canvas, x, 547.5 + S, x + 1.4, 630.2 + S);

        Fill(canvas, 182.2, 548.2 + S, 183.5, 630.2 + S);
        Fill(canvas, 234.2, 548.2 + S, 235.5, 630.2 + S);
        Fill(canvas, 30.2, 565.1 + S, 782.0, 566.4 + S);
        Fill(canvas, 533.5, 565.7 + S, 534.9, 630.4 + S);
        Fill(canvas, 635.5, 565.7 + S, 636.9, 630.4 + S);
        Fill(canvas, 583.7, 566.2 + S, 585.0, 630.9 + S);
        Fill(canvas, 30.2, 587.4 + S, 346.7, 588.7 + S);
        Fill(canvas, 445.2, 587.4 + S, 637.0, 588.7 + S);
        Fill(canvas, 31.0, 628.7 + S, 782.0, 630.1 + S);

        Text(canvas, "Vital Signs", 32.2, 562.1 + S);
        Text(canvas, "1st", 134.9, 562.1 + S);
        Text(canvas, "2nd", 186.2, 562.1 + S);
        Text(canvas, "Measurements", 236.2, 562.1 + S);
        Text(canvas, "BMI Category", 347.5, 562.1 + S);
        Text(canvas, "Visual Acuity", 446.9, 562.1 + S);

        Text(canvas, "BP Reading (mm/Hg)", 32.2, 578.5 + S, Small);
        Text(canvas, Value("BP1st"), 134.9, 577.7 + S, Small, width: 45);
        Text(canvas, Value("BP2nd"), 185.3, 577.7 + S, Small, width: 45);
        Text(canvas, "Cardiac Rate (bpm)", 32.2, 601.8 + S, Small);
        Text(canvas, Value("CardiacRate1st"), 134.5, 600.9 + S, Small, width: 45);
        Text(canvas, Value("CardiacRate2nd"), 185.8, 601.1 + S, Small, width: 45);
        Text(canvas, "Height (feet):", 236.5, 578.5 + S, Small);
        Text(canvas, Value("Height"), 297.5, 577.7 + S, Small, width: 45);
        Text(canvas, "Weight (Kg):", 236.2, 601.8 + S, Small);
        Text(canvas, Value("Weight"), 298.5, 601.1 + S, Small, width: 45);
        Text(canvas, Value("BMICategory"), 348.8, 593.8 + S, Small, width: 93);

        Text(canvas, "Right Eye", 537.8, 577.1 + S, Small);
        Text(canvas, "Left Eye", 589.1, 577.1 + S, Small);
        Text(canvas, "Far w/ eyeglasses", 446.9, 601.7 + S, Small);
        Text(canvas, "Far w/o eyeglasses", 446.9, 620.4 + S, Small);
        Text(canvas, Value("VARightEyeWGlasses"), 538.5, 601.7 + S, Small, width: 44);
        Text(canvas, Value("VALeftEyeWGlasses"), 589.0, 601.7 + S, Small, width: 44);
        Text(canvas, Value("VARightEyeWOGlasses"), 538.5, 620.4 + S, Small, width: 44);
        Text(canvas, Value("VALeftEyeWOGlasses"), 589.0, 620.4 + S, Small, width: 44);

        Text(canvas, "Normal", 657.5, 578.7 + S, Small);
        Text(canvas, "EOR", 657.5, 594.7 + S, Small);
        Text(canvas, "Corrected with", 657.5, 610.0 + S, Small);
        Text(canvas, "eyeglasses/contact lenses", 657.5, 623.1 + S, Small);
        switch (Value("VisualAcuity"))
        {
            case "Normal":
                Mark(canvas, 639.5, 582.8 + S);
                break;
            case "EOR":
                Mark(canvas, 639.5, 598.8 + S);
                break;
            case "Corrected":
                Mark(canvas, 639.5, 614.1 + S);
                break;
        }

        // ---- physical examination -----------------------------------------------------------------------
        Label(canvas, "PHYSICAL EXAMINATION:", 31.7, 648.5 + S);
        Text(canvas, "N - Normal or None; F - With Findings, if with Findings, describe", 31.7, 666.5 + S);

        Fill(canvas, 30.2, 670.4 + S, 31.5, 785.2 + S);
        Fill(canvas, 30.2, 670.4 + S, 782.0, 671.7 + S);
        Fill(canvas, 781.5, 670.4 + S, 782.9, 785.2 + S);
        foreach (var x in new[] { 225.7, 251.5, 277.5, 481.5, 506.9, 533.5, 729.7, 755.7 })
            Fill(canvas, x, 671.1 + S, x + 1.4, 784.7 + S);

        foreach (var y in new[] { 689.1, 707.7, 727.1, 745.7, 764.4, 783.7 })
            Fill(canvas, 31.2, y + S, 781.9, y + 1.4 + S);

        double[] headerX = [234.1, 261.3, 490.1, 516.5, 738.1, 764.0];
        string[] headerText = ["N", "F", "N", "F", "N", "F"];
        for (var i = 0; i < headerX.Length; i++)
            Text(canvas, headerText[i], headerX[i], 685.7 + S);

        (string Label, string Field)[][] groups =
        [
            [("Skin", "Skin"), ("Head, Scalp", "HeadScalp"), ("Eyes", "Eyes"), ("Ears", "Ears"), ("Nose", "Nose")],
            [("Teeth, Tonsils, Throat, Pharynx", "TeethTonsilsThroatPharynx"), ("Neck, Lymph Nodes, Thyroid", "NeckLymphNodesThyroid"),
             ("Thorax, Breast", "ThoraxBreast"), ("Heart, Lungs", "HeartLungs"), ("Abdomen, Liver, Spleen", "AbdomenLiverSpleen")],
            [("Inguinal Area, Genitals, Anus", "InguinalAreaGenitalsAnus"), ("Extremities, Spine", "ExtremetiesSpine"), ("Tattoo", "Tattoo"),
             ("Mass, Cyst", "MassCyst"), ("Others", "OthersPE")],
        ];
        double[] labelX = [33.0, 281.0, 537.0];
        double[] nX = [232.0, 487.4, 736.1];
        double[] fX = [258.2, 514.0, 762.5];
        double[] rowText = [704.4, 723.5, 743.1, 761.1, 780.4];
        double[] rowMark = [706.2, 725.7, 744.7, 763.7, 782.7];
        for (var g = 0; g < groups.Length; g++)
        {
            for (var r = 0; r < groups[g].Length; r++)
            {
                Text(canvas, groups[g][r].Label, labelX[g], rowText[r] + S);
                var state = Value(groups[g][r].Field);
                if (state == "N")
                    Mark(canvas, nX[g], rowMark[r] + S);
                else if (state == "F")
                    Mark(canvas, fX[g], rowMark[r] + S);
            }
        }

        // ---- findings (a text that can grow the box) -------------------------------------------------------
        Text(canvas, "Findings:", 33.3, 801.9 + S);
        var findings = Wrapped(canvas, Value("Findings"), 86.5, 801.6 + S, 690, Size);
        var s4 = Math.Max(0, findings - (827.9 + S) + 6);
        PageDrawing.PutRect(canvas, 30.2, 787.2 + S, 752.7, 41.4 + s4, null, Brushes.Black, 1.33);
        var T = S + s4;

        // ---- consent, signatures, who took the measurements ----------------------------------------------------
        Italic(canvas, "I certify that the answers and statements I provided are all true and correct to the best of my knowledge, and I understand that ", 849.2 + T);
        Italic(canvas, "non-disclosure and/or misdeclaration of any of the above items from part of my application and/or may be use for any administrative ", 865.4 + T);
        Italic(canvas, "action, I also give my consent for a thorough physical examination.", 881.6 + T);
        Italic(canvas, $"I hereby authorized {report.Letterhead.CompanyName}, to share and transmit my medical record and results to our HR department/clinic.", 906.5 + T);

        Fill(canvas, 31.3, 942.5 + T, 346.0, 943.9 + T);
        Fill(canvas, 466.0, 945.2 + T, 780.7, 946.5 + T);
        Text(canvas, "Applicant’s/Employee’s Signature over Printed Name", 49.9, 957.9 + T);
        Text(canvas, "Physician", 599.0, 960.5 + T);

        Text(canvas, "Vital Signs Done By:", 31.3, 987.7 + T);
        Text(canvas, Value("VitalSignsBy"), 193.7, 987.4 + T, width: 159);
        Fill(canvas, 193.7, 990.0 + T, 352.9, 991.3 + T);
        Text(canvas, "Height and Weight Done By:", 31.3, 1006.7 + T);
        Text(canvas, Value("HeightWeightBy"), 193.7, 1006.4 + T, width: 159);
        Fill(canvas, 193.7, 1008.7 + T, 352.9, 1010.0 + T);
    }

    private static string? Value(string name) => _v.TryGetValue(name, out var value) ? value : null;

    private static void Header(Canvas canvas, ReportLetterhead letterhead)
    {
        Centered(canvas, letterhead.CompanyName, 42.7, 16, Arial, bold: true);
        Centered(canvas, letterhead.Address, 60.5, Size, Arial, bold: false);
        Centered(canvas, letterhead.ContactNumbers, 78.0, Size, Arial, bold: false);
        Centered(canvas, letterhead.Email, 94.3, Size, Arial, bold: false);
        Centered(canvas, "MEDICAL EXAMINATION REPORT", 123.4, 14, Calibri, bold: true);

        Text(canvas, "Date:", 686.0, 138.0);
        Text(canvas, Value("Date"), 717.0, 137.7, width: 64);
        Fill(canvas, 717.0, 142.7, 781.5, 144.1);
    }

    private static void PersonBox(Canvas canvas)
    {
        Fill(canvas, 30.5, 147.3, 782.5, 148.6);
        Fill(canvas, 30.2, 147.9, 31.5, 207.3);
        Fill(canvas, 345.9, 148.3, 347.2, 207.3);
        Fill(canvas, 548.2, 148.6, 549.5, 207.3);
        Fill(canvas, 781.5, 148.6, 782.9, 207.3);
        Fill(canvas, 30.5, 185.9, 782.5, 187.3);
        Fill(canvas, 103.9, 186.1, 105.2, 206.4);
        Fill(canvas, 233.9, 186.5, 235.2, 206.9);
        Fill(canvas, 30.5, 205.9, 782.5, 207.3);

        Text(canvas, "Name:", 32.7, 161.7);
        Text(canvas, Value("Name"), 74.5, 161.4, width: 268);
        Text(canvas, "Company Name:", 348.3, 161.7);
        Text(canvas, Value("CompanyName"), 348.3, 178.9, width: 196);
        Text(canvas, "Department/Agency:", 550.7, 161.7);
        Text(canvas, Value("DepartmentOrAgency"), 550.7, 178.9, width: 228);

        Text(canvas, "Age:", 32.0, 201.1);
        Text(canvas, (Value("Age") ?? string.Empty).Replace(" years old", string.Empty).Replace(" year old", string.Empty), 58.5, 200.8);
        Text(canvas, "Birth Date:", 108.0, 201.1);
        Text(canvas, Value("BirthDate"), 167.5, 201.4, width: 64);
        Text(canvas, "Gender:", 236.5, 201.1);
        Text(canvas, Value("Sex"), 281.5, 200.8, width: 62);
        Text(canvas, "Civil Status:", 348.3, 201.1);
        Text(canvas, Value("CivilStatus"), 413.9, 200.8, width: 132);
        Text(canvas, "Contact No.:", 550.0, 201.1);
        Text(canvas, Value("ContactNo"), 622.5, 200.8, width: 158);
    }

    // ---- small drawing helpers --------------------------------------------------------------------------

    private static void Fill(Canvas canvas, double x1, double y1, double x2, double y2) =>
        PageDrawing.PutRect(canvas, x1, y1, x2 - x1, y2 - y1, Brushes.Black, null, 0);

    private static TextBlock Block(string? value, double size, FontFamily family, bool bold, bool italic, double? width, TextAlignment alignment = TextAlignment.Left)
    {
        var block = new TextBlock
        {
            Text = value ?? string.Empty,
            FontFamily = family,
            FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
            TextAlignment = alignment,
            TextWrapping = width is null ? TextWrapping.NoWrap : TextWrapping.Wrap,
            Foreground = Brushes.Black,
        };
        if (width is { } w)
            block.Width = w;

        block.Measure(new Size(width ?? double.PositiveInfinity, double.PositiveInfinity));
        return block;
    }

    private static TextBlock Place(Canvas canvas, TextBlock block, double x, double baseline)
    {
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, baseline - block.BaselineOffset);
        canvas.Children.Add(block);
        return block;
    }

    private static void Text(Canvas canvas, string? value, double x, double baseline, double size = Size, double? width = null)
    {
        if (!string.IsNullOrEmpty(value))
            Place(canvas, Block(value, size, Calibri, bold: false, italic: false, width: width), x, baseline);
    }

    private static void Label(Canvas canvas, string value, double x, double baseline) => Text(canvas, value, x, baseline);

    private static void Italic(Canvas canvas, string value, double baseline) =>
        Place(canvas, Block(value, Size, Calibri, bold: false, italic: true, width: null), 31.3, baseline);

    private static void Centered(Canvas canvas, string? value, double baseline, double size, FontFamily family, bool bold)
    {
        if (!string.IsNullOrEmpty(value))
            Place(canvas, Block(value, size, family, bold, italic: false, width: ReportDocumentBuilder.PageWidth, alignment: TextAlignment.Center), 0, baseline);
    }

    /// <summary>Wrapped text whose first line sits on <paramref name="firstBaseline"/>; returns where its last line ends.</summary>
    private static double Wrapped(Canvas canvas, string? value, double x, double firstBaseline, double width, double size)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var block = Place(canvas, Block(value, size, Calibri, bold: false, italic: false, width: width), x, firstBaseline);
        return Canvas.GetTop(block) + block.DesiredSize.Height;
    }

    private static void Mark(Canvas canvas, double x, double baseline) =>
        Place(canvas, Block(Check, 16, Wingdings, bold: false, italic: false, width: null), x, baseline);

    private static void YesNo(Canvas canvas, string? answer, double textBaseline, double markBaseline)
    {
        Text(canvas, "No", 215.5, textBaseline);
        Text(canvas, "Yes", 254.7, textBaseline);
        if (answer == "No")
            Mark(canvas, 201.0, markBaseline);
        else if (answer == "Yes")
            Mark(canvas, 240.2, markBaseline);
    }

    private static void Option(Canvas canvas, string text, double x, double textBaseline, double markBaseline, bool selected, double markX)
    {
        Text(canvas, text, x, textBaseline);
        if (selected)
            Mark(canvas, markX, markBaseline);
    }
}