using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Wpf.Services;

namespace DiagnosticLabs.Wpf.Printing;

/// <summary>
/// Lays the Sales Report out on landscape US Letter pages (1056 x 816 pixels): the letterhead and a yellow title bar, then a table with a row per
/// registration and a column per service, the count and amount of each column under the table, and the grand total. Long reports continue on
/// further pages (the header row is repeated); a report with more services than fit across the page is printed in sets of columns, one after another.
/// Everything on the page comes from <see cref="SalesReportData"/>, which is worked out and tested in the Application layer.
/// </summary>
public static class SalesReportDocumentBuilder
{
    public const double PageWidth = 1056;
    public const double PageHeight = 816;

    private const double Margin = 40;
    private const double NameWidth = 210;
    private const double MinColumnWidth = 74;
    private const double HeaderHeight = 46;
    private const double RowHeight = 18;
    private const double Size = 11.5;
    private const double Bottom = PageHeight - 46;

    private static readonly Brush Faint = new SolidColorBrush(Color.FromRgb(200, 200, 200));

    /// <summary>One page worth of the table: which columns, which rows, and what goes at the foot of it.</summary>
    private sealed record PageSpec(int FirstColumn, int ColumnCount, int FirstRow, int RowCount, bool HasTotals, bool HasGrandTotal, bool FirstOfReport);

    public static FixedDocument Build(SalesReportData report)
    {
        var specs = Plan(report);
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(PageWidth, PageHeight);

        for (var i = 0; i < specs.Count; i++)
        {
            var canvas = new Canvas { Width = PageWidth, Height = PageHeight, Background = Brushes.White };
            Draw(canvas, report, specs[i], i + 1, specs.Count);

            var page = new FixedPage { Width = PageWidth, Height = PageHeight, Background = Brushes.White };
            page.Children.Add(canvas);
            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            document.Pages.Add(content);
        }

        return document;
    }

    /// <summary>How many pages the report takes (for the harness and tests of the layout).</summary>
    internal static int PageCount(SalesReportData report) => Plan(report).Count;

    // ---------------------------------------------------------------- pagination

    /// <summary>Where the title bar starts on the first page: under the logo (92 x 100 at the top) and the company details.</summary>
    private const double HeadingBottom = 142;

    /// <summary>Where the table starts: lower on the first page, under the letterhead, the title bar and the parameters.</summary>
    private static double TableTop(bool first) => first ? HeadingBottom + 21.4 + 14 + 17 + 14 : 100;

    private static IReadOnlyList<PageSpec> Plan(SalesReportData report)
    {
        // The columns are split into equal sets that each fit the page width.
        var perSet = Math.Max(1, (int)Math.Floor((PageWidth - (2 * Margin) - NameWidth) / MinColumnWidth));
        var sets = Math.Max(1, (int)Math.Ceiling(report.Columns.Count / (double)perSet));
        var setSize = report.Columns.Count == 0 ? 0 : (int)Math.Ceiling(report.Columns.Count / (double)sets);

        var specs = new List<PageSpec>();
        for (var set = 0; set < sets; set++)
        {
            var firstColumn = set * setSize;
            var columnCount = Math.Min(setSize, report.Columns.Count - firstColumn);
            var lastSet = set == sets - 1;
            var totalsHeight = (2 * RowHeight) + 8 + (lastSet ? 2 * RowHeight : 0);

            var row = 0;
            while (true)
            {
                var first = specs.Count == 0;
                var available = Bottom - TableTop(first) - HeaderHeight;
                var capacity = (int)Math.Floor(available / RowHeight);
                var remaining = report.Rows.Count - row;

                if (((remaining * RowHeight) + totalsHeight) <= available)
                {
                    specs.Add(new PageSpec(firstColumn, columnCount, row, remaining, true, lastSet, first));
                    break;
                }

                if (remaining <= capacity)
                {
                    // The rows fit but the totals do not: the totals get a page of their own rather than being split from the rows.
                    specs.Add(new PageSpec(firstColumn, columnCount, row, remaining, false, false, first));
                    specs.Add(new PageSpec(firstColumn, columnCount, report.Rows.Count, 0, true, lastSet, false));
                    break;
                }

                specs.Add(new PageSpec(firstColumn, columnCount, row, capacity, false, false, first));
                row += capacity;
            }
        }

        return specs;
    }

    // ---------------------------------------------------------------- drawing

    private static void Draw(Canvas canvas, SalesReportData report, PageSpec page, int number, int pages)
    {
        DrawHeading(canvas, report, page.FirstOfReport);

        var top = TableTop(page.FirstOfReport);
        var left = Margin;
        var right = PageWidth - Margin;
        var columns = report.Columns.Skip(page.FirstColumn).Take(page.ColumnCount).ToList();
        var columnWidth = columns.Count == 0 ? 0 : (right - left - NameWidth) / columns.Count;
        var rows = report.Rows.Skip(page.FirstRow).Take(page.RowCount).ToList();

        var bodyTop = top + HeaderHeight;
        var bodyBottom = bodyTop + (rows.Count * RowHeight);
        var tableBottom = page.HasTotals ? bodyBottom + 8 + (2 * RowHeight) : bodyBottom;

        PageDrawing.PutRect(canvas, left, top, right - left, tableBottom - top, null, Brushes.Black, 1.33);
        PageDrawing.PutRule(canvas, left, bodyTop, right - left);
        PageDrawing.PutVerticalRule(canvas, left + NameWidth, top, tableBottom - top);
        for (var c = 1; c < columns.Count; c++)
            PageDrawing.PutVerticalRule(canvas, left + NameWidth + (c * columnWidth), top, tableBottom - top, 0.75);

        PageDrawing.PutText(canvas, "PATIENTS", left, top + 27, Size, bold: true, width: NameWidth, alignment: TextAlignment.Center);
        for (var c = 0; c < columns.Count; c++)
        {
            var x = left + NameWidth + (c * columnWidth);
            var block = PageDrawing.Text(columns[c].Name, Size, bold: true, width: columnWidth - 8);
            block.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(block, x + 4);
            Canvas.SetTop(block, top + ((HeaderHeight - Math.Min(block.DesiredSize.Height, HeaderHeight - 4)) / 2));
            canvas.Children.Add(block);
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var baseline = bodyTop + (r * RowHeight) + 13;
            if (r > 0)
                PageDrawing.PutRect(canvas, left + 1, bodyTop + (r * RowHeight), right - left - 2, 0.5, Faint, null, 0);

            PageDrawing.PutFit(canvas, rows[r].PatientName.ToUpperInvariant(), left + 6, baseline, NameWidth - 12, Size);
            for (var c = 0; c < columns.Count; c++)
            {
                if (rows[r].Prices.TryGetValue(columns[c].ServiceId, out var price))
                    PutAmount(canvas, price, left + NameWidth + (c * columnWidth), baseline, columnWidth);
            }
        }

        if (page.HasTotals)
        {
            var countBaseline = bodyBottom + 8 + 13;
            var amountBaseline = countBaseline + RowHeight;
            PageDrawing.PutRule(canvas, left, bodyBottom + 8, right - left);
            PageDrawing.PutRule(canvas, left, bodyBottom + 8 + RowHeight, right - left, 0.75);
            PageDrawing.PutText(canvas, "TOTAL COUNT:", left + 6, countBaseline, Size, bold: true);
            PageDrawing.PutText(canvas, "TOTAL AMOUNT:", left + 6, amountBaseline, Size, bold: true);
            for (var c = 0; c < columns.Count; c++)
            {
                var x = left + NameWidth + (c * columnWidth);
                PageDrawing.PutText(canvas, columns[c].Count.ToString("N0", CultureInfo.InvariantCulture), x, countBaseline, Size, bold: true, width: columnWidth - 6, alignment: TextAlignment.Right);
                PutAmount(canvas, columns[c].Amount, x, amountBaseline, columnWidth, bold: true);
            }
        }

        if (page.HasGrandTotal)
        {
            var baseline = tableBottom + 24;
            PageDrawing.PutText(canvas, "GRAND TOTAL:", right - 260, baseline, 13, bold: true);
            PageDrawing.PutText(canvas, Money(report.GrandTotal), right - 120, baseline, 13, bold: true, width: 120, alignment: TextAlignment.Right);
        }

        PageDrawing.PutText(canvas, $"Page {number} of {pages}", left, PageHeight - 22, 10, bold: false, width: right - left, alignment: TextAlignment.Right);
    }

    private static void DrawHeading(Canvas canvas, SalesReportData report, bool full)
    {
        var width = PageWidth - (2 * Margin);
        var yellow = new SolidColorBrush(Color.FromRgb(225, 225, 0));

        if (!full)
        {
            PageDrawing.PutText(canvas, report.Letterhead.CompanyName, Margin, 44, 14, bold: true, width: width, alignment: TextAlignment.Center);
            PageDrawing.PutRect(canvas, Margin, 54, width, 21.4, yellow, Brushes.Black, 1.4);
            PageDrawing.PutText(canvas, "SALES REPORT", Margin, 69.5, Size + 1, bold: true, width: width, alignment: TextAlignment.Center);
            return;
        }

        // The logo on the left and the company details left-aligned beside it, as on the result forms.
        const double TextLeft = 150.6;
        if (ImageLoader.FromBytes(report.Letterhead.Logo) is { } logo)
        {
            var image = new Image { Source = logo, Width = 92, Height = 100, Stretch = Stretch.Uniform };
            Canvas.SetLeft(image, Margin);
            Canvas.SetTop(image, 32);
            canvas.Children.Add(image);
        }

        PageDrawing.PutText(canvas, report.Letterhead.CompanyName, TextLeft, 46, 18, bold: true);
        var y = 68.0;
        foreach (var line in new[] { report.Letterhead.SubCompanyName, report.Letterhead.Address, report.Letterhead.ContactNumbers, report.Letterhead.Email })
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            PageDrawing.PutText(canvas, line, TextLeft, y, Size, bold: line == report.Letterhead.SubCompanyName);
            y += 17;
        }

        // The logo is 100 tall, so the title bar always starts below it.
        y = HeadingBottom;
        PageDrawing.PutRect(canvas, Margin, y, width, 21.4, yellow, Brushes.Black, 1.4);
        PageDrawing.PutText(canvas, "SALES REPORT", Margin, y + 15.5, Size + 1.5, bold: true, width: width, alignment: TextAlignment.Center);
        y += 21.4 + 14;

        // The subheader: what this report was run for, two to a line.
        const double RightColumn = 520;
        void Parameter(string label, string value, double x, double baseline)
        {
            PageDrawing.PutText(canvas, label, x, baseline, Size, bold: true);
            PageDrawing.PutText(canvas, value, x + 85, baseline, Size, bold: false);
        }

        Parameter("DATES:", DatesText(report), Margin, y);
        Parameter("BATCH:", report.Batch?.ToUpperInvariant() ?? "ALL BATCHES", Margin + RightColumn, y);
        Parameter("COMPANY:", report.CompanyName?.ToUpperInvariant() ?? "ALL COMPANIES", Margin, y + 17);
        Parameter("PAYMENT:", PaymentText(report.Payment), Margin + RightColumn, y + 17);
    }

    private static string PaymentText(SalesPaymentFilter payment) => payment switch
    {
        SalesPaymentFilter.DirectPayments => "DIRECT PAYMENTS ONLY",
        SalesPaymentFilter.ChargedToCompany => "CHARGED TO COMPANY ONLY",
        _ => "ALL SALES",
    };

    private static string DatesText(SalesReportData report)
    {
        string Day(DateOnly d) => d.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture).ToUpperInvariant();
        return report.From == report.To ? Day(report.From) : $"{Day(report.From)} - {Day(report.To)}";
    }

    private static void PutAmount(Canvas canvas, decimal amount, double x, double baseline, double width, bool bold = false) =>
        PageDrawing.PutText(canvas, Money(amount), x, baseline, Size, bold, width: width - 6, alignment: TextAlignment.Right);

    /// <summary>Every amount prints with two decimals and a comma between thousands (12,345.60), whatever the PC's regional settings are.</summary>
    private static string Money(decimal amount) =>
        amount.ToString("N2", CultureInfo.InvariantCulture);
}
