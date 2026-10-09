namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>ClinicalChemistries2</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class ClinicalChemistry2Report : LabReportDetail
{
    public string? AlkalinePhosphataseCNValue { get; set; }

    public string? AlkalinePhosphataseCUnit { get; set; }

    public string? AlkalinePhosphataseCResults { get; set; }

    public string? AlkalinePhosphataseSNValue { get; set; }

    public string? AlkalinePhosphataseSUnit { get; set; }

    public string? AlkalinePhosphataseSResults { get; set; }

    public string? SGOTCNValue { get; set; }

    public string? SGOTCUnit { get; set; }

    public string? SGOTCResults { get; set; }

    public string? SGOTSNValue { get; set; }

    public string? SGOTSUnit { get; set; }

    public string? SGOTSResults { get; set; }

}
