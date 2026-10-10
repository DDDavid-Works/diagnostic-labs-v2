using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Board;

public enum BoardStatus
{
    All,

    /// <summary>Not charged to the company and not fully paid.</summary>
    Unpaid,

    /// <summary>At least one of the result forms the registration's services call for has no result yet.</summary>
    ResultsPending,
}

public enum PaymentState
{
    Unpaid,
    Partial,
    Paid,
    Charged,
}

/// <summary>The registrations to show: a day (or a range of days), who, which company, and a status. Newest first, paged.</summary>
public sealed record BoardSearch(
    string? Text,
    long? CompanyId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    BoardStatus Status = BoardStatus.All,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

public sealed record BoardPayment(PaymentState State, decimal Paid, decimal Due);

/// <summary>
/// One service of a registration. <see cref="ModuleId"/> is the result form that service calls for (null when it has none);
/// <see cref="CanOpen"/> is false while that form is not built yet. <see cref="ResultCount"/> is how many results exist for it.
/// </summary>
public sealed record BoardService(long ServiceId, string Name, int? ModuleId, bool CanOpen, int ResultCount, long? LatestResultId);

public sealed record BoardItem(
    long RegistrationId,
    string RegistrationCode,
    string PatientName,
    DateOnly Date,
    string? CompanyName,
    string BatchName,
    BoardPayment Payment,
    IReadOnlyList<BoardService> Services);

/// <summary>Which result form belongs to which report type (the forms that are built).</summary>
public static class LabResultModules
{
    private static readonly Dictionary<int, LabReportType> Types = new()
    {
        [ModuleIds.StoolFecalysis] = LabReportType.StoolFecalysis,
        [ModuleIds.Urinalysis] = LabReportType.Urinalysis,
        [ModuleIds.Hematology] = LabReportType.Hematology,
        [ModuleIds.Immunology] = LabReportType.Immunology,
        [ModuleIds.Serology] = LabReportType.Serology,
        [ModuleIds.PregnancyTest] = LabReportType.PregnancyTest,
        [ModuleIds.ClinicalChemistry] = LabReportType.ClinicalChemistry,
        [ModuleIds.ClinicalChemistry1] = LabReportType.ClinicalChemistry1,
        [ModuleIds.ClinicalChemistry2] = LabReportType.ClinicalChemistry2,
        [ModuleIds.AnnualPhysicalExam] = LabReportType.AnnualPhysicalExam,
        [ModuleIds.AnnualPhysicalExamPage2] = LabReportType.MedicalExamination,
    };

    public static LabReportType? ReportTypeOf(int moduleId) => Types.TryGetValue(moduleId, out var type) ? type : null;
}

/// <summary>The home screen's list: registrations with what has been paid and which results have been made.</summary>
public interface IRegistrationBoardService
{
    Task<Result<PagedResult<BoardItem>>> SearchAsync(BoardSearch search, CancellationToken cancellationToken = default);
}

public sealed class RegistrationBoardService(IAppDbContext db, ICurrentUser currentUser) : IRegistrationBoardService
{
    /// <summary>The registration the old app kept for its "Default Patient"; it is not a real visit.</summary>
    public const string DefaultRegistrationCode = "000-000-000";

    public const int MinSearchLength = 2;

    public async Task<Result<PagedResult<BoardItem>>> SearchAsync(BoardSearch search, CancellationToken cancellationToken = default)
    {
        if (!currentUser.CanAccess(ModuleIds.PatientRegistrations) && !currentUser.CanAccess(ModuleIds.Payments))
            return Result<PagedResult<BoardItem>>.Failure(Errors.Forbidden);

        var (page, pageSize) = Paging.Normalize(search.Page, search.PageSize);

        // Which services call for which result form (the old app linked a module to its service).
        var links = await db.Modules.AsNoTracking().Where(m => m.ServiceId != null)
            .Select(m => new { ModuleId = m.Id, ServiceId = m.ServiceId!.Value })
            .ToListAsync(cancellationToken);
        var moduleOfService = links.GroupBy(l => l.ServiceId).ToDictionary(g => g.Key, g => g.Min(l => l.ModuleId));

        var query = db.PatientRegistrations.AsNoTracking().Where(r => r.RegistrationCode != DefaultRegistrationCode);

        foreach (var word in (search.Text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var term = word;
            query = query.Where(r => r.RegistrationCode.Contains(term) || r.Patient.PatientName.Contains(term) || r.Patient.PatientCode.Contains(term));
        }

        if (search.CompanyId is { } companyId)
            query = query.Where(r => r.CompanyId == companyId);

        if (search.From is { } from)
            query = query.Where(r => r.InputDate >= LocalTime.DayBoundsUtc(from).From);
        if (search.To is { } to)
            query = query.Where(r => r.InputDate < LocalTime.DayBoundsUtc(to).To);

        if (search.Status == BoardStatus.Unpaid)
        {
            query = query.Where(r => !r.Payments.Any(p => p.Type == PaymentType.Charge)
                && (r.Payments.Where(p => p.Type == PaymentType.Payment).Sum(p => (decimal?)p.PaymentAmount) ?? 0m) < r.AmountDue - r.DiscountTotal);
        }
        else if (search.Status == BoardStatus.ResultsPending)
        {
            IQueryable<long>? pending = null;
            foreach (var (serviceId, moduleId) in moduleOfService)
            {
                if (LabResultModules.ReportTypeOf(moduleId) is not { } type)
                    continue;

                var sid = serviceId;
                var ids = db.PatientRegistrationServices.Where(s => s.ServiceId == sid).Select(s => s.PatientRegistrationId)
                    .Where(id => !db.LabReports.Any(l => l.PatientRegistrationId == id && l.ReportType == type));
                pending = pending is null ? ids : pending.Union(ids);
            }

            if (pending is null)
                return Result<PagedResult<BoardItem>>.Success(PagedResult<BoardItem>.Empty(page, pageSize));

            var pendingIds = pending;
            query = query.Where(r => pendingIds.Contains(r.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.InputDate).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.RegistrationCode,
                r.Patient.PatientName,
                r.InputDate,
                CompanyName = r.Company != null ? r.Company.CompanyName : null,
                r.BatchName,
                r.AmountDue,
                r.DiscountTotal,
                Paid = r.Payments.Where(p => p.Type == PaymentType.Payment).Sum(p => (decimal?)p.PaymentAmount) ?? 0m,
                Charged = r.Payments.Any(p => p.Type == PaymentType.Charge),
                Services = r.Services.OrderBy(s => s.Id).Select(s => new { s.ServiceId, s.Service.ServiceName }).ToList(),
            })
            .ToListAsync(cancellationToken);

        // Which results exist for these registrations (a handful of rows, grouped here).
        var registrationIds = rows.Select(r => (long?)r.Id).ToList();
        var results = await db.LabReports.AsNoTracking()
            .Where(l => registrationIds.Contains(l.PatientRegistrationId))
            .Select(l => new { RegistrationId = l.PatientRegistrationId!.Value, l.ReportType, l.Id })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r =>
        {
            var due = Math.Max(r.AmountDue - r.DiscountTotal, 0m);
            var state = r.Charged ? PaymentState.Charged
                : r.Paid >= due ? PaymentState.Paid
                : r.Paid > 0m ? PaymentState.Partial
                : PaymentState.Unpaid;

            var services = r.Services.Select(s =>
            {
                int? moduleId = moduleOfService.TryGetValue(s.ServiceId, out var m) ? m : null;
                var type = moduleId is { } id ? LabResultModules.ReportTypeOf(id) : null;
                var made = type is null ? [] : results.Where(x => x.RegistrationId == r.Id && x.ReportType == type).OrderBy(x => x.Id).ToList();
                return new BoardService(s.ServiceId, s.ServiceName, moduleId, type is not null, made.Count, made.Count > 0 ? made[^1].Id : null);
            }).ToList();

            return new BoardItem(
                r.Id, r.RegistrationCode, r.PatientName, LocalTime.ToLocalDate(r.InputDate), r.CompanyName, r.BatchName,
                new BoardPayment(state, r.Paid, due), services);
        }).ToList();

        return Result<PagedResult<BoardItem>>.Success(new PagedResult<BoardItem>(items, total, page, pageSize));
    }
}
