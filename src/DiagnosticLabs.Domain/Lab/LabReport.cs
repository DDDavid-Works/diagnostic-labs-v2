using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;

namespace DiagnosticLabs.Domain.Lab;

public enum LabReportType
{
    StoolFecalysis = 1,
    Urinalysis = 2,
    Hematology = 3,
    Immunology = 4,
    Serology = 5,
    PregnancyTest = 6,
    ClinicalChemistry = 7,
    ClinicalChemistry1 = 8,
    ClinicalChemistry2 = 9,
    MedicalExamination = 10,
    AnnualPhysicalExam = 12,
}

/// <summary>
/// The header shared by every lab report. The type-specific result fields live in a 1:1 detail
/// table (see <see cref="LabReportDetail"/> and the classes in <c>Lab.Reports</c>).
/// Patient fields are a snapshot taken when the report was issued.
/// </summary>
public class LabReport : SoftDeletableEntity
{
    public LabReportType ReportType { get; set; }

    public long PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    /// <summary>Null for results recorded before the registration workflow existed (legacy data); new reports always have one.</summary>
    public long? PatientRegistrationId { get; set; }

    public PatientRegistration? PatientRegistration { get; set; }

    public string PatientCode { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public string? Age { get; set; }

    public string? Sex { get; set; }

    public string? CompanyOrPhysician { get; set; }

    public DateTime DateRequested { get; set; }

    public string? Remarks { get; set; }

    public string? MedicalTechnologist { get; set; }

    public string? Pathologist { get; set; }

    public LabReportPhoto? Photo { get; set; }
}

public class LabReportPhoto
{
    public long LabReportId { get; set; }

    public byte[] Content { get; set; } = [];

    public string? ContentType { get; set; }
}

/// <summary>Base of the per-type result classes; shares its key with <see cref="LabReport"/>.</summary>
public abstract class LabReportDetail
{
    public long LabReportId { get; set; }

    public LabReport LabReport { get; set; } = null!;
}
