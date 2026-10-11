using System.Reflection;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>
/// The tests of the Clinical Microscopy form, described once: the service saves and validates them, the screen builds its rows from them and the page design
/// reads them by name. Each test is saved as the columns <c>{Name}Result</c> and <c>{Name}Reference</c> of <see cref="ClinicalMicroscopyReport"/>.
/// </summary>
public static class ClinicalMicroscopyFields
{
    public const int ResultMaxLength = 50;
    public const int ReferenceMaxLength = 100;

    /// <summary>One line of the table: <see cref="IsChoice"/> tests are picked from a list of their own (maintained in the entry builder), the others are typed.</summary>
    public sealed record Test(string Name, string Label, bool IsChoice = true)
    {
        public string ResultColumn => Name + "Result";

        public string ReferenceColumn => Name + "Reference";

        /// <summary>The list this test's result is picked from (it belongs to this form).</summary>
        public EntryField Entry => new(Label, EntryKind.SingleLine, ModuleIds.ClinicalMicroscopy);
    }

    public sealed record Section(string Title, IReadOnlyList<Test> Tests);

    /// <summary>The three parts of the printed form, in order.</summary>
    public static readonly Section[] Sections =
    [
        new("PHYSICAL EXAMINATION", [new("Color", "Color"), new("Transparency", "Transparency")]),
        new("CHEMICAL EXAMINATION",
        [
            new("PH", "pH", false), new("SpecificGravity", "Specific Gravity", false), new("Protein", "Protein"), new("Glucose", "Glucose"), new("Blood", "Blood"),
            new("Leukocytes", "Leukocytes"), new("Nitrite", "Nitrite"), new("Bilirubin", "Bilirubin"), new("Urobilinogen", "Urobilinogen"), new("Ketones", "Ketones"),
        ]),
        new("MICROSCOPIC EXAMINATION",
        [
            new("WBC", "WBC", false), new("RBC", "RBC", false), new("MucusThreads", "Mucus Threads"), new("EpithelialCells", "Epithelial Cells"),
            new("AmorphousUratesPhosphates", "Amorphous Urates/Phosphates"), new("Bacteria", "Bacteria"), new("Crystals", "Crystals"), new("Casts", "Casts"),
        ]),
    ];

    public static IEnumerable<Test> Tests => Sections.SelectMany(s => s.Tests);
}

/// <summary>The values of a Clinical Microscopy by column name (<c>ColorResult</c>, <c>PHReference</c>...); a value that is not listed is empty.</summary>
public sealed record ClinicalMicroscopyData(IReadOnlyDictionary<string, string?> Values)
{
    public static ClinicalMicroscopyData Empty { get; } = new(new Dictionary<string, string?>());

    public string? Value(string column) => Values.TryGetValue(column, out var value) ? value : null;
}

public sealed record ClinicalMicroscopyDetails(long Id, LabResultHeader Header, ClinicalMicroscopyData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record ClinicalMicroscopyInput(long Id, LabResultHeader Header, ClinicalMicroscopyData Data, byte[]? RowVersion) : ICrudInput;

public interface IClinicalMicroscopyService
    : ICrudService<LabResultListItem, ClinicalMicroscopyDetails, ClinicalMicroscopyInput>, ILabResultPrinting;

/// <summary>
/// Clinical Microscopy: a urine examination (physical, chemical and microscopic) with a result and a reference range for each test, then Remarks. The reference
/// ranges are what a new form starts with (the Defaults button). Signed by two medical technologists and the pathologist. A form of its own: it has no connection
/// with the Urinalysis form.
/// </summary>
public sealed class ClinicalMicroscopyService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<ClinicalMicroscopyInput, ClinicalMicroscopyDetails, ClinicalMicroscopyReport>(
        db, currentUser, clock, ModuleIds.ClinicalMicroscopy, "Clinical Microscopy", LabReportType.ClinicalMicroscopy),
      IClinicalMicroscopyService
{
    // Every value is read and written by column name, so the form is described in one place (ClinicalMicroscopyFields).
    private static readonly Dictionary<string, PropertyInfo> Columns = ClinicalMicroscopyFields.Tests
        .SelectMany(t => new[] { t.ResultColumn, t.ReferenceColumn })
        .ToDictionary(n => n, n => typeof(ClinicalMicroscopyReport).GetProperty(n)!);

    protected override string Title => "Clinical Microscopy";

    protected override ReportLayout Layout => ReportLayout.ClinicalMicroscopy;

    protected override string FooterText => ElectronicNote;

    protected override bool HasSecondTechnologist => true;

    protected override LabResultHeader HeaderOf(ClinicalMicroscopyInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(ClinicalMicroscopyInput input)
    {
        var errors = new List<string>();
        foreach (var test in ClinicalMicroscopyFields.Tests)
        {
            Max(errors, input.Data.Value(test.ResultColumn), ClinicalMicroscopyFields.ResultMaxLength, $"{test.Label} result");
            Max(errors, input.Data.Value(test.ReferenceColumn), ClinicalMicroscopyFields.ReferenceMaxLength, $"{test.Label} reference range");
        }

        return errors;
    }

    protected override void ApplyDetail(ClinicalMicroscopyReport e, ClinicalMicroscopyInput input)
    {
        foreach (var (column, property) in Columns)
            property.SetValue(e, Clean(input.Data.Value(column)));
    }

    private static ClinicalMicroscopyData DataOf(ClinicalMicroscopyReport e) =>
        new(Columns.ToDictionary(c => c.Key, c => (string?)c.Value.GetValue(e)));

    protected override ClinicalMicroscopyDetails BuildDetails(LabReport report, LabResultHeader header, ClinicalMicroscopyReport e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    // The printed table places every value in its own cell, so the page design reads them by name ("ColorResult", "PHReference"...).
    protected override IReadOnlyList<PrintLine> ResultLines(ClinicalMicroscopyReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(ClinicalMicroscopyReport detail) => [];

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, ClinicalMicroscopyReport e) =>
        Columns.ToDictionary(c => c.Key, c => (string?)c.Value.GetValue(e));
}
