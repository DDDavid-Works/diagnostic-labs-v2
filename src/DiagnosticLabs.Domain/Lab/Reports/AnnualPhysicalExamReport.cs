namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>APEs</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class AnnualPhysicalExamReport : LabReportDetail
{
    public string? DepartmentOrAgency { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? CivilStatus { get; set; }

    public string? ContactNo { get; set; }

    public string? ENT { get; set; }

    public string? Gastroenterology { get; set; }

    public string? Respiratory { get; set; }

    public string? IntegumentarySkin { get; set; }

    public string? Cardiology { get; set; }

    public string? Psychology { get; set; }

    public string? Endocrinology { get; set; }

    public string? OBGyneUrology { get; set; }

    public string? Muscoloskeletal { get; set; }

    public string? InfectiousCommunicable { get; set; }

    public string? Neurological { get; set; }

    public string? Surgical { get; set; }

    public string? OthersPast { get; set; }

    public string? Medications { get; set; }

    public string? ReviewOfSystems { get; set; }

    public string? Allergies { get; set; }

    public bool? IsSmoking { get; set; }

    public string? SmokingSinceWhen { get; set; }

    public int? NumberOfSticksPerDay { get; set; }

    public bool? IsDrinking { get; set; }

    public string? DrinkingSinceWhen { get; set; }

    public int? NumberOfBottles { get; set; }

    public string? DrinkingFrequency { get; set; }

    public string? LMP { get; set; }

    public string LMPType { get; set; } = string.Empty;

    public string? BP1st { get; set; }

    public string? BP2nd { get; set; }

    public string? CardiacRate1st { get; set; }

    public string? CardiacRate2nd { get; set; }

    public string? Height { get; set; }

    public string? Weight { get; set; }

    public string? BMICategory { get; set; }

    public string? VARightEyeWGlasses { get; set; }

    public string? VARightEyeWOGlasses { get; set; }

    public string? VALeftEyeWGlasses { get; set; }

    public string? VALeftEyeWOGlasses { get; set; }

    public string? VisualAcuity { get; set; }

    public string? Skin { get; set; }

    public string? HeadScalp { get; set; }

    public string? Eyes { get; set; }

    public string? Ears { get; set; }

    public string? Nose { get; set; }

    public string? TeethTonsilsThroatPharynx { get; set; }

    public string? NeckLymphNodesThyroid { get; set; }

    public string? ThoraxBreast { get; set; }

    public string? HeartLungs { get; set; }

    public string? AbdomenLiverSpleen { get; set; }

    public string? InguinalAreaGenitalsAnus { get; set; }

    public string? ExtremetiesSpine { get; set; }

    public string? Tattoo { get; set; }

    public string? MassCyst { get; set; }

    public string? OthersPE { get; set; }

    public string? Findings { get; set; }

    public string VitalSignsBy { get; set; } = string.Empty;

    public string HeightWeightBy { get; set; } = string.Empty;

}
