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

    public UnitResultEntry ASTSGOTConventional { get; init; } = new(null, null, null);

    public UnitResultEntry ASTSGOTSystem { get; init; } = new(null, null, null);
}

public sealed record ClinicalChemistry2Details(long Id, LabResultHeader Header, ClinicalChemistry2Data Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record ClinicalChemistry2Input(long Id, LabResultHeader Header, ClinicalChemistry2Data Data, byte[]? RowVersion) : ICrudInput;

public interface IClinicalChemistry2Service
    : ICrudService<LabResultListItem, ClinicalChemistry2Details, ClinicalChemistry2Input>, ILabResultPrinting;

/// <summary>Clinical Chemistry 2: alkaline phosphatase and AST/SGOT, each with normal values, unit and result in two unit systems. The form has no Remarks.</summary>
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
        ("AST/SGOT (conventional)", d.ASTSGOTConventional), ("AST/SGOT (system unit)", d.ASTSGOTSystem),
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
        e.ASTSGOTCNValue = Clean(d.ASTSGOTConventional.NormalValue);
        e.ASTSGOTCUnit = Clean(d.ASTSGOTConventional.Unit);
        e.ASTSGOTCResults = Clean(d.ASTSGOTConventional.Result);
        e.ASTSGOTSNValue = Clean(d.ASTSGOTSystem.NormalValue);
        e.ASTSGOTSUnit = Clean(d.ASTSGOTSystem.Unit);
        e.ASTSGOTSResults = Clean(d.ASTSGOTSystem.Result);
    }

    private static ClinicalChemistry2Data DataOf(ClinicalChemistry2Report e) => new()
    {
        AlkalinePhosphataseConventional = new(e.AlkalinePhosphataseCNValue, e.AlkalinePhosphataseCUnit, e.AlkalinePhosphataseCResults),
        AlkalinePhosphataseSystem = new(e.AlkalinePhosphataseSNValue, e.AlkalinePhosphataseSUnit, e.AlkalinePhosphataseSResults),
        ASTSGOTConventional = new(e.ASTSGOTCNValue, e.ASTSGOTCUnit, e.ASTSGOTCResults),
        ASTSGOTSystem = new(e.ASTSGOTSNValue, e.ASTSGOTSUnit, e.ASTSGOTSResults),
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
        ["ASTSGOTCNValue"] = e.ASTSGOTCNValue, ["ASTSGOTCUnit"] = e.ASTSGOTCUnit, ["ASTSGOTCResults"] = e.ASTSGOTCResults,
        ["ASTSGOTSNValue"] = e.ASTSGOTSNValue, ["ASTSGOTSUnit"] = e.ASTSGOTSUnit, ["ASTSGOTSResults"] = e.ASTSGOTSResults,
    };
}
