namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>ClinicalChemistries1</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class ClinicalChemistry1Report : LabReportDetail
{
    public string? Test { get; set; }

    public string Result { get; set; } = string.Empty;

}
