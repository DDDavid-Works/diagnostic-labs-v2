using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One row of ticks on the form (a history item, a review-of-systems row, a laboratory line): its label, its two ticks and, for a laboratory line, a note.</summary>
public sealed class MarkRow(string name, string label, ExclusiveGroup state, ValueCell? note = null, ChoiceField? noteList = null)
{
    public string Name { get; } = name;

    public string Label { get; } = label;

    public ExclusiveGroup State { get; } = state;

    public ExclusiveOption First => State.Options[0];

    public ExclusiveOption Second => State.Options[1];

    /// <summary>The note beside a laboratory line; none for the other rows.</summary>
    public ValueCell? Note { get; } = note;

    /// <summary>The Entry Builder list the note is picked from.</summary>
    public ChoiceField? NoteList { get; } = noteList;
}

/// <summary>A person who signs a line at the end of the form: the name (picked from the technologist list or typed) and the licence number that follows it.</summary>
public sealed partial class SignatoryEntry(string name, string label, Func<string?, string?> licenseOf) : ObservableObject
{
    /// <summary>While a saved form is shown, the saved licence must not be replaced by the one from the list.</summary>
    public bool Loading { get; set; }

    public string Name { get; } = name;

    public string Label { get; } = label;

    [ObservableProperty]
    private string? _person;

    [ObservableProperty]
    private string? _license;

    partial void OnPersonChanged(string? value)
    {
        if (!Loading)
            License = licenseOf(value);
    }
}

/// <summary>
/// Everything the Medical Examination Report holds beyond the shared header, built from <see cref="MedicalExamFields"/> so the screen, the saved values and
/// the page design use the same names: the ticks (<see cref="Marks"/>), the typed values (<see cref="Cells"/>) and the four signatories.
/// </summary>
public sealed class MedicalExamForm
{
    /// <summary>Not part of the saved defaults: they belong to the person.</summary>
    private static readonly string[] PersonFields = ["CivilStatus", "ContactNo"];

    public MedicalExamForm()
    {
        var signatoryFields = MedicalExamFields.Signatories.SelectMany(s => new[] { s.Name, s.Name + "License" }).ToHashSet();
        foreach (var field in MedicalExamFields.All)
        {
            if (field.Options is { } options)
                _marks[field.Name] = new ExclusiveGroup([.. options]);
            else if (!signatoryFields.Contains(field.Name))
                _cells[field.Name] = new ValueCell(field.Name);
        }

        HistoryColumns = [.. MedicalExamFields.HistoryColumns.Select(c => (IReadOnlyList<MarkRow>)[.. c.Select(r => new MarkRow(r.Name, r.Label, Marks[r.Name]))])];
        ReviewColumns = [.. MedicalExamFields.ReviewColumns.Select(c => (IReadOnlyList<MarkRow>)[.. c.Select(r => new MarkRow(r.Name, r.Label, Marks[r.Name]))])];
        FamilyRows = [.. MedicalExamFields.Family.Select(r => new MarkRow(r.Name, r.Label, Marks[r.Name]))];
        ObRows = [.. MedicalExamFields.ObYesNo.Select(r => new MarkRow(r.Name, r.Label, Marks[r.Name]))];
        LabRows = [.. MedicalExamFields.LabRows.Select(r => new MarkRow(r.Name, r.Label, Marks[r.Name], Cells[r.Name + "Note"], new ChoiceField(r.Name + "NoteList", r.Label + " note", MedicalExamFields.LabNoteEntry(r.Name))))];
        Signatories = [.. MedicalExamFields.Signatories.Select(s => new SignatoryEntry(s.Name, s.Label, name => LicenseOf?.Invoke(name)))];
    }

    /// <summary>Finds the licence number of a person in the technologist list (set by the screen once the lists are loaded).</summary>
    public Func<string?, string?>? LicenseOf { get; set; }

    private readonly Dictionary<string, ExclusiveGroup> _marks = [];
    private readonly Dictionary<string, ValueCell> _cells = [];

    /// <summary>The ticks that exclude each other (No/Yes, N/F, A-D, ...), by field name.</summary>
    public IReadOnlyDictionary<string, ExclusiveGroup> Marks => _marks;

    /// <summary>The typed values, by field name.</summary>
    public IReadOnlyDictionary<string, ValueCell> Cells => _cells;

    public IReadOnlyList<IReadOnlyList<MarkRow>> HistoryColumns { get; }

    public IReadOnlyList<IReadOnlyList<MarkRow>> ReviewColumns { get; }

    public IReadOnlyList<MarkRow> FamilyRows { get; }

    public IReadOnlyList<MarkRow> ObRows { get; }

    public IReadOnlyList<MarkRow> LabRows { get; }

    public IReadOnlyList<SignatoryEntry> Signatories { get; }

    /// <summary>Every value by name, as the service saves them.</summary>
    public Dictionary<string, string?> Read()
    {
        var values = new Dictionary<string, string?>();
        foreach (var (name, group) in Marks)
            values[name] = group.Value;

        foreach (var (name, cell) in Cells)
            values[name] = cell.Value;

        foreach (var signatory in Signatories)
        {
            values[signatory.Name] = signatory.Person;
            values[signatory.Name + "License"] = signatory.License;
        }

        return values;
    }

    public void Show(MedicalExamData data)
    {
        foreach (var (name, group) in Marks)
            group.Value = data.Value(name);

        foreach (var (name, cell) in Cells)
            cell.Value = data.Value(name);

        foreach (var signatory in Signatories)
        {
            signatory.Loading = true;
            try
            {
                signatory.Person = data.Value(signatory.Name);
                signatory.License = data.Value(signatory.Name + "License");
            }
            finally
            {
                signatory.Loading = false;
            }
        }
    }

    public void Clear()
    {
        foreach (var group in Marks.Values)
            group.Value = null;

        foreach (var cell in Cells.Values)
            cell.Value = null;

        foreach (var signatory in Signatories)
        {
            signatory.Person = null;
            signatory.License = null;
        }
    }

    /// <summary>What a new form can start with: every tick, typed value and signatory name except the person's own details.</summary>
    public IEnumerable<DefaultField> DefaultFields() =>
    [
        .. Marks.Select(m => new DefaultField(m.Key, () => m.Value.Value, v => m.Value.Value = v)),
        .. Cells.Where(c => !PersonFields.Contains(c.Key)).Select(c => new DefaultField(c.Key, () => c.Value.Value, v => c.Value.Value = v)),
        .. Signatories.Select(s => new DefaultField(s.Name, () => s.Person, v => s.Person = v)),
    ];
}

/// <summary>
/// The Medical Examination Report: the client's two-page form. The screen has three tabs (History, Physical Exam, and Page 2 with the laboratory results,
/// classification, assessment and sign-off); the values are described once in <see cref="MedicalExamFields"/> and found by name through <see cref="Form"/>.
/// The physician who signs is the header's Pathologist; the other people who sign are saved on the form.
/// </summary>
public partial class MedicalExamReportViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<MedicalExamReportViewModel> logger)
    : LabResultViewModel<IMedicalExamReportService, MedicalExamReportDetails, MedicalExamReportInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.MedicalExamReport, MedicalExamReportService.ScreenName, logger)
{
    private bool _wired;

    public MedicalExamForm Form { get; } = new();

    /// <summary>The lists behind the two single-line "Others" boxes; the typed value itself is in <see cref="Form"/> (<c>Cells[FamOthers]</c>, <c>Cells[ObOthers]</c>).</summary>
    public ChoiceField FamOthersChoices { get; } = new("FamOthersList", "Family History Others", MedicalExamFields.FamOthersEntry);

    public ChoiceField ObOthersChoices { get; } = new("ObOthersList", "OB-Gyne Others", MedicalExamFields.ObOthersEntry);

    public ChoiceField IshiharaChoices { get; } = new("IshiharaList", "Ishihara Test", MedicalExamFields.IshiharaEntry);

    protected override IEnumerable<ChoiceField> ChoiceFields =>
        [FamOthersChoices, ObOthersChoices, IshiharaChoices, .. Form.LabRows.Select(r => r.NoteList!)];

    public TemplateOptions BmiCategoryTemplates { get; } = new();

    public TemplateOptions FindingsTemplates { get; } = new();

    public TemplateOptions AssessmentTemplates { get; } = new();

    public TemplateOptions HxOthersTemplates { get; } = new();

    public TemplateOptions HxMedicationTemplates { get; } = new();

    /// <summary>The OB-Gyne history is for female applicants; it is switched off once the sex is male (and open while it is not known).</summary>
    public bool IsObGyneEnabled => !(Sex ?? string.Empty).Trim().StartsWith("M", StringComparison.OrdinalIgnoreCase);

    protected override EntryField RemarksField => EntryFields.MedicalExamReportRemarks;

    protected override void OnPersonPicked(DateOnly? dateOfBirth, string? civilStatus, string? contactNumbers)
    {
        if (!string.IsNullOrWhiteSpace(civilStatus))
            Form.Cells["CivilStatus"].Value = civilStatus;
        if (!string.IsNullOrWhiteSpace(contactNumbers))
            Form.Cells["ContactNo"].Value = contactNumbers;
    }

    protected override async Task OnInitializeAsync()
    {
        if (!_wired)
        {
            _wired = true;
            Form.LicenseOf = TechnologistLicenseOf;
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Sex))
                    OnPropertyChanged(nameof(IsObGyneEnabled));
            };
        }

        await base.OnInitializeAsync();
        await LoadTextTemplatesAsync();
    }

    protected override async Task ReloadChoicesAsync()
    {
        await base.ReloadChoicesAsync();
        await LoadTextTemplatesAsync();
    }

    private TemplateOptions TemplatesOf(string name) => name switch
    {
        "BmiCategory" => BmiCategoryTemplates,
        "Findings" => FindingsTemplates,
        "HxOthers" => HxOthersTemplates,
        "HxMedication" => HxMedicationTemplates,
        _ => AssessmentTemplates,
    };

    private async Task LoadTextTemplatesAsync()
    {
        foreach (var (name, entry) in MedicalExamFields.TemplateTexts)
        {
            var templates = TemplatesOf(name);
            var cell = Form.Cells[name];
            var list = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(entry, ModuleId));
            templates.Apply ??= text => cell.Value = text;
            templates.Replace(list.IsSuccess ? list.Value.Items : []);
        }
    }

    protected override MedicalExamReportInput BuildInput() => new(Id, BuildHeader(), new MedicalExamData(Form.Read()), RowVersion);

    protected override void ShowFields(MedicalExamReportDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        Form.Show(d.Data);
        RowVersion = d.RowVersion;
        OnPropertyChanged(nameof(IsObGyneEnabled));
    }

    protected override void ResetDetail()
    {
        Form.Clear();
        OnPropertyChanged(nameof(IsObGyneEnabled));
    }

    protected override IEnumerable<DefaultField> ExtraDefaultFields() => Form.DefaultFields();

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IMedicalExamReportService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditBmiCategoryTemplatesAsync() => EditMultiLineAsync(MedicalExamFields.TemplateTexts.First(t => t.Name == "BmiCategory").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditFindingsTemplatesAsync() => EditMultiLineAsync(MedicalExamFields.TemplateTexts.First(t => t.Name == "Findings").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditHxOthersTemplatesAsync() => EditMultiLineAsync(MedicalExamFields.TemplateTexts.First(t => t.Name == "HxOthers").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditHxMedicationTemplatesAsync() => EditMultiLineAsync(MedicalExamFields.TemplateTexts.First(t => t.Name == "HxMedication").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditAssessmentTemplatesAsync() => EditMultiLineAsync(MedicalExamFields.TemplateTexts.First(t => t.Name == "Assessment").Entry);
}
