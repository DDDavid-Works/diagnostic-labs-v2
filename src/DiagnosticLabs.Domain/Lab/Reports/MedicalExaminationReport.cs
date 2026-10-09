namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>MERs</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class MedicalExaminationReport : LabReportDetail
{
    public string? ContactNo { get; set; }

    public string? CivilStatus { get; set; }

    public string? ChestXray { get; set; }

    public string? ChestXrayRemarks { get; set; }

    public string? CBC { get; set; }

    public string? CBCRemarks { get; set; }

    public string? Urinalysis { get; set; }

    public string? UrinalysisRemarks { get; set; }

    public string? Fecalysis { get; set; }

    public string? FecalysisRemarks { get; set; }

    public string? HBsAg { get; set; }

    public string? HBsAgRemarks { get; set; }

    public string? DrugTest2Panel { get; set; }

    public string? DrugTest2PanelRemarks { get; set; }

    public string? DrugTest4Panel { get; set; }

    public string? DrugTest4PanelRemarks { get; set; }

    public string? Classification { get; set; }

    public string? MedicalSurgicalHistory { get; set; }

    public string? Assessment { get; set; }

    public string? AssessmentDoneBy { get; set; }

    public string? PhysicianName { get; set; }

    public string? PhysicianLicense { get; set; }

}
