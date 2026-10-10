namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>
/// Physical Examination (the printed "Laboratory Results" sheet): a complete blood count, blood typing, urinalysis and fecalysis section, each with its own date,
/// and an Others box. It has its own data and is not connected to the Hematology, Urinalysis or Fecalysis forms (the patient header lives on <see cref="LabReport"/>).
/// </summary>
public class PhysicalExaminationReport : LabReportDetail
{
    // Complete blood count: normal values (what a new form starts with) and the patient result of each test; Hematocrit and Hemoglobin have a male and a female normal value.
    public DateOnly? CbcDate { get; set; }

    public DateOnly? BloodTypingDate { get; set; }

    public DateOnly? UrinalysisDate { get; set; }

    public DateOnly? FecalysisDate { get; set; }

    public string? HematocritNValue { get; set; }

    public string? HematocritFemaleNValue { get; set; }

    public string? HematocritResult { get; set; }

    public string? HemoglobinNValue { get; set; }

    public string? HemoglobinFemaleNValue { get; set; }

    public string? HemoglobinResult { get; set; }

    public string? WBCCountNValue { get; set; }

    public string? WBCCountResult { get; set; }

    public string? SegmentersNValue { get; set; }

    public string? SegmentersResult { get; set; }

    public string? LymphocytesNValue { get; set; }

    public string? LymphocytesResult { get; set; }

    public string? EosinophilsNValue { get; set; }

    public string? EosinophilsResult { get; set; }

    public string? MonocytesNValue { get; set; }

    public string? MonocytesResult { get; set; }

    public string? BasophilsNValue { get; set; }

    public string? BasophilsResult { get; set; }

    public string? StabNValue { get; set; }

    public string? StabResult { get; set; }

    // Blood typing
    public string? BloodTyping { get; set; }

    public string? RhTyping { get; set; }

    // Urinalysis
    public string? UrineColor { get; set; }

    public string? UrineAppearance { get; set; }

    public string? UrineReaction { get; set; }

    public string? UrineSPGravity { get; set; }

    public string? UrineAlbumin { get; set; }

    public string? UrineSugar { get; set; }

    public string? UrinePusCells { get; set; }

    public string? UrineRedCells { get; set; }

    public string? UrineMucusThreads { get; set; }

    public string? UrineEpithelialCells { get; set; }

    public string? UrineAmorphousUratesPO4 { get; set; }

    public string? UrineBacteria { get; set; }

    public string? UrineCrystals { get; set; }

    public string? UrineCasts { get; set; }

    public string? FecalysisColor { get; set; }

    public string? FecalysisConsistency { get; set; }

    public string? UrineOthers { get; set; }

    public string? FecalysisResult { get; set; }

    // Others
    public string? Others { get; set; }
}
