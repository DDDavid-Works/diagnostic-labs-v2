using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>Serology and Immunology: the test (picked from a maintained list), its result text, then the usual remarks and signatories.</summary>
public abstract partial class TestResultViewModel<TService>(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    int moduleId,
    string title,
    EntryField testField,
    EntryField resultField,
    EntryField remarksField,
    ILogger logger,
    bool hasTest = true)
    : LabResultViewModel<TService, TestResultDetails, TestResultInput>(runner, dialogs, user, entryBuilder, preview, moduleId, title, logger)
    where TService : ICrudService<LabResultListItem, TestResultDetails, TestResultInput>, ILabResultPrinting
{
    private readonly EntryField _resultField = resultField;
    private readonly EntryField _remarksField = remarksField;

    public ChoiceField Test { get; } = new("Test", "Test", testField);

    [ObservableProperty]
    private string? _result;

    public TemplateOptions ResultTemplates { get; } = new();

    /// <summary>Whether the form has a test to pick (the pregnancy test has only its result).</summary>
    public bool HasTest { get; } = hasTest;

    protected override IEnumerable<ChoiceField> ChoiceFields => HasTest ? [Test] : [];

    protected override EntryField RemarksField => _remarksField;

    protected override async Task OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await LoadResultTemplatesAsync();
    }

    protected override async Task ReloadChoicesAsync()
    {
        await base.ReloadChoicesAsync();
        await LoadResultTemplatesAsync();
    }

    private async Task LoadResultTemplatesAsync()
    {
        var results = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(_resultField, ModuleId));
        ResultTemplates.Apply ??= text => Result = text;
        ResultTemplates.Replace(results.IsSuccess ? results.Value.Items : []);
    }

    protected override TestResultInput BuildInput() => new(Id, BuildHeader(), Test.Value, Result, RowVersion);

    protected override void ShowFields(TestResultDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        Test.Value = d.Test;
        Result = d.Result;
        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail() => Result = null;

    protected override IEnumerable<DefaultField> ExtraDefaultFields() => [new("Result", () => Result, v => Result = v)];

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditResultTemplatesAsync() => EditMultiLineAsync(_resultField);
}

public sealed class SerologyViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<SerologyViewModel> logger)
    : TestResultViewModel<ISerologyService>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.Serology, "Serology",
        EntryFields.SerologyTest, EntryFields.SerologyResult, EntryFields.SerologyRemarks, logger)
{
    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<ISerologyService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}

public sealed class ImmunologyViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<ImmunologyViewModel> logger)
    : TestResultViewModel<IImmunologyService>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.Immunology, "Immunology",
        EntryFields.ImmunologyTest, EntryFields.ImmunologyResult, EntryFields.ImmunologyRemarks, logger)
{
    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IImmunologyService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}

public sealed class PregnancyTestViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<PregnancyTestViewModel> logger)
    : TestResultViewModel<IPregnancyTestService>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.PregnancyTest, "Pregnancy Test",
        EntryFields.SerologyTest, EntryFields.PregnancyResult, EntryFields.PregnancyRemarks, logger, hasTest: false)
{
    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IPregnancyTestService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}

public sealed class ClinicalChemistry1ViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<ClinicalChemistry1ViewModel> logger)
    : TestResultViewModel<IClinicalChemistry1Service>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.ClinicalChemistry1, "Clinical Chemistry 1",
        EntryFields.ClinicalChemistry1Test, EntryFields.ClinicalChemistry1Result, EntryFields.ClinicalChemistry1Remarks, logger)
{
    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IClinicalChemistry1Service, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}