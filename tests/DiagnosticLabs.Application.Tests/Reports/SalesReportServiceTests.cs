using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Domain.Settings;
using DiagnosticLabs.Infrastructure.Persistence;

namespace DiagnosticLabs.Application.Tests.Reports;

public class SalesReportServiceTests
{
    private readonly TestEnvironment _env = new();

    private SalesReportService CreateService(AppDbContext db) => new(db, _env.Session);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private static readonly DateOnly Day = new(2026, 10, 10);

    private static DateTime At(DateOnly day, int hour) => LocalTime.ToUtc(day, new TimeSpan(hour, 0, 0));

    private static async Task<Service> AddServiceAsync(AppDbContext db, string name, decimal price)
    {
        var service = new Service { ServiceName = name, ServiceDescription = "d", Price = price };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return service;
    }

    private static async Task<PatientRegistration> RegisterAsync(
        AppDbContext db, string name, DateTime when, long? companyId = null, string batch = "", params (Service Service, decimal Price)[] lines)
    {
        var registration = new PatientRegistration
        {
            RegistrationCode = "R-" + Guid.NewGuid().ToString("N")[..8],
            Patient = new Patient { PatientCode = "P-" + Guid.NewGuid().ToString("N")[..8], PatientName = name },
            InputDate = when,
            CompanyId = companyId,
            BatchName = batch,
            AmountDue = lines.Sum(l => l.Price),
        };
        foreach (var (service, price) in lines)
            registration.Services.Add(new PatientRegistrationService { Service = service, Price = price });

        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    [Fact]
    public async Task It_has_a_column_per_service_sold_and_a_row_per_registration_with_the_totals_under_the_columns()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var stool = await AddServiceAsync(db, "Fecalysis", 120m);
        var urine = await AddServiceAsync(db, "Urinalysis", 110m);
        await AddServiceAsync(db, "Never sold", 999m);
        await RegisterAsync(db, "Ana", At(Day, 9), lines: [(stool, 120m), (urine, 110m)]);
        await RegisterAsync(db, "Ben", At(Day, 10), lines: [(stool, 120m)]);

        var report = (await CreateService(db).GetAsync(new SalesReportRequest(Day, Day))).Value;

        Assert.Equal(["Fecalysis", "Urinalysis"], report.Columns.Select(c => c.Name));
        Assert.Equal(["Ana", "Ben"], report.Rows.Select(r => r.PatientName));
        Assert.Equal(new Dictionary<long, decimal> { [stool.Id] = 120m, [urine.Id] = 110m }, report.Rows[0].Prices);
        Assert.False(report.Rows[1].Prices.ContainsKey(urine.Id));

        var stoolColumn = report.Columns.Single(c => c.ServiceId == stool.Id);
        Assert.Equal((2, 240m), (stoolColumn.Count, stoolColumn.Amount));
        var urineColumn = report.Columns.Single(c => c.ServiceId == urine.Id);
        Assert.Equal((1, 110m), (urineColumn.Count, urineColumn.Amount));
        Assert.Equal(350m, report.GrandTotal);
    }

    [Fact]
    public async Task It_uses_the_price_charged_on_the_registration_not_the_current_price_of_the_service()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var stool = await AddServiceAsync(db, "Fecalysis", 150m);
        await RegisterAsync(db, "Ana", At(Day, 9), lines: [(stool, 100m)]);

        var report = (await CreateService(db).GetAsync(new SalesReportRequest(Day, Day))).Value;

        Assert.Equal(100m, report.Rows.Single().Prices[stool.Id]);
        Assert.Equal(100m, report.GrandTotal);
    }

    [Fact]
    public async Task It_includes_both_end_days_and_leaves_out_the_days_around_them()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var stool = await AddServiceAsync(db, "Fecalysis", 120m);
        await RegisterAsync(db, "Before", At(Day.AddDays(-1), 23), lines: [(stool, 120m)]);
        await RegisterAsync(db, "First morning", At(Day, 0), lines: [(stool, 120m)]);
        await RegisterAsync(db, "Last evening", At(Day.AddDays(1), 23), lines: [(stool, 120m)]);
        await RegisterAsync(db, "After", At(Day.AddDays(2), 1), lines: [(stool, 120m)]);

        var report = (await CreateService(db).GetAsync(new SalesReportRequest(Day, Day.AddDays(1)))).Value;

        Assert.Equal(["First morning", "Last evening"], report.Rows.Select(r => r.PatientName));
    }

    [Fact]
    public async Task It_can_be_limited_to_a_company_and_to_a_batch()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var stool = await AddServiceAsync(db, "Fecalysis", 120m);
        var acme = new Company { CompanyName = "Acme" };
        var other = new Company { CompanyName = "Other" };
        db.Companies.AddRange(acme, other);
        await db.SaveChangesAsync();
        await RegisterAsync(db, "Acme first", At(Day, 9), acme.Id, "Batch 1", [(stool, 120m)]);
        await RegisterAsync(db, "Acme second", At(Day, 10), acme.Id, "Batch 2", [(stool, 120m)]);
        await RegisterAsync(db, "Other", At(Day, 11), other.Id, "Batch 1", [(stool, 120m)]);
        await RegisterAsync(db, "Walk-in", At(Day, 12), null, "", [(stool, 120m)]);
        var service = CreateService(db);

        var everyone = (await service.GetAsync(new SalesReportRequest(Day, Day))).Value;
        var company = (await service.GetAsync(new SalesReportRequest(Day, Day, acme.Id))).Value;
        var batch = (await service.GetAsync(new SalesReportRequest(Day, Day, acme.Id, "Batch 2"))).Value;

        Assert.Equal(4, everyone.Rows.Count);
        Assert.Equal(["Acme first", "Acme second"], company.Rows.Select(r => r.PatientName));
        Assert.Equal("Acme", company.CompanyName);
        Assert.Equal(["Acme second"], batch.Rows.Select(r => r.PatientName));
        Assert.Equal("Batch 2", batch.Batch);
        Assert.Equal(120m, batch.GrandTotal);
    }

    [Fact]
    public async Task It_can_separate_what_was_charged_to_a_company_from_the_direct_payments()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var stool = await AddServiceAsync(db, "Fecalysis", 120m);
        var paid = await RegisterAsync(db, "Paid", At(Day, 9), lines: [(stool, 120m)]);
        await RegisterAsync(db, "Not paid yet", At(Day, 10), lines: [(stool, 120m)]);
        var charged = await RegisterAsync(db, "Charged", At(Day, 11), lines: [(stool, 120m)]);
        var partlyThenCharged = await RegisterAsync(db, "Part paid then charged", At(Day, 12), lines: [(stool, 120m)]);
        db.Payments.AddRange(
            new Payment { PatientRegistrationId = paid.Id, PaymentAmount = 120m, Type = PaymentType.Payment, PaymentDate = DateTime.UtcNow },
            new Payment { PatientRegistrationId = charged.Id, PaymentAmount = 120m, Type = PaymentType.Charge, PaymentDate = DateTime.UtcNow },
            new Payment { PatientRegistrationId = partlyThenCharged.Id, PaymentAmount = 20m, Type = PaymentType.Payment, PaymentDate = DateTime.UtcNow },
            new Payment { PatientRegistrationId = partlyThenCharged.Id, PaymentAmount = 100m, Type = PaymentType.Charge, PaymentDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var all = (await service.GetAsync(new SalesReportRequest(Day, Day))).Value;
        var direct = (await service.GetAsync(new SalesReportRequest(Day, Day, Payment: SalesPaymentFilter.DirectPayments))).Value;
        var company = (await service.GetAsync(new SalesReportRequest(Day, Day, Payment: SalesPaymentFilter.ChargedToCompany))).Value;

        Assert.Equal(4, all.Rows.Count);
        Assert.Equal(SalesPaymentFilter.All, all.Payment);
        Assert.Equal(["Paid", "Not paid yet"], direct.Rows.Select(r => r.PatientName));
        Assert.Equal(SalesPaymentFilter.DirectPayments, direct.Payment);
        Assert.Equal(["Charged", "Part paid then charged"], company.Rows.Select(r => r.PatientName));
        Assert.Equal(240m, company.GrandTotal);
        Assert.Equal(all.GrandTotal, direct.GrandTotal + company.GrandTotal);
    }

    [Fact]
    public async Task It_leaves_out_deleted_registrations_deleted_services_and_the_old_apps_default_visit()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var stool = await AddServiceAsync(db, "Fecalysis", 120m);
        var urine = await AddServiceAsync(db, "Urinalysis", 110m);
        var kept = await RegisterAsync(db, "Kept", At(Day, 9), lines: [(stool, 120m), (urine, 110m)]);
        var deleted = await RegisterAsync(db, "Deleted", At(Day, 10), lines: [(stool, 120m)]);
        var defaultVisit = await RegisterAsync(db, "Default Patient", At(Day, 11), lines: [(stool, 120m)]);
        defaultVisit.RegistrationCode = RegistrationBoardService.DefaultRegistrationCode;
        deleted.IsDeleted = true;
        db.PatientRegistrationServices.Single(s => s.PatientRegistrationId == kept.Id && s.ServiceId == urine.Id).IsDeleted = true;
        await db.SaveChangesAsync();

        var report = (await CreateService(db).GetAsync(new SalesReportRequest(Day, Day))).Value;

        Assert.Equal(["Kept"], report.Rows.Select(r => r.PatientName));
        Assert.Equal(["Fecalysis"], report.Columns.Select(c => c.Name));
        Assert.Equal(120m, report.GrandTotal);
    }

    [Fact]
    public async Task It_reports_nothing_for_a_quiet_period_and_carries_the_letterhead()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        db.CompanySetups.Add(new CompanySetup { CompanyName = "BIO ASSAY DIAGNOSTIC CENTER", SubCompanyName = "Angeles City" });
        await db.SaveChangesAsync();

        var report = (await CreateService(db).GetAsync(new SalesReportRequest(Day, Day))).Value;

        Assert.Empty(report.Rows);
        Assert.Empty(report.Columns);
        Assert.Equal(0m, report.GrandTotal);
        Assert.Equal("BIO ASSAY DIAGNOSTIC CENTER", report.Letterhead.CompanyName);
        Assert.Equal("Angeles City", report.Letterhead.SubCompanyName);
    }

    [Fact]
    public async Task It_refuses_a_to_date_before_the_from_date()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var result = await CreateService(db).GetAsync(new SalesReportRequest(Day, Day.AddDays(-1)));

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Invalid", result.Error.Code);
    }

    [Fact]
    public async Task It_needs_the_print_permission()
    {
        await using var db = _env.CreateDb();
        _env.Session.SignIn(new AuthenticatedUser(2, "u", "User", false, false, [new ModulePermission(ModuleIds.SalesReport, false, false, false, false)]));
        Assert.Equal(Errors.Forbidden, (await CreateService(db).GetAsync(new SalesReportRequest(Day, Day))).Error);

        _env.Session.SignIn(new AuthenticatedUser(2, "u", "User", false, false, [new ModulePermission(ModuleIds.SalesReport, false, false, false, true)]));
        Assert.True((await CreateService(db).GetAsync(new SalesReportRequest(Day, Day))).IsSuccess);
    }
}
