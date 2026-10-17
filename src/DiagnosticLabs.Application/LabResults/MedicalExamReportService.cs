using System.Reflection;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>
/// One saved value of the Medical Examination Report. <see cref="Name"/> is also the column of <see cref="MedicalExamReport"/>; a field with
/// <see cref="Options"/> is a tick (one of the options, or empty), any other is typed text.
/// </summary>
public sealed record MedicalExamField(string Name, string Label, int MaxLength, IReadOnlyList<string>? Options = null);

/// <summary>
/// The Medical Examination Report described once: the service saves and validates it, the screen builds its boxes from it and the two printed pages
/// read the values by name. The form follows the client's printed sheet: Part 1 (history, family history, OB-Gyne, social history) and Part 2
/// (physical examination, laboratory / X-ray results, classification) and who did and signed it.
/// </summary>
public static class MedicalExamFields
{
    public const int FlagMaxLength = 20;
    public const int ShortMaxLength = 50;
    public const int TextMaxLength = 500;
    public const int LongTextMaxLength = 1000;

    public static readonly string[] YesNo = ["No", "Yes"];

    /// <summary>Normal or none, or with findings.</summary>
    public static readonly string[] NormalOrFindings = ["N", "F"];

    public static readonly string[] Classifications = ["A", "B", "C", "D", "Pending"];

    /// <summary>The 44 No/Yes rows of the medical history, in the four columns of the printed form (the fourth holds the five rows beside "Others" and the medication).</summary>
    public static readonly (string Name, string Label)[][] HistoryColumns =
    [
        [
            ("HxEyeDisease", "Eye Disease"), ("HxEarInfection", "Ear Infection"), ("HxHearingDefect", "Hearing Defect"), ("HxSinusitis", "Sinusitis"),
            ("HxNoseBleeding", "Nose Bleeding"), ("HxCoughColds", "Frequent Cough / Colds"), ("HxTonsillitisPharyngitis", "Tonsillitis / Pharyngitis"),
            ("HxNeckMassGoiter", "Neck Mass / Goiter"), ("HxAsthma", "Asthma"), ("HxTuberculosis", "Tuberculosis"), ("HxPneumonia", "Pneumonia"),
            ("HxDifficultyBreathing", "Difficulty of Breathing"), ("HxHypertension", "Hypertension (High Blood)"),
        ],
        [
            ("HxHypotension", "Hypotension (Low Blood)"), ("HxAnemia", "Anemia (Low Blood Count)"), ("HxChestPainPalpitations", "Chest Pain / Palpitations"),
            ("HxHeartDisease", "Heart Disease"), ("HxBreastMass", "Breast Mass"), ("HxDiabetes", "Diabetes"), ("HxUlcerHyperacidity", "Ulcer / Hyperacidity"),
            ("HxAbdominalPain", "Frequent Abdominal Pain"), ("HxDiarrhea", "Diarrhea"), ("HxKidneyDiseaseUti", "Kidney Disease / UTI"), ("HxHernia", "Hernia (Luslos)"),
            ("HxHemorrhoids", "Hemorrhoids (Almoranas)"), ("HxLossOfConsciousness", "Loss of Consciousness (Hinimatay)"),
        ],
        [
            ("HxBackPainScoliosis", "Back Pain / Scoliosis"), ("HxHeadacheMigraine", "Frequent Headache / Migraine"), ("HxDizzinessVertigo", "Dizziness / Vertigo"),
            ("HxEpilepsyConvulsion", "Epilepsy / Convulsion"), ("HxEasyFatigability", "Easy Fatigability"), ("HxArthritisJointPain", "Arthritis / Joint Pain"),
            ("HxFracture", "Fracture (Bali)"), ("HxMeasles", "Measles (Tigdas)"), ("HxChickenPox", "Chicken Pox (Bulutong)"), ("HxMumps", "Mumps (Beke)"),
            ("HxHepatitis", "Hepatitis"), ("HxMalaria", "Malaria"), ("HxStd", "Sexually Transmitted Disease"),
        ],
        [
            ("HxAllergies", "Allergies"), ("HxSkinDisease", "Skin Disease"), ("HxPreviousAccident", "Previous Accident"),
            ("HxPreviousHospitalization", "Previous Hospitalization"), ("HxPreviousOperation", "Previous Operation"),
        ],
    ];

    public static readonly (string Name, string Label)[] Family =
        [("FamCancer", "Cancer"), ("FamHypertension", "Hypertension"), ("FamDiabetes", "Diabetes Mellitus")];

    /// <summary>The No/Yes rows of the OB-Gyne history (the others there are ticks of three or typed numbers).</summary>
    public static readonly (string Name, string Label)[] ObYesNo =
        [("PregnantNow", "Pregnant Now?"), ("Myoma", "Myoma"), ("OvarianCyst", "Ovarian Cyst")];

    public static readonly string[] MenstrualPatterns = ["Monthly", "Irregular"];

    public static readonly string[] Dysmenorrheas = ["No", "Moderate", "Severe"];

    public static readonly string[] DrinkingFrequencies = ["Daily", "Weekly", "Occasional"];

    /// <summary>The 15 rows of the review of systems, in the three columns of the printed form.</summary>
    public static readonly (string Name, string Label)[][] ReviewColumns =
    [
        [("RosSkin", "Skin"), ("RosHeadScalp", "Head, Scalp"), ("RosEyes", "Eyes"), ("RosEars", "Ears"), ("RosNose", "Nose")],
        [
            ("RosTeethThroat", "Teeth, Tonsils, Throat, Pharynx"), ("RosNeckThyroid", "Neck, Lymph Nodes, Thyroid"), ("RosThoraxBreast", "Thorax, Breast"),
            ("RosHeartLungs", "Heart, Lungs"), ("RosAbdomen", "Abdomen, Liver, Spleen"),
        ],
        [
            ("RosInguinalGenitalia", "Inguinal area, Genitalia, Anus"), ("RosExtremitiesSpine", "Extremities, Spine"), ("RosTattoo", "Tattoo"),
            ("RosMassCyst", "Mass, Cyst"), ("RosOthers", "Others"),
        ],
    ];

    /// <summary>The four laboratory / X-ray rows: normal or with findings, and a note beside it (the name of the note is the name plus "Note").</summary>
    public static readonly (string Name, string Label)[] LabRows =
        [("LabChestXray", "Chest X-ray"), ("LabCbc", "CBC"), ("LabUrinalysis", "Urinalysis"), ("LabFecalysis", "Fecalysis")];

    /// <summary>The four people named under lines at the end (each with a licence number named like the person plus "License"); the physician is the header's.</summary>
    public static readonly (string Name, string Label)[] Signatories =
    [
        ("VitalSignsBy", "Vital Signs done by"), ("InterviewBy", "Interview done by"), ("AssessmentBy", "Assessment done by"), ("NotedBy", "Noted by"),
    ];

    /// <summary>The fields with reusable texts of their own (the multi-line lists of the Entry Builder); the Remarks list is the usual one.</summary>
    public static readonly (string Name, EntryField Entry)[] TemplateTexts =
    [
        ("BmiCategory", new EntryField("BMI Category", EntryKind.MultiLine, ModuleIds.MedicalExamReport)),
        ("Findings", new EntryField("Findings", EntryKind.MultiLine, ModuleIds.MedicalExamReport)),
        ("Assessment", new EntryField("Assessment", EntryKind.MultiLine, ModuleIds.MedicalExamReport)),
        ("HxOthers", new EntryField("Medical History Others", EntryKind.MultiLine, ModuleIds.MedicalExamReport)),
        ("HxMedication", new EntryField("Present/Past Medication", EntryKind.MultiLine, ModuleIds.MedicalExamReport)),
    ];

    /// <summary>The two "Others (Pls. Specify)" boxes that pick from a single-line list of the Entry Builder (typing something else is allowed).</summary>
    public static readonly EntryField FamOthersEntry = new("Family History Others", EntryKind.SingleLine, ModuleIds.MedicalExamReport);

    public static readonly EntryField ObOthersEntry = new("OB-Gyne Others", EntryKind.SingleLine, ModuleIds.MedicalExamReport);

    /// <summary>The note beside each laboratory / X-ray line picks from a single-line list of its own (typing something else is allowed).</summary>
    public static EntryField LabNoteEntry(string labName) =>
        new(LabRows.First(r => r.Name == labName).Label + " Note", EntryKind.SingleLine, ModuleIds.MedicalExamReport);

    /// <summary>The Ishihara Test result, picked from a single-line list (typing something else is allowed).</summary>
    public static readonly EntryField IshiharaEntry = new("Ishihara Test", EntryKind.SingleLine, ModuleIds.MedicalExamReport);

    /// <summary>Every value the form saves, in the order of the form.</summary>
    public static readonly IReadOnlyList<MedicalExamField> All = Build();

    private static List<MedicalExamField> Build()
    {
        var all = new List<MedicalExamField>
        {
            new("CivilStatus", "Civil Status", ShortMaxLength),
            new("ContactNo", "Contact #", 100),
        };

        all.AddRange(HistoryColumns.SelectMany(c => c).Select(h => new MedicalExamField(h.Name, h.Label, FlagMaxLength, YesNo)));
        all.Add(new("HxOthers", "Others (Pls. Specify)", TextMaxLength));
        all.Add(new("HxMedication", "Present/Past Medication", TextMaxLength));
        all.AddRange(Family.Select(f => new MedicalExamField(f.Name, f.Label, FlagMaxLength, YesNo)));
        all.Add(new("FamOthers", "Family history: Others", TextMaxLength));

        all.Add(new("MenstrualPattern", "Menstrual Pattern", FlagMaxLength, MenstrualPatterns));
        all.Add(new("Lmp", "LMP (Last Menstrual Period)", ShortMaxLength));
        all.Add(new("Dysmenorrhea", "Dysmenorrhea", FlagMaxLength, Dysmenorrheas));
        all.AddRange(ObYesNo.Select(o => new MedicalExamField(o.Name, o.Label, FlagMaxLength, YesNo)));
        all.Add(new("NumPregnancies", "Number of Pregnancies", ShortMaxLength));
        all.Add(new("NumMiscarriages", "Number of Miscarriages", ShortMaxLength));
        all.Add(new("ObOthers", "OB-Gyne: Others", TextMaxLength));

        all.Add(new("SmokingStatus", "Smoking", FlagMaxLength, YesNo));
        all.Add(new("SmokingSince", "Smoking since", ShortMaxLength));
        all.Add(new("SticksPerDay", "# of sticks per day", ShortMaxLength));
        all.Add(new("DrinkingStatus", "Drinking", FlagMaxLength, YesNo));
        all.Add(new("DrinkingSince", "Drinking since", ShortMaxLength));
        all.Add(new("Bottles", "# of bottles", ShortMaxLength));
        all.Add(new("DrinkingFrequency", "Drinking frequency", FlagMaxLength, DrinkingFrequencies));

        foreach (var (name, label) in new[] { ("Bp1", "BP 1st reading"), ("Bp2", "BP 2nd reading"), ("Pulse1", "Cardiac rate 1st reading"), ("Pulse2", "Cardiac rate 2nd reading"), ("Temperature", "Temperature"), ("Height", "Height"), ("Weight", "Weight") })
            all.Add(new(name, label, ShortMaxLength));

        all.Add(new("BmiCategory", "BMI Category", TextMaxLength));
        foreach (var (name, label) in new[] { ("VisionFarWithR", "Far with eyeglasses, right eye"), ("VisionFarWithL", "Far with eyeglasses, left eye"), ("VisionFarWithoutR", "Far without eyeglasses, right eye"), ("VisionFarWithoutL", "Far without eyeglasses, left eye") })
            all.Add(new(name, label, ShortMaxLength));

        all.Add(new("Ishihara", "Ishihara Test", 100));
        all.AddRange(ReviewColumns.SelectMany(c => c).Select(r => new MedicalExamField(r.Name, r.Label, FlagMaxLength, NormalOrFindings)));
        all.Add(new("RosOthersNote", "Review of systems: Others", 100));
        all.Add(new("Findings", "Findings", LongTextMaxLength));

        foreach (var (name, label) in LabRows)
        {
            all.Add(new(name, label, FlagMaxLength, NormalOrFindings));
            all.Add(new(name + "Note", label + " note", 200));
        }

        all.Add(new("Classification", "Classification", FlagMaxLength, Classifications));
        all.Add(new("Assessment", "Assessment", LongTextMaxLength));
        foreach (var (name, label) in Signatories)
        {
            all.Add(new(name, label, 100));
            all.Add(new(name + "License", label + " license number", 50));
        }

        return all;
    }

    public static MedicalExamField Get(string name) => All.First(f => f.Name == name);
}

/// <summary>Every value of the form by name (a field that is not listed is empty).</summary>
public sealed record MedicalExamData(IReadOnlyDictionary<string, string?> Values)
{
    public static MedicalExamData Empty { get; } = new(new Dictionary<string, string?>());

    public string? Value(string name) => Values.TryGetValue(name, out var value) ? value : null;
}

public sealed record MedicalExamReportDetails(long Id, LabResultHeader Header, MedicalExamData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record MedicalExamReportInput(long Id, LabResultHeader Header, MedicalExamData Data, byte[]? RowVersion) : ICrudInput;

public interface IMedicalExamReportService
    : ICrudService<LabResultListItem, MedicalExamReportDetails, MedicalExamReportInput>, ILabResultPrinting;

/// <summary>
/// Medical Examination Report: the client's two-page form (Part 1 filled in by the applicant, Part 2 by the medical practitioner). It is a form of its own;
/// the physician who signs it is the header's Pathologist, the four people who did the vital signs, the interview, the assessment and noted it are saved on it.
/// </summary>
public sealed class MedicalExamReportService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<MedicalExamReportInput, MedicalExamReportDetails, MedicalExamReport>(
        db, currentUser, clock, ModuleIds.MedicalExamReport, "Medical Examination Report", LabReportType.MedicalExamReport),
      IMedicalExamReportService
{
    /// <summary>The role printed for the physician under the last signature.</summary>
    public const string PhysicianRole = "Physician";

    // Every field is read and written by name, so the form is described in one place (MedicalExamFields).
    private static readonly Dictionary<string, PropertyInfo> Columns = MedicalExamFields.All
        .ToDictionary(f => f.Name, f => typeof(MedicalExamReport).GetProperty(f.Name)!);

    protected override string Title => "Medical Examination Report";

    protected override ReportLayout Layout => ReportLayout.MedicalExamReport;

    protected override string FooterText => string.Empty;

    protected override LabResultHeader HeaderOf(MedicalExamReportInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(MedicalExamReportInput input)
    {
        var errors = new List<string>();
        foreach (var field in MedicalExamFields.All)
        {
            var value = input.Data.Value(field.Name);
            Max(errors, value, field.MaxLength, field.Label);
            if (field.Options is { } options && !string.IsNullOrWhiteSpace(value) && !options.Contains(value.Trim()))
                errors.Add($"{field.Label} must be one of: {string.Join(", ", options)}.");
        }

        return errors;
    }

    protected override void ApplyDetail(MedicalExamReport e, MedicalExamReportInput input)
    {
        foreach (var field in MedicalExamFields.All)
            Columns[field.Name].SetValue(e, Clean(input.Data.Value(field.Name)));
    }

    private static MedicalExamData DataOf(MedicalExamReport e) =>
        new(MedicalExamFields.All.ToDictionary(f => f.Name, f => (string?)Columns[f.Name].GetValue(e)));

    protected override MedicalExamReportDetails BuildDetails(LabReport report, LabResultHeader header, MedicalExamReport e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    // The printed pages place every value in its own spot, so the page designs read them by name.
    protected override IReadOnlyList<PrintLine> ResultLines(MedicalExamReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(MedicalExamReport detail) => [];

    protected override IReadOnlyList<PrintSignatory> Signatories(LabReport report, MedicalExamReport e) =>
    [
        .. MedicalExamFields.Signatories.Select(s => new PrintSignatory(s.Label, (string?)Columns[s.Name].GetValue(e), (string?)Columns[s.Name + "License"].GetValue(e))),
        new PrintSignatory(PhysicianRole, report.Pathologist, report.PathologistLicense),
    ];

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, MedicalExamReport e) =>
        MedicalExamFields.All.ToDictionary(f => f.Name, f => (string?)Columns[f.Name].GetValue(e));
}
