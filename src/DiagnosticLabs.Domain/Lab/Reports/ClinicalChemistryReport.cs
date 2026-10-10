namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>
/// Clinical Chemistry: ten tests, each with a result, a unit and a reference range in conventional units (C...) and in S.I. units (S...).
/// Columns are named <c>{Test}{CNValue|CUnit|CResults|SNValue|SUnit|SResults}</c>, like Clinical Chemistry 2 (the patient header lives on <see cref="LabReport"/>).
/// </summary>
public class ClinicalChemistryReport : LabReportDetail
{
    public string? FastingBloodSugarCNValue { get; set; }

    public string? FastingBloodSugarCUnit { get; set; }

    public string? FastingBloodSugarCResults { get; set; }

    public string? FastingBloodSugarSNValue { get; set; }

    public string? FastingBloodSugarSUnit { get; set; }

    public string? FastingBloodSugarSResults { get; set; }

    public string? CholesterolCNValue { get; set; }

    public string? CholesterolCUnit { get; set; }

    public string? CholesterolCResults { get; set; }

    public string? CholesterolSNValue { get; set; }

    public string? CholesterolSUnit { get; set; }

    public string? CholesterolSResults { get; set; }

    public string? TriglyceridesCNValue { get; set; }

    public string? TriglyceridesCUnit { get; set; }

    public string? TriglyceridesCResults { get; set; }

    public string? TriglyceridesSNValue { get; set; }

    public string? TriglyceridesSUnit { get; set; }

    public string? TriglyceridesSResults { get; set; }

    public string? HDLCNValue { get; set; }

    public string? HDLCUnit { get; set; }

    public string? HDLCResults { get; set; }

    public string? HDLSNValue { get; set; }

    public string? HDLSUnit { get; set; }

    public string? HDLSResults { get; set; }

    public string? LDLCNValue { get; set; }

    public string? LDLCUnit { get; set; }

    public string? LDLCResults { get; set; }

    public string? LDLSNValue { get; set; }

    public string? LDLSUnit { get; set; }

    public string? LDLSResults { get; set; }

    public string? CreatinineCNValue { get; set; }

    public string? CreatinineCUnit { get; set; }

    public string? CreatinineCResults { get; set; }

    public string? CreatinineSNValue { get; set; }

    public string? CreatinineSUnit { get; set; }

    public string? CreatinineSResults { get; set; }

    public string? BloodUreaNitrogenCNValue { get; set; }

    public string? BloodUreaNitrogenCUnit { get; set; }

    public string? BloodUreaNitrogenCResults { get; set; }

    public string? BloodUreaNitrogenSNValue { get; set; }

    public string? BloodUreaNitrogenSUnit { get; set; }

    public string? BloodUreaNitrogenSResults { get; set; }

    public string? BloodUricAcidCNValue { get; set; }

    public string? BloodUricAcidCUnit { get; set; }

    public string? BloodUricAcidCResults { get; set; }

    public string? BloodUricAcidSNValue { get; set; }

    public string? BloodUricAcidSUnit { get; set; }

    public string? BloodUricAcidSResults { get; set; }

    public string? ALTSGPTCNValue { get; set; }

    public string? ALTSGPTCUnit { get; set; }

    public string? ALTSGPTCResults { get; set; }

    public string? ALTSGPTSNValue { get; set; }

    public string? ALTSGPTSUnit { get; set; }

    public string? ALTSGPTSResults { get; set; }

    public string? ASTSGOTCNValue { get; set; }

    public string? ASTSGOTCUnit { get; set; }

    public string? ASTSGOTCResults { get; set; }

    public string? ASTSGOTSNValue { get; set; }

    public string? ASTSGOTSUnit { get; set; }

    public string? ASTSGOTSResults { get; set; }
}
