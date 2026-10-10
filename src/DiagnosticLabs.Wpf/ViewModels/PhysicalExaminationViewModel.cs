using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
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

    /// <summary>The cells of the blood count and the three free texts.</summary>
    public IReadOnlyDictionary<string, ValueCell> Cells { get; } = PhysicalExaminationFields.CbcCells
        .Concat(PhysicalExaminationFields.Texts.Select(t => t.Name))
        .ToDictionary(n => n, n => new ValueCell(n));

    /// <summary>The fields picked from a list of their own (blood typing, urinalysis, fecalysis).</summary>
    public IReadOnlyDictionary<string, ChoiceField> Choices { get; } = PhysicalExaminationFields.Choices
        .ToDictionary(c => c.Name, c => new ChoiceField(c.Name, c.Label, c.Entry));

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
        foreach (var (name, templates) in new[] { ("UrineOthers", UrineOthersTemplates), ("FecalysisResult", FecalysisResultTemplates), ("Others", OthersTemplates) })
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
    }

    protected override void ResetDetail()
    {
        foreach (var cell in Dates.Values)
            cell.Value = null;

        foreach (var cell in Cells.Values)
            cell.Value = null;
    }

    // The normal values of the blood count are what a new form starts with (the results are per person); the dates start empty.
    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
        Cells.Values.Where(c => c.Name.EndsWith("NValue", StringComparison.Ordinal)).Select(c => new DefaultField(c.Name, () => c.Value, v => c.Value = v));

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IPhysicalExaminationService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditUrineOthersTemplatesAsync() => EditMultiLineAsync(PhysicalExaminationFields.Texts.First(t => t.Name == "UrineOthers").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditFecalysisResultTemplatesAsync() => EditMultiLineAsync(PhysicalExaminationFields.Texts.First(t => t.Name == "FecalysisResult").Entry);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditOthersTemplatesAsync() => EditMultiLineAsync(PhysicalExaminationFields.Texts.First(t => t.Name == "Others").Entry);
}
