namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>ClinicalChemistries</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class ClinicalChemistryReport : LabReportDetail
{
    public string? FBSNValue { get; set; }

    public string? FBSResult { get; set; }

    public string? TotalCholesterolNValue { get; set; }

    public string? TotalCholesterolResult { get; set; }

    public string? TriglyceridesNValue { get; set; }

    public string? TriglyceridesResult { get; set; }

    public string? HDLNValue { get; set; }

    public string? HDLResult { get; set; }

    public string? BUNNValue { get; set; }

    public string? BUNResult { get; set; }

    public string? CreatinineNValue { get; set; }

    public string? CreatinineResult { get; set; }

    public string? BloodUricAcidNValue { get; set; }

    public string? BloodUricAcidResult { get; set; }

    public string? LDLNValue { get; set; }

    public string? LDLResult { get; set; }

    public string? ALTSGPTNValue { get; set; }

    public string? ALTSGPTResult { get; set; }

}
