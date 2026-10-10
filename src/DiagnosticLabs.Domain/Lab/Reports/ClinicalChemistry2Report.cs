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

    public string? ASTSGOTCNValue { get; set; }

    public string? ASTSGOTCUnit { get; set; }

    public string? ASTSGOTCResults { get; set; }

    public string? ASTSGOTSNValue { get; set; }

    public string? ASTSGOTSUnit { get; set; }

    public string? ASTSGOTSResults { get; set; }

}
