using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.LabResults.CustomForms;

namespace DiagnosticLabs.Wpf.Printing;

/// <summary>
/// The page of a form made in the Custom Lab Form designer: the usual letterhead and yellow title bar, the header grid (3 columns, 2 rows) the designer chose, the groups (Test Name,
/// Result Value and, unless hidden, Reference/Normal Values) side by side, the text boxes and Remarks box that grow down with their text, and the signatories the
/// design has. The spacing comes from <see cref="CustomFormPage"/>, which is also what the designer's one-page warning is worked out from: a row is one line high
/// unless a value has to wrap, so the warning is a lower bound. <see cref="Draw"/> returns where the last line ended.
/// </summary>
public static class CustomFormLayout
{
    private const double Size = 12.0;
    private const double CaptionSize = 11.0;
    private const double RowPadding = 5.0;

    private static readonly double[] CellLeft = [52.7, 290.2, 527.7];
    private static readonly double[] CellBaseline = [161.7, 182.3];
    private const double CellWidth = 237.5;

    public static double Draw(Canvas canvas, PrintableReport report)
    {
        var custom = report.Custom ?? throw new InvalidOperationException("The report has no custom form design to lay out.");

        PageDrawing.PutHeader(canvas, report.Letterhead);
        PageDrawing.PutTitleBar(canvas, report.Title.ToUpperInvariant());
        DrawHeaderGrid(canvas, custom.HeaderCells);

        var y = CustomFormPage.Top + DrawGroups(canvas, custom.Groups);
        foreach (var box in custom.Textboxes)
            y = DrawTextBlock(canvas, box.Label, box.Text, y);

        if (custom.ShowRemarks)
            y = DrawTextBlock(canvas, "Remarks", report.ResultTexts.FirstOrDefault(t => t.Label == "Remarks")?.Text, y);

        return DrawSignatories(canvas, report, custom, y);
    }

    // ---------------------------------------------------------------- the header grid

    /// <summary>Each chosen detail as "Label : value" in its cell of the grid, on the first two lines of the patient block of the other forms.</summary>
    private static void DrawHeaderGrid(Canvas canvas, IReadOnlyList<PrintLine?> cells)
    {
        for (var i = 0; i < cells.Count && i < CellLeft.Length * CellBaseline.Length; i++)
        {
            if (cells[i] is not { } cell)
                continue;

            var x = CellLeft[i % 3];
            var baseline = CellBaseline[i / 3];
            PageDrawing.PutLabel(canvas, cell.Label, x, baseline, Size);
            var labelWidth = PageDrawing.Text(cell.Label, Size, bold: true).DesiredSize.Width + PageDrawing.Text(":", Size, bold: true).DesiredSize.Width;
            PutValue(canvas, cell.Value, x + labelWidth + 6.0, baseline - 0.4, CellWidth - labelWidth - 14.0);
        }
    }

    /// <summary>A header value on its baseline, set smaller (down to 9 px) until it fits its cell, like the patient block of the other forms.</summary>
    private static void PutValue(Canvas canvas, string? value, double x, double baseline, double width)
    {
        var size = Size;
        while (size > 9.0 && PageDrawing.Text(value, size, bold: false).DesiredSize.Width > width)
            size -= 0.5;

        PageDrawing.PutText(canvas, value, x, baseline, size, bold: false);
    }

    // ---------------------------------------------------------------- the groups

    private static (double Test, double Result, double Reference) Columns(bool showReference, double width) =>
        showReference
            ? (width * 0.46, width * 0.30, width - (width * 0.46) - (width * 0.30))
            : (width * 0.55, width - (width * 0.55), 0);

    /// <summary>How tall a text is where it sits in its cell: shrunk (down to 9 px) first and wrapped only when even that is too wide, as PutFit draws it.</summary>
    private static double TextHeight(string? text, double width, bool bold)
    {
        var size = Size;
        while (size > 9.0 && PageDrawing.Text(text, size, bold).DesiredSize.Width > width)
            size -= 0.5;

        return PageDrawing.Text(text, size, bold, width).DesiredSize.Height;
    }

    /// <summary>The height of a row: one line (<see cref="CustomFormPage.RowHeight"/>), or taller when a name, result or reference value has to wrap.</summary>
    private static double RowHeight(CustomFormPrintRow row, bool showReference, double width)
    {
        var (test, result, reference) = Columns(showReference, width);
        var tallest = row.IsSection
            ? TextHeight(row.Name, width - 8.0, bold: true)
            : Math.Max(
                TextHeight(row.Name, test - 12.0, bold: false),
                Math.Max(TextHeight(row.Result, result - 6.0, bold: false), showReference ? TextHeight(row.Reference, reference - 6.0, bold: false) : 0));
        return Math.Max(CustomFormPage.RowHeight, tallest + RowPadding);
    }

    /// <summary>Draws the groups side by side from <see cref="CustomFormPage.Top"/>; returns the height of the table (0 when there is no group).</summary>
    private static double DrawGroups(Canvas canvas, IReadOnlyList<CustomFormPrintGroup> groups)
    {
        if (groups.Count == 0)
            return 0;

        var anyTitle = groups.Any(g => !string.IsNullOrWhiteSpace(g.Title));
        var total = CustomFormPage.Right - CustomFormPage.Left;
        var width = groups.Count >= 2 ? (total - CustomFormPage.GroupGap) / 2 : total;

        // Every group is drawn as tall as the tallest one.
        var fixedPart = CustomFormPage.TableHeight(groups.Count, anyTitle, 0);
        var height = fixedPart + groups.Max(g => g.Rows.Sum(r => RowHeight(r, g.ShowReference, width)));

        for (var i = 0; i < groups.Count; i++)
            DrawGroup(canvas, groups[i], CustomFormPage.Left + (i * (width + CustomFormPage.GroupGap)), width, anyTitle, groups.Count, height);

        return height;
    }

    private static void DrawGroup(Canvas canvas, CustomFormPrintGroup group, double x, double width, bool anyTitle, int groupCount, double height)
    {
        var line = PageStyle.Standard.Line;
        var top = CustomFormPage.Top;
        var bottom = top + height;
        PageDrawing.PutRect(canvas, x, top, width, height, null, Brushes.Black, line);

        // the columns: Test Name | Result Value | Reference/Normal Values (hidden per group)
        var (testWidth, resultWidth, referenceWidth) = Columns(group.ShowReference, width);
        var resultX = x + testWidth;
        var referenceX = resultX + resultWidth;

        var y = top;
        if (anyTitle)
        {
            if (!string.IsNullOrWhiteSpace(group.Title))
                PageDrawing.PutFit(canvas, group.Title, x + 4.0, y + 14.5, width - 8.0, Size, bold: true, center: true);

            y += CustomFormPage.TitleRowHeight;
            PageDrawing.PutRule(canvas, x, y, width, line);
        }

        var caption = CustomFormPage.CaptionHeight(groupCount);
        PageDrawing.PutFlow(canvas, "Test Name", x + 2.0, y + 14.0, testWidth - 4.0, CaptionSize, bold: true, center: true);
        PageDrawing.PutFlow(canvas, "Result Value", resultX + 2.0, y + 14.0, resultWidth - 4.0, CaptionSize, bold: true, center: true);
        if (group.ShowReference)
            PageDrawing.PutFlow(canvas, "Reference/Normal Values", referenceX + 2.0, y + 14.0, referenceWidth - 4.0, CaptionSize, bold: true, center: true);

        PageDrawing.PutVerticalRule(canvas, resultX, y, caption, line);
        if (group.ShowReference)
            PageDrawing.PutVerticalRule(canvas, referenceX, y, caption, line);

        y += caption;
        PageDrawing.PutRule(canvas, x, y, width, line);

        // rows: a heading is ruled above and below and has no columns, the tests between headings share their column lines
        var runTop = y;
        foreach (var row in group.Rows)
        {
            var baseline = y + 14.0;
            var rowHeight = RowHeight(row, group.ShowReference, width);
            if (row.IsSection)
            {
                DrawColumnLines(canvas, resultX, referenceX, group.ShowReference, runTop, y - runTop, line);
                PageDrawing.PutRule(canvas, x, y, width, line);
                PageDrawing.PutFit(canvas, row.Name, x + 4.0, baseline, width - 8.0, Size, bold: true);
                y += rowHeight;
                PageDrawing.PutRule(canvas, x, y, width, line);
                runTop = y;
                continue;
            }

            PageDrawing.PutFit(canvas, row.Name, x + 8.0, baseline, testWidth - 12.0, Size);
            PageDrawing.PutFit(canvas, row.Result, resultX + 3.0, baseline, resultWidth - 6.0, Size, center: true);
            if (group.ShowReference)
                PageDrawing.PutFit(canvas, row.Reference, referenceX + 3.0, baseline, referenceWidth - 6.0, Size, center: true);

            y += rowHeight;
        }

        // the column lines run on to the bottom of the table, also under a group that has fewer rows than the other
        DrawColumnLines(canvas, resultX, referenceX, group.ShowReference, runTop, bottom - runTop, line);
    }

    private static void DrawColumnLines(Canvas canvas, double resultX, double referenceX, bool showReference, double top, double height, double line)
    {
        if (height <= 0)
            return;

        PageDrawing.PutVerticalRule(canvas, resultX, top, height, line);
        if (showReference)
            PageDrawing.PutVerticalRule(canvas, referenceX, top, height, line);
    }

    // ---------------------------------------------------------------- text boxes

    /// <summary>A label and a box under it that grows with its text (the same as the Remarks box of the other forms); returns the bottom of the box.</summary>
    private static double DrawTextBlock(Canvas canvas, string label, string? text, double top)
    {
        PageDrawing.PutLabel(canvas, label, 52.0, top + 24.2);
        var block = PageDrawing.Text(text, PageDrawing.Body, bold: false, width: 700);
        Canvas.SetLeft(block, 54.7);
        Canvas.SetTop(block, top + 32.7);
        canvas.Children.Add(block);

        var bottom = Math.Max(top + CustomFormPage.TextBlockHeight, Canvas.GetTop(block) + block.DesiredSize.Height + 6);
        PageDrawing.PutRect(canvas, 51.7, top + 29.7, 712.0, bottom - (top + 29.7), null, Brushes.Black, 1.4);
        return bottom;
    }

    // ---------------------------------------------------------------- signatories and the note

    private const double ColumnWidth = (712.4 - (2 * 10.0)) / 3;
    private const double ColumnGap = 10.0;

    /// <summary>The signatories of the design in equal columns, centred on the page, then the note under them; returns where the note ends.</summary>
    private static double DrawSignatories(Canvas canvas, PrintableReport report, CustomFormPrint custom, double top)
    {
        const double size = 11.7;
        var count = Math.Min(report.Signatories.Count, 3);
        var total = (count * ColumnWidth) + (Math.Max(count - 1, 0) * ColumnGap);
        var start = 50.3 + ((712.4 - total) / 2);
        for (var i = 0; i < count; i++)
        {
            var signatory = report.Signatories[i];
            var x = start + (i * (ColumnWidth + ColumnGap));
            PageDrawing.PutFit(canvas, signatory.Name, x, top + 48.7, ColumnWidth, size, bold: true, center: true);
            PageDrawing.PutRule(canvas, x, top + 54.4, ColumnWidth);
            PageDrawing.PutText(canvas, signatory.Role.ToUpperInvariant(), x, top + 68.9, size, bold: false, width: ColumnWidth, alignment: TextAlignment.Center);
            if (!string.IsNullOrWhiteSpace(signatory.LicenseNo))
                PageDrawing.PutFit(canvas, $"LICENSE NO: {signatory.LicenseNo}", x, top + 83.9, ColumnWidth, size, bold: false, center: true);
        }

        var y = top + CustomFormPage.NoteOffset(count, custom.ShowLicenseNumber);
        var last = y;
        foreach (var note in report.FooterNote.Split('\n'))
        {
            PageDrawing.PutText(canvas, note, 50.3, y, PageDrawing.Body, bold: false, width: 712.4, alignment: TextAlignment.Center);
            last = y;
            y += CustomFormPage.NoteLineHeight;
        }

        return last + 5.0;
    }
}
