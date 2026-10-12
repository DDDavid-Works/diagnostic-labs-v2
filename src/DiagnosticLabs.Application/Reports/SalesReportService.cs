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

/// <summary>One service the report has a column for, and how many registrations have it.</summary>
public sealed record SalesReportColumn(long ServiceId, string Name, int Count);

/// <summary>
/// One registration: which services it has (the check marks), what it came to (its amount due, which is not the sum of the service prices when
/// a package or a bundle was priced differently) and the discount given on it.
/// </summary>
public sealed record SalesReportRow(long RegistrationId, string PatientName, IReadOnlySet<long> ServiceIds, decimal AmountDue, decimal DiscountTotal)
{
    /// <summary>What is left to pay after the discount (the Net Amount column).</summary>
    public decimal NetAmount => AmountDue - DiscountTotal;
}

/// <summary>
/// The Sales Report as it is printed: a column per service with a check mark for each registration that has it, a count under each column, and
/// the gross total (the amounts due of the registrations), the discounts given and the net total.
/// </summary>
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
    public decimal GrossTotal => Rows.Sum(r => r.AmountDue);

    public decimal DiscountTotal => Rows.Sum(r => r.DiscountTotal);

    public decimal NetTotal => GrossTotal - DiscountTotal;
}

public interface ISalesReportService
{
    Task<Result<SalesReportData>> GetAsync(SalesReportRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the lab sold in a period: which services each registration has, and the totals from what was charged on the registrations (the amount due and the discount
/// stored on each one), so a later price change does not alter an old report. The gross total is the registrations' amount due, not the sum of service prices.
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
                r.AmountDue,
                r.DiscountTotal,
                Services = r.Services.Select(s => new { s.ServiceId, s.Service.ServiceName }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var rows = registrations
            .Select(r => new SalesReportRow(
                r.Id,
                r.PatientName,
                r.Services.Select(s => s.ServiceId).ToHashSet(),
                r.AmountDue,
                r.DiscountTotal))
            .ToList();

        // The columns are the services that were sold, in the order the lab created them; each carries how many registrations have it.
        var columns = registrations
            .SelectMany(r => r.Services.DistinctBy(s => s.ServiceId))
            .GroupBy(s => s.ServiceId)
            .OrderBy(g => g.Key)
            .Select(g => new SalesReportColumn(g.Key, g.First().ServiceName, g.Count()))
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
