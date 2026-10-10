using CommunityToolkit.Mvvm.ComponentModel;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One test of Clinical Chemistry 2, with normal values, unit and result in conventional and in system units.</summary>
public sealed partial class UnitResultRow(string name, string label) : ObservableObject
{
    public string Name { get; } = name;

    public string Label { get; } = label;

    [ObservableProperty]
    private string? _conventionalNormalValue;

    [ObservableProperty]
    private string? _conventionalUnit;

    [ObservableProperty]
    private string? _conventionalResult;

    [ObservableProperty]
    private string? _systemNormalValue;

    [ObservableProperty]
    private string? _systemUnit;

    [ObservableProperty]
    private string? _systemResult;

    public UnitResultEntry Conventional => new(ConventionalNormalValue, ConventionalUnit, ConventionalResult);

    public UnitResultEntry System => new(SystemNormalValue, SystemUnit, SystemResult);

    public void Show(UnitResultEntry conventional, UnitResultEntry system)
    {
        ConventionalNormalValue = conventional.NormalValue;
        ConventionalUnit = conventional.Unit;
        ConventionalResult = conventional.Result;
        SystemNormalValue = system.NormalValue;
        SystemUnit = system.Unit;
        SystemResult = system.Result;
    }
}

/// <summary>Clinical Chemistry 2: alkaline phosphatase and AST/SGOT in two unit systems. The normal values and units are what a new form starts with.</summary>
public partial class ClinicalChemistry2ViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<ClinicalChemistry2ViewModel> logger)
    : LabResultViewModel<IClinicalChemistry2Service, ClinicalChemistry2Details, ClinicalChemistry2Input>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.ClinicalChemistry2, "Clinical Chemistry 2", logger)
{
    public bool ShowRemarks => false;

    public UnitResultRow AlkalinePhosphatase { get; } = new("AlkalinePhosphatase", "Alkaline Phosphatase");

    public UnitResultRow ASTSGOT { get; } = new("ASTSGOT", "AST/SGOT");

    protected override EntryField RemarksField => EntryFields.ClinicalChemistry2Remarks;

    protected override ClinicalChemistry2Input BuildInput() => new(
        Id,
        BuildHeader(),
        new ClinicalChemistry2Data
        {
            AlkalinePhosphataseConventional = AlkalinePhosphatase.Conventional,
            AlkalinePhosphataseSystem = AlkalinePhosphatase.System,
            ASTSGOTConventional = ASTSGOT.Conventional,
            ASTSGOTSystem = ASTSGOT.System,
        },
        RowVersion);

    protected override void ShowFields(ClinicalChemistry2Details d)
    {
        ShowHeader(d.Header, d.Photo);
        AlkalinePhosphatase.Show(d.Data.AlkalinePhosphataseConventional, d.Data.AlkalinePhosphataseSystem);
        ASTSGOT.Show(d.Data.ASTSGOTConventional, d.Data.ASTSGOTSystem);
        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        var none = new UnitResultEntry(null, null, null);
        AlkalinePhosphatase.Show(none, none);
        ASTSGOT.Show(none, none);
    }

    // The normal values and units are what a new form starts with (the results are per person).
    protected override IEnumerable<DefaultField> ExtraDefaultFields()
    {
        foreach (var row in new[] { AlkalinePhosphatase, ASTSGOT })
        {
            var r = row;
            yield return new(r.Name + "ConventionalNormalValue", () => r.ConventionalNormalValue, v => r.ConventionalNormalValue = v);
            yield return new(r.Name + "ConventionalUnit", () => r.ConventionalUnit, v => r.ConventionalUnit = v);
            yield return new(r.Name + "SystemNormalValue", () => r.SystemNormalValue, v => r.SystemNormalValue = v);
            yield return new(r.Name + "SystemUnit", () => r.SystemUnit, v => r.SystemUnit = v);
        }
    }

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IClinicalChemistry2Service, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}
