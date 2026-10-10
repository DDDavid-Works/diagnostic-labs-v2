namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>Hematologies</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class HematologyReport : LabReportDetail
{
    public string? HematocritNValue { get; set; }

    public string? HematocritResult { get; set; }

    public string? HemoglobinNValue { get; set; }

    public string? HemoglobinResult { get; set; }

    public string? WBCCountNValue { get; set; }

    public string? WBCCountResult { get; set; }

    public string? NeutrophilsNValue { get; set; }

    public string? NeutrophilsResult { get; set; }

    public string? LymphocytesNValue { get; set; }

    public string? LymphocytesResult { get; set; }

    public string? EosinophilsNValue { get; set; }

    public string? EosinophilsResult { get; set; }

    public string? MonocytesNValue { get; set; }

    public string? MonocytesResult { get; set; }

    public string? BasophilsNValue { get; set; }

    public string? BasophilsResult { get; set; }

    public string? StabNValue { get; set; }

    public string? StabResult { get; set; }

    public string? PlateletCountNValue { get; set; }

    public string? PlateletCountResult { get; set; }

}
