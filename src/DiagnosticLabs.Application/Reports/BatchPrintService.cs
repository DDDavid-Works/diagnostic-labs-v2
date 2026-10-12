using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Reports;

/// <summary>A service the batch can be printed for: one that has a result form the user may print.</summary>
public sealed record BatchPrintServiceOption(long ServiceId, string Name, int ModuleId);

/// <summary>
/// What to look for: the patients of a company, optionally of one batch of it, and their results of one service, optionally only the results made
/// within a range of days (both included).
/// </summary>
public sealed record BatchPrintRequest(long CompanyId, long ServiceId, string? Batch = null, DateOnly? From = null, DateOnly? To = null);

/// <summary>One patient of the batch: whether the result of the service has been inputted (<see cref="ResultId"/> is the latest one) and the day it was.</summary>
public sealed record BatchPrintRow(
    long RegistrationId, string PatientName, string RegistrationCode, string BatchName, long? ResultId, DateOnly? ResultDate)
{
    public bool HasResult => ResultId is not null;
}

/// <summary>Every patient of the batch who has the service, sorted by patient name, with or without a result.</summary>
public sealed record BatchPrintSearchResult(string ServiceName, string CompanyName, IReadOnlyList<BatchPrintRow> Rows);

public interface IBatchPrintService
{
    Task<IReadOnlyList<BatchPrintServiceOption>> GetServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>The patients of the batch and whether each has the result.</summary>
    Task<Result<BatchPrintSearchResult>> SearchAsync(BatchPrintRequest request, CancellationToken cancellationToken = default);

    /// <summary>The printouts of the given results of a service, in the order given (the results a search found).</summary>
    Task<Result<IReadOnlyList<PrintableReport>>> GetPrintablesAsync(long serviceId, IReadOnlyList<long> resultIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Batch Print: finds the patients of a company (or one of its batches) and the latest saved result of a service for each, so the results can be
/// printed in one go. A patient is expected to have a result when the service is on their registration; the ones that do not have one are
/// listed too, marked as having no result. Printing a result needs the Print permission of its own form as well as Batch Print.
/// </summary>
public sealed class BatchPrintService(IAppDbContext db, ICurrentUser currentUser, ILabResultPrinters printers) : IBatchPrintService
{
    public async Task<IReadOnlyList<BatchPrintServiceOption>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.BatchPrint, ModuleAction.Print))
            return [];

        var links = await db.Modules.AsNoTracking().Where(m => m.ServiceId != null && m.IsActive)
            .Select(m => new { ModuleId = m.Id, ServiceId = m.ServiceId!.Value })
            .ToListAsync(cancellationToken);
        var names = await db.Services.AsNoTracking().Where(s => s.IsActive).ToDictionaryAsync(s => s.Id, s => s.ServiceName, cancellationToken);

        // Only services whose result form exists and that this user may print; a service shared by two forms is offered once, for the first.
        return
        [
            .. links
                .Where(l => LabResultModules.ReportTypeOf(l.ModuleId) is not null && names.ContainsKey(l.ServiceId) && currentUser.Can(l.ModuleId, ModuleAction.Print))
                .GroupBy(l => l.ServiceId)
                .Select(g => g.OrderBy(l => l.ModuleId).First())
                .Select(l => new BatchPrintServiceOption(l.ServiceId, names[l.ServiceId], l.ModuleId))
                .OrderBy(o => o.ModuleId),
        ];
    }

    public async Task<Result<BatchPrintSearchResult>> SearchAsync(BatchPrintRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.BatchPrint, ModuleAction.Print))
            return Result<BatchPrintSearchResult>.Failure(Errors.Forbidden);

        var problems = new List<string>();
        if (request.CompanyId == 0)
            problems.Add("Pick a company.");
        if (request.ServiceId == 0)
            problems.Add("Pick a service.");
        if (request.From is { } f && request.To is { } t && t < f)
            problems.Add("The \"to\" date is before the \"from\" date.");
        if (problems.Count > 0)
            return Result<BatchPrintSearchResult>.Failure(Errors.Invalid(problems));

        var service = (await GetServicesAsync(cancellationToken)).FirstOrDefault(s => s.ServiceId == request.ServiceId);
        if (service is null || LabResultModules.ReportTypeOf(service.ModuleId) is not { } reportType)
            return Result<BatchPrintSearchResult>.Failure(Errors.Forbidden);

        var companyName = await db.Companies.AsNoTracking().Where(c => c.Id == request.CompanyId).Select(c => c.CompanyName).FirstOrDefaultAsync(cancellationToken);
        if (companyName is null)
            return Result<BatchPrintSearchResult>.Failure(Errors.NotFound("Company"));

        // The patients expected to have a result: registrations of the company (and batch) that have the service.
        var batch = request.Batch?.Trim();
        var registrations = db.PatientRegistrations.AsNoTracking()
            .Where(r => r.RegistrationCode != RegistrationBoardService.DefaultRegistrationCode && r.CompanyId == request.CompanyId
                        && r.Services.Any(s => s.ServiceId == request.ServiceId));
        if (!string.IsNullOrEmpty(batch))
            registrations = registrations.Where(r => r.BatchName == batch);

        var expected = await registrations
            .Select(r => new { r.Id, r.RegistrationCode, r.BatchName, r.Patient.PatientName })
            .ToListAsync(cancellationToken);
        var ids = expected.Select(r => (long?)r.Id).ToList();

        var results = db.LabReports.AsNoTracking().Where(l => l.ReportType == reportType && ids.Contains(l.PatientRegistrationId));
        if (request.From is { } from)
            results = results.Where(l => l.CreatedAtUtc >= LocalTime.DayBoundsUtc(from).From);
        if (request.To is { } to)
            results = results.Where(l => l.CreatedAtUtc < LocalTime.DayBoundsUtc(to).To);

        var made = await results.Select(l => new { l.Id, RegistrationId = l.PatientRegistrationId!.Value, l.CreatedAtUtc }).ToListAsync(cancellationToken);

        // The latest result of each registration.
        var latest = made
            .GroupBy(l => l.RegistrationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id).First());

        var rows = expected
            .Select(r => latest.TryGetValue(r.Id, out var result)
                ? new BatchPrintRow(r.Id, r.PatientName, r.RegistrationCode, r.BatchName, result.Id, LocalTime.ToLocalDate(result.CreatedAtUtc))
                : new BatchPrintRow(r.Id, r.PatientName, r.RegistrationCode, r.BatchName, null, null))
            .OrderBy(r => r.PatientName, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.RegistrationId)
            .ToList();

        return Result<BatchPrintSearchResult>.Success(new BatchPrintSearchResult(service.Name, companyName, rows));
    }

    public async Task<Result<IReadOnlyList<PrintableReport>>> GetPrintablesAsync(
        long serviceId, IReadOnlyList<long> resultIds, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.BatchPrint, ModuleAction.Print))
            return Result<IReadOnlyList<PrintableReport>>.Failure(Errors.Forbidden);

        var service = (await GetServicesAsync(cancellationToken)).FirstOrDefault(s => s.ServiceId == serviceId);
        if (service is null || printers.For(service.ModuleId) is not { } printer)
            return Result<IReadOnlyList<PrintableReport>>.Failure(Errors.Forbidden);

        var reports = new List<PrintableReport>(resultIds.Count);
        foreach (var id in resultIds)
        {
            var printable = await printer.GetPrintableAsync(id, cancellationToken);
            if (printable.IsFailure)
                return Result<IReadOnlyList<PrintableReport>>.Failure(printable.Error);

            reports.Add(printable.Value);
        }

        return Result<IReadOnlyList<PrintableReport>>.Success(reports);
    }
}
