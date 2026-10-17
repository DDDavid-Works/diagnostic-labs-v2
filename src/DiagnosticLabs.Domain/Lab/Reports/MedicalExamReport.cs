namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>
/// The Medical Examination Report (the two-page pre-employment form: applicant history, physical examination, laboratory results, classification and sign-off).
/// Every field is a short text column named like its entry in <c>MedicalExamFields</c>: a tick is saved as its caption ("No"/"Yes", "N"/"F"), a classification as "A".."D" or "Pending".
/// It is a form of its own: it has no connection with the Annual Physical Exam pages (the patient header lives on <see cref="LabReport"/>).
/// </summary>
public class MedicalExamReport : LabReportDetail
{
    public string? CivilStatus { get; set; }

    public string? ContactNo { get; set; }

    public string? HxEyeDisease { get; set; }

    public string? HxEarInfection { get; set; }

    public string? HxHearingDefect { get; set; }

    public string? HxSinusitis { get; set; }

    public string? HxNoseBleeding { get; set; }

    public string? HxCoughColds { get; set; }

    public string? HxTonsillitisPharyngitis { get; set; }

    public string? HxNeckMassGoiter { get; set; }

    public string? HxAsthma { get; set; }

    public string? HxTuberculosis { get; set; }

    public string? HxPneumonia { get; set; }

    public string? HxDifficultyBreathing { get; set; }

    public string? HxHypertension { get; set; }

    public string? HxHypotension { get; set; }

    public string? HxAnemia { get; set; }

    public string? HxChestPainPalpitations { get; set; }

    public string? HxHeartDisease { get; set; }

    public string? HxBreastMass { get; set; }

    public string? HxDiabetes { get; set; }

    public string? HxUlcerHyperacidity { get; set; }

    public string? HxAbdominalPain { get; set; }

    public string? HxDiarrhea { get; set; }

    public string? HxKidneyDiseaseUti { get; set; }

    public string? HxHernia { get; set; }

    public string? HxHemorrhoids { get; set; }

    public string? HxLossOfConsciousness { get; set; }

    public string? HxBackPainScoliosis { get; set; }

    public string? HxHeadacheMigraine { get; set; }

    public string? HxDizzinessVertigo { get; set; }

    public string? HxEpilepsyConvulsion { get; set; }

    public string? HxEasyFatigability { get; set; }

    public string? HxArthritisJointPain { get; set; }

    public string? HxFracture { get; set; }

    public string? HxMeasles { get; set; }

    public string? HxChickenPox { get; set; }

    public string? HxMumps { get; set; }

    public string? HxHepatitis { get; set; }

    public string? HxMalaria { get; set; }

    public string? HxStd { get; set; }

    public string? HxAllergies { get; set; }

    public string? HxSkinDisease { get; set; }

    public string? HxPreviousAccident { get; set; }

    public string? HxPreviousHospitalization { get; set; }

    public string? HxPreviousOperation { get; set; }

    public string? HxOthers { get; set; }

    public string? HxMedication { get; set; }

    public string? FamCancer { get; set; }

    public string? FamHypertension { get; set; }

    public string? FamDiabetes { get; set; }

    public string? FamOthers { get; set; }

    public string? MenstrualPattern { get; set; }

    public string? Lmp { get; set; }

    public string? Dysmenorrhea { get; set; }

    public string? PregnantNow { get; set; }

    public string? NumPregnancies { get; set; }

    public string? NumMiscarriages { get; set; }

    public string? Myoma { get; set; }

    public string? OvarianCyst { get; set; }

    public string? ObOthers { get; set; }

    public string? SmokingStatus { get; set; }

    public string? SmokingSince { get; set; }

    public string? SticksPerDay { get; set; }

    public string? DrinkingStatus { get; set; }

    public string? DrinkingSince { get; set; }

    public string? Bottles { get; set; }

    public string? DrinkingFrequency { get; set; }

    public string? Bp1 { get; set; }

    public string? Bp2 { get; set; }

    public string? Pulse1 { get; set; }

    public string? Pulse2 { get; set; }

    public string? Temperature { get; set; }

    public string? Height { get; set; }

    public string? Weight { get; set; }

    public string? BmiCategory { get; set; }

    public string? VisionFarWithR { get; set; }

    public string? VisionFarWithL { get; set; }

    public string? VisionFarWithoutR { get; set; }

    public string? VisionFarWithoutL { get; set; }

    public string? Ishihara { get; set; }

    public string? RosSkin { get; set; }

    public string? RosHeadScalp { get; set; }

    public string? RosEyes { get; set; }

    public string? RosEars { get; set; }

    public string? RosNose { get; set; }

    public string? RosTeethThroat { get; set; }

    public string? RosNeckThyroid { get; set; }

    public string? RosThoraxBreast { get; set; }

    public string? RosHeartLungs { get; set; }

    public string? RosAbdomen { get; set; }

    public string? RosInguinalGenitalia { get; set; }

    public string? RosExtremitiesSpine { get; set; }

    public string? RosTattoo { get; set; }

    public string? RosMassCyst { get; set; }

    public string? RosOthers { get; set; }

    public string? RosOthersNote { get; set; }

    public string? Findings { get; set; }

    public string? LabChestXray { get; set; }

    public string? LabChestXrayNote { get; set; }

    public string? LabCbc { get; set; }

    public string? LabCbcNote { get; set; }

    public string? LabUrinalysis { get; set; }

    public string? LabUrinalysisNote { get; set; }

    public string? LabFecalysis { get; set; }

    public string? LabFecalysisNote { get; set; }

    public string? Classification { get; set; }

    public string? Assessment { get; set; }

    public string? VitalSignsBy { get; set; }

    public string? VitalSignsByLicense { get; set; }

    public string? InterviewBy { get; set; }

    public string? InterviewByLicense { get; set; }

    public string? AssessmentBy { get; set; }

    public string? AssessmentByLicense { get; set; }

    public string? NotedBy { get; set; }

    public string? NotedByLicense { get; set; }
}
