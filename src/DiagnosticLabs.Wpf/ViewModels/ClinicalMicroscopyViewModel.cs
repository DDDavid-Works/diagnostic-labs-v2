using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One test of the Clinical Microscopy table: a result (picked from a list of its own or typed) and its reference range.</summary>
public sealed class MicroscopyRow
{
    public MicroscopyRow(ClinicalMicroscopyFields.Test test)
    {
        Test = test;
        Choice = test.IsChoice ? new ChoiceField(test.Name, test.Label, test.Entry) : null;
        ResultText = new ValueCell(test.ResultColumn);
        Reference = new ValueCell(test.ReferenceColumn);
    }

    public ClinicalMicroscopyFields.Test Test { get; }

    public string Label => Test.Label;

    public bool IsChoice => Test.IsChoice;

    public bool IsText => !Test.IsChoice;

    /// <summary>The result of a test that is picked from a list.</summary>
    public ChoiceField? Choice { get; }

    /// <summary>The result of a test that is typed (pH, specific gravity, the cell counts).</summary>
    public ValueCell ResultText { get; }

    public ValueCell Reference { get; }

    public string? Result
    {
        get => Choice is not null ? Choice.Value : ResultText.Value;
        set
        {
            if (Choice is not null)
                Choice.Value = value;
            else
                ResultText.Value = value;
        }
    }
}

public sealed record MicroscopySection(string Title, IReadOnlyList<MicroscopyRow> Rows);

/// <summary>
/// Clinical Microscopy: a urine examination in three parts with a result and a reference range for each test, then Remarks. The reference ranges are what a new
/// form starts with (an administrator can change them with the Defaults button); the results start empty. The tests are described once in
/// <see cref="ClinicalMicroscopyFields"/>.
/// </summary>
public partial class ClinicalMicroscopyViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<ClinicalMicroscopyViewModel> logger)
    : LabResultViewModel<IClinicalMicroscopyService, ClinicalMicroscopyDetails, ClinicalMicroscopyInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.ClinicalMicroscopy, "Clinical Microscopy", logger)
{
    public bool ShowRemarks => true;

    public IReadOnlyList<MicroscopySection> Sections { get; } =
        [.. ClinicalMicroscopyFields.Sections.Select(s => new MicroscopySection(s.Title, [.. s.Tests.Select(t => new MicroscopyRow(t))]))];

    private IEnumerable<MicroscopyRow> Rows => Sections.SelectMany(s => s.Rows);

    protected override IEnumerable<ChoiceField> ChoiceFields => Rows.Where(r => r.Choice is not null).Select(r => r.Choice!);

    protected override EntryField RemarksField => EntryFields.ClinicalMicroscopyRemarks;

    protected override ClinicalMicroscopyInput BuildInput()
    {
        var values = new Dictionary<string, string?>();
        foreach (var row in Rows)
        {
            values[row.Test.ResultColumn] = row.Result;
            values[row.Test.ReferenceColumn] = row.Reference.Value;
        }

        return new ClinicalMicroscopyInput(Id, BuildHeader(), new ClinicalMicroscopyData(values), RowVersion);
    }

    protected override void ShowFields(ClinicalMicroscopyDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        foreach (var row in Rows)
        {
            row.Result = d.Data.Value(row.Test.ResultColumn);
            row.Reference.Value = d.Data.Value(row.Test.ReferenceColumn);
        }

        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        foreach (var row in Rows)
        {
            row.ResultText.Value = null;
            row.Reference.Value = null;
        }
    }

    // The reference ranges are what a new form starts with; the results are per person.
    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
        Rows.Select(row => new DefaultField(row.Test.ReferenceColumn, () => row.Reference.Value, v => row.Reference.Value = v));

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IClinicalMicroscopyService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}
