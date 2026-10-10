using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>One unit system of a test: its normal values, the unit and the result.</summary>
public sealed record UnitResultEntry(string? NormalValue, string? Unit, string? Result);

/// <summary>The two tests of Clinical Chemistry 2, each in conventional and in system units.</summary>
public sealed record ClinicalChemistry2Data
{
    public UnitResultEntry AlkalinePhosphataseConventional { get; init; } = new(null, null, null);

    public UnitResultEntry AlkalinePhosphataseSystem { get; init; } = new(null, null, null);

    public UnitResultEntry SGOTConventional { get; init; } = new(null, null, null);

    public UnitResultEntry SGOTSystem { get; init; } = new(null, null, null);
}

public sealed record ClinicalChemistry2Details(long Id, LabResultHeader Header, ClinicalChemistry2Data Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record ClinicalChemistry2Input(long Id, LabResultHeader Header, ClinicalChemistry2Data Data, byte[]? RowVersion) : ICrudInput;

public interface IClinicalChemistry2Service
    : ICrudService<LabResultListItem, ClinicalChemistry2Details, ClinicalChemistry2Input>, ILabResultPrinting;

/// <summary>Clinical Chemistry 2: alkaline phosphatase and SGOT, each with normal values, unit and result in two unit systems. The form has no Remarks.</summary>
public sealed class ClinicalChemistry2Service(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<ClinicalChemistry2Input, ClinicalChemistry2Details, ClinicalChemistry2Report>(
        db, currentUser, clock, ModuleIds.ClinicalChemistry2, "Clinical Chemistry 2", LabReportType.ClinicalChemistry2),
      IClinicalChemistry2Service
{
    public const int ValueMaxLength = 100;

    protected override string Title => "Clinical Chemistry 2";

    protected override ReportLayout Layout => ReportLayout.ClinicalChemistry2;

    protected override LabResultHeader HeaderOf(ClinicalChemistry2Input input) => input.Header;

    private static IEnumerable<(string Label, UnitResultEntry Entry)> Entries(ClinicalChemistry2Data d) =>
    [
        ("Alkaline phosphatase (conventional)", d.AlkalinePhosphataseConventional), ("Alkaline phosphatase (system unit)", d.AlkalinePhosphataseSystem),
        ("SGOT (conventional)", d.SGOTConventional), ("SGOT (system unit)", d.SGOTSystem),
    ];

    protected override IEnumerable<string> ValidateDetail(ClinicalChemistry2Input input)
    {
        var errors = new List<string>();
        foreach (var (label, entry) in Entries(input.Data))
        {
            Max(errors, entry.NormalValue, ValueMaxLength, $"{label} normal values");
            Max(errors, entry.Unit, ValueMaxLength, $"{label} unit");
            Max(errors, entry.Result, ValueMaxLength, $"{label} result");
        }

        return errors;
    }

    protected override void ApplyDetail(ClinicalChemistry2Report e, ClinicalChemistry2Input input)
    {
        var d = input.Data;
        e.AlkalinePhosphataseCNValue = Clean(d.AlkalinePhosphataseConventional.NormalValue);
        e.AlkalinePhosphataseCUnit = Clean(d.AlkalinePhosphataseConventional.Unit);
        e.AlkalinePhosphataseCResults = Clean(d.AlkalinePhosphataseConventional.Result);
        e.AlkalinePhosphataseSNValue = Clean(d.AlkalinePhosphataseSystem.NormalValue);
        e.AlkalinePhosphataseSUnit = Clean(d.AlkalinePhosphataseSystem.Unit);
        e.AlkalinePhosphataseSResults = Clean(d.AlkalinePhosphataseSystem.Result);
        e.SGOTCNValue = Clean(d.SGOTConventional.NormalValue);
        e.SGOTCUnit = Clean(d.SGOTConventional.Unit);
        e.SGOTCResults = Clean(d.SGOTConventional.Result);
        e.SGOTSNValue = Clean(d.SGOTSystem.NormalValue);
        e.SGOTSUnit = Clean(d.SGOTSystem.Unit);
        e.SGOTSResults = Clean(d.SGOTSystem.Result);
    }

    private static ClinicalChemistry2Data DataOf(ClinicalChemistry2Report e) => new()
    {
        AlkalinePhosphataseConventional = new(e.AlkalinePhosphataseCNValue, e.AlkalinePhosphataseCUnit, e.AlkalinePhosphataseCResults),
        AlkalinePhosphataseSystem = new(e.AlkalinePhosphataseSNValue, e.AlkalinePhosphataseSUnit, e.AlkalinePhosphataseSResults),
        SGOTConventional = new(e.SGOTCNValue, e.SGOTCUnit, e.SGOTCResults),
        SGOTSystem = new(e.SGOTSNValue, e.SGOTSUnit, e.SGOTSResults),
    };

    protected override ClinicalChemistry2Details BuildDetails(LabReport report, LabResultHeader header, ClinicalChemistry2Report e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    // The printed table places every value in its own cell, so the page design reads them by name ("AlkalinePhosphataseCNValue" and so on).
    protected override IReadOnlyList<PrintLine> ResultLines(ClinicalChemistry2Report detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(ClinicalChemistry2Report detail) => [];

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, ClinicalChemistry2Report e) => new Dictionary<string, string?>
    {
        ["AlkalinePhosphataseCNValue"] = e.AlkalinePhosphataseCNValue, ["AlkalinePhosphataseCUnit"] = e.AlkalinePhosphataseCUnit, ["AlkalinePhosphataseCResults"] = e.AlkalinePhosphataseCResults,
        ["AlkalinePhosphataseSNValue"] = e.AlkalinePhosphataseSNValue, ["AlkalinePhosphataseSUnit"] = e.AlkalinePhosphataseSUnit, ["AlkalinePhosphataseSResults"] = e.AlkalinePhosphataseSResults,
        ["SGOTCNValue"] = e.SGOTCNValue, ["SGOTCUnit"] = e.SGOTCUnit, ["SGOTCResults"] = e.SGOTCResults,
        ["SGOTSNValue"] = e.SGOTSNValue, ["SGOTSUnit"] = e.SGOTSUnit, ["SGOTSResults"] = e.SGOTSResults,
    };
}
