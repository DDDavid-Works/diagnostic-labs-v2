namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>Serologies</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class SerologyReport : LabReportDetail
{
    public string? Test { get; set; }

    public string Result { get; set; } = string.Empty;

}
