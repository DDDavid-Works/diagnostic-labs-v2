using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>
/// Clinical Chemistry: ten tests, each with a result, a unit and a reference range in conventional and in S.I. units. The units and
/// reference ranges are what a new form starts with (an administrator can change them with the Defaults button); the results are per person.
/// </summary>
public partial class ClinicalChemistryViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<ClinicalChemistryViewModel> logger)
    : LabResultViewModel<IClinicalChemistryService, ClinicalChemistryDetails, ClinicalChemistryInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.ClinicalChemistry, "Clinical Chemistry", logger)
{
    public bool ShowRemarks => true;

    public IReadOnlyList<UnitResultRow> Rows { get; } = [.. ClinicalChemistryService.Tests.Select(t => new UnitResultRow(t.Name, t.Label))];

    protected override EntryField RemarksField => EntryFields.ClinicalChemistryRemarks;

    protected override ClinicalChemistryInput BuildInput() => new(
        Id,
        BuildHeader(),
        new ClinicalChemistryData(Rows.ToDictionary(r => r.Name, r => new ChemistryTestEntry(r.Conventional, r.System))),
        RowVersion);

    protected override void ShowFields(ClinicalChemistryDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        foreach (var row in Rows)
        {
            var test = d.Data.Of(row.Name);
            row.Show(test.Conventional, test.System);
        }

        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        foreach (var row in Rows)
            row.Show(ChemistryTestEntry.Empty.Conventional, ChemistryTestEntry.Empty.System);
    }

    // The units and reference ranges are what a new form starts with (the results are per person).
    protected override IEnumerable<DefaultField> ExtraDefaultFields()
    {
        foreach (var row in Rows)
        {
            var r = row;
            yield return new(r.Name + "ConventionalNormalValue", () => r.ConventionalNormalValue, v => r.ConventionalNormalValue = v);
            yield return new(r.Name + "ConventionalUnit", () => r.ConventionalUnit, v => r.ConventionalUnit = v);
            yield return new(r.Name + "SystemNormalValue", () => r.SystemNormalValue, v => r.SystemNormalValue = v);
            yield return new(r.Name + "SystemUnit", () => r.SystemUnit, v => r.SystemUnit = v);
        }
    }

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IClinicalChemistryService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}
