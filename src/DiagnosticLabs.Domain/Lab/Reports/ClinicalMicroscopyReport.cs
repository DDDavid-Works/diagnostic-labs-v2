namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>
/// Clinical Microscopy: a urine examination in three parts (physical, chemical, microscopic). Each test has a result and a reference range, saved as
/// <c>{Test}Result</c> and <c>{Test}Reference</c>. The reference ranges are what a new form starts with. The form has its own data and is not connected to
/// the Urinalysis form (the patient header lives on <see cref="LabReport"/>).
/// </summary>
public class ClinicalMicroscopyReport : LabReportDetail
{
    public string? ColorResult { get; set; }

    public string? ColorReference { get; set; }

    public string? TransparencyResult { get; set; }

    public string? TransparencyReference { get; set; }

    public string? PHResult { get; set; }

    public string? PHReference { get; set; }

    public string? SpecificGravityResult { get; set; }

    public string? SpecificGravityReference { get; set; }

    public string? ProteinResult { get; set; }

    public string? ProteinReference { get; set; }

    public string? GlucoseResult { get; set; }

    public string? GlucoseReference { get; set; }

    public string? BloodResult { get; set; }

    public string? BloodReference { get; set; }

    public string? LeukocytesResult { get; set; }

    public string? LeukocytesReference { get; set; }

    public string? NitriteResult { get; set; }

    public string? NitriteReference { get; set; }

    public string? BilirubinResult { get; set; }

    public string? BilirubinReference { get; set; }

    public string? UrobilinogenResult { get; set; }

    public string? UrobilinogenReference { get; set; }

    public string? KetonesResult { get; set; }

    public string? KetonesReference { get; set; }

    public string? WBCResult { get; set; }

    public string? WBCReference { get; set; }

    public string? RBCResult { get; set; }

    public string? RBCReference { get; set; }

    public string? MucusThreadsResult { get; set; }

    public string? MucusThreadsReference { get; set; }

    public string? EpithelialCellsResult { get; set; }

    public string? EpithelialCellsReference { get; set; }

    public string? AmorphousUratesPhosphatesResult { get; set; }

    public string? AmorphousUratesPhosphatesReference { get; set; }

    public string? BacteriaResult { get; set; }

    public string? BacteriaReference { get; set; }

    public string? CrystalsResult { get; set; }

    public string? CrystalsReference { get; set; }

    public string? CastsResult { get; set; }

    public string? CastsReference { get; set; }
}
