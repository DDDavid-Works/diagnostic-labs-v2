using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Reports;

/// <summary>A form a service's results are printed with (Urinalysis: its own form or the Physical Examination sheet).</summary>
public sealed record BatchPrintFormOption(int ModuleId, string Name);

/// <summary>
/// A service the batch can be printed for: one that has a result form the user may print (<see cref="ModuleId"/> is the first of them).
/// <see cref="Forms"/> lists every form its results can be on that the user may print; more than one lets the search look in one of them only.
/// </summary>
public sealed record BatchPrintServiceOption(long ServiceId, string Name, int ModuleId, IReadOnlyList<BatchPrintFormOption>? Forms = null)
{
    public bool HasFormChoice => Forms is { Count: > 1 };
}

/// <summary>
/// What to look for: the patients of a company, optionally of one batch of it, and their results of one service, optionally only the results made
/// within a range of days (both included), and for a service with more than one form optionally only the results on one form
/// (<see cref="FormModuleId"/>; null: the latest result on any of them).
/// </summary>
public sealed record BatchPrintRequest(long CompanyId, long ServiceId, string? Batch = null, DateOnly? From = null, DateOnly? To = null, int? FormModuleId = null);

/// <summary>
/// One patient of the batch: whether the result of the service has been inputted (<see cref="ResultId"/> is the latest one), the day it was,
/// and the form it is on.
/// </summary>
public sealed record BatchPrintRow(
    long RegistrationId, string PatientName, string RegistrationCode, string BatchName, long? ResultId, DateOnly? ResultDate, string? FormName = null)
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
/// listed too, marked as having no result. A service whose result can be entered in more than one form (Urinalysis on its own form or on the
/// Physical Examination sheet) is looked for in each, and each result prints with its own form. Printing a result needs the Print permission of
/// its own form as well as Batch Print.
/// </summary>
public sealed class BatchPrintService(IAppDbContext db, ICurrentUser currentUser, ILabResultPrinters printers) : IBatchPrintService
{
    public async Task<IReadOnlyList<BatchPrintServiceOption>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.BatchPrint, ModuleAction.Print))
            return [];

        var forms = await PrintableFormsAsync(cancellationToken);
        var names = await db.Services.AsNoTracking().Where(s => s.IsActive).ToDictionaryAsync(s => s.Id, s => s.ServiceName, cancellationToken);
        var formNames = await db.Modules.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.ModuleName, cancellationToken);

        // Only services whose result form exists and that this user may print.
        return
        [
            .. forms.Where(f => names.ContainsKey(f.Key))
                .Select(f => new BatchPrintServiceOption(f.Key, names[f.Key], f.Value[0].ModuleId, [.. f.Value.Select(x => new BatchPrintFormOption(x.ModuleId, FormName(x, formNames)))]))
                .OrderBy(o => o.ModuleId).ThenBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    // A section of the sheet is printed with the whole sheet, so it is named after the sheet.
    private static string FormName(ServiceForm form, IReadOnlyDictionary<int, string> formNames) =>
        formNames.GetValueOrDefault(form.ModuleId, "Result form") + (form.Section is null ? string.Empty : " sheet");

    // The built forms of each service that this user may print (services with none are left out).
    private async Task<Dictionary<long, List<ServiceForm>>> PrintableFormsAsync(CancellationToken cancellationToken)
    {
        var all = await ResultFormLinks.LoadAsync(db, activeOnly: true, cancellationToken);
        return all
            .Select(kv => (kv.Key, Forms: kv.Value.Where(f => (f.Section is not null || ResultFormLinks.TypeOf(f.ModuleId) is not null) && currentUser.Can(f.ModuleId, ModuleAction.Print)).ToList()))
            .Where(x => x.Forms.Count > 0)
            .ToDictionary(x => x.Key, x => x.Forms);
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
        if (service is null || !(await PrintableFormsAsync(cancellationToken)).TryGetValue(service.ServiceId, out var forms))
            return Result<BatchPrintSearchResult>.Failure(Errors.Forbidden);

        // One form of the service only, when asked
        if (request.FormModuleId is { } formModuleId)
        {
            forms = [.. forms.Where(x => x.ModuleId == formModuleId)];
            if (forms.Count == 0)
                return Result<BatchPrintSearchResult>.Failure(Errors.Invalid(["The results of this service are not printed with that form."]));
        }

        var formNames = (service.Forms ?? []).ToDictionary(x => x.ModuleId, x => x.Name);

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

        var made = new List<(long Id, long RegistrationId, DateTime CreatedAtUtc, string FormName)>();
        foreach (var form in forms)
        {
            var results = Results(form).Where(l => ids.Contains(l.PatientRegistrationId));
            if (request.From is { } from)
                results = results.Where(l => l.CreatedAtUtc >= LocalTime.DayBoundsUtc(from).From);
            if (request.To is { } to)
                results = results.Where(l => l.CreatedAtUtc < LocalTime.DayBoundsUtc(to).To);

            var found = await results.Select(l => new { l.Id, RegistrationId = l.PatientRegistrationId!.Value, l.CreatedAtUtc }).ToListAsync(cancellationToken);
            var formName = formNames.GetValueOrDefault(form.ModuleId, service.Name);
            made.AddRange(found.Select(x => (x.Id, x.RegistrationId, x.CreatedAtUtc, formName)));
        }

        // The latest result of each registration, whichever form it is on.
        var latest = made
            .GroupBy(l => l.RegistrationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id).First());

        var rows = expected
            .Select(r => latest.TryGetValue(r.Id, out var result)
                ? new BatchPrintRow(r.Id, r.PatientName, r.RegistrationCode, r.BatchName, result.Id, LocalTime.ToLocalDate(result.CreatedAtUtc), result.FormName)
                : new BatchPrintRow(r.Id, r.PatientName, r.RegistrationCode, r.BatchName, null, null))
            .OrderBy(r => r.PatientName, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.RegistrationId)
            .ToList();

        return Result<BatchPrintSearchResult>.Success(new BatchPrintSearchResult(service.Name, companyName, rows));
    }

    // The saved results of one form (for a section of the sheet: the sheets with that section entered).
    private IQueryable<LabReport> Results(ServiceForm form)
    {
        if (form.Section is { } section)
        {
            var sheets = db.Set<PhysicalExaminationReport>().Where(PhysicalExaminationFields.Filled(section)).Select(d => d.LabReportId);
            return db.LabReports.AsNoTracking().Where(l => l.ReportType == LabReportType.PhysicalExamination && sheets.Contains(l.Id));
        }

        var type = ResultFormLinks.TypeOf(form.ModuleId)!.Value;
        var moduleId = form.ModuleId;
        var custom = LabResultModules.IsCustomForm(moduleId);
        // every custom form shares one report type, so only the results saved with this service's own form count
        return db.LabReports.AsNoTracking().Where(l => l.ReportType == type
            && (!custom || db.CustomFormReports.Any(c => c.LabReportId == l.Id && c.CustomFormVersion.CustomForm.ModuleId == moduleId)));
    }

    public async Task<Result<IReadOnlyList<PrintableReport>>> GetPrintablesAsync(
        long serviceId, IReadOnlyList<long> resultIds, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.BatchPrint, ModuleAction.Print))
            return Result<IReadOnlyList<PrintableReport>>.Failure(Errors.Forbidden);

        if (!(await PrintableFormsAsync(cancellationToken)).TryGetValue(serviceId, out var forms))
            return Result<IReadOnlyList<PrintableReport>>.Failure(Errors.Forbidden);

        // Which form each result belongs to, so it prints with that form.
        var kinds = await db.LabReports.AsNoTracking().Where(l => resultIds.Contains(l.Id))
            .Select(l => new
            {
                l.Id,
                l.ReportType,
                CustomModuleId = db.CustomFormReports.Where(c => c.LabReportId == l.Id).Select(c => (int?)c.CustomFormVersion.CustomForm.ModuleId).FirstOrDefault(),
            })
            .ToDictionaryAsync(k => k.Id, cancellationToken);

        var reports = new List<PrintableReport>(resultIds.Count);
        foreach (var id in resultIds)
        {
            var form = kinds.TryGetValue(id, out var kind)
                ? forms.FirstOrDefault(f => f.Section is not null
                    ? kind.ReportType == LabReportType.PhysicalExamination
                    : ResultFormLinks.TypeOf(f.ModuleId) == kind.ReportType && (!LabResultModules.IsCustomForm(f.ModuleId) || kind.CustomModuleId == f.ModuleId))
                : null;
            if (form is null || printers.For(form.ModuleId) is not { } printer)
                return Result<IReadOnlyList<PrintableReport>>.Failure(Errors.Forbidden);

            var printable = await printer.GetPrintableAsync(id, cancellationToken);
            if (printable.IsFailure)
                return Result<IReadOnlyList<PrintableReport>>.Failure(printable.Error);

            reports.Add(printable.Value);
        }

        return Result<IReadOnlyList<PrintableReport>>.Success(reports);
    }
}
