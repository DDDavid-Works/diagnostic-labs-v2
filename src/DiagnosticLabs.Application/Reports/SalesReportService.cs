using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Reports;

/// <summary>Which registrations to count by how they were settled: a registration is charged to its company when it has a charge (the Home list's "Charged to company"); every other one is a direct payment, paid or not yet paid.</summary>
public enum SalesPaymentFilter
{
    All,
    DirectPayments,
    ChargedToCompany,
}

/// <summary>What to report on: the days (both included), optionally one company and one batch of it, and optionally only direct payments or only what was charged to companies.</summary>
public sealed record SalesReportRequest(DateOnly From, DateOnly To, long? CompanyId = null, string? Batch = null, SalesPaymentFilter Payment = SalesPaymentFilter.All);

/// <summary>One service the report has a column for.</summary>
public sealed record SalesReportColumn(long ServiceId, string Name, int Count, decimal Amount);

/// <summary>One registration: the price charged for each of its services (a service it did not have is simply missing).</summary>
public sealed record SalesReportRow(long RegistrationId, string PatientName, IReadOnlyDictionary<long, decimal> Prices);

/// <summary>The Sales Report as it is printed: a column per service, a row per registration, and the totals under the columns.</summary>
public sealed record SalesReportData(
    ReportLetterhead Letterhead,
    DateOnly From,
    DateOnly To,
    string? CompanyName,
    string? Batch,
    SalesPaymentFilter Payment,
    IReadOnlyList<SalesReportColumn> Columns,
    IReadOnlyList<SalesReportRow> Rows)
{
    public decimal GrandTotal => Columns.Sum(c => c.Amount);
}

public interface ISalesReportService
{
    Task<Result<SalesReportData>> GetAsync(SalesReportRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the lab sold in a period, from the prices charged on each registration (so a later price change does not alter an old report).
/// Gross prices: discounts are not part of this report.
/// </summary>
public sealed class SalesReportService(IAppDbContext db, ICurrentUser currentUser) : ISalesReportService
{
    public async Task<Result<SalesReportData>> GetAsync(SalesReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.SalesReport, ModuleAction.Print))
            return Result<SalesReportData>.Failure(Errors.Forbidden);

        if (request.To < request.From)
            return Result<SalesReportData>.Failure(Errors.Invalid(["The \"to\" date is before the \"from\" date."]));

        var batch = request.Batch?.Trim();
        var query = db.PatientRegistrations.AsNoTracking()
            .Where(r => r.RegistrationCode != RegistrationBoardService.DefaultRegistrationCode)
            .Where(r => r.InputDate >= LocalTime.DayBoundsUtc(request.From).From && r.InputDate < LocalTime.DayBoundsUtc(request.To).To);

        if (request.CompanyId is { } companyId)
            query = query.Where(r => r.CompanyId == companyId);
        if (request.Payment == SalesPaymentFilter.ChargedToCompany)
            query = query.Where(r => r.Payments.Any(p => p.Type == PaymentType.Charge));
        else if (request.Payment == SalesPaymentFilter.DirectPayments)
            query = query.Where(r => !r.Payments.Any(p => p.Type == PaymentType.Charge));
        if (!string.IsNullOrEmpty(batch))
            query = query.Where(r => r.BatchName == batch);

        var registrations = await query
            .OrderBy(r => r.InputDate).ThenBy(r => r.Id)
            .Select(r => new
            {
                r.Id,
                r.Patient.PatientName,
                Services = r.Services.Select(s => new { s.ServiceId, s.Service.ServiceName, s.Price }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var rows = registrations
            .Select(r => new SalesReportRow(
                r.Id,
                r.PatientName,
                r.Services.GroupBy(s => s.ServiceId).ToDictionary(g => g.Key, g => g.Sum(s => s.Price))))
            .ToList();

        // The columns are the services that were sold, in the order the lab created them; each carries its own count and total.
        var columns = registrations
            .SelectMany(r => r.Services)
            .GroupBy(s => s.ServiceId)
            .OrderBy(g => g.Key)
            .Select(g => new SalesReportColumn(g.Key, g.First().ServiceName, g.Count(), g.Sum(s => s.Price)))
            .ToList();

        var companyName = request.CompanyId is { } id
            ? await db.Companies.AsNoTracking().Where(c => c.Id == id).Select(c => c.CompanyName).FirstOrDefaultAsync(cancellationToken)
            : null;

        var setup = await db.CompanySetups.AsNoTracking().OrderByDescending(c => c.UpdatedAtUtc).ThenByDescending(c => c.Id).FirstOrDefaultAsync(cancellationToken);
        var letterhead = new ReportLetterhead(
            setup?.CompanyName ?? string.Empty, setup?.SubCompanyName, setup?.Address, setup?.ContactNumbers, setup?.Email, setup?.Logo);

        return Result<SalesReportData>.Success(new SalesReportData(
            letterhead, request.From, request.To, companyName, string.IsNullOrEmpty(batch) ? null : batch, request.Payment, columns, rows));
    }
}
