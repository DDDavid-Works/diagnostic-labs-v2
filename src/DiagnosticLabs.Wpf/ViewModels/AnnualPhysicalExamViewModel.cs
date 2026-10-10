using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>A set of tick boxes of which at most one can be ticked (Yes/No, Daily/Weekly/Occasional, N/F). Ticking the ticked one clears it.</summary>
public sealed partial class ExclusiveGroup : ObservableObject
{
    public ExclusiveGroup(params string[] options)
    {
        foreach (var option in options)
            Options.Add(new ExclusiveOption(this, option));
    }

    public ObservableCollection<ExclusiveOption> Options { get; } = [];

    [ObservableProperty]
    private string? _value;

    partial void OnValueChanged(string? value)
    {
        foreach (var option in Options)
            option.Refresh();
    }

    public ExclusiveOption this[string option] => Options.First(o => o.Text == option);
}

public sealed class ExclusiveOption(ExclusiveGroup group, string text) : ObservableObject
{
    public string Text { get; } = text;

    public bool IsChecked
    {
        get => group.Value == Text;
        set
        {
            if (value)
                group.Value = Text;
            else if (group.Value == Text)
                group.Value = null;
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsChecked));
}

/// <summary>One line of the physical examination table: N (normal or none) or F (with findings).</summary>
public sealed class PhysicalExamRow(string name, string label)
{
    public string Name { get; } = name;

    public string Label { get; } = label;

    public ExclusiveGroup State { get; } = new("N", "F");

    public ExclusiveOption Normal => State["N"];

    public ExclusiveOption Findings => State["F"];
}

public partial class AnnualPhysicalExamViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<AnnualPhysicalExamViewModel> logger)
    : LabResultViewModel<IAnnualPhysicalExamService, AnnualPhysicalExamDetails, AnnualPhysicalExamInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.AnnualPhysicalExam, "Annual Physical Exam", logger)
{
    // ----- the person (beyond the shared patient block) -----
    [ObservableProperty]
    private string? _departmentOrAgency;

    [ObservableProperty]
    private DateTime? _birthDate;

    [ObservableProperty]
    private string? _civilStatus;

    [ObservableProperty]
    private string? _contactNo;

    // ----- past medical history -----
    public ChoiceField Ent { get; } = new("ENT", "Eyes, Ears, Nose, Throat", EntryFields.ApeEnt);

    public ChoiceField Gastroenterology { get; } = new("Gastroenterology", "Gastroenterology", EntryFields.ApeGastroenterology);

    public ChoiceField Respiratory { get; } = new("Respiratory", "Respiratory", EntryFields.ApeRespiratory);

    public ChoiceField IntegumentarySkin { get; } = new("IntegumentarySkin", "Integumentary/Skin", EntryFields.ApeIntegumentarySkin);

    public ChoiceField Cardiology { get; } = new("Cardiology", "Cardiology", EntryFields.ApeCardiology);

    public ChoiceField Psychology { get; } = new("Psychology", "Psychology", EntryFields.ApePsychology);

    public ChoiceField Endocrinology { get; } = new("Endocrinology", "Endocrinology", EntryFields.ApeEndocrinology);

    public ChoiceField ObGyneUrology { get; } = new("OBGyneUrology", "OB-Gyne/Urology", EntryFields.ApeObGyneUrology);

    public ChoiceField Musculoskeletal { get; } = new("Muscoloskeletal", "Musculoskeletal", EntryFields.ApeMusculoskeletal);

    public ChoiceField Infectious { get; } = new("InfectiousCommunicable", "Infectious/Communicable", EntryFields.ApeInfectious);

    public ChoiceField Neurological { get; } = new("Neurological", "Neurology", EntryFields.ApeNeurological);

    public ChoiceField Surgical { get; } = new("Surgical", "Surgical", EntryFields.ApeSurgical);

    public ChoiceField VitalSignsBy { get; } = new("VitalSignsBy", "Vital Signs Done By", EntryFields.ApeVitalSignsBy);

    public ChoiceField HeightWeightBy { get; } = new("HeightWeightBy", "Height and Weight Done By", EntryFields.ApeHeightWeightBy);

    /// <summary>The history table in the order of the paper: the left column and the right column side by side.</summary>
    public IReadOnlyList<ChoiceRow> HistoryRows =>
    [
        new(Ent, Gastroenterology),
        new(Respiratory, IntegumentarySkin),
        new(Cardiology, Psychology),
        new(Endocrinology, ObGyneUrology),
        new(Musculoskeletal, Infectious),
        new(Neurological, Surgical),
    ];

    [ObservableProperty]
    private string? _othersPast;

    [ObservableProperty]
    private string? _medications;

    [ObservableProperty]
    private string? _allergies;

    [ObservableProperty]
    private string? _reviewOfSystems;

    public TemplateOptions OthersPastTemplates { get; } = new();

    public TemplateOptions MedicationsTemplates { get; } = new();

    public TemplateOptions AllergiesTemplates { get; } = new();

    public TemplateOptions ReviewOfSystemsTemplates { get; } = new();

    // ----- present medical history -----
    public ExclusiveGroup Smoking { get; } = new("No", "Yes");

    public ExclusiveGroup Drinking { get; } = new("No", "Yes");

    public ExclusiveGroup DrinkingFrequency { get; } = new(AnnualPhysicalExamService.DrinkingFrequencies);

    public ExclusiveGroup LmpType { get; } = new(AnnualPhysicalExamService.LmpTypes);

    [ObservableProperty]
    private string? _smokingSinceWhen;

    [ObservableProperty]
    private string? _sticksPerDay;

    [ObservableProperty]
    private string? _drinkingSinceWhen;

    [ObservableProperty]
    private string? _bottles;

    [ObservableProperty]
    private string? _lmp;

    // ----- vital signs -----
    [ObservableProperty]
    private string? _bp1st;

    [ObservableProperty]
    private string? _bp2nd;

    [ObservableProperty]
    private string? _cardiacRate1st;

    [ObservableProperty]
    private string? _cardiacRate2nd;

    [ObservableProperty]
    private string? _height;

    [ObservableProperty]
    private string? _weight;

    [ObservableProperty]
    private string? _bmiCategory;

    public TemplateOptions BmiCategoryTemplates { get; } = new();

    // ----- visual acuity -----
    [ObservableProperty]
    private string? _vaRightWith;

    [ObservableProperty]
    private string? _vaLeftWith;

    [ObservableProperty]
    private string? _vaRightWithout;

    [ObservableProperty]
    private string? _vaLeftWithout;

    public ExclusiveGroup VisualAcuity { get; } = new(AnnualPhysicalExamService.VisualAcuities);

    // ----- physical examination (three columns of five) -----
    public IReadOnlyList<PhysicalExamRow> Exam { get; } =
    [
        new("Skin", "Skin"), new("HeadScalp", "Head, Scalp"), new("Eyes", "Eyes"), new("Ears", "Ears"), new("Nose", "Nose"),
        new("TeethTonsilsThroatPharynx", "Teeth, Tonsils, Throat, Pharynx"), new("NeckLymphNodesThyroid", "Neck, Lymph Nodes, Thyroid"),
        new("ThoraxBreast", "Thorax, Breast"), new("HeartLungs", "Heart, Lungs"), new("AbdomenLiverSpleen", "Abdomen, Liver, Spleen"),
        new("InguinalAreaGenitalsAnus", "Inguinal Area, Genitals, Anus"), new("ExtremetiesSpine", "Extremities, Spine"), new("Tattoo", "Tattoo"),
        new("MassCyst", "Mass, Cyst"), new("OthersPE", "Others"),
    ];

    public IEnumerable<PhysicalExamRow> ExamColumn1 => Exam.Take(5);

    public IEnumerable<PhysicalExamRow> ExamColumn2 => Exam.Skip(5).Take(5);

    public IEnumerable<PhysicalExamRow> ExamColumn3 => Exam.Skip(10);

    // ----- findings -----
    [ObservableProperty]
    private string? _findings;

    public TemplateOptions FindingsTemplates { get; } = new();

    public IReadOnlyList<string> CivilStatuses { get; } = ["Single", "Married", "Widowed", "Separated", "Divorced"];

    protected override IEnumerable<ChoiceField> ChoiceFields =>
    [
        Ent, Gastroenterology, Respiratory, IntegumentarySkin, Cardiology, Psychology, Endocrinology, ObGyneUrology, Musculoskeletal,
        Infectious, Neurological, Surgical, VitalSignsBy, HeightWeightBy,
    ];

    // The remarks box is not on this form; the findings take its place.
    protected override EntryField RemarksField => EntryFields.ApeFindings;

    protected override async Task OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await LoadTextTemplatesAsync();
    }

    protected override async Task ReloadChoicesAsync()
    {
        await base.ReloadChoicesAsync();
        await LoadTextTemplatesAsync();
    }

    private async Task LoadTextTemplatesAsync()
    {
        await LoadAsync(OthersPastTemplates, EntryFields.ApeOthersPast, text => OthersPast = text);
        await LoadAsync(MedicationsTemplates, EntryFields.ApeMedications, text => Medications = text);
        await LoadAsync(AllergiesTemplates, EntryFields.ApeAllergies, text => Allergies = text);
        await LoadAsync(ReviewOfSystemsTemplates, EntryFields.ApeReviewOfSystems, text => ReviewOfSystems = text);
        await LoadAsync(BmiCategoryTemplates, EntryFields.ApeBmiCategory, text => BmiCategory = text);
        await LoadAsync(FindingsTemplates, EntryFields.ApeFindings, text => Findings = text);
    }

    private async Task LoadAsync(TemplateOptions options, EntryField field, Action<string> apply)
    {
        var list = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(field, ModuleId));
        options.Apply ??= apply;
        options.Replace(list.IsSuccess ? list.Value.Items : []);
    }

    protected override AnnualPhysicalExamInput BuildInput()
    {
        string? PE(string name) => Exam.First(r => r.Name == name).State.Value;

        var data = new AnnualPhysicalExamData
        {
            DepartmentOrAgency = DepartmentOrAgency,
            BirthDate = BirthDate is { } b ? DateOnly.FromDateTime(b) : null,
            CivilStatus = CivilStatus,
            ContactNo = ContactNo,
            ENT = Ent.Value,
            Gastroenterology = Gastroenterology.Value,
            Respiratory = Respiratory.Value,
            IntegumentarySkin = IntegumentarySkin.Value,
            Cardiology = Cardiology.Value,
            Psychology = Psychology.Value,
            Endocrinology = Endocrinology.Value,
            OBGyneUrology = ObGyneUrology.Value,
            Muscoloskeletal = Musculoskeletal.Value,
            InfectiousCommunicable = Infectious.Value,
            Neurological = Neurological.Value,
            Surgical = Surgical.Value,
            OthersPast = OthersPast,
            Medications = Medications,
            ReviewOfSystems = ReviewOfSystems,
            Allergies = Allergies,
            IsSmoking = YesNo(Smoking.Value),
            SmokingSinceWhen = SmokingSinceWhen,
            NumberOfSticksPerDay = Count(SticksPerDay),
            IsDrinking = YesNo(Drinking.Value),
            DrinkingSinceWhen = DrinkingSinceWhen,
            NumberOfBottles = Count(Bottles),
            DrinkingFrequency = DrinkingFrequency.Value,
            LMP = Lmp,
            LMPType = LmpType.Value,
            BP1st = Bp1st,
            BP2nd = Bp2nd,
            CardiacRate1st = CardiacRate1st,
            CardiacRate2nd = CardiacRate2nd,
            Height = Height,
            Weight = Weight,
            BMICategory = BmiCategory,
            VARightEyeWGlasses = VaRightWith,
            VARightEyeWOGlasses = VaRightWithout,
            VALeftEyeWGlasses = VaLeftWith,
            VALeftEyeWOGlasses = VaLeftWithout,
            VisualAcuity = VisualAcuity.Value,
            Skin = PE("Skin"),
            HeadScalp = PE("HeadScalp"),
            Eyes = PE("Eyes"),
            Ears = PE("Ears"),
            Nose = PE("Nose"),
            TeethTonsilsThroatPharynx = PE("TeethTonsilsThroatPharynx"),
            NeckLymphNodesThyroid = PE("NeckLymphNodesThyroid"),
            ThoraxBreast = PE("ThoraxBreast"),
            HeartLungs = PE("HeartLungs"),
            AbdomenLiverSpleen = PE("AbdomenLiverSpleen"),
            InguinalAreaGenitalsAnus = PE("InguinalAreaGenitalsAnus"),
            ExtremetiesSpine = PE("ExtremetiesSpine"),
            Tattoo = PE("Tattoo"),
            MassCyst = PE("MassCyst"),
            OthersPE = PE("OthersPE"),
            Findings = Findings,
            VitalSignsBy = VitalSignsBy.Value,
            HeightWeightBy = HeightWeightBy.Value,
        };

        return new AnnualPhysicalExamInput(Id, BuildHeader(), data, RowVersion);
    }

    private static bool? YesNo(string? answer) => answer switch { "Yes" => true, "No" => false, _ => null };

    private static string? Answer(bool? value) => value switch { true => "Yes", false => "No", null => null };

    // A count that is not a whole number is sent as -1 so the service refuses it with its message instead of dropping it silently.
    private static int? Count(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : int.TryParse(text.Trim(), out var n) ? n : -1;

    protected override void ShowFields(AnnualPhysicalExamDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        var data = d.Data;
        DepartmentOrAgency = data.DepartmentOrAgency;
        BirthDate = data.BirthDate?.ToDateTime(TimeOnly.MinValue);
        CivilStatus = data.CivilStatus;
        ContactNo = data.ContactNo;
        Ent.Value = data.ENT;
        Gastroenterology.Value = data.Gastroenterology;
        Respiratory.Value = data.Respiratory;
        IntegumentarySkin.Value = data.IntegumentarySkin;
        Cardiology.Value = data.Cardiology;
        Psychology.Value = data.Psychology;
        Endocrinology.Value = data.Endocrinology;
        ObGyneUrology.Value = data.OBGyneUrology;
        Musculoskeletal.Value = data.Muscoloskeletal;
        Infectious.Value = data.InfectiousCommunicable;
        Neurological.Value = data.Neurological;
        Surgical.Value = data.Surgical;
        OthersPast = data.OthersPast;
        Medications = data.Medications;
        ReviewOfSystems = data.ReviewOfSystems;
        Allergies = data.Allergies;
        Smoking.Value = Answer(data.IsSmoking);
        SmokingSinceWhen = data.SmokingSinceWhen;
        SticksPerDay = data.NumberOfSticksPerDay?.ToString();
        Drinking.Value = Answer(data.IsDrinking);
        DrinkingSinceWhen = data.DrinkingSinceWhen;
        Bottles = data.NumberOfBottles?.ToString();
        DrinkingFrequency.Value = data.DrinkingFrequency;
        Lmp = data.LMP;
        LmpType.Value = data.LMPType;
        Bp1st = data.BP1st;
        Bp2nd = data.BP2nd;
        CardiacRate1st = data.CardiacRate1st;
        CardiacRate2nd = data.CardiacRate2nd;
        Height = data.Height;
        Weight = data.Weight;
        BmiCategory = data.BMICategory;
        VaRightWith = data.VARightEyeWGlasses;
        VaRightWithout = data.VARightEyeWOGlasses;
        VaLeftWith = data.VALeftEyeWGlasses;
        VaLeftWithout = data.VALeftEyeWOGlasses;
        VisualAcuity.Value = data.VisualAcuity;
        SetExam("Skin", data.Skin);
        SetExam("HeadScalp", data.HeadScalp);
        SetExam("Eyes", data.Eyes);
        SetExam("Ears", data.Ears);
        SetExam("Nose", data.Nose);
        SetExam("TeethTonsilsThroatPharynx", data.TeethTonsilsThroatPharynx);
        SetExam("NeckLymphNodesThyroid", data.NeckLymphNodesThyroid);
        SetExam("ThoraxBreast", data.ThoraxBreast);
        SetExam("HeartLungs", data.HeartLungs);
        SetExam("AbdomenLiverSpleen", data.AbdomenLiverSpleen);
        SetExam("InguinalAreaGenitalsAnus", data.InguinalAreaGenitalsAnus);
        SetExam("ExtremetiesSpine", data.ExtremetiesSpine);
        SetExam("Tattoo", data.Tattoo);
        SetExam("MassCyst", data.MassCyst);
        SetExam("OthersPE", data.OthersPE);
        Findings = data.Findings;
        VitalSignsBy.Value = data.VitalSignsBy;
        HeightWeightBy.Value = data.HeightWeightBy;
        RowVersion = d.RowVersion;
    }

    private void SetExam(string name, string? state) => Exam.First(r => r.Name == name).State.Value = state;

    protected override void OnPersonPicked(DateOnly? dateOfBirth, string? civilStatus, string? contactNumbers)
    {
        BirthDate = dateOfBirth?.ToDateTime(TimeOnly.MinValue);
        if (!string.IsNullOrWhiteSpace(civilStatus))
            CivilStatus = civilStatus;
        if (!string.IsNullOrWhiteSpace(contactNumbers))
            ContactNo = contactNumbers;
    }

    protected override void ResetDetail()
    {
        DepartmentOrAgency = null;
        BirthDate = null;
        CivilStatus = null;
        ContactNo = null;
        OthersPast = null;
        Medications = null;
        Allergies = null;
        ReviewOfSystems = null;
        Smoking.Value = null;
        SmokingSinceWhen = null;
        SticksPerDay = null;
        Drinking.Value = null;
        DrinkingSinceWhen = null;
        Bottles = null;
        DrinkingFrequency.Value = null;
        Lmp = null;
        LmpType.Value = null;
        Bp1st = null;
        Bp2nd = null;
        CardiacRate1st = null;
        CardiacRate2nd = null;
        Height = null;
        Weight = null;
        BmiCategory = null;
        VaRightWith = null;
        VaLeftWith = null;
        VaRightWithout = null;
        VaLeftWithout = null;
        VisualAcuity.Value = null;
        foreach (var row in Exam)
            row.State.Value = null;

        Findings = null;
    }

    // What a new form starts with: the usual wording of the long texts and the physical examination marks (everything else is per person).
    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
    [
        new("OthersPast", () => OthersPast, v => OthersPast = v),
        new("Medications", () => Medications, v => Medications = v),
        new("Allergies", () => Allergies, v => Allergies = v),
        new("ReviewOfSystems", () => ReviewOfSystems, v => ReviewOfSystems = v),
        new("BMICategory", () => BmiCategory, v => BmiCategory = v),
        new("Findings", () => Findings, v => Findings = v),
        .. Exam.Select(r => new DefaultField("Exam." + r.Name, () => r.State.Value, v => r.State.Value = v)),
    ];

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IAnnualPhysicalExamService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditOthersPastTemplatesAsync() => EditMultiLineAsync(EntryFields.ApeOthersPast);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditMedicationsTemplatesAsync() => EditMultiLineAsync(EntryFields.ApeMedications);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditAllergiesTemplatesAsync() => EditMultiLineAsync(EntryFields.ApeAllergies);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditReviewOfSystemsTemplatesAsync() => EditMultiLineAsync(EntryFields.ApeReviewOfSystems);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditBmiCategoryTemplatesAsync() => EditMultiLineAsync(EntryFields.ApeBmiCategory);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditFindingsTemplatesAsync() => EditMultiLineAsync(EntryFields.ApeFindings);
}
