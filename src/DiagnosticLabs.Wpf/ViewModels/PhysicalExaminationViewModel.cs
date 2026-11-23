using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One typed value of the form, found by name (<c>Cells[HematocritResult].Value</c>).</summary>
public sealed partial class ValueCell(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    private string? _value;
}

/// <summary>The date of one section of the form; empty until the user picks one.</summary>
public sealed partial class DateCell(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    private DateTime? _value;
}

/// <summary>
/// Physical Examination: the printed "Laboratory Results" sheet with a complete blood count, blood typing, urinalysis and fecalysis section (each with its own
/// date) and an Others box. The normal values of the blood count are what a new form starts with; everything else starts empty. The fields are
/// described once in <see cref="PhysicalExaminationFields"/> and found by name, so the screen binds to <c>Cells[...]</c>, <c>Choices[...]</c> and <c>Dates[...]</c>.
/// </summary>
public partial class PhysicalExaminationViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<PhysicalExaminationViewModel> logger)
    : LabResultViewModel<IPhysicalExaminationService, PhysicalExaminationDetails, PhysicalExaminationInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.PhysicalExamination, "Physical Examination", logger)
{
    public bool ShowRemarks => false;

    public IReadOnlyDictionary<string, DateCell> Dates { get; } = PhysicalExaminationFields.Dates.ToDictionary(n => n, n => new DateCell(n));

    /// <summary>The cells of the blood count and the four free texts.</summary>
    public IReadOnlyDictionary<string, ValueCell> Cells { get; } = PhysicalExaminationFields.CbcCells
        .Concat(PhysicalExaminationFields.Texts.Select(t => t.Name))
        .ToDictionary(n => n, n => new ValueCell(n));

    /// <summary>The fields picked from a list of their own (blood typing, urinalysis, fecalysis).</summary>
    public IReadOnlyDictionary<string, ChoiceField> Choices { get; } = PhysicalExaminationFields.Choices
        .ToDictionary(c => c.Name, c => new ChoiceField(c.Name, c.Label, c.Entry));

    public TemplateOptions BloodTypingNotesTemplates { get; } = new();

    public TemplateOptions UrineOthersTemplates { get; } = new();

    public TemplateOptions FecalysisResultTemplates { get; } = new();

    public TemplateOptions OthersTemplates { get; } = new();

    protected override IEnumerable<ChoiceField> ChoiceFields => Choices.Values;

    // The sheet has no Remarks box; the base screen still wants a list for it.
    protected override EntryField RemarksField => EntryFields.PhysicalExaminationRemarks;

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
        foreach (var (name, templates) in new[] { ("BloodTypingNotes", BloodTypingNotesTemplates), ("UrineOthers", UrineOthersTemplates), ("FecalysisResult", FecalysisResultTemplates), ("Others", OthersTemplates) })
        {
            var cell = Cells[name];
            var entry = PhysicalExaminationFields.Texts.First(t => t.Name == name).Entry;
            var list = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(entry, ModuleId));
            templates.Apply ??= text => cell.Value = text;
            templates.Replace(list.IsSuccess ? list.Value.Items : []);
        }
    }

    protected override PhysicalExaminationInput BuildInput() => new(
        Id,
        BuildHeader(),
        new PhysicalExaminationData(
            Dates.ToDictionary(d => d.Key, d => d.Value.Value is { } date ? (DateOnly?)DateOnly.FromDateTime(date) : null),
            Cells.ToDictionary(c => c.Key, c => c.Value.Value).Concat(Choices.ToDictionary(c => c.Key, c => c.Value.Value)).ToDictionary(x => x.Key, x => x.Value)),
        RowVersion);

    protected override void ShowFields(PhysicalExaminationDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        foreach (var (name, cell) in Dates)
            cell.Value = d.Data.Date(name)?.ToDateTime(TimeOnly.MinValue);

        foreach (var (name, cell) in Cells)
            cell.Value = d.Data.Value(name);

        foreach (var (name, choice) in Choices)
            choice.Value = d.Data.Value(name);

        RowVersion = d.RowVersion;
        ApplyRequestedSection(d.Id);
    }

    protected override void ResetDetail()
    {
        foreach (var cell in Dates.Values)
            cell.Value = null;

        foreach (var cell in Cells.Values)
            cell.Value = null;
    }

    // The normal values of the blood count and the blood typing notes are what a new form starts with (the results are per person); the dates start empty.
    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
        Cells.Values.Where(c => c.Name.EndsWith("NValue", StringComparison.Ordinal) || c.Name == "BloodTypingNotes")
            .Select(c => new DefaultField(c.Name, () => c.Value, v => c.Value = v));

    // ----- the sections charged as services of their own (Complete Blood Count, Urinalysis, Fecalysis) -----

    private (long ResultId, FormSection Section)? _requestedSection;

    /// <summary>
    /// Opened from the home screen for one section: on a sheet that exists (<paramref name="resultId"/>) that section takes its defaults if it is
    /// still empty; on a new sheet (0) it keeps them.
    /// </summary>
    public void RequestSection(long resultId, FormSection section) => _requestedSection = (resultId, section);

    private string? ValueOf(string name) =>
        Cells.TryGetValue(name, out var cell) ? cell.Value : Choices.TryGetValue(name, out var choice) ? choice.Value : null;

    private void SetValue(string name, string? value)
    {
        if (Cells.TryGetValue(name, out var cell))
            cell.Value = value;
        else if (Choices.TryGetValue(name, out var choice))
            choice.Value = value;
    }

    // A new sheet for a registration: the sections it is not charged for (or that go on their own forms) print blank, so the defaults leave them.
    // A value that is not the default was typed in and stays.
    protected override void OnRegistrationPickedForNew(ResultRegistration registration)
    {
        // the section this new sheet was opened for (Urinalysis picked for the sheet on the home screen) is one of its own
        FormSection? requested = _requestedSection is { ResultId: 0 } r ? r.Section : null;
        if (requested is not null)
            _requestedSection = null;

        foreach (var (section, fields) in PhysicalExaminationFields.SectionFields)
        {
            if (registration.SheetSections.Contains(section) || section == requested)
                continue;

            foreach (var name in fields.Where(n => ValueOf(n) is not null && ValueOf(n) == DefaultOf(n)))
                SetValue(name, null);
        }
    }

    private void ApplyRequestedSection(long id)
    {
        if (_requestedSection is not { } requested || requested.ResultId != id)
            return;

        _requestedSection = null;
        var fields = PhysicalExaminationFields.SectionFields[requested.Section];
        if (PhysicalExaminationFields.IsFilled(requested.Section, ValueOf))
            return;

        foreach (var name in fields)
            SetValue(name, DefaultOf(name));
    }

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IPhysicalExaminationService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditBloodTypingNotesTemplatesAsync() => EditMultiLineAsync(PhysicalExaminationFields.Texts.First(t => t.Name == "BloodTypingNotes").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditUrineOthersTemplatesAsync() => EditMultiLineAsync(PhysicalExaminationFields.Texts.First(t => t.Name == "UrineOthers").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditFecalysisResultTemplatesAsync() => EditMultiLineAsync(PhysicalExaminationFields.Texts.First(t => t.Name == "FecalysisResult").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditOthersTemplatesAsync() => EditMultiLineAsync(PhysicalExaminationFields.Texts.First(t => t.Name == "Others").Entry);
}
