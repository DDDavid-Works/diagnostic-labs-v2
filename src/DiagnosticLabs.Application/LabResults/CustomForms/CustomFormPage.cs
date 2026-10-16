namespace DiagnosticLabs.Application.LabResults.CustomForms;

/// <summary>
/// The geometry of the printed page of a custom form (US Letter, 816 x 1056 px). The page layout and the designer's "does it fit on one page" warning both
/// read these numbers, so the warning cannot drift from what is drawn. Everything below the letterhead, title bar and the header grid (which always take the
/// same space) is a column of blocks: the groups table, the text boxes, the Remarks box and the signatories with the note under them.
/// </summary>
public static class CustomFormPage
{
    public const double PageHeight = 1056.0;

    /// <summary>The note under the signatories has to end above this line (the paper's lower margin).</summary>
    public const double BottomLimit = 1036.0;

    public const double Left = 51.3;
    public const double Right = 763.3;

    /// <summary>Where the table of groups starts, under the patient block and the title bar.</summary>
    public const double Top = 246.5;

    public const double GroupGap = 8.0;
    public const double TitleRowHeight = 21.0;
    public const double RowHeight = 20.0;

    /// <summary>The column captions (Test Name, Result Value, Reference/Normal Values): one line when a group has the page to itself, two when two groups share it.</summary>
    public static double CaptionHeight(int groups) => groups >= 2 ? 34.0 : 21.0;

    /// <summary>From the bottom of what is above to the bottom of an empty text box or Remarks box (the label and the box); a box with text grows past it.</summary>
    public const double TextBlockHeight = 71.8;

    public const double NoteLineHeight = 17.0;

    /// <summary>From the bottom of the last box to the first line of the note: just the note, signatories without / with their licence number lines.</summary>
    public const double NoteOffsetWithoutSignatories = 21.0;

    public const double NoteOffsetWithSignatories = 91.0;
    public const double NoteOffsetWithLicenses = 106.0;

    /// <summary>The height of the table: an optional title row, the captions and the longest group (every group is drawn as tall as the longest).</summary>
    public static double TableHeight(BodyDefinition body) =>
        TableHeight(body.Groups.Count, body.Groups.Any(g => !string.IsNullOrWhiteSpace(g.Title)), body.Groups.Count == 0 ? 0 : body.Groups.Max(g => g.Rows.Count));

    /// <summary>The same from its three numbers, for the page layout that works from the report.</summary>
    public static double TableHeight(int groups, bool anyTitle, int longestGroup) =>
        groups == 0 ? 0 : (anyTitle ? TitleRowHeight : 0) + CaptionHeight(groups) + (longestGroup * RowHeight);

    public static double NoteOffset(FooterDefinition footer) => NoteOffset(footer.Signatories.Count, footer.ShowLicenseNumber);

    public static double NoteOffset(int signatories, bool showLicenseNumber) =>
        signatories == 0 ? NoteOffsetWithoutSignatories : showLicenseNumber ? NoteOffsetWithLicenses : NoteOffsetWithSignatories;

    /// <summary>Where the note ends when every row is one line and every text box is empty (text that wraps makes the page longer than this).</summary>
    public static double EstimateBottom(FormDefinition design)
    {
        var y = Top + TableHeight(design.Body);
        y += (design.Body.Textboxes.Count + (design.Footer.ShowRemarks ? 1 : 0)) * TextBlockHeight;
        return y + NoteOffset(design.Footer) + NoteLineHeight + 5.0;
    }

    public static bool FitsOnOnePage(FormDefinition design) => EstimateBottom(design) <= BottomLimit;

    /// <summary>How far below the limit the page ends (0 when it fits).</summary>
    public static double Overflow(FormDefinition design) => Math.Max(0, EstimateBottom(design) - BottomLimit);
}
