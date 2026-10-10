using CommunityToolkit.Mvvm.ComponentModel;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One line of the hematology table: what it is called, its normal values and its result.</summary>
public sealed partial class HematologyRow(string name, string label, bool indented) : ObservableObject
{
    public string Name { get; } = name;

    public string Label { get; } = label;

    /// <summary>The white cell differential lines are indented under "White Blood Cell Count".</summary>
    public bool Indented { get; } = indented;

    [ObservableProperty]
    private string? _normalValue;

    [ObservableProperty]
    private string? _result;

    public HematologyEntry ToEntry() => new(NormalValue, Result);

    public void Show(HematologyEntry entry)
    {
        NormalValue = entry.NormalValue;
        Result = entry.Result;
    }
}

public partial class HematologyViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<HematologyViewModel> logger)
    : LabResultViewModel<IHematologyService, HematologyDetails, HematologyInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.Hematology, "Hematology", logger)
{
    /// <summary>The table of the form, in the order of the printed form.</summary>
    public IReadOnlyList<HematologyRow> Rows { get; } =
    [
        new("Hematocrit", "Hematocrit", false), new("Hemoglobin", "Hemoglobin", false), new("WBCCount", "White Blood Cell Count", false),
        new("Segmenters", "Segmenters", true), new("Lymphocytes", "Lymphocytes", true), new("Eosinophils", "Eosinophils", true),
        new("Monocytes", "Monocytes", true), new("Basophils", "Basophils", true), new("Stab", "Stab", true),
        new("PlateletCount", "Platelet Count", false),
    ];

    protected override EntryField RemarksField => EntryFields.HematologyRemarks;

    private HematologyRow Row(string name) => Rows.First(r => r.Name == name);

    protected override HematologyInput BuildInput() => new(
        Id,
        BuildHeader(),
        new HematologyData
        {
            Hematocrit = Row("Hematocrit").ToEntry(),
            Hemoglobin = Row("Hemoglobin").ToEntry(),
            WBCCount = Row("WBCCount").ToEntry(),
            Segmenters = Row("Segmenters").ToEntry(),
            Lymphocytes = Row("Lymphocytes").ToEntry(),
            Eosinophils = Row("Eosinophils").ToEntry(),
            Monocytes = Row("Monocytes").ToEntry(),
            Basophils = Row("Basophils").ToEntry(),
            Stab = Row("Stab").ToEntry(),
            PlateletCount = Row("PlateletCount").ToEntry(),
        },
        RowVersion);

    protected override void ShowFields(HematologyDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        Row("Hematocrit").Show(d.Data.Hematocrit);
        Row("Hemoglobin").Show(d.Data.Hemoglobin);
        Row("WBCCount").Show(d.Data.WBCCount);
        Row("Segmenters").Show(d.Data.Segmenters);
        Row("Lymphocytes").Show(d.Data.Lymphocytes);
        Row("Eosinophils").Show(d.Data.Eosinophils);
        Row("Monocytes").Show(d.Data.Monocytes);
        Row("Basophils").Show(d.Data.Basophils);
        Row("Stab").Show(d.Data.Stab);
        Row("PlateletCount").Show(d.Data.PlateletCount);
        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        foreach (var row in Rows)
            row.Show(new HematologyEntry(null, null));
    }

    // The normal values are what a new form starts with (the results are per person).
    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
        Rows.Select(r => new DefaultField(r.Name + "NormalValue", () => r.NormalValue, v => r.NormalValue = v));

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IHematologyService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));
}
