using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Wpf.Services;

namespace DiagnosticLabs.Wpf.Printing;

/// <summary>
/// Lays a Statement of Account out on portrait US Letter pages (816 x 1056 pixels): the letterhead and a yellow title bar, then what the statement is for
/// (company and batch, or the patient, with the dates), then the table. A company statement lists the patients with their Gross Amount, Discount and Net
/// Amount; a patient statement lists the registrations with their code and date and adds Paid Amount and Balance. TOTALS close the table on the last page.
/// Long statements continue on further pages (the heading is shortened and the column headers repeat). A voided statement prints with VOID across it.
/// Everything on the page comes from <see cref="StatementData"/>, which is worked out and tested in the Application layer.
/// </summary>
public static class StatementDocumentBuilder
{
    public const double PageWidth = 816;
    public const double PageHeight = 1056;

    private const double Margin = 48;
    private const double HeaderHeight = 28;
    private const double RowHeight = 19;
    private const double TotalsHeight = 8 + 24;
    private const double Size = 11.5;
    private const double Bottom = PageHeight - 56;

    /// <summary>Where the title bar starts on the first page: under the logo (92 x 100 at the top) and the company details.</summary>
    private const double HeadingBottom = 142;

    private static readonly Brush Faint = new SolidColorBrush(Color.FromRgb(210, 210, 210));
    private static readonly Brush Yellow = new SolidColorBrush(Color.FromRgb(225, 225, 0));

    private sealed record Column(string Header, double Width, bool IsMoney);

    private sealed record PageSpec(int FirstRow, int RowCount, bool HasTotals, bool First);

    public static FixedDocument Build(StatementData statement)
    {
        var specs = Plan(statement);
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(PageWidth, PageHeight);

        for (var i = 0; i < specs.Count; i++)
        {
            var canvas = new Canvas { Width = PageWidth, Height = PageHeight, Background = Brushes.White };
            Draw(canvas, statement, specs[i], i + 1, specs.Count);

            var page = new FixedPage { Width = PageWidth, Height = PageHeight, Background = Brushes.White };
            page.Children.Add(canvas);
            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            document.Pages.Add(content);
        }

        return document;
    }

    /// <summary>How many pages the statement takes (for the harness and tests of the layout).</summary>
    internal static int PageCount(StatementData statement) => Plan(statement).Count;

    // ---------------------------------------------------------------- pagination

    /// <summary>The parameters printed under the title bar on the first page, two to a line.</summary>
    private static int ParameterLines(StatementData statement) => statement.Mode == StatementMode.Company ? 3 : 2;

    private static double TableTop(StatementData statement, bool first) => first ? HeadingBottom + 21.4 + 14 + (ParameterLines(statement) * 17) + 14 : 100;

    private static IReadOnlyList<PageSpec> Plan(StatementData statement)
    {
        var specs = new List<PageSpec>();
        var row = 0;
        while (true)
        {
            var first = specs.Count == 0;
            var available = Bottom - TableTop(statement, first) - HeaderHeight;
            var capacity = (int)Math.Floor(available / RowHeight);
            var remaining = statement.Lines.Count - row;

            if (((remaining * RowHeight) + TotalsHeight) <= available)
            {
                specs.Add(new PageSpec(row, remaining, true, first));
                break;
            }

            if (remaining <= capacity)
            {
                // The rows fit but the totals do not: the totals get a page of their own rather than being split from the rows.
                specs.Add(new PageSpec(row, remaining, false, first));
                specs.Add(new PageSpec(statement.Lines.Count, 0, true, false));
                break;
            }

            specs.Add(new PageSpec(row, capacity, false, first));
            row += capacity;
        }

        return specs;
    }

    // ---------------------------------------------------------------- drawing

    private static IReadOnlyList<Column> Columns(StatementData statement)
    {
        const double money = 88;
        return statement.Mode == StatementMode.Company
            ?
            [
                new Column("Employees/Patients", PageWidth - (2 * Margin) - 110 - (3 * 110), false),
                new Column("Registration Date", 110, false),
                new Column("Gross Amount", 110, true),
                new Column("Discount", 110, true),
                new Column("Net Amount", 110, true),
            ]
            :
            [
                new Column("Patient Registration Code", 180, false),
                new Column("Date", PageWidth - (2 * Margin) - 180 - (5 * money), false),
                new Column("Gross Amount", money, true),
                new Column("Discount", money, true),
                new Column("Net Amount", money, true),
                new Column("Paid Amount", money, true),
                new Column("Balance", money, true),
            ];
    }

    private static string[] Cells(StatementData statement, StatementLine line) => statement.Mode == StatementMode.Company
        ? [line.PatientName.ToUpperInvariant(), line.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture), Money(line.Gross), Money(line.Discount), Money(line.Net)]
        :
        [
            line.RegistrationCode,
            line.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
            Money(line.Gross), Money(line.Discount), Money(line.Net), Money(line.Paid), Money(line.Balance),
        ];

    private static string[] Totals(StatementData statement) => statement.Mode == StatementMode.Company
        ? ["TOTALS", string.Empty, Money(statement.GrossTotal), Money(statement.DiscountTotal), Money(statement.NetTotal)]
        :
        [
            "TOTALS", string.Empty,
            Money(statement.GrossTotal), Money(statement.DiscountTotal), Money(statement.NetTotal), Money(statement.PaidTotal), Money(statement.BalanceTotal),
        ];

    private static void Draw(Canvas canvas, StatementData statement, PageSpec page, int number, int pages)
    {
        DrawHeading(canvas, statement, page.First);

        var left = Margin;
        var right = PageWidth - Margin;
        var top = TableTop(statement, page.First);
        var columns = Columns(statement);
        var rows = statement.Lines.Skip(page.FirstRow).Take(page.RowCount).ToList();
        var bodyTop = top + HeaderHeight;

        // The column headers (the money ones right-aligned over their figures) with a rule beneath.
        var x = left;
        foreach (var column in columns)
        {
            PageDrawing.PutText(canvas, column.Header, x + (column.IsMoney ? 0 : 2), top + 19, Size, bold: true,
                width: column.Width - (column.IsMoney ? 6 : 0), alignment: column.IsMoney ? TextAlignment.Right : TextAlignment.Left);
            x += column.Width;
        }

        PageDrawing.PutRule(canvas, left, bodyTop - 4, right - left);

        for (var r = 0; r < rows.Count; r++)
        {
            var baseline = bodyTop + (r * RowHeight) + 13;
            if (r > 0)
                PageDrawing.PutRect(canvas, left, bodyTop + (r * RowHeight) - 3, right - left, 0.5, Faint, null, 0);

            PutRow(canvas, columns, Cells(statement, rows[r]), left, baseline, bold: false);
        }

        var bodyBottom = bodyTop + (rows.Count * RowHeight);
        if (page.HasTotals)
        {
            PageDrawing.PutRule(canvas, left, bodyBottom + 4, right - left);
            PutRow(canvas, columns, Totals(statement), left, bodyBottom + 4 + 19, bold: true);
        }

        var footer = string.IsNullOrEmpty(statement.Number) ? string.Empty : statement.Number + "    ";
        PageDrawing.PutText(canvas, $"{footer}Page {number} of {pages}", left, PageHeight - 28, 10, bold: false, width: right - left, alignment: TextAlignment.Right);

        if (statement.IsVoid)
            DrawVoid(canvas);
    }

    private static void PutRow(Canvas canvas, IReadOnlyList<Column> columns, string[] cells, double left, double baseline, bool bold)
    {
        var x = left;
        for (var c = 0; c < columns.Count; c++)
        {
            if (columns[c].IsMoney)
                PageDrawing.PutText(canvas, cells[c], x, baseline, Size, bold, width: columns[c].Width - 6, alignment: TextAlignment.Right);
            else
                PageDrawing.PutFit(canvas, cells[c], x + 2, baseline, columns[c].Width - 8, Size, bold);
            x += columns[c].Width;
        }
    }

    private static void DrawHeading(Canvas canvas, StatementData statement, bool full)
    {
        var width = PageWidth - (2 * Margin);

        if (!full)
        {
            PageDrawing.PutText(canvas, statement.Letterhead.CompanyName, Margin, 44, 14, bold: true, width: width, alignment: TextAlignment.Center);
            PageDrawing.PutRect(canvas, Margin, 54, width, 21.4, Yellow, Brushes.Black, 1.4);
            PageDrawing.PutText(canvas, "STATEMENT OF ACCOUNT", Margin, 69.5, Size + 1, bold: true, width: width, alignment: TextAlignment.Center);
            return;
        }

        // The logo on the left and the company details left-aligned beside it, as on the result forms.
        const double textLeft = 150.6;
        if (ImageLoader.FromBytes(statement.Letterhead.Logo) is { } logo)
        {
            var image = new Image { Source = logo, Width = 92, Height = 100, Stretch = Stretch.Uniform };
            Canvas.SetLeft(image, Margin);
            Canvas.SetTop(image, 32);
            canvas.Children.Add(image);
        }

        PageDrawing.PutText(canvas, statement.Letterhead.CompanyName, textLeft, 46, 18, bold: true);
        var y = 68.0;
        foreach (var line in new[] { statement.Letterhead.SubCompanyName, statement.Letterhead.Address, statement.Letterhead.ContactNumbers, statement.Letterhead.Email })
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            PageDrawing.PutText(canvas, line, textLeft, y, Size, bold: line == statement.Letterhead.SubCompanyName);
            y += 17;
        }

        // The logo is 100 tall, so the title bar always starts below it.
        y = HeadingBottom;
        PageDrawing.PutRect(canvas, Margin, y, width, 21.4, Yellow, Brushes.Black, 1.4);
        PageDrawing.PutText(canvas, "STATEMENT OF ACCOUNT", Margin, y + 15.5, Size + 1.5, bold: true, width: width, alignment: TextAlignment.Center);
        y += 21.4 + 14;

        // What the statement is for. The statement number (once it is saved) sits on the right of the first line.
        void Parameter(string label, string value, double x, double baseline, double valueWidth)
        {
            PageDrawing.PutText(canvas, label, x, baseline, Size, bold: true);
            PageDrawing.PutFit(canvas, value, x + 82, baseline, valueWidth, Size);
        }

        const double rightColumn = 400;
        var number = string.IsNullOrEmpty(statement.Number) ? "(not saved)" : statement.Number;
        if (statement.Mode == StatementMode.Company)
        {
            Parameter("COMPANY:", (statement.CompanyName ?? string.Empty).ToUpperInvariant(), Margin, y, rightColumn - 100);
            Parameter("SOA NO.:", number, Margin + rightColumn, y, width - rightColumn - 82);
            Parameter("BATCH:", statement.Batch?.ToUpperInvariant() ?? "ALL BATCHES", Margin, y + 17, rightColumn - 100);
            Parameter("DATES:", DatesText(statement), Margin, y + 34, width - 82);
            Parameter("ATTENTION:", (statement.Attention ?? string.Empty).ToUpperInvariant(), Margin + rightColumn, y + 17, width - rightColumn - 82);
        }
        else
        {
            var patient = (statement.PatientName ?? string.Empty).ToUpperInvariant();
            Parameter("PATIENT:", patient, Margin, y, rightColumn - 100);
            Parameter("SOA NO.:", number, Margin + rightColumn, y, width - rightColumn - 82);
            Parameter("DATES:", DatesText(statement), Margin, y + 17, width - 82);
        }
    }

    /// <summary>Both ends, one end, or (no range) every date.</summary>
    public static string DatesText(StatementData statement)
    {
        static string Day(DateOnly d) => d.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        return (statement.From, statement.To) switch
        {
            ({ } from, { } to) when from == to => Day(from),
            ({ } from, { } to) => $"{Day(from)} - {Day(to)}",
            ({ } from, null) => $"FROM {Day(from)}",
            (null, { } to) => $"UP TO {Day(to)}",
            _ => "ALL DATES",
        };
    }

    /// <summary>A voided statement stays readable but can not be mistaken for a live one.</summary>
    private static void DrawVoid(Canvas canvas)
    {
        var block = PageDrawing.Text("VOID", 150, bold: true);
        block.Foreground = new SolidColorBrush(Color.FromArgb(70, 200, 0, 0));
        block.RenderTransformOrigin = new Point(0.5, 0.5);
        block.RenderTransform = new RotateTransform(-35);
        Canvas.SetLeft(block, 190);
        Canvas.SetTop(block, 400);
        canvas.Children.Add(block);
    }

    /// <summary>Every amount prints with two decimals and a comma between thousands (12,345.60), whatever the PC's regional settings are.</summary>
    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
}
