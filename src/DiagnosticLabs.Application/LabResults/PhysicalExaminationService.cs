using System.Globalization;
using System.Reflection;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>
/// The fields of the Physical Examination form (the printed "Laboratory Results" sheet), described once: the service saves and validates them,
/// the screen builds its cells from them and the page design reads them by name. Each name is also a column of <see cref="PhysicalExaminationReport"/>.
/// </summary>
public static class PhysicalExaminationFields
{
    public const int ChoiceMaxLength = 50;
    public const int CellMaxLength = 100;
    public const int TextMaxLength = 500;

    /// <summary>The date of each of the four sections.</summary>
    public static readonly string[] Dates = ["CbcDate", "BloodTypingDate", "UrinalysisDate", "FecalysisDate"];

    /// <summary>The tests of the complete blood count as printed; Hematocrit and Hemoglobin have a male and a female normal value.</summary>
    public static readonly (string Name, string Label, bool Gendered)[] CbcTests =
    [
        ("Hematocrit", "HEMATOCRIT", true), ("Hemoglobin", "HEMOGLOBIN", true), ("WBCCount", "WHITE BLOOD CELL COUNT", false),
        ("Segmenters", "Segmenters", false), ("Lymphocytes", "Lymphocytes", false), ("Eosinophils", "Eosinophils", false),
        ("Monocytes", "Monocytes", false), ("Basophils", "Basophils", false), ("Stab", "Stab", false),
    ];

    /// <summary>The value cells of the complete blood count: the normal values (what a new form starts with) and the results.</summary>
    public static readonly string[] CbcCells =
    [
        .. CbcTests.SelectMany(t => new[] { t.Name + "NValue" }.Concat(t.Gendered ? [t.Name + "FemaleNValue"] : []).Append(t.Name + "Result")),
    ];

    /// <summary>The fields picked from a list (the lists belong to this form and are maintained in the entry builder) or typed.</summary>
    public static readonly (string Name, string Label, EntryField Entry)[] Choices =
    [
        Choice("BloodTyping", "Blood Typing", "Blood Typing"), Choice("RhTyping", "Rh Typing", "Rh Typing"),
        Choice("UrineColor", "Color", "Urinalysis Color"), Choice("UrineAppearance", "Appearance", "Urinalysis Appearance"),
        Choice("UrineReaction", "Reaction", "Urinalysis Reaction"), Choice("UrineSPGravity", "SP. Gravity", "Urinalysis SP. Gravity"),
        Choice("UrineAlbumin", "Albumin", "Urinalysis Albumin"), Choice("UrineSugar", "Sugar", "Urinalysis Sugar"),
        Choice("UrinePusCells", "Pus Cells", "Urinalysis Pus Cells"), Choice("UrineRedCells", "Red Cells", "Urinalysis Red Cells"),
        Choice("UrineMucusThreads", "Mucus Threads", "Urinalysis Mucus Threads"), Choice("UrineEpithelialCells", "Epithelial Cells", "Urinalysis Epithelial Cells"),
        Choice("UrineAmorphousUratesPO4", "Amorphous Urates/PO4", "Urinalysis Amorphous Urates/PO4"), Choice("UrineBacteria", "Bacteria", "Urinalysis Bacteria"),
        Choice("UrineCrystals", "Crystals", "Urinalysis Crystals"), Choice("UrineCasts", "Casts", "Urinalysis Casts"),
        Choice("FecalysisColor", "Color", "Fecalysis Color"), Choice("FecalysisConsistency", "Consistency", "Fecalysis Consistency"),
    ];

    /// <summary>The free texts, each with a list of reusable texts of its own.</summary>
    public static readonly (string Name, string Label, EntryField Entry)[] Texts =
    [
        ("UrineOthers", "Others", Template("Urinalysis Others")), ("FecalysisResult", "Result", Template("Fecalysis Result")), ("Others", "Others", Template("Others")),
    ];

    /// <summary>Every field saved as text, with its longest allowed value.</summary>
    public static IEnumerable<(string Name, string Label, int MaxLength)> TextFields() =>
        CbcCells.Select(c => (c, c, CellMaxLength))
            .Concat(Choices.Select(c => (c.Name, c.Label, ChoiceMaxLength)))
            .Concat(Texts.Select(t => (t.Name, t.Label, TextMaxLength)));

    private static (string, string, EntryField) Choice(string name, string label, string list) =>
        (name, label, new EntryField(list, EntryKind.SingleLine, ModuleIds.PhysicalExamination));

    private static EntryField Template(string list) => new(list, EntryKind.MultiLine, ModuleIds.PhysicalExamination);
}

/// <summary>The sections of a Physical Examination: the four dates and every text field by name (a field that is not listed is empty).</summary>
public sealed record PhysicalExaminationData(IReadOnlyDictionary<string, DateOnly?> Dates, IReadOnlyDictionary<string, string?> Values)
{
    public static PhysicalExaminationData Empty { get; } = new(new Dictionary<string, DateOnly?>(), new Dictionary<string, string?>());

    public string? Value(string name) => Values.TryGetValue(name, out var value) ? value : null;

    public DateOnly? Date(string name) => Dates.TryGetValue(name, out var date) ? date : null;
}

public sealed record PhysicalExaminationDetails(long Id, LabResultHeader Header, PhysicalExaminationData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record PhysicalExaminationInput(long Id, LabResultHeader Header, PhysicalExaminationData Data, byte[]? RowVersion) : ICrudInput;

public interface IPhysicalExaminationService
    : ICrudService<LabResultListItem, PhysicalExaminationDetails, PhysicalExaminationInput>, ILabResultPrinting;

/// <summary>
/// Physical Examination: the printed "Laboratory Results" sheet with a complete blood count, blood typing, urinalysis and fecalysis section (each dated
/// on its own) and an Others box. It is a form of its own: it has no connection with the other result forms.
/// </summary>
public sealed class PhysicalExaminationService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<PhysicalExaminationInput, PhysicalExaminationDetails, PhysicalExaminationReport>(
        db, currentUser, clock, ModuleIds.PhysicalExamination, "Physical Examination", LabReportType.PhysicalExamination),
      IPhysicalExaminationService
{
    // Every field is read and written by name, so the form is described in one place (PhysicalExaminationFields).
    private static readonly Dictionary<string, PropertyInfo> Columns = PhysicalExaminationFields.Dates
        .Concat(PhysicalExaminationFields.TextFields().Select(f => f.Name))
        .ToDictionary(n => n, n => typeof(PhysicalExaminationReport).GetProperty(n)!);

    // The page is titled "LABORATORY RESULTS"; the menu entry and the messages call the form "Physical Examination".
    protected override string Title => "Laboratory Results";

    protected override ReportLayout Layout => ReportLayout.PhysicalExamination;

    protected override LabResultHeader HeaderOf(PhysicalExaminationInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(PhysicalExaminationInput input)
    {
        var errors = new List<string>();
        foreach (var (name, label, max) in PhysicalExaminationFields.TextFields())
            Max(errors, input.Data.Value(name), max, label);

        return errors;
    }

    protected override void ApplyDetail(PhysicalExaminationReport e, PhysicalExaminationInput input)
    {
        foreach (var name in PhysicalExaminationFields.Dates)
            Columns[name].SetValue(e, input.Data.Date(name));

        foreach (var (name, _, _) in PhysicalExaminationFields.TextFields())
            Columns[name].SetValue(e, Clean(input.Data.Value(name)));
    }

    private static PhysicalExaminationData DataOf(PhysicalExaminationReport e) => new(
        PhysicalExaminationFields.Dates.ToDictionary(n => n, n => (DateOnly?)Columns[n].GetValue(e)),
        PhysicalExaminationFields.TextFields().ToDictionary(f => f.Name, f => (string?)Columns[f.Name].GetValue(e)));

    protected override PhysicalExaminationDetails BuildDetails(LabReport report, LabResultHeader header, PhysicalExaminationReport e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    // The printed sheet places every value in its own spot, so the page design reads them by name ("HematocritResult", "CbcDate"...).
    protected override IReadOnlyList<PrintLine> ResultLines(PhysicalExaminationReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(PhysicalExaminationReport detail) => [];

    protected override string FooterText => string.Empty;

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, PhysicalExaminationReport e)
    {
        var fields = new Dictionary<string, string?>();
        foreach (var name in PhysicalExaminationFields.Dates)
            fields[name] = ((DateOnly?)Columns[name].GetValue(e))?.ToString("d", CultureInfo.CurrentCulture);

        foreach (var (name, _, _) in PhysicalExaminationFields.TextFields())
            fields[name] = (string?)Columns[name].GetValue(e);

        return fields;
    }
}
