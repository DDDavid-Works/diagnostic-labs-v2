using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

public sealed record UrinalysisDetails(
    long Id,
    LabResultHeader Header,
    string? Color,
    string? Appearance,
    string? Reaction,
    string? SPGravity,
    string? Protein,
    string? Glucose,
    string? PusCells,
    string? RedCells,
    string? MucusThreads,
    string? EpithelialCells,
    string? AmorphousUratesPO4,
    string? Bacteria,
    string? Casts,
    string? Crystals,
    string Others,
    byte[]? Photo,
    byte[] RowVersion) : IHasId;

public sealed record UrinalysisInput(
    long Id,
    LabResultHeader Header,
    string? Color,
    string? Appearance,
    string? Reaction,
    string? SPGravity,
    string? Protein,
    string? Glucose,
    string? PusCells,
    string? RedCells,
    string? MucusThreads,
    string? EpithelialCells,
    string? AmorphousUratesPO4,
    string? Bacteria,
    string? Casts,
    string? Crystals,
    string? Others,
    byte[]? RowVersion) : ICrudInput;

public interface IUrinalysisService : ICrudService<LabResultListItem, UrinalysisDetails, UrinalysisInput>, ILabResultPrinting;

public sealed class UrinalysisService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<UrinalysisInput, UrinalysisDetails, UrinalysisReport>(
        db, currentUser, clock, ModuleIds.Urinalysis, "Urinalysis", LabReportType.Urinalysis),
      IUrinalysisService
{
    public const int ValueMaxLength = 50;
    public const int OthersMaxLength = 500;

    protected override string Title => "Urinalysis";

    protected override ReportLayout Layout => ReportLayout.Urinalysis;

    protected override LabResultHeader HeaderOf(UrinalysisInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(UrinalysisInput input)
    {
        var errors = new List<string>();
        foreach (var (label, value) in Values(input))
            Max(errors, value, ValueMaxLength, label);

        Max(errors, input.Others, OthersMaxLength, "Others");
        return errors;
    }

    private static IEnumerable<(string Label, string? Value)> Values(UrinalysisInput i) =>
    [
        ("Color", i.Color), ("Appearance", i.Appearance), ("Reaction (PH)", i.Reaction), ("SP. Gravity", i.SPGravity),
        ("Protein", i.Protein), ("Glucose", i.Glucose), ("Pus Cells", i.PusCells), ("Red Cells", i.RedCells),
        ("Mucus Threads", i.MucusThreads), ("Epithelial Cells", i.EpithelialCells), ("Amorphous Urates / PO4", i.AmorphousUratesPO4),
        ("Bacteria", i.Bacteria), ("Casts", i.Casts), ("Crystals", i.Crystals),
    ];

    protected override void ApplyDetail(UrinalysisReport d, UrinalysisInput i)
    {
        d.Color = Clean(i.Color);
        d.Appearance = Clean(i.Appearance);
        d.Reaction = Clean(i.Reaction);
        d.SPGravity = Clean(i.SPGravity);
        d.Protein = Clean(i.Protein);
        d.Glucose = Clean(i.Glucose);
        d.PusCells = Clean(i.PusCells);
        d.RedCells = Clean(i.RedCells);
        d.MucusThreads = Clean(i.MucusThreads);
        d.EpithelialCells = Clean(i.EpithelialCells);
        d.AmorphousUratesPO4 = Clean(i.AmorphousUratesPO4);
        d.Bacteria = Clean(i.Bacteria);
        d.Casts = Clean(i.Casts);
        d.Crystals = Clean(i.Crystals);
        d.Others = i.Others?.Trim() ?? string.Empty;
    }

    protected override UrinalysisDetails BuildDetails(LabReport report, LabResultHeader header, UrinalysisReport d) => new(
        report.Id, header, d.Color, d.Appearance, d.Reaction, d.SPGravity, d.Protein, d.Glucose, d.PusCells, d.RedCells, d.MucusThreads,
        d.EpithelialCells, d.AmorphousUratesPO4, d.Bacteria, d.Casts, d.Crystals, d.Others, report.Photo?.Content, report.RowVersion);

    // The order and wording of the labels is the order and wording on the printed form: left column, then right column.
    protected override IReadOnlyList<PrintLine> ResultLines(UrinalysisReport d) =>
    [
        new("Color", d.Color), new("Appearance", d.Appearance), new("Reaction (PH)", d.Reaction), new("SP. Gravity", d.SPGravity),
        new("Protein", d.Protein), new("Glucose", d.Glucose), new("Pus Cells", d.PusCells), new("Red Cells", d.RedCells),
        new("Mucus Threads", d.MucusThreads), new("Epithelial Cells", d.EpithelialCells),
        new("Amorphous Urates / PO4", d.AmorphousUratesPO4), new("Bacteria", d.Bacteria), new("Casts", d.Casts), new("Crystals", d.Crystals),
    ];

    protected override IReadOnlyList<PrintText> ResultTexts(UrinalysisReport d) => [new("Others", d.Others)];
}