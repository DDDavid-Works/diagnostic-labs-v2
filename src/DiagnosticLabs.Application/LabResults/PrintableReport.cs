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

    /// <summary>The Medical Examination Report: two pages (the history and examination, then the results, classification and sign-off).</summary>
    MedicalExamReport,
}

/// <summary>The company block at the top of a printout, from Company Setup.</summary>
public sealed record ReportLetterhead(string CompanyName, string? SubCompanyName, string? Address, string? ContactNumbers, string? Email, byte[]? Logo);

/// <summary>One line on the paper: a label and its value ("Color:" "BROWN").</summary>
public sealed record PrintLine(string Label, string? Value);

/// <summary>A label with a block of text under it (Result, Remarks).</summary>
public sealed record PrintText(string Label, string? Text);

/// <summary>
/// A person who signs the printout. <paramref name="List"/> is the Entry Builder list the name was picked from (where its signature is looked up
/// first); <paramref name="Signature"/> is the stored signature, a PNG printed above the name, or <c>null</c>.
/// </summary>
public sealed record PrintSignatory(string Role, string? Name, string? LicenseNo = null, Entries.EntryField? List = null, byte[]? Signature = null);

/// <summary>
/// Everything a printout needs, already worked out and free of any UI, so what appears on paper is covered by tests.
/// The WPF layer only lays it out on a page. <paramref name="NextPage"/> is a page printed right after this one, for a form whose pages
/// have designs of their own (the Annual Physical Exam and its Page 2).
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
    CustomFormPrint? Custom = null,
    PrintableReport? NextPage = null)
{
    /// <summary>True when a signature would be printed on this report or the page that follows it.</summary>
    public bool HasSignatures => Signatories.Any(s => s.Signature is not null) || NextPage?.HasSignatures == true;

    /// <summary>The same report printed with no signatures (the "Print signatures" box of the preview switched off).</summary>
    public PrintableReport WithoutSignatures() => this with
    {
        Signatories = [.. Signatories.Select(s => s.Signature is null ? s : s with { Signature = null })],
        NextPage = NextPage?.WithoutSignatures(),
    };
}

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
