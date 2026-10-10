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

/// <summary>One line of the laboratory/X-ray table: normal or with findings (two tick boxes of which one can be ticked) and a note.</summary>
public sealed partial class FindingRow(string name, string label, string normalCaption, string findingsCaption) : ObservableObject
{
    public string Name { get; } = name;

    public string Label { get; } = label;

    public string NormalCaption { get; } = normalCaption;

    public string FindingsCaption { get; } = findingsCaption;

    public ExclusiveGroup State { get; } = new("N", "F");

    public ExclusiveOption Normal => State["N"];

    public ExclusiveOption Findings => State["F"];

    [ObservableProperty]
    private string? _remarks;

    public FindingEntry ToEntry() => new(State.Value, Remarks);

    public void Show(FindingEntry entry)
    {
        State.Value = entry.Result;
        Remarks = entry.Remarks;
    }
}

/// <summary>
/// The Medical Examination Report, which the app calls Annual Physical Exam Page 2: the laboratory and X-ray results, the classification,
/// the history, the assessment and who did and signed it. (It is saved in the Medical Examination tables.)
/// </summary>
public partial class MedicalExaminationViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<MedicalExaminationViewModel> logger)
    : LabResultViewModel<IMedicalExaminationService, MedicalExaminationDetails, MedicalExaminationInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.AnnualPhysicalExamPage2, "Annual Physical Exam Page 2", logger)
{
    // ----- the person (beyond the shared patient block) -----
    [ObservableProperty]
    private string? _contactNo;

    [ObservableProperty]
    private string? _civilStatus;

    public IReadOnlyList<string> CivilStatuses { get; } = ["Single", "Married", "Widowed", "Separated", "Divorced"];

    // ----- laboratory / X-ray results -----
    public IReadOnlyList<FindingRow> Rows { get; } =
    [
        new("ChestXray", "Chest X-Ray", "Normal", "With Findings"),
        new("CBC", "CBC", "Normal", "With Findings"),
        new("Urinalysis", "Urinalysis", "Normal", "With Findings"),
        new("Fecalysis", "Fecalysis", "Normal", "With Findings"),
        new("HBsAg", "HBsAg", "Non-Reactive", "Reactive"),
        new("DrugTest2Panel", "Drug Test: METH/THC (2 Panel)", "Negative", "Positive"),
        new("DrugTest4Panel", "Drug Test: COC/PCP (4 Panel) OPI, AMP", "Negative", "Positive"),
    ];

    // ----- classification (the values the old app saved) -----
    public ExclusiveGroup Classification { get; } = new(MedicalExaminationService.Classifications);

    // ----- history, assessment, remarks -----
    [ObservableProperty]
    private string? _medicalSurgicalHistory;

    [ObservableProperty]
    private string? _assessment;

    public TemplateOptions HistoryTemplates { get; } = new();

    public TemplateOptions AssessmentTemplates { get; } = new();

    // ----- who did the assessment and the physician -----
    public ChoiceField AssessmentDoneBy { get; } = new("AssessmentDoneBy", "Assessment Done By", EntryFields.MerAssessmentDoneBy);

    public ChoiceField PhysicianName { get; } = new("PhysicianName", "Physician Name", EntryFields.MerPhysicianName);

    public ChoiceField PhysicianLicense { get; } = new("PhysicianLicense", "Physician License", EntryFields.MerPhysicianLicense);

    protected override IEnumerable<ChoiceField> ChoiceFields => [AssessmentDoneBy, PhysicianName, PhysicianLicense];

    protected override EntryField RemarksField => EntryFields.MerRemarks;

    private FindingRow Row(string name) => Rows.First(r => r.Name == name);

    protected override void OnPersonPicked(DateOnly? dateOfBirth, string? civilStatus, string? contactNumbers)
    {
        if (!string.IsNullOrWhiteSpace(civilStatus))
            CivilStatus = civilStatus;
        if (!string.IsNullOrWhiteSpace(contactNumbers))
            ContactNo = contactNumbers;
    }

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
        await LoadAsync(HistoryTemplates, EntryFields.MerMedicalSurgicalHistory, text => MedicalSurgicalHistory = text);
        await LoadAsync(AssessmentTemplates, EntryFields.MerAssessment, text => Assessment = text);
    }

    private async Task LoadAsync(TemplateOptions options, EntryField field, Action<string> apply)
    {
        var list = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(field, ModuleId));
        options.Apply ??= apply;
        options.Replace(list.IsSuccess ? list.Value.Items : []);
    }

    protected override MedicalExaminationInput BuildInput() => new(
        Id,
        BuildHeader(),
        new MedicalExaminationData
        {
            ContactNo = ContactNo,
            CivilStatus = CivilStatus,
            ChestXray = Row("ChestXray").ToEntry(),
            CBC = Row("CBC").ToEntry(),
            Urinalysis = Row("Urinalysis").ToEntry(),
            Fecalysis = Row("Fecalysis").ToEntry(),
            HBsAg = Row("HBsAg").ToEntry(),
            DrugTest2Panel = Row("DrugTest2Panel").ToEntry(),
            DrugTest4Panel = Row("DrugTest4Panel").ToEntry(),
            Classification = Classification.Value,
            MedicalSurgicalHistory = MedicalSurgicalHistory,
            Assessment = Assessment,
            AssessmentDoneBy = AssessmentDoneBy.Value,
            PhysicianName = PhysicianName.Value,
            PhysicianLicense = PhysicianLicense.Value,
        },
        RowVersion);

    protected override void ShowFields(MedicalExaminationDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        var data = d.Data;
        ContactNo = data.ContactNo;
        CivilStatus = data.CivilStatus;
        Row("ChestXray").Show(data.ChestXray);
        Row("CBC").Show(data.CBC);
        Row("Urinalysis").Show(data.Urinalysis);
        Row("Fecalysis").Show(data.Fecalysis);
        Row("HBsAg").Show(data.HBsAg);
        Row("DrugTest2Panel").Show(data.DrugTest2Panel);
        Row("DrugTest4Panel").Show(data.DrugTest4Panel);
        Classification.Value = data.Classification;
        MedicalSurgicalHistory = data.MedicalSurgicalHistory;
        Assessment = data.Assessment;
        AssessmentDoneBy.Value = data.AssessmentDoneBy;
        PhysicianName.Value = data.PhysicianName;
        PhysicianLicense.Value = data.PhysicianLicense;
        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        ContactNo = null;
        CivilStatus = null;
        foreach (var row in Rows)
            row.Show(new FindingEntry(null, null));

        Classification.Value = null;
        MedicalSurgicalHistory = null;
        Assessment = null;
    }

    // What a new form starts with: the usual wording, the marks and the classification (the person's own details are not part of it).
    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
    [
        new("MedicalSurgicalHistory", () => MedicalSurgicalHistory, v => MedicalSurgicalHistory = v),
        new("Assessment", () => Assessment, v => Assessment = v),
        new("Classification", () => Classification.Value, v => Classification.Value = v),
        .. Rows.Select(r => new DefaultField("State." + r.Name, () => r.State.Value, v => r.State.Value = v)),
        .. Rows.Select(r => new DefaultField("Note." + r.Name, () => r.Remarks, v => r.Remarks = v)),
    ];

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IMedicalExaminationService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditHistoryTemplatesAsync() => EditMultiLineAsync(EntryFields.MerMedicalSurgicalHistory);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditAssessmentTemplatesAsync() => EditMultiLineAsync(EntryFields.MerAssessment);
}
