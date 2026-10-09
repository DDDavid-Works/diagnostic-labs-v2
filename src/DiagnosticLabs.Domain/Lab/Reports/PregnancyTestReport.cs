namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>PregnancyTests</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class PregnancyTestReport : LabReportDetail
{
    public string Result { get; set; } = string.Empty;

}
