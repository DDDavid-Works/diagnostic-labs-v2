namespace DiagnosticLabs.Application.LabResults;

/// <summary>Which page design a report is laid out with; every result type has its own, copied from the client's paper form.</summary>
public enum ReportLayout
{
    StoolFecalysis,
    Urinalysis,
    Hematology,
    TestResult,
    ClinicalChemistry,
    ClinicalChemistry2,
    MedicalExamination,
    AnnualPhysicalExam,
    PhysicalExamination,
    ClinicalMicroscopy,

    /// <summary>A form made in the Custom Lab Form designer: its page is built from the design that travels with the report.</summary>
    CustomForm,
}

/// <summary>The company block at the top of a printout, from Company Setup.</summary>
public sealed record ReportLetterhead(string CompanyName, string? SubCompanyName, string? Address, string? ContactNumbers, string? Email, byte[]? Logo);

/// <summary>One line on the paper: a label and its value ("Color:" "BROWN").</summary>
public sealed record PrintLine(string Label, string? Value);

/// <summary>A label with a block of text under it (Result, Remarks).</summary>
public sealed record PrintText(string Label, string? Text);

public sealed record PrintSignatory(string Role, string? Name, string? LicenseNo = null);

/// <summary>
/// Everything a printout needs, already worked out and free of any UI, so what appears on paper is covered by tests.
/// The WPF layer only lays it out on a page.
/// </summary>
public sealed record PrintableReport(
    ReportLayout Layout,
    string Title,
    ReportLetterhead Letterhead,
    IReadOnlyList<PrintLine> PatientLines,
    IReadOnlyList<PrintLine> ResultLines,
    IReadOnlyList<PrintText> ResultTexts,
    IReadOnlyList<PrintSignatory> Signatories,
    string FooterNote,
    IReadOnlyDictionary<string, string?>? Fields = null,
    CustomFormPrint? Custom = null);

/// <summary>One row of a group on the printout of a custom form: a test with its result and reference value, or a heading line.</summary>
public sealed record CustomFormPrintRow(bool IsSection, string Name, string? Result, string? Reference);

public sealed record CustomFormPrintGroup(string? Title, bool ShowReference, IReadOnlyList<CustomFormPrintRow> Rows);

public sealed record CustomFormPrintText(string Label, string? Text);

/// <summary>
/// What the page of a form made in the Custom Lab Form designer is built from. <paramref name="HeaderCells"/> are the nine cells of the header grid read row by row
/// (a label and its value, or <c>null</c> for an empty cell); the Remarks text and the signatories travel in the usual places of the report.
/// </summary>
public sealed record CustomFormPrint(
    IReadOnlyList<PrintLine?> HeaderCells,
    IReadOnlyList<CustomFormPrintGroup> Groups,
    IReadOnlyList<CustomFormPrintText> Textboxes,
    bool ShowRemarks,
    bool ShowLicenseNumber);
