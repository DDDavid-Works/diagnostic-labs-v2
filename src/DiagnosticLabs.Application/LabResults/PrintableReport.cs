namespace DiagnosticLabs.Application.LabResults;

/// <summary>Which page design a report is laid out with; every result type has its own, copied from the client's paper form.</summary>
public enum ReportLayout
{
    StoolFecalysis,
    Urinalysis,
    Hematology,
    TestResult,
    AnnualPhysicalExam,
}

/// <summary>The company block at the top of a printout, from Company Setup.</summary>
public sealed record ReportLetterhead(string CompanyName, string? SubCompanyName, string? Address, string? ContactNumbers, string? Email, byte[]? Logo);

/// <summary>One line on the paper: a label and its value ("Color:" "BROWN").</summary>
public sealed record PrintLine(string Label, string? Value);

/// <summary>A label with a block of text under it (Result, Remarks).</summary>
public sealed record PrintText(string Label, string? Text);

public sealed record PrintSignatory(string Role, string? Name);

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
    IReadOnlyDictionary<string, string?>? Fields = null);
