using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

public sealed record StoolFecalysisDetails(
    long Id, LabResultHeader Header, string? Color, string? Consistency, string Result, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record StoolFecalysisInput(
    long Id, LabResultHeader Header, string? Color, string? Consistency, string? Result, byte[]? RowVersion) : ICrudInput;

public interface IStoolFecalysisService : ICrudService<LabResultListItem, StoolFecalysisDetails, StoolFecalysisInput>, ILabResultPrinting;

public sealed class StoolFecalysisService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<StoolFecalysisInput, StoolFecalysisDetails, StoolFecalysisReport>(
        db, currentUser, clock, ModuleIds.StoolFecalysis, "Stool/Fecalysis", LabReportType.StoolFecalysis),
      IStoolFecalysisService
{
    protected override string Title => "Stool/Fecalysis";

    protected override ReportLayout Layout => ReportLayout.StoolFecalysis;

    protected override LabResultHeader HeaderOf(StoolFecalysisInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(StoolFecalysisInput input)
    {
        var errors = new List<string>();
        Max(errors, input.Color, 50, "Color");
        Max(errors, input.Consistency, 50, "Consistency");
        Max(errors, input.Result, 500, "Result");
        return errors;
    }

    protected override void ApplyDetail(StoolFecalysisReport detail, StoolFecalysisInput input)
    {
        detail.Color = Clean(input.Color);
        detail.Consistency = Clean(input.Consistency);
        detail.Result = input.Result?.Trim() ?? string.Empty;
    }

    protected override StoolFecalysisDetails BuildDetails(LabReport report, LabResultHeader header, StoolFecalysisReport detail) =>
        new(report.Id, header, detail.Color, detail.Consistency, detail.Result, report.Photo?.Content, report.RowVersion);

    protected override IReadOnlyList<PrintLine> ResultLines(StoolFecalysisReport detail) =>
        [new("Color", detail.Color), new("Consistency", detail.Consistency)];

    protected override IReadOnlyList<PrintText> ResultTexts(StoolFecalysisReport detail) => [new("Result", detail.Result)];
}