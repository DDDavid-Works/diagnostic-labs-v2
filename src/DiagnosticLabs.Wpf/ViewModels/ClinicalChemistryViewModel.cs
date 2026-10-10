using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>Clinical Chemistry: nine tests, each with its normal values (what a new form starts with) and a result. There are no remarks on this form.</summary>
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
    /// <summary>The first cell of the table's header.</summary>
    public string TableTitle => "Test";

    public bool ShowRemarks => false;

    public IReadOnlyList<NormalResultRow> Rows { get; } =
        [.. ClinicalChemistryService.Tests.Select(t => new NormalResultRow(t.Name, t.Label, false))];

    protected override EntryField RemarksField => EntryFields.ClinicalChemistryRemarks;

    private NormalResultRow Row(string name) => Rows.First(r => r.Name == name);

    protected override ClinicalChemistryInput BuildInput() => new(
        Id,
        BuildHeader(),
        new ClinicalChemistryData
        {
            FBS = Row("FBS").ToEntry(),
            TotalCholesterol = Row("TotalCholesterol").ToEntry(),
            Triglycerides = Row("Triglycerides").ToEntry(),
            HDL = Row("HDL").ToEntry(),
            BUN = Row("BUN").ToEntry(),
            Creatinine = Row("Creatinine").ToEntry(),
            BloodUricAcid = Row("BloodUricAcid").ToEntry(),
            LDL = Row("LDL").ToEntry(),
            ALTSGPT = Row("ALTSGPT").ToEntry(),
        },
        RowVersion);

    protected override void ShowFields(ClinicalChemistryDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        Row("FBS").Show(d.Data.FBS);
        Row("TotalCholesterol").Show(d.Data.TotalCholesterol);
        Row("Triglycerides").Show(d.Data.Triglycerides);
        Row("HDL").Show(d.Data.HDL);
        Row("BUN").Show(d.Data.BUN);
        Row("Creatinine").Show(d.Data.Creatinine);
        Row("BloodUricAcid").Show(d.Data.BloodUricAcid);
        Row("LDL").Show(d.Data.LDL);
        Row("ALTSGPT").Show(d.Data.ALTSGPT);
        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        foreach (var row in Rows)
            row.Show(new NormalResultEntry(null, null));
    }

    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
        Rows.Select(r => new DefaultField(r.Name + "NormalValue", () => r.NormalValue, v => r.NormalValue = v));

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IClinicalChemistryService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}
