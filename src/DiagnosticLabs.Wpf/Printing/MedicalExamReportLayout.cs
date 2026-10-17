using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiagnosticLabs.Application.LabResults;

namespace DiagnosticLabs.Wpf.Printing;

/// <summary>
/// The Medical Examination Report, printed on two US Letter pages under the system letterhead. Page 1 holds the applicant's Part 1 (medical history, family history,
/// OB-Gyne, social history, certification) and the practitioner's physical examination (vital signs, visual acuity, review of systems, findings) with the people
/// who took the vital signs and did the interview. Page 2 repeats the patient block and holds the laboratory / X-ray results, classification, assessment, remarks
/// and the signatories with their licence numbers. The wording and the order follow the client's printed sheet; every tick is drawn in its own box.
/// </summary>
internal static class MedicalExamReportLayout
{
    private const double Left = 50.3;
    private const double Right = 762.7;
    private const double Width = Right - Left;
    private const double Thin = 0.8;

    public static IReadOnlyList<Canvas> Draw(PrintableReport report) => [DrawFirstPage(report), DrawSecondPage(report)];

    private static Canvas NewPage(PrintableReport report, int number)
    {
        var canvas = new Canvas { Width = ReportDocumentBuilder.PageWidth, Height = ReportDocumentBuilder.PageHeight, Background = Brushes.White };
        PageDrawing.PutHeader(canvas, report.Letterhead);
        PageDrawing.PutTitleBar(canvas, report.Title.ToUpperInvariant());
        PageDrawing.PutPatientBlock(canvas, report);
        PageDrawing.PutText(canvas, $"Page {number} of 2", Right - 100, 1040, 8, bold: false, width: 100, alignment: TextAlignment.Right);
        return canvas;
    }

    private static string? Value(PrintableReport report, string name) =>
        report.Fields is { } fields && fields.TryGetValue(name, out var value) ? value : null;

    private static bool Is(PrintableReport report, string name, string option) =>
        string.Equals(Value(report, name)?.Trim(), option, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ small drawing helpers

    private static void Box(Canvas canvas, double x, double y, double width, double height) =>
        PageDrawing.PutRect(canvas, x, y, width, height, null, Brushes.Black, Thin);

    private static void HRule(Canvas canvas, double x, double y, double width) => PageDrawing.PutRule(canvas, x, y, width, Thin);

    private static void VRule(Canvas canvas, double x, double y, double height) => PageDrawing.PutVerticalRule(canvas, x, y, height, Thin);

    /// <summary>A check mark centred in a cell.</summary>
    private static void Check(Canvas canvas, double x, double y, double width, double height, bool ticked)
    {
        if (ticked)
            PageDrawing.PutText(canvas, "✓", x, y + (height / 2) + 4.2, 11, bold: true, width: width, alignment: TextAlignment.Center);
    }

    /// <summary>A small square tick box on a baseline, ticked or empty.</summary>
    private static void TickBox(Canvas canvas, double x, double baseline, bool ticked)
    {
        PageDrawing.PutRect(canvas, x, baseline - 8.2, 8.6, 8.6, null, Brushes.Black, Thin);
        if (ticked)
            PageDrawing.PutText(canvas, "✓", x - 0.3, baseline + 0.5, 10.5, bold: true, width: 9.4, alignment: TextAlignment.Center);
    }

    /// <summary>A tick box followed by its caption; returns where the caption ended so the next one can follow.</summary>
    private static double Option(Canvas canvas, double x, double baseline, string caption, bool ticked, double size = 9)
    {
        TickBox(canvas, x, baseline, ticked);
        var text = PageDrawing.PutText(canvas, caption, x + 12.5, baseline, size, bold: false);
        return x + 12.5 + text.DesiredSize.Width + 10;
    }

    private static void Bold(Canvas canvas, string text, double x, double baseline, double size = 9.5) =>
        PageDrawing.PutText(canvas, text, x, baseline, size, bold: true);

    /// <summary>A value on one line that is made smaller until it fits.</summary>
    private static void Fit(Canvas canvas, string? text, double x, double baseline, double width, double size = 9)
    {
        // The shared helper stops at 9 px; a printed cell here may need to go below that rather than wrap onto the next line.
        while (size > 6.5 && PageDrawing.Text(text, size, bold: false).DesiredSize.Width > width)
            size -= 0.25;

        PageDrawing.PutFlow(canvas, text, x, baseline, width, size);
    }

    /// <summary>A text that wraps inside a box: it starts at <paramref name="top"/> and is made smaller until it fits <paramref name="height"/>.</summary>
    private static void FitBlock(Canvas canvas, string? text, double x, double top, double width, double height, double size)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var block = PageDrawing.Text(text, size, bold: false, width);
        while (block.DesiredSize.Height > height && size > 6.5)
        {
            size -= 0.5;
            block = PageDrawing.Text(text, size, bold: false, width);
        }

        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, top);
        canvas.Children.Add(block);
    }

    // ------------------------------------------------------------------ page 1

    private static Canvas DrawFirstPage(PrintableReport report)
    {
        var canvas = NewPage(report, 1);

        PageDrawing.PutLabel(canvas, "Civil Status", 52.7, 259.5, 11);
        Fit(canvas, Value(report, "CivilStatus"), 133, 259.5, 290, 11);
        PageDrawing.PutLabel(canvas, "Contact #", 453.3, 259.5, 11);
        Fit(canvas, Value(report, "ContactNo"), 530, 259.5, 232, 11);

        Bold(canvas, "PART 1. TO BE ACCOMPLISHED BY THE APPLICANT / EMPLOYEE", Left, 277, 10);
        Bold(canvas, "I. MEDICAL HISTORY: Present and Past Illnesses of Applicant", Left, 290, 9);
        DrawHistory(canvas, report);
        DrawFamily(canvas, report);
        DrawObGyne(canvas, report);
        DrawSocial(canvas, report);
        DrawCertification(canvas, report);
        DrawExamination(canvas, report);
        return canvas;
    }

    private const double HistoryTop = 294;
    private const double HistoryRow = 14.0;
    private const double HistoryHead = 11.5;

    private static void DrawHistory(Canvas canvas, PrintableReport report)
    {
        var columns = MedicalExamFields.HistoryColumns;
        var columnWidth = Width / columns.Length;
        const double tick = 24.0;
        var rowsTop = HistoryTop + HistoryHead;
        var bottom = rowsTop + (13 * HistoryRow);

        for (var c = 0; c < columns.Length; c++)
        {
            var x = Left + (c * columnWidth);
            var labelWidth = columnWidth - (2 * tick);
            Box(canvas, x, HistoryTop, columnWidth, bottom - HistoryTop);
            HRule(canvas, x, rowsTop, columnWidth);
            PageDrawing.PutText(canvas, "No", x + labelWidth, HistoryTop + 9, 7.5, bold: true, width: tick, alignment: TextAlignment.Center);
            PageDrawing.PutText(canvas, "Yes", x + labelWidth + tick, HistoryTop + 9, 7.5, bold: true, width: tick, alignment: TextAlignment.Center);

            // The fourth column has five tick rows (the first two and the last three) with room between them for the two free texts.
            int[] rowOf = c == 3 ? [0, 1, 10, 11, 12] : [.. Enumerable.Range(0, columns[c].Length)];
            for (var i = 0; i < columns[c].Length; i++)
            {
                var (name, label) = columns[c][i];
                var y = rowsTop + (rowOf[i] * HistoryRow);
                Fit(canvas, label, x + 3, y + 10.2, labelWidth - 5, 8.3);
                if (rowOf[i] > 0)
                    HRule(canvas, x, y, columnWidth);

                VRule(canvas, x + labelWidth, y, HistoryRow);
                VRule(canvas, x + labelWidth + tick, y, HistoryRow);
                Check(canvas, x + labelWidth, y, tick, HistoryRow, Is(report, name, "No"));
                Check(canvas, x + labelWidth + tick, y, tick, HistoryRow, Is(report, name, "Yes"));
            }

            if (c != 3)
                continue;

            // Others (Pls. Specify) and Present/Past Medication, in the room the fourth column leaves.
            HRule(canvas, x, rowsTop + (2 * HistoryRow), columnWidth);
            Bold(canvas, "Others (Pls. Specify):", x + 3, rowsTop + (2 * HistoryRow) + 10, 8.3);
            FitBlock(canvas, Value(report, "HxOthers"), x + 3, rowsTop + (3 * HistoryRow) + 1, columnWidth - 6, 3 * HistoryRow - 2, 8.5);
            HRule(canvas, x, rowsTop + (6 * HistoryRow), columnWidth);
            Bold(canvas, "Present/Past Medication:", x + 3, rowsTop + (6 * HistoryRow) + 10, 8.3);
            FitBlock(canvas, Value(report, "HxMedication"), x + 3, rowsTop + (7 * HistoryRow) + 1, columnWidth - 6, 3 * HistoryRow - 2, 8.5);
            HRule(canvas, x, rowsTop + (10 * HistoryRow), columnWidth);
        }
    }

    private const double FamilyTop = 500;

    private static void DrawFamily(Canvas canvas, PrintableReport report)
    {
        Bold(canvas, "Remarks (To Include Family History):", Left, FamilyTop + 1, 9);
        const double top = FamilyTop + 5;
        const double head = 10.5;
        const double row = 14.0;
        const double tick = 24.0;
        const double itemWidth = 150.0;

        Box(canvas, Left, top, Width, head + row);
        HRule(canvas, Left, top + head, Width);
        for (var i = 0; i < MedicalExamFields.Family.Length; i++)
        {
            var (name, label) = MedicalExamFields.Family[i];
            var x = Left + (i * itemWidth);
            var labelWidth = itemWidth - (2 * tick);
            PageDrawing.PutText(canvas, "No", x + labelWidth, top + 8.5, 7.5, bold: true, width: tick, alignment: TextAlignment.Center);
            PageDrawing.PutText(canvas, "Yes", x + labelWidth + tick, top + 8.5, 7.5, bold: true, width: tick, alignment: TextAlignment.Center);
            Fit(canvas, label, x + 3, top + head + 10.2, labelWidth - 5, 8.5);
            VRule(canvas, x + labelWidth, top, head + row);
            VRule(canvas, x + labelWidth + tick, top, head + row);
            VRule(canvas, x + itemWidth, top, head + row);
            Check(canvas, x + labelWidth, top + head, tick, row, Is(report, name, "No"));
            Check(canvas, x + labelWidth + tick, top + head, tick, row, Is(report, name, "Yes"));
        }

        var othersX = Left + (MedicalExamFields.Family.Length * itemWidth) + 4;
        Bold(canvas, "Others (Pls. Specify):", othersX, top + 9, 8.3);
        Fit(canvas, Value(report, "FamOthers"), othersX, top + head + 10.2, Right - othersX - 4, 8.5);
    }

    private const double ObTop = 556;

    private static void DrawObGyne(Canvas canvas, PrintableReport report)
    {
        Bold(canvas, "II. OB-GYNE HISTORY: For FEMALE applicant / employee only.", Left, ObTop, 9);
        const double top = ObTop + 4;
        const double row = 14.5;
        const double half = Width / 2;
        Box(canvas, Left, top, Width, 5 * row);
        for (var i = 1; i < 5; i++)
            HRule(canvas, Left, top + (i * row), Width);
        VRule(canvas, Left + half, top, 4 * row);

        double Base(int i) => top + (i * row) + 10.4;
        const double rx = Left + half + 4;
        const double lx = Left + 4;

        // row 1
        Bold(canvas, "Menstrual Pattern:", lx, Base(0), 8.5);
        var x = Option(canvas, lx + 90, Base(0), "Monthly", Is(report, "MenstrualPattern", "Monthly"), 8.5);
        Option(canvas, x, Base(0), "Irregular", Is(report, "MenstrualPattern", "Irregular"), 8.5);
        Bold(canvas, "Pregnant Now?", rx, Base(0), 8.5);
        x = Option(canvas, rx + 78, Base(0), "No", Is(report, "PregnantNow", "No"), 8.5);
        Option(canvas, x, Base(0), "Yes", Is(report, "PregnantNow", "Yes"), 8.5);

        // row 2
        Bold(canvas, "LMP (Last Menstrual Period):", lx, Base(1), 8.5);
        Fit(canvas, Value(report, "Lmp"), lx + 128, Base(1), half - 136, 8.5);
        Bold(canvas, "Number of Pregnancies:", rx, Base(1), 8.5);
        Fit(canvas, Value(report, "NumPregnancies"), rx + 120, Base(1), half - 128, 8.5);

        // row 3
        Bold(canvas, "Dysmenorrhea:", lx, Base(2), 8.5);
        x = Option(canvas, lx + 75, Base(2), "No", Is(report, "Dysmenorrhea", "No"), 8.5);
        x = Option(canvas, x, Base(2), "Moderate", Is(report, "Dysmenorrhea", "Moderate"), 8.5);
        Option(canvas, x, Base(2), "Severe", Is(report, "Dysmenorrhea", "Severe"), 8.5);
        Bold(canvas, "Number of Miscarriages (nakunan):", rx, Base(2), 8.5);
        Fit(canvas, Value(report, "NumMiscarriages"), rx + 170, Base(2), half - 178, 8.5);

        // row 4
        Bold(canvas, "Myoma:", lx, Base(3), 8.5);
        x = Option(canvas, lx + 40, Base(3), "No", Is(report, "Myoma", "No"), 8.5);
        Option(canvas, x, Base(3), "Yes", Is(report, "Myoma", "Yes"), 8.5);
        Bold(canvas, "Ovarian Cyst:", rx, Base(3), 8.5);
        x = Option(canvas, rx + 68, Base(3), "No", Is(report, "OvarianCyst", "No"), 8.5);
        Option(canvas, x, Base(3), "Yes", Is(report, "OvarianCyst", "Yes"), 8.5);

        // row 5
        Bold(canvas, "Others (Pls. Specify):", lx, Base(4), 8.5);
        Fit(canvas, Value(report, "ObOthers"), lx + 105, Base(4), Width - 113, 8.5);
    }

    private const double SocialTop = 647;

    private static void DrawSocial(Canvas canvas, PrintableReport report)
    {
        Bold(canvas, "III. SOCIAL HISTORY (Pls. Specify the Year)", Left, SocialTop, 9);
        const double top = SocialTop + 4;
        const double row = 14.5;
        Box(canvas, Left, top, Width, 2 * row);
        HRule(canvas, Left, top + row, Width);

        const double lx = Left + 4;
        var b1 = top + 10.4;
        Bold(canvas, "1. Smoking", lx, b1, 8.5);
        var x = Option(canvas, lx + 150, b1, "No", Is(report, "SmokingStatus", "No"), 8.5);
        x = Option(canvas, x, b1, "Yes, since", Is(report, "SmokingStatus", "Yes"), 8.5);
        Fit(canvas, Value(report, "SmokingSince"), x - 6, b1, 70, 8.5);
        HRule(canvas, x - 6, b1 + 1.5, 70);
        Bold(canvas, "# of sticks per day:", x + 80, b1, 8.5);
        Fit(canvas, Value(report, "SticksPerDay"), x + 168, b1, 70, 8.5);
        HRule(canvas, x + 168, b1 + 1.5, 70);

        var b2 = top + row + 10.4;
        Bold(canvas, "2. Drinking (Alcoholic Beverage)", lx, b2, 8.5);
        x = Option(canvas, lx + 150, b2, "No", Is(report, "DrinkingStatus", "No"), 8.5);
        x = Option(canvas, x, b2, "Yes, since", Is(report, "DrinkingStatus", "Yes"), 8.5);
        Fit(canvas, Value(report, "DrinkingSince"), x - 6, b2, 70, 8.5);
        HRule(canvas, x - 6, b2 + 1.5, 70);
        Bold(canvas, "# of bottles:", x + 80, b2, 8.5);
        Fit(canvas, Value(report, "Bottles"), x + 140, b2, 60, 8.5);
        HRule(canvas, x + 140, b2 + 1.5, 60);
        x = Option(canvas, x + 215, b2, "Daily", Is(report, "DrinkingFrequency", "Daily"), 8.5);
        x = Option(canvas, x, b2, "Weekly", Is(report, "DrinkingFrequency", "Weekly"), 8.5);
        Option(canvas, x, b2, "Occasional", Is(report, "DrinkingFrequency", "Occasional"), 8.5);
    }

    private static void DrawCertification(Canvas canvas, PrintableReport report)
    {
        const double top = 690;
        var certify = PageDrawing.Text(
            "I certify that the answers and statements I provided are all true and correct to the best of my knowledge, and I understand that non-disclosure and/or misdeclaration of any of the above items from part of my application and/or may be used for any administrative action, I also give my consent for a thorough physical examination.",
            7.2, bold: true, Width);
        Canvas.SetLeft(certify, Left);
        Canvas.SetTop(certify, top);
        canvas.Children.Add(certify);

        var company = string.IsNullOrWhiteSpace(report.Letterhead.CompanyName) ? "the company" : report.Letterhead.CompanyName;
        PageDrawing.PutText(canvas, $"I hereby authorized {company}, to share and transmit my medical record and results to our HR dept/clinic.", Left, top + 27, 7.2, bold: true);

        HRule(canvas, 470, top + 52, Right - 470);
        PageDrawing.PutText(canvas, "Applicant's Signature over Printed Name", 470, top + 62, 7.5, bold: false, width: Right - 470, alignment: TextAlignment.Right);
    }

    private static void DrawExamination(Canvas canvas, PrintableReport report)
    {
        Bold(canvas, "PART 2. TO BE ACCOMPLISHED BY THE MEDICAL PRACTITIONER.", Left, 764, 10);
        Bold(canvas, "IV. PHYSICAL EXAMINATION", Left, 776, 9);

        // vital signs and measurements
        const double top = 780;
        const double head = 11.5;
        const double row = 14.0;
        double[] cuts = [Left, Left + 96, Left + 150, Left + 204, Left + 268, Left + 350, Left + 462];
        Box(canvas, Left, top, cuts[^1] - Left, head + (2 * row));
        HRule(canvas, Left, top + head, cuts[^1] - Left);
        HRule(canvas, Left, top + head + row, cuts[3] - Left);
        HRule(canvas, cuts[4], top + head + row, cuts[5] - cuts[4]);
        foreach (var cut in cuts.Skip(1).Take(cuts.Length - 2))
            VRule(canvas, cut, top, head + (2 * row));

        VRule(canvas, cuts[4] + 50, top + head, 2 * row);
        string[] heads = ["VITAL SIGNS", "1st reading", "2nd reading", "Temperature", "MEASUREMENTS", "BMI Category"];
        for (var i = 0; i < heads.Length; i++)
            PageDrawing.PutText(canvas, heads[i], cuts[i], top + 8.5, 7.5, bold: true, width: cuts[i + 1] - cuts[i], alignment: TextAlignment.Center);

        Bold(canvas, "BP Reading (mm/Hg):", cuts[0] + 3, top + head + 10, 7.8);
        Bold(canvas, "Cardiac Rate (bpm):", cuts[0] + 3, top + head + row + 10, 7.8);
        PageDrawing.PutFit(canvas, Value(report, "Bp1"), cuts[1] + 3, top + head + 10, cuts[2] - cuts[1] - 6, 8.5, center: true);
        PageDrawing.PutFit(canvas, Value(report, "Bp2"), cuts[2] + 3, top + head + 10, cuts[3] - cuts[2] - 6, 8.5, center: true);
        PageDrawing.PutFit(canvas, Value(report, "Pulse1"), cuts[1] + 3, top + head + row + 10, cuts[2] - cuts[1] - 6, 8.5, center: true);
        PageDrawing.PutFit(canvas, Value(report, "Pulse2"), cuts[2] + 3, top + head + row + 10, cuts[3] - cuts[2] - 6, 8.5, center: true);
        PageDrawing.PutFit(canvas, Value(report, "Temperature"), cuts[3] + 3, top + head + row + 3, cuts[4] - cuts[3] - 6, 8.5, center: true);
        Bold(canvas, "Height:", cuts[4] + 3, top + head + 10, 7.8);
        Bold(canvas, "Weight:", cuts[4] + 3, top + head + row + 10, 7.8);
        Fit(canvas, Value(report, "Height"), cuts[4] + 53, top + head + 10, cuts[5] - cuts[4] - 56, 8.3);
        Fit(canvas, Value(report, "Weight"), cuts[4] + 53, top + head + row + 10, cuts[5] - cuts[4] - 56, 8.3);
        FitBlock(canvas, Value(report, "BmiCategory"), cuts[5] + 3, top + head + 1.5, cuts[6] - cuts[5] - 6, (2 * row) - 3, 8);

        // visual acuity and the Ishihara test
        const double ax = 524;
        double[] acuity = [ax, ax + 100, ax + 169, Right];
        Box(canvas, ax, top, Right - ax, head + (2 * row));
        HRule(canvas, ax, top + head, Right - ax);
        HRule(canvas, ax, top + head + row, Right - ax);
        VRule(canvas, acuity[1], top, head + (2 * row));
        VRule(canvas, acuity[2], top, head + (2 * row));
        string[] eyeHeads = ["VISUAL ACUITY", "Right eye", "Left eye"];
        for (var i = 0; i < eyeHeads.Length; i++)
            PageDrawing.PutText(canvas, eyeHeads[i], acuity[i], top + 8.5, 7.5, bold: true, width: acuity[i + 1] - acuity[i], alignment: TextAlignment.Center);

        Bold(canvas, "Far with eyeglasses:", ax + 3, top + head + 10, 7.5);
        Bold(canvas, "Far without eyeglasses:", ax + 3, top + head + row + 10, 7.5);
        PageDrawing.PutFit(canvas, Value(report, "VisionFarWithR"), acuity[1] + 3, top + head + 10, acuity[2] - acuity[1] - 6, 8.5, center: true);
        PageDrawing.PutFit(canvas, Value(report, "VisionFarWithL"), acuity[2] + 3, top + head + 10, acuity[3] - acuity[2] - 6, 8.5, center: true);
        PageDrawing.PutFit(canvas, Value(report, "VisionFarWithoutR"), acuity[1] + 3, top + head + row + 10, acuity[2] - acuity[1] - 6, 8.5, center: true);
        PageDrawing.PutFit(canvas, Value(report, "VisionFarWithoutL"), acuity[2] + 3, top + head + row + 10, acuity[3] - acuity[2] - 6, 8.5, center: true);

        const double ishiharaTop = top + head + (2 * row);
        Box(canvas, ax, ishiharaTop, Right - ax, row);
        Bold(canvas, "ISHIHARA TEST:", ax + 3, ishiharaTop + 10, 7.8);
        Fit(canvas, Value(report, "Ishihara"), ax + 78, ishiharaTop + 10, Right - ax - 82, 8.5);

        DrawReview(canvas, report);
        DrawFindings(canvas, report);
        DrawDoneBy(canvas, report);
    }

    private const double ReviewTop = 838;

    private static void DrawReview(Canvas canvas, PrintableReport report)
    {
        Bold(canvas, "Review of Systems:  N – Normal or None;  F – With Findings. If with findings, describe below.", Left, ReviewTop, 8.5);
        const double top = ReviewTop + 4;
        const double head = 10.5;
        const double row = 13.5;
        const double tick = 24.0;
        var columns = MedicalExamFields.ReviewColumns;
        var columnWidth = Width / columns.Length;

        for (var c = 0; c < columns.Length; c++)
        {
            var x = Left + (c * columnWidth);
            var labelWidth = columnWidth - (2 * tick);
            Box(canvas, x, top, columnWidth, head + (5 * row));
            HRule(canvas, x, top + head, columnWidth);
            VRule(canvas, x + labelWidth, top, head + (5 * row));
            VRule(canvas, x + labelWidth + tick, top, head + (5 * row));
            PageDrawing.PutText(canvas, "N", x + labelWidth, top + 8.5, 7.5, bold: true, width: tick, alignment: TextAlignment.Center);
            PageDrawing.PutText(canvas, "F", x + labelWidth + tick, top + 8.5, 7.5, bold: true, width: tick, alignment: TextAlignment.Center);

            for (var i = 0; i < columns[c].Length; i++)
            {
                var (name, label) = columns[c][i];
                var y = top + head + (i * row);
                if (i > 0)
                    HRule(canvas, x, y, columnWidth);

                if (name == "RosOthers" && Value(report, "RosOthersNote") is { } note && !string.IsNullOrWhiteSpace(note))
                    label = $"Others: {note.Trim()}";

                Fit(canvas, label, x + 3, y + 9.8, labelWidth - 5, 8.3);
                Check(canvas, x + labelWidth, y, tick, row, Is(report, name, "N"));
                Check(canvas, x + labelWidth + tick, y, tick, row, Is(report, name, "F"));
            }
        }
    }

    private const double FindingsTop = 925;

    private static void DrawFindings(Canvas canvas, PrintableReport report)
    {
        Bold(canvas, "Findings:", Left, FindingsTop + 8, 9.5);
        const double lineGap = 13.0;
        for (var i = 0; i < 4; i++)
            HRule(canvas, Left + 52, FindingsTop + 10 + (i * lineGap), Width - 52);

        FitBlock(canvas, Value(report, "Findings"), Left + 54, FindingsTop - 1, Width - 56, (4 * lineGap) + 6, 9);
    }

    private static void DrawDoneBy(Canvas canvas, PrintableReport report)
    {
        // Vital Signs done by (left) and Interview done by (right): the name above a line, the licence number under it.
        const double baseline = 1010;
        const double half = Width / 2;
        for (var i = 0; i < 2; i++)
        {
            var person = report.Signatories.FirstOrDefault(s => s.Role == MedicalExamFields.Signatories[i].Label);
            var x = Left + (i * half);
            var label = MedicalExamFields.Signatories[i].Label + ":";
            Bold(canvas, label, x, baseline, 9);
            var labelWidth = PageDrawing.Text(label, 9, bold: true).DesiredSize.Width;
            var lineX = x + labelWidth + 4;
            var lineWidth = half - labelWidth - 14;
            Fit(canvas, person?.Name, lineX, baseline - 1, lineWidth, 9);
            HRule(canvas, lineX, baseline + 1.5, lineWidth);
            if (!string.IsNullOrWhiteSpace(person?.LicenseNo))
                Fit(canvas, $"License No: {person.LicenseNo}", lineX, baseline + 11, lineWidth, 7.5);
        }
    }

    // ------------------------------------------------------------------ page 2

    private static Canvas DrawSecondPage(PrintableReport report)
    {
        var canvas = NewPage(report, 2);

        Bold(canvas, "V. LABORATORY / X-RAY RESULTS:", Left, 270, 11);
        const double labTop = 278;
        const double labRow = 25;
        var labs = MedicalExamFields.LabRows;
        Box(canvas, Left, labTop, Width, labs.Length * labRow);
        const double labelWidth = 105;
        const double normalWidth = 100;
        const double findingsWidth = 120;
        VRule(canvas, Left + labelWidth, labTop, labs.Length * labRow);
        VRule(canvas, Left + labelWidth + normalWidth, labTop, labs.Length * labRow);
        for (var i = 0; i < labs.Length; i++)
        {
            var (name, label) = labs[i];
            var y = labTop + (i * labRow);
            if (i > 0)
                HRule(canvas, Left, y, Width);

            var baseline = y + 16.5;
            Fit(canvas, label, Left + 5, baseline, labelWidth - 10, 10.5);
            Option(canvas, Left + labelWidth + 8, baseline, "Normal", Is(report, name, "N"), 10);
            var x = Left + labelWidth + normalWidth + 8;
            Option(canvas, x, baseline, "With findings", Is(report, name, "F"), 10);
            var noteX = x + findingsWidth;
            Fit(canvas, Value(report, name + "Note"), noteX, baseline, Right - noteX - 6, 10);
        }

        Bold(canvas, "VI. CLASSIFICATION:", Left, 412, 11);
        const double classTop = 420;
        const double classRow = 25;
        (string Name, string Caption)[] classes =
        [
            ("A", "Class A – Physically Fit."),
            ("B", "Class B – Fit for employment; with minor ailment / defect."),
            ("C", "Class C – Acceptable employment; defect may not be easily corrected."),
            ("D", "Class D – Unfit for Employment."),
            ("Pending", "PENDING – Incomplete medical laboratory exam."),
        ];
        Box(canvas, Left, classTop, Width, classes.Length * classRow);
        for (var i = 0; i < classes.Length; i++)
        {
            var y = classTop + (i * classRow);
            if (i > 0)
                HRule(canvas, Left, y, Width);

            Option(canvas, Left + 8, y + 16.5, classes[i].Caption, Is(report, "Classification", classes[i].Name), 10);
        }

        Bold(canvas, "Assessment:", Left, 570, 11);
        Box(canvas, Left, 578, Width, 150);
        FitBlock(canvas, Value(report, "Assessment"), Left + 6, 583, Width - 12, 140, 10.5);

        Bold(canvas, "Remarks:", Left, 750, 11);
        Box(canvas, Left, 758, Width, 110);
        var remarks = report.ResultTexts.FirstOrDefault(t => t.Label == "Remarks")?.Text;
        FitBlock(canvas, remarks, Left + 6, 763, Width - 12, 100, 10.5);

        DrawSignatories(canvas, report);
        return canvas;
    }

    /// <summary>Assessment done by, Noted by and the physician side by side: the name on a line, the role under it and the licence number.</summary>
    private static void DrawSignatories(Canvas canvas, PrintableReport report)
    {
        const double top = 884;
        const double gap = 14.0;
        const double width = (Width - (2 * gap)) / 3;
        string[] roles = [MedicalExamFields.Signatories[2].Label, MedicalExamFields.Signatories[3].Label, MedicalExamReportService.PhysicianRole];
        for (var i = 0; i < roles.Length; i++)
        {
            var person = report.Signatories.FirstOrDefault(s => s.Role == roles[i]);
            var x = Left + (i * (width + gap));
            PageDrawing.PutFit(canvas, person?.Name, x, top + 48.7, width, 11.7, bold: true, center: true);
            HRule(canvas, x, top + 54.4, width);
            PageDrawing.PutText(canvas, roles[i].ToUpperInvariant(), x, top + 68.9, 10.5, bold: false, width: width, alignment: TextAlignment.Center);
            if (!string.IsNullOrWhiteSpace(person?.LicenseNo))
                PageDrawing.PutFit(canvas, $"LICENSE NO: {person.LicenseNo}", x, top + 83.9, width, 10.5, bold: false, center: true);
        }
    }
}
