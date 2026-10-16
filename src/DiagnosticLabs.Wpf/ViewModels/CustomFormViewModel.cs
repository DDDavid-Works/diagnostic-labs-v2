using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>
/// Runs every call of one custom form's screen in a scope that knows which form it is for: <see cref="CustomFormContext"/> is set before the service is
/// asked for, because one <see cref="CustomFormResultService"/> class serves all the forms.
/// </summary>
public sealed class ModuleBoundRunner(IServiceScopeFactory scopeFactory, int moduleId) : IServiceRunner
{
    public async Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<CustomFormContext>().ModuleId = moduleId;
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}

/// <summary>One row of a group on the entry screen: a test (result picked from the row's own list or typed, and a reference value) or a heading line.</summary>
public sealed class CustomRowViewModel
{
    public CustomRowViewModel(RowDefinition row, bool showReference, int moduleId)
    {
        Id = row.Id;
        Label = row.Name;
        IsSection = row.Kind == RowKind.Section;
        ShowReference = showReference;
        if (!IsSection)
        {
            Choice = new ChoiceField(CustomFormValues.ResultKey(row.Id), row.Name, new EntryField(row.Id.ToString("N"), EntryKind.SingleLine, moduleId, Title: row.Name));
            Reference = new ValueCell(CustomFormValues.ReferenceKey(row.Id));
        }
    }

    public Guid Id { get; }

    public string Label { get; }

    public bool IsSection { get; }

    public bool IsTest => !IsSection;

    /// <summary>Whether the group the row is in shows the Reference/Normal Values column.</summary>
    public bool ShowReference { get; }

    public bool ShowReferenceCell => IsTest && ShowReference;

    public ChoiceField? Choice { get; }

    public ValueCell? Reference { get; }
}

public sealed record CustomGroupViewModel(string? Title, bool ShowReference, IReadOnlyList<CustomRowViewModel> Rows)
{
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}

/// <summary>A multiline text box of the form, with the reusable texts kept for it in the entry builder.</summary>
public sealed partial class CustomTextboxViewModel(TextboxDefinition box, int moduleId) : ObservableObject
{
    public Guid Id { get; } = box.Id;

    public string Label { get; } = box.Label;

    public string TextKey { get; } = CustomFormValues.TextKey(box.Id);

    public EntryField Entry { get; } = new(box.Id.ToString("N"), EntryKind.MultiLine, moduleId, Title: box.Label);

    public TemplateOptions Templates { get; } = new();

    [ObservableProperty]
    private string? _text;

    [ObservableProperty]
    private ICommand? _editCommand;
}

/// <summary>
/// The entry screen of a form made in the Custom Lab Form designer (Home opens it from the service's chip, the menu from its module). The rows come from the form's
/// design: a new result uses the newest design, a saved one is shown with the design it was saved with. Everything else (registration, patient block, Remarks,
/// signatories, defaults, dirty tracking) is the same as on every other result screen.
/// </summary>
public partial class CustomFormViewModel(
    IServiceScopeFactory scopeFactory,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    int moduleId,
    string title,
    ILogger<CustomFormViewModel> logger)
    : LabResultViewModel<ICustomFormResultService, CustomFormDetails, CustomFormInput>(
        new ModuleBoundRunner(scopeFactory, moduleId), dialogs, user, entryBuilder, preview, moduleId, title, logger)
{
    private CustomFormDesign? _shown;
    private CustomFormDesign? _latest;
    private readonly int _moduleId = moduleId;

    public ObservableCollection<CustomGroupViewModel> Groups { get; } = [];

    public ObservableCollection<CustomTextboxViewModel> Textboxes { get; } = [];

    [ObservableProperty]
    private bool _showRemarks = true;

    [ObservableProperty]
    private bool _showFirstTechnologist = true;

    [ObservableProperty]
    private bool _showSecondTechnologist;

    [ObservableProperty]
    private bool _showPathologist = true;

    [ObservableProperty]
    private bool _showLicenses = true;

    [ObservableProperty]
    private bool _hasSignatories = true;

    [ObservableProperty]
    private string _designNote = string.Empty;

    public bool HasDesignNote => DesignNote.Length > 0;

    partial void OnDesignNoteChanged(string value) => OnPropertyChanged(nameof(HasDesignNote));

    public bool HasGroups => Groups.Count > 0;

    private IEnumerable<CustomRowViewModel> Rows => Groups.SelectMany(g => g.Rows);

    private IEnumerable<CustomRowViewModel> TestRows => Rows.Where(r => r.IsTest);

    protected override IEnumerable<ChoiceField> ChoiceFields => TestRows.Select(r => r.Choice!);

    protected override EntryField RemarksField => new("Remarks", EntryKind.MultiLine, _moduleId);

    // ---------------------------------------------------------------- the design on screen

    protected override async Task OnInitializeAsync()
    {
        var result = await Call<ICustomFormResultService, Result<CustomFormDesign>>(s => s.GetDesignAsync());
        if (result.IsSuccess)
        {
            _latest = result.Value;
            ApplyDesign(result.Value, reloadLists: false);
        }
        else
        {
            ShowError(result.Error.Message);
        }

        await base.OnInitializeAsync();
        await LoadTextboxTemplatesAsync();
    }

    /// <summary>Builds the rows, text boxes and footer of the screen from a design. Nothing changes when that design is already on screen.</summary>
    private void ApplyDesign(CustomFormDesign design, bool reloadLists)
    {
        if (_shown?.VersionId == design.VersionId)
            return;

        _shown = design;
        var body = design.Definition.Body;
        Groups.Clear();
        foreach (var group in body.Groups)
            Groups.Add(new CustomGroupViewModel(group.Title, group.ShowReference, [.. group.Rows.Select(r => new CustomRowViewModel(r, group.ShowReference, _moduleId))]));

        Textboxes.Clear();
        foreach (var box in body.Textboxes)
            Textboxes.Add(new CustomTextboxViewModel(box, _moduleId));

        var footer = design.Definition.Footer;
        var technologists = footer.Signatories.Count(s => s.Role == SignatoryRole.MedicalTechnologist);
        ShowRemarks = footer.ShowRemarks;
        ShowFirstTechnologist = technologists >= 1;
        ShowSecondTechnologist = technologists >= 2;
        ShowPathologist = footer.Signatories.Any(s => s.Role == SignatoryRole.Pathologist);
        ShowLicenses = footer.ShowLicenseNumber;
        HasSignatories = footer.Signatories.Count > 0;
        OnPropertyChanged(nameof(HasGroups));

        DesignNote = _latest is { } latest && latest.VersionId != design.VersionId
            ? $"This result was saved with an earlier design of the form (version {design.VersionNumber}). It is shown as it was."
            : string.Empty;

        if (reloadLists)
            Fire(async () =>
            {
                await LoadListsAsync();
                await LoadTextboxTemplatesAsync();
            });
    }

    private async Task LoadTextboxTemplatesAsync()
    {
        foreach (var box in Textboxes)
        {
            var current = box;
            current.Templates.Apply ??= text => current.Text = text;
            current.EditCommand ??= new AsyncRelayCommand(() => EditMultiLineAsync(current.Entry), () => CanEditLists);
            var list = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(current.Entry, ModuleId));
            current.Templates.Replace(list.IsSuccess ? list.Value.Items : []);
        }
    }

    protected override async Task ReloadChoicesAsync()
    {
        await base.ReloadChoicesAsync();
        await LoadTextboxTemplatesAsync();
    }

    // ---------------------------------------------------------------- the form fields

    protected override CustomFormInput BuildInput()
    {
        var values = new Dictionary<string, string?>();
        foreach (var row in TestRows)
        {
            values[row.Choice!.Name] = row.Choice.Value;
            values[row.Reference!.Name] = row.Reference.Value;
        }

        foreach (var box in Textboxes)
            values[box.TextKey] = box.Text;

        return new CustomFormInput(Id, BuildHeader(), values, RowVersion);
    }

    protected override void ShowFields(CustomFormDetails d)
    {
        ApplyDesign(d.Design, reloadLists: true);
        ShowHeader(d.Header, d.Photo);
        foreach (var row in TestRows)
        {
            row.Choice!.Value = d.Values.GetValueOrDefault(row.Choice.Name);
            row.Reference!.Value = d.Values.GetValueOrDefault(row.Reference.Name);
        }

        foreach (var box in Textboxes)
            box.Text = d.Values.GetValueOrDefault(box.TextKey);

        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        // A new result is made with the newest design, even when the screen was showing an older result.
        if (_latest is not null)
            ApplyDesign(_latest, reloadLists: true);

        foreach (var row in TestRows)
            row.Reference!.Value = null;

        foreach (var box in Textboxes)
            box.Text = null;
    }

    // The reference values and the text of the text boxes are what a new result can start with (the result fields are covered for every screen).
    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
    [
        .. TestRows.Select(r => new DefaultField(r.Reference!.Name, () => r.Reference.Value, v => r.Reference.Value = v)),
        .. Textboxes.Select(t => new DefaultField(t.TextKey, () => t.Text, v => t.Text = v)),
    ];

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<ICustomFormResultService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}
