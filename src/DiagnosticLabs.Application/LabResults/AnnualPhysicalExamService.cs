using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>
/// Every field of the Annual Physical Exam that is not part of the shared result header. Grouped as on the form: who the person is,
/// past medical history, present medical findings, vital signs and visual acuity, the physical examination (N or F) and the closing notes.
/// </summary>
public sealed record AnnualPhysicalExamData
{
    // the person (Name, Age, Gender and Company Name are in the shared header)
    public string? DepartmentOrAgency { get; init; }

    public DateOnly? BirthDate { get; init; }

    public string? CivilStatus { get; init; }

    public string? ContactNo { get; init; }

    // past medical history
    public string? ENT { get; init; }

    public string? Gastroenterology { get; init; }

    public string? Respiratory { get; init; }

    public string? IntegumentarySkin { get; init; }

    public string? Cardiology { get; init; }

    public string? Psychology { get; init; }

    public string? Endocrinology { get; init; }

    public string? OBGyneUrology { get; init; }

    public string? Muscoloskeletal { get; init; }

    public string? InfectiousCommunicable { get; init; }

    public string? Neurological { get; init; }

    public string? Surgical { get; init; }

    public string? OthersPast { get; init; }

    public string? Medications { get; init; }

    public string? ReviewOfSystems { get; init; }

    public string? Allergies { get; init; }

    // present medical findings
    public bool? IsSmoking { get; init; }

    public string? SmokingSinceWhen { get; init; }

    public int? NumberOfSticksPerDay { get; init; }

    public bool? IsDrinking { get; init; }

    public string? DrinkingSinceWhen { get; init; }

    public int? NumberOfBottles { get; init; }

    /// <summary>Daily, Weekly or Occasional.</summary>
    public string? DrinkingFrequency { get; init; }

    public string? LMP { get; init; }

    /// <summary>Regular or Irregular.</summary>
    public string? LMPType { get; init; }

    // vital signs
    public string? BP1st { get; init; }

    public string? BP2nd { get; init; }

    public string? CardiacRate1st { get; init; }

    public string? CardiacRate2nd { get; init; }

    public string? Height { get; init; }

    public string? Weight { get; init; }

    public string? BMICategory { get; init; }

    // visual acuity
    public string? VARightEyeWGlasses { get; init; }

    public string? VARightEyeWOGlasses { get; init; }

    public string? VALeftEyeWGlasses { get; init; }

    public string? VALeftEyeWOGlasses { get; init; }

    /// <summary>Normal, EOR or Corrected.</summary>
    public string? VisualAcuity { get; init; }

    // physical examination: "N" (normal or none), "F" (with findings) or nothing
    public string? Skin { get; init; }

    public string? HeadScalp { get; init; }

    public string? Eyes { get; init; }

    public string? Ears { get; init; }

    public string? Nose { get; init; }

    public string? TeethTonsilsThroatPharynx { get; init; }

    public string? NeckLymphNodesThyroid { get; init; }

    public string? ThoraxBreast { get; init; }

    public string? HeartLungs { get; init; }

    public string? AbdomenLiverSpleen { get; init; }

    public string? InguinalAreaGenitalsAnus { get; init; }

    public string? ExtremetiesSpine { get; init; }

    public string? Tattoo { get; init; }

    public string? MassCyst { get; init; }

    public string? OthersPE { get; init; }

    // closing notes
    public string? Findings { get; init; }

    public string? VitalSignsBy { get; init; }

    public string? HeightWeightBy { get; init; }
}

public sealed record AnnualPhysicalExamDetails(long Id, LabResultHeader Header, AnnualPhysicalExamData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record AnnualPhysicalExamInput(long Id, LabResultHeader Header, AnnualPhysicalExamData Data, byte[]? RowVersion) : ICrudInput;

public interface IAnnualPhysicalExamService
    : ICrudService<LabResultListItem, AnnualPhysicalExamDetails, AnnualPhysicalExamInput>, ILabResultPrinting;

public sealed class AnnualPhysicalExamService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<AnnualPhysicalExamInput, AnnualPhysicalExamDetails, AnnualPhysicalExamReport>(
        db, currentUser, clock, ModuleIds.AnnualPhysicalExam, "Annual Physical Exam", LabReportType.AnnualPhysicalExam),
      IAnnualPhysicalExamService
{
    public static readonly string[] DrinkingFrequencies = ["Daily", "Weekly", "Occasional"];
    public static readonly string[] LmpTypes = ["Regular", "Irregular"];
    public static readonly string[] VisualAcuities = ["Normal", "EOR", "Corrected"];

    protected override string Title => "Annual Physical Exam";

    protected override ReportLayout Layout => ReportLayout.AnnualPhysicalExam;

    protected override LabResultHeader HeaderOf(AnnualPhysicalExamInput input) => input.Header;

    // ---------------------------------------------------------------- validation

    protected override IEnumerable<string> ValidateDetail(AnnualPhysicalExamInput input)
    {
        var d = input.Data;
        var errors = new List<string>();

        void Text(string label, string? value, int max) => Max(errors, value, max, label);

        Text("Department/Agency", d.DepartmentOrAgency, 100);
        Text("Civil status", d.CivilStatus, 50);
        Text("Contact number", d.ContactNo, 100);

        foreach (var (label, value) in new (string, string?)[]
                 {
                     ("ENT", d.ENT), ("Gastroenterology", d.Gastroenterology), ("Respiratory", d.Respiratory), ("Integumentary/Skin", d.IntegumentarySkin),
                     ("Cardiology", d.Cardiology), ("Psychology", d.Psychology), ("Endocrinology", d.Endocrinology), ("OB-Gyne/Urology", d.OBGyneUrology),
                     ("Musculo-skeletal", d.Muscoloskeletal), ("Infectious/Communicable", d.InfectiousCommunicable), ("Neurological", d.Neurological),
                     ("Surgical", d.Surgical), ("Others (past medical history)", d.OthersPast), ("Medications", d.Medications),
                     ("Review of systems", d.ReviewOfSystems), ("Allergies", d.Allergies), ("Smoking since when", d.SmokingSinceWhen),
                     ("Drinking since when", d.DrinkingSinceWhen),
                 })
        {
            Text(label, value, 200);
        }

        Text("LMP", d.LMP, 50);
        foreach (var (label, value) in new (string, string?)[]
                 {
                     ("BP reading 1st", d.BP1st), ("BP reading 2nd", d.BP2nd), ("Cardiac rate 1st", d.CardiacRate1st),
                     ("Cardiac rate 2nd", d.CardiacRate2nd), ("Height", d.Height), ("Weight", d.Weight),
                 })
        {
            Text(label, value, 20);
        }

        Text("BMI category", d.BMICategory, 50);
        Text("Visual acuity right eye with eyeglasses", d.VARightEyeWGlasses, 50);
        Text("Visual acuity right eye without eyeglasses", d.VARightEyeWOGlasses, 50);
        Text("Visual acuity left eye with eyeglasses", d.VALeftEyeWGlasses, 50);
        Text("Visual acuity left eye without eyeglasses", d.VALeftEyeWOGlasses, 50);
        Text("Findings", d.Findings, 500);
        Text("Vital signs done by", d.VitalSignsBy, 50);
        Text("Height and weight done by", d.HeightWeightBy, 50);

        OneOf(errors, "Drinking frequency", d.DrinkingFrequency, DrinkingFrequencies);
        OneOf(errors, "LMP type", d.LMPType, LmpTypes);
        OneOf(errors, "Visual acuity", d.VisualAcuity, VisualAcuities);

        foreach (var (label, value) in PhysicalExam(d))
            OneOf(errors, label, value, ["N", "F"]);

        if (d.NumberOfSticksPerDay is < 0)
            errors.Add("The number of sticks per day must not be negative.");
        if (d.NumberOfBottles is < 0)
            errors.Add("The number of bottles must not be negative.");

        return errors;
    }

    private static void OneOf(List<string> errors, string label, string? value, string[] allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value.Trim(), StringComparer.Ordinal))
            errors.Add($"{label} must be one of: {string.Join(", ", allowed)}.");
    }

    private static IEnumerable<(string Label, string? Value)> PhysicalExam(AnnualPhysicalExamData d) =>
    [
        ("Skin", d.Skin), ("Head, scalp", d.HeadScalp), ("Eyes", d.Eyes), ("Ears", d.Ears), ("Nose", d.Nose),
        ("Teeth, tonsils, throat, pharynx", d.TeethTonsilsThroatPharynx), ("Neck, lymph nodes, thyroid", d.NeckLymphNodesThyroid),
        ("Thorax, breast", d.ThoraxBreast), ("Heart, lungs", d.HeartLungs), ("Abdomen, liver, spleen", d.AbdomenLiverSpleen),
        ("Inguinal area, genitals, anus", d.InguinalAreaGenitalsAnus), ("Extremities, spine", d.ExtremetiesSpine), ("Tattoo", d.Tattoo),
        ("Mass, cyst", d.MassCyst), ("Others (physical examination)", d.OthersPE),
    ];

    // ---------------------------------------------------------------- saving and reading

    protected override void ApplyDetail(AnnualPhysicalExamReport e, AnnualPhysicalExamInput input)
    {
        var d = input.Data;

        e.DepartmentOrAgency = Clean(d.DepartmentOrAgency);
        e.BirthDate = d.BirthDate?.ToDateTime(TimeOnly.MinValue);
        e.CivilStatus = Clean(d.CivilStatus);
        e.ContactNo = Clean(d.ContactNo);

        e.ENT = Clean(d.ENT);
        e.Gastroenterology = Clean(d.Gastroenterology);
        e.Respiratory = Clean(d.Respiratory);
        e.IntegumentarySkin = Clean(d.IntegumentarySkin);
        e.Cardiology = Clean(d.Cardiology);
        e.Psychology = Clean(d.Psychology);
        e.Endocrinology = Clean(d.Endocrinology);
        e.OBGyneUrology = Clean(d.OBGyneUrology);
        e.Muscoloskeletal = Clean(d.Muscoloskeletal);
        e.InfectiousCommunicable = Clean(d.InfectiousCommunicable);
        e.Neurological = Clean(d.Neurological);
        e.Surgical = Clean(d.Surgical);
        e.OthersPast = Clean(d.OthersPast);
        e.Medications = Clean(d.Medications);
        e.ReviewOfSystems = Clean(d.ReviewOfSystems);
        e.Allergies = Clean(d.Allergies);

        // Smoking and drinking details only make sense for someone who does; a "No" clears them.
        e.IsSmoking = d.IsSmoking;
        e.SmokingSinceWhen = d.IsSmoking == true ? Clean(d.SmokingSinceWhen) : null;
        e.NumberOfSticksPerDay = d.IsSmoking == true ? d.NumberOfSticksPerDay : null;
        e.IsDrinking = d.IsDrinking;
        e.DrinkingSinceWhen = d.IsDrinking == true ? Clean(d.DrinkingSinceWhen) : null;
        e.NumberOfBottles = d.IsDrinking == true ? d.NumberOfBottles : null;
        e.DrinkingFrequency = d.IsDrinking == true ? Clean(d.DrinkingFrequency) : null;
        e.LMP = Clean(d.LMP);
        e.LMPType = Clean(d.LMPType) ?? string.Empty;

        e.BP1st = Clean(d.BP1st);
        e.BP2nd = Clean(d.BP2nd);
        e.CardiacRate1st = Clean(d.CardiacRate1st);
        e.CardiacRate2nd = Clean(d.CardiacRate2nd);
        e.Height = Clean(d.Height);
        e.Weight = Clean(d.Weight);
        e.BMICategory = Clean(d.BMICategory);

        e.VARightEyeWGlasses = Clean(d.VARightEyeWGlasses);
        e.VARightEyeWOGlasses = Clean(d.VARightEyeWOGlasses);
        e.VALeftEyeWGlasses = Clean(d.VALeftEyeWGlasses);
        e.VALeftEyeWOGlasses = Clean(d.VALeftEyeWOGlasses);
        e.VisualAcuity = Clean(d.VisualAcuity);

        e.Skin = Clean(d.Skin);
        e.HeadScalp = Clean(d.HeadScalp);
        e.Eyes = Clean(d.Eyes);
        e.Ears = Clean(d.Ears);
        e.Nose = Clean(d.Nose);
        e.TeethTonsilsThroatPharynx = Clean(d.TeethTonsilsThroatPharynx);
        e.NeckLymphNodesThyroid = Clean(d.NeckLymphNodesThyroid);
        e.ThoraxBreast = Clean(d.ThoraxBreast);
        e.HeartLungs = Clean(d.HeartLungs);
        e.AbdomenLiverSpleen = Clean(d.AbdomenLiverSpleen);
        e.InguinalAreaGenitalsAnus = Clean(d.InguinalAreaGenitalsAnus);
        e.ExtremetiesSpine = Clean(d.ExtremetiesSpine);
        e.Tattoo = Clean(d.Tattoo);
        e.MassCyst = Clean(d.MassCyst);
        e.OthersPE = Clean(d.OthersPE);

        e.Findings = Clean(d.Findings);
        e.VitalSignsBy = Clean(d.VitalSignsBy) ?? string.Empty;
        e.HeightWeightBy = Clean(d.HeightWeightBy) ?? string.Empty;
    }

    protected override AnnualPhysicalExamDetails BuildDetails(LabReport report, LabResultHeader header, AnnualPhysicalExamReport e) => new(
        report.Id,
        header,
        new AnnualPhysicalExamData
        {
            DepartmentOrAgency = e.DepartmentOrAgency,
            BirthDate = e.BirthDate is { } birth ? DateOnly.FromDateTime(birth) : null,
            CivilStatus = e.CivilStatus,
            ContactNo = e.ContactNo,
            ENT = e.ENT,
            Gastroenterology = e.Gastroenterology,
            Respiratory = e.Respiratory,
            IntegumentarySkin = e.IntegumentarySkin,
            Cardiology = e.Cardiology,
            Psychology = e.Psychology,
            Endocrinology = e.Endocrinology,
            OBGyneUrology = e.OBGyneUrology,
            Muscoloskeletal = e.Muscoloskeletal,
            InfectiousCommunicable = e.InfectiousCommunicable,
            Neurological = e.Neurological,
            Surgical = e.Surgical,
            OthersPast = e.OthersPast,
            Medications = e.Medications,
            ReviewOfSystems = e.ReviewOfSystems,
            Allergies = e.Allergies,
            IsSmoking = e.IsSmoking,
            SmokingSinceWhen = e.SmokingSinceWhen,
            NumberOfSticksPerDay = e.NumberOfSticksPerDay,
            IsDrinking = e.IsDrinking,
            DrinkingSinceWhen = e.DrinkingSinceWhen,
            NumberOfBottles = e.NumberOfBottles,
            DrinkingFrequency = e.DrinkingFrequency,
            LMP = e.LMP,
            LMPType = string.IsNullOrEmpty(e.LMPType) ? null : e.LMPType,
            BP1st = e.BP1st,
            BP2nd = e.BP2nd,
            CardiacRate1st = e.CardiacRate1st,
            CardiacRate2nd = e.CardiacRate2nd,
            Height = e.Height,
            Weight = e.Weight,
            BMICategory = e.BMICategory,
            VARightEyeWGlasses = e.VARightEyeWGlasses,
            VARightEyeWOGlasses = e.VARightEyeWOGlasses,
            VALeftEyeWGlasses = e.VALeftEyeWGlasses,
            VALeftEyeWOGlasses = e.VALeftEyeWOGlasses,
            VisualAcuity = e.VisualAcuity,
            Skin = e.Skin,
            HeadScalp = e.HeadScalp,
            Eyes = e.Eyes,
            Ears = e.Ears,
            Nose = e.Nose,
            TeethTonsilsThroatPharynx = e.TeethTonsilsThroatPharynx,
            NeckLymphNodesThyroid = e.NeckLymphNodesThyroid,
            ThoraxBreast = e.ThoraxBreast,
            HeartLungs = e.HeartLungs,
            AbdomenLiverSpleen = e.AbdomenLiverSpleen,
            InguinalAreaGenitalsAnus = e.InguinalAreaGenitalsAnus,
            ExtremetiesSpine = e.ExtremetiesSpine,
            Tattoo = e.Tattoo,
            MassCyst = e.MassCyst,
            OthersPE = e.OthersPE,
            Findings = e.Findings,
            VitalSignsBy = string.IsNullOrEmpty(e.VitalSignsBy) ? null : e.VitalSignsBy,
            HeightWeightBy = string.IsNullOrEmpty(e.HeightWeightBy) ? null : e.HeightWeightBy,
        },
        report.Photo?.Content,
        report.RowVersion);

    // The printed form places every value at its own spot, so the page design reads them by name.
    protected override IReadOnlyList<PrintLine> ResultLines(AnnualPhysicalExamReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(AnnualPhysicalExamReport detail) => [];

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, AnnualPhysicalExamReport e)
    {
        const string DateFormat = "MM/dd/yyyy";
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        return new Dictionary<string, string?>
        {
            ["Date"] = LocalTime.ToLocalDate(report.DateRequested).ToString(DateFormat, culture),
            ["Name"] = report.PatientName,
            ["Age"] = report.Age,
            ["Sex"] = report.Sex,
            ["CompanyName"] = report.CompanyOrPhysician,
            ["DepartmentOrAgency"] = e.DepartmentOrAgency,
            ["BirthDate"] = e.BirthDate?.ToString(DateFormat, culture),
            ["CivilStatus"] = e.CivilStatus,
            ["ContactNo"] = e.ContactNo,
            ["ENT"] = e.ENT, ["Gastroenterology"] = e.Gastroenterology, ["Respiratory"] = e.Respiratory, ["IntegumentarySkin"] = e.IntegumentarySkin,
            ["Cardiology"] = e.Cardiology, ["Psychology"] = e.Psychology, ["Endocrinology"] = e.Endocrinology, ["OBGyneUrology"] = e.OBGyneUrology,
            ["Muscoloskeletal"] = e.Muscoloskeletal, ["InfectiousCommunicable"] = e.InfectiousCommunicable, ["Neurological"] = e.Neurological,
            ["Surgical"] = e.Surgical, ["OthersPast"] = e.OthersPast, ["Medications"] = e.Medications, ["ReviewOfSystems"] = e.ReviewOfSystems,
            ["Allergies"] = e.Allergies,
            ["IsSmoking"] = e.IsSmoking is { } smoking ? (smoking ? "Yes" : "No") : null,
            ["SmokingSinceWhen"] = e.SmokingSinceWhen,
            ["NumberOfSticksPerDay"] = e.NumberOfSticksPerDay?.ToString(culture),
            ["IsDrinking"] = e.IsDrinking is { } drinking ? (drinking ? "Yes" : "No") : null,
            ["DrinkingSinceWhen"] = e.DrinkingSinceWhen,
            ["NumberOfBottles"] = e.NumberOfBottles?.ToString(culture),
            ["DrinkingFrequency"] = e.DrinkingFrequency,
            ["LMP"] = e.LMP,
            ["LMPType"] = e.LMPType,
            ["BP1st"] = e.BP1st, ["BP2nd"] = e.BP2nd, ["CardiacRate1st"] = e.CardiacRate1st, ["CardiacRate2nd"] = e.CardiacRate2nd,
            ["Height"] = e.Height, ["Weight"] = e.Weight, ["BMICategory"] = e.BMICategory,
            ["VARightEyeWGlasses"] = e.VARightEyeWGlasses, ["VARightEyeWOGlasses"] = e.VARightEyeWOGlasses,
            ["VALeftEyeWGlasses"] = e.VALeftEyeWGlasses, ["VALeftEyeWOGlasses"] = e.VALeftEyeWOGlasses,
            ["VisualAcuity"] = e.VisualAcuity,
            ["Skin"] = e.Skin, ["HeadScalp"] = e.HeadScalp, ["Eyes"] = e.Eyes, ["Ears"] = e.Ears, ["Nose"] = e.Nose,
            ["TeethTonsilsThroatPharynx"] = e.TeethTonsilsThroatPharynx, ["NeckLymphNodesThyroid"] = e.NeckLymphNodesThyroid,
            ["ThoraxBreast"] = e.ThoraxBreast, ["HeartLungs"] = e.HeartLungs, ["AbdomenLiverSpleen"] = e.AbdomenLiverSpleen,
            ["InguinalAreaGenitalsAnus"] = e.InguinalAreaGenitalsAnus, ["ExtremetiesSpine"] = e.ExtremetiesSpine, ["Tattoo"] = e.Tattoo,
            ["MassCyst"] = e.MassCyst, ["OthersPE"] = e.OthersPE,
            ["Findings"] = e.Findings, ["VitalSignsBy"] = e.VitalSignsBy, ["HeightWeightBy"] = e.HeightWeightBy,
        };
    }
}
