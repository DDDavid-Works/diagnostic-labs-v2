using System.Reflection;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>One test of the clinical chemistry table: its result, unit and reference range in conventional units and in S.I. units.</summary>
public sealed record ChemistryTestEntry(UnitResultEntry Conventional, UnitResultEntry System)
{
    public static ChemistryTestEntry Empty { get; } = new(new UnitResultEntry(null, null, null), new UnitResultEntry(null, null, null));
}

/// <summary>The lines of the clinical chemistry table by test name (see <see cref="ClinicalChemistryService.Tests"/>); a test that is not listed is empty.</summary>
public sealed record ClinicalChemistryData(IReadOnlyDictionary<string, ChemistryTestEntry> Tests)
{
    public static ClinicalChemistryData Empty { get; } = new(new Dictionary<string, ChemistryTestEntry>());

    public ChemistryTestEntry Of(string name) => Tests.TryGetValue(name, out var entry) ? entry : ChemistryTestEntry.Empty;
}

public sealed record ClinicalChemistryDetails(long Id, LabResultHeader Header, ClinicalChemistryData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record ClinicalChemistryInput(long Id, LabResultHeader Header, ClinicalChemistryData Data, byte[]? RowVersion) : ICrudInput;

public interface IClinicalChemistryService
    : ICrudService<LabResultListItem, ClinicalChemistryDetails, ClinicalChemistryInput>, ILabResultPrinting;

/// <summary>
/// Clinical Chemistry: a table of ten tests, each with a result, a unit and a reference range in conventional and in S.I. units.
/// The units and ranges are what a new form starts with (the Defaults button); the results are per person. Signed by two medical technologists and the pathologist.
/// </summary>
public sealed class ClinicalChemistryService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<ClinicalChemistryInput, ClinicalChemistryDetails, ClinicalChemistryReport>(
        db, currentUser, clock, ModuleIds.ClinicalChemistry, "Clinical Chemistry", LabReportType.ClinicalChemistry),
      IClinicalChemistryService
{
    public const int ValueMaxLength = 100;

    /// <summary>The lines of the table with the name each is saved under and the label it is printed with, in the order of the printed form.</summary>
    public static readonly (string Name, string Label)[] Tests =
    [
        ("FastingBloodSugar", "Fasting Blood Sugar"), ("Cholesterol", "Cholesterol"), ("Triglycerides", "Triglycerides"), ("HDL", "HDL"), ("LDL", "LDL"),
        ("Creatinine", "Creatinine"), ("BloodUreaNitrogen", "Blood Urea Nitrogen"), ("BloodUricAcid", "Blood Uric Acid"),
        ("ALTSGPT", "ALT/SGPT"), ("ASTSGOT", "AST/SGOT"),
    ];

    /// <summary>The columns of one test, in the order they are printed: conventional (reference range, unit, result), then S.I.</summary>
    public static readonly string[] Suffixes = ["CNValue", "CUnit", "CResults", "SNValue", "SUnit", "SResults"];

    // Each test has six columns named {Test}{Suffix}; they are read and written by name so the table is described in one place.
    private static readonly Dictionary<string, PropertyInfo> Columns = Tests
        .SelectMany(t => Suffixes.Select(s => t.Name + s))
        .ToDictionary(n => n, n => typeof(ClinicalChemistryReport).GetProperty(n)!);

    protected override string Title => "Clinical Chemistry";

    protected override ReportLayout Layout => ReportLayout.ClinicalChemistry;

    protected override string FooterText => ElectronicNote;

    protected override bool HasSecondTechnologist => true;

    protected override LabResultHeader HeaderOf(ClinicalChemistryInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(ClinicalChemistryInput input)
    {
        var errors = new List<string>();
        foreach (var (name, label) in Tests)
        {
            var test = input.Data.Of(name);
            Max(errors, test.Conventional.NormalValue, ValueMaxLength, $"{label} conventional reference range");
            Max(errors, test.Conventional.Unit, ValueMaxLength, $"{label} conventional unit");
            Max(errors, test.Conventional.Result, ValueMaxLength, $"{label} conventional result");
            Max(errors, test.System.NormalValue, ValueMaxLength, $"{label} S.I. reference range");
            Max(errors, test.System.Unit, ValueMaxLength, $"{label} S.I. unit");
            Max(errors, test.System.Result, ValueMaxLength, $"{label} S.I. result");
        }

        return errors;
    }

    protected override void ApplyDetail(ClinicalChemistryReport e, ClinicalChemistryInput input)
    {
        foreach (var (name, _) in Tests)
        {
            var test = input.Data.Of(name);
            string?[] values =
                [test.Conventional.NormalValue, test.Conventional.Unit, test.Conventional.Result, test.System.NormalValue, test.System.Unit, test.System.Result];
            for (var i = 0; i < Suffixes.Length; i++)
                Columns[name + Suffixes[i]].SetValue(e, Clean(values[i]));
        }
    }

    private static string? Read(ClinicalChemistryReport e, string column) => (string?)Columns[column].GetValue(e);

    private static ClinicalChemistryData DataOf(ClinicalChemistryReport e) => new(Tests.ToDictionary(
        t => t.Name,
        t => new ChemistryTestEntry(
            new UnitResultEntry(Read(e, t.Name + "CNValue"), Read(e, t.Name + "CUnit"), Read(e, t.Name + "CResults")),
            new UnitResultEntry(Read(e, t.Name + "SNValue"), Read(e, t.Name + "SUnit"), Read(e, t.Name + "SResults")))));

    protected override ClinicalChemistryDetails BuildDetails(LabReport report, LabResultHeader header, ClinicalChemistryReport e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    // The printed table places every value in its own cell, so the page design reads them by name ("FastingBloodSugarCResults" and so on).
    protected override IReadOnlyList<PrintLine> ResultLines(ClinicalChemistryReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(ClinicalChemistryReport detail) => [];

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, ClinicalChemistryReport e) =>
        Columns.Keys.ToDictionary(name => name, name => Read(e, name));
}
