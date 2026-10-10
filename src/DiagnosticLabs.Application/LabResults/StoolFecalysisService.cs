using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

public sealed record StoolFecalysisDetails(
    long Id,
    LabResultHeader Header,
    string? Color,
    string? Consistency,
    string Others,
    string? Wbc,
    string? Rbc,
    string? Bacteria,
    string? YeastCells,
    string? FatGlobules,
    string? OvaParasite,
    string? MedicalTechnologist2,
    string? MedicalTechnologist2License,
    byte[]? Photo,
    byte[] RowVersion) : IHasId;

/// <summary>The values after <see cref="RowVersion"/> are optional so a caller that only has Color and Consistency can leave them out.</summary>
public sealed record StoolFecalysisInput(
    long Id,
    LabResultHeader Header,
    string? Color,
    string? Consistency,
    string? Others,
    byte[]? RowVersion,
    string? Wbc = null,
    string? Rbc = null,
    string? Bacteria = null,
    string? YeastCells = null,
    string? FatGlobules = null,
    string? OvaParasite = null,
    string? MedicalTechnologist2 = null,
    string? MedicalTechnologist2License = null) : ICrudInput;

public interface IStoolFecalysisService : ICrudService<LabResultListItem, StoolFecalysisDetails, StoolFecalysisInput>, ILabResultPrinting;

public sealed class StoolFecalysisService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<StoolFecalysisInput, StoolFecalysisDetails, StoolFecalysisReport>(
        db, currentUser, clock, ModuleIds.StoolFecalysis, "Stool/Fecalysis", LabReportType.StoolFecalysis),
      IStoolFecalysisService
{
    public const int ValueMaxLength = 50;
    public const int OvaParasiteMaxLength = 100;
    public const int OthersMaxLength = 500;

    /// <summary>The two lines at the foot of the Fecalysis page; the page design prints them one under the other.</summary>
    public const string ElectronicNote = "* This is a validated and original report *\nThis is an electronically signed document";

    // The menu and the screen keep the name "Stool/Fecalysis"; the printed form is titled "FECALYSIS".
    protected override string Title => "Fecalysis";

    protected override ReportLayout Layout => ReportLayout.StoolFecalysis;

    protected override string FooterText => ElectronicNote;

    protected override LabResultHeader HeaderOf(StoolFecalysisInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(StoolFecalysisInput input)
    {
        var errors = new List<string>();
        Max(errors, input.Color, ValueMaxLength, "Color");
        Max(errors, input.Consistency, ValueMaxLength, "Consistency");
        Max(errors, input.Wbc, ValueMaxLength, "WBC");
        Max(errors, input.Rbc, ValueMaxLength, "RBC");
        Max(errors, input.Bacteria, ValueMaxLength, "Bacteria");
        Max(errors, input.YeastCells, ValueMaxLength, "Yeast cells");
        Max(errors, input.FatGlobules, ValueMaxLength, "Fat globules");
        Max(errors, input.OvaParasite, OvaParasiteMaxLength, "Ova/Parasite");
        Max(errors, input.Others, OthersMaxLength, "Others");
        Max(errors, input.MedicalTechnologist2, 100, "Second medical technologist");
        Max(errors, input.MedicalTechnologist2License, 50, "Second medical technologist license number");
        return errors;
    }

    protected override void ApplyDetail(StoolFecalysisReport detail, StoolFecalysisInput input)
    {
        detail.Color = Clean(input.Color);
        detail.Consistency = Clean(input.Consistency);
        detail.Others = input.Others?.Trim() ?? string.Empty;
        detail.Wbc = Clean(input.Wbc);
        detail.Rbc = Clean(input.Rbc);
        detail.Bacteria = Clean(input.Bacteria);
        detail.YeastCells = Clean(input.YeastCells);
        detail.FatGlobules = Clean(input.FatGlobules);
        detail.OvaParasite = Clean(input.OvaParasite);
        detail.MedicalTechnologist2 = Clean(input.MedicalTechnologist2);
        detail.MedicalTechnologist2License = Clean(input.MedicalTechnologist2License);
    }

    protected override StoolFecalysisDetails BuildDetails(LabReport report, LabResultHeader header, StoolFecalysisReport detail) => new(
        report.Id, header, detail.Color, detail.Consistency, detail.Others, detail.Wbc, detail.Rbc, detail.Bacteria, detail.YeastCells,
        detail.FatGlobules, detail.OvaParasite, detail.MedicalTechnologist2, detail.MedicalTechnologist2License, report.Photo?.Content, report.RowVersion);

    // Macroscopic findings first (left half of the page), then microscopic findings (right half), in the order of the printed form.
    protected override IReadOnlyList<PrintLine> ResultLines(StoolFecalysisReport detail) =>
    [
        new("Color", detail.Color), new("Consistency", detail.Consistency),
        new("WBC", detail.Wbc), new("RBC", detail.Rbc), new("Bacteria", detail.Bacteria), new("Yeast Cells", detail.YeastCells),
        new("Fat Globules", detail.FatGlobules), new("Ova/Parasite", detail.OvaParasite),
    ];

    protected override IReadOnlyList<PrintText> ResultTexts(StoolFecalysisReport detail) => [new("Others", detail.Others)];

    // Three people sign this form: two medical technologists and the pathologist.
    protected override IReadOnlyList<PrintSignatory> Signatories(LabReport report, StoolFecalysisReport detail) =>
    [
        new PrintSignatory("Medical Technologist", report.MedicalTechnologist, report.MedicalTechnologistLicense),
        new PrintSignatory("Medical Technologist", detail.MedicalTechnologist2, detail.MedicalTechnologist2License),
        new PrintSignatory("Pathologist", report.Pathologist, report.PathologistLicense),
    ];
}
