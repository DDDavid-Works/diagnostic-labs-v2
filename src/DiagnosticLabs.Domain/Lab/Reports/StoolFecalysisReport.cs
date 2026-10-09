namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>StoolFecalyses</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class StoolFecalysisReport : LabReportDetail
{
    public string? Color { get; set; }

    public string? Consistency { get; set; }

    public string Result { get; set; } = string.Empty;

}
