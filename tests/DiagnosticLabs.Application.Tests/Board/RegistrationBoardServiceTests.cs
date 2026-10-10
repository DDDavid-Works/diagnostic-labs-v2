using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Infrastructure.Persistence;

namespace DiagnosticLabs.Application.Tests.Board;

public class RegistrationBoardServiceTests
{
    private readonly TestEnvironment _env = new();

    public RegistrationBoardServiceTests() => _env.Clock.UtcNow = DateTime.UtcNow;

    private RegistrationBoardService CreateService(AppDbContext db) => new(db, _env.Session);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private DateOnly Today => LocalTime.ToLocalDate(DateTime.UtcNow);

    // Midday of today: the registrations of "today" must not depend on what time of day the test happens to run.
    private DateTime Noon => LocalTime.ToUtc(Today, new TimeSpan(12, 0, 0));

    /// <summary>Services Stool (module 5), Urinalysis (module 6) and Medical Examination (module 14, a form that is not built yet).</summary>
    private async Task<(Service Stool, Service Urinalysis, Service MedicalExam)> SeedServicesAsync(AppDbContext db)
    {
        var type = new ModuleType { Id = 3 };
        var stool = new Service { ServiceName = "Stool/Fecalysis", ServiceDescription = "d", Price = 100m };
        var urinalysis = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 100m };
        var medical = new Service { ServiceName = "Medical Examination", ServiceDescription = "d", Price = 100m };
        db.Services.AddRange(stool, urinalysis, medical);
        await db.SaveChangesAsync();
        db.Modules.AddRange(
            new Module { Id = ModuleIds.StoolFecalysis, ModuleName = "Stool/Fecalysis", ModuleType = type, ServiceId = stool.Id },
            new Module { Id = ModuleIds.Urinalysis, ModuleName = "Urinalysis", ModuleType = type, ServiceId = urinalysis.Id },
            new Module { Id = ModuleIds.MedicalExamination, ModuleName = "Medical Examination", ModuleType = type, ServiceId = medical.Id });
        await db.SaveChangesAsync();
        return (stool, urinalysis, medical);
    }

    private async Task<PatientRegistration> RegisterAsync(
        AppDbContext db, string name, string code, DateTime when, decimal price, params Service[] services)
    {
        var registration = new PatientRegistration
        {
            RegistrationCode = code,
            Patient = new Patient { PatientCode = "P-" + code, PatientName = name },
            InputDate = when,
            AmountDue = price,
            DiscountAmount = 0,
        };
        foreach (var service in services)
            registration.Services.Add(new PatientRegistrationService { Service = service, Price = service.Price });

        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    private static void Pay(AppDbContext db, PatientRegistration registration, decimal amount, PaymentType type = PaymentType.Payment) =>
        db.Payments.Add(new Payment { PatientRegistrationId = registration.Id, PaymentAmount = amount, Type = type, PaymentDate = DateTime.UtcNow });

    private static void Result(AppDbContext db, PatientRegistration registration, LabReportType type) =>
        db.LabReports.Add(new LabReport
        {
            ReportType = type, PatientRegistrationId = registration.Id, PatientId = registration.PatientId, PatientName = registration.Patient.PatientName,
            DateRequested = DateTime.UtcNow,
        });

    [Fact]
    public async Task It_lists_a_days_registrations_newest_first_and_leaves_out_the_old_apps_default_visit()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (stool, _, _) = await SeedServicesAsync(db);
        var first = await RegisterAsync(db, "Ana Cruz", "BADC-1", Noon.AddHours(-2), 100m, stool);
        var second = await RegisterAsync(db, "Ben Reyes", "BADC-2", Noon.AddHours(-1), 100m, stool);
        await RegisterAsync(db, "Old Visit", "BADC-3", Noon.AddDays(-5), 100m, stool);
        await RegisterAsync(db, "Default Patient", RegistrationBoardService.DefaultRegistrationCode, Noon, 0m);

        var page = (await CreateService(db).SearchAsync(new BoardSearch(null, From: Today, To: Today))).Value;

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([second.Id, first.Id], page.Items.Select(i => i.RegistrationId));
    }

    [Fact]
    public async Task A_service_shows_whether_its_result_has_been_made()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (stool, urinalysis, medical) = await SeedServicesAsync(db);
        var registration = await RegisterAsync(db, "Ana Cruz", "BADC-1", DateTime.UtcNow, 300m, stool, urinalysis, medical);
        Result(db, registration, LabReportType.StoolFecalysis);
        Result(db, registration, LabReportType.StoolFecalysis);
        await db.SaveChangesAsync();

        var item = (await CreateService(db).SearchAsync(new BoardSearch("Ana"))).Value.Items.Single();

        var stoolChip = item.Services.Single(s => s.Name == "Stool/Fecalysis");
        var urineChip = item.Services.Single(s => s.Name == "Urinalysis");
        var medicalChip = item.Services.Single(s => s.Name == "Medical Examination");
        Assert.Equal((ModuleIds.StoolFecalysis, 2, true), (stoolChip.ModuleId, stoolChip.ResultCount, stoolChip.CanOpen));
        Assert.NotNull(stoolChip.LatestResultId);
        Assert.Equal((0, null), (urineChip.ResultCount, urineChip.LatestResultId));
        Assert.True(urineChip.CanOpen);
        Assert.False(medicalChip.CanOpen);
    }

    [Fact]
    public async Task Payment_status_is_unpaid_partial_paid_or_charged()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (stool, _, _) = await SeedServicesAsync(db);
        var unpaid = await RegisterAsync(db, "Unpaid One", "BADC-1", DateTime.UtcNow, 100m, stool);
        var partial = await RegisterAsync(db, "Partial One", "BADC-2", DateTime.UtcNow, 100m, stool);
        var paid = await RegisterAsync(db, "Paid One", "BADC-3", DateTime.UtcNow, 100m, stool);
        var charged = await RegisterAsync(db, "Charged One", "BADC-4", DateTime.UtcNow, 100m, stool);
        var discounted = await RegisterAsync(db, "Discounted One", "BADC-5", DateTime.UtcNow, 100m, stool);
        discounted.DiscountTotal = 20m;
        Pay(db, partial, 40m);
        Pay(db, paid, 100m);
        Pay(db, charged, 0m, PaymentType.Charge);
        Pay(db, discounted, 80m);
        await db.SaveChangesAsync();

        var items = (await CreateService(db).SearchAsync(new BoardSearch(null))).Value.Items.ToDictionary(i => i.PatientName);

        Assert.Equal(PaymentState.Unpaid, items["Unpaid One"].Payment.State);
        Assert.Equal((PaymentState.Partial, 40m, 100m), (items["Partial One"].Payment.State, items["Partial One"].Payment.Paid, items["Partial One"].Payment.Due));
        Assert.Equal(PaymentState.Paid, items["Paid One"].Payment.State);
        Assert.Equal(PaymentState.Charged, items["Charged One"].Payment.State);
        Assert.Equal((PaymentState.Paid, 80m), (items["Discounted One"].Payment.State, items["Discounted One"].Payment.Due));
        Assert.Equal(unpaid.Id, items["Unpaid One"].RegistrationId);
    }

    [Fact]
    public async Task The_unpaid_filter_leaves_out_paid_and_charged_registrations()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (stool, _, _) = await SeedServicesAsync(db);
        await RegisterAsync(db, "Unpaid One", "BADC-1", DateTime.UtcNow, 100m, stool);
        var partial = await RegisterAsync(db, "Partial One", "BADC-2", DateTime.UtcNow, 100m, stool);
        var paid = await RegisterAsync(db, "Paid One", "BADC-3", DateTime.UtcNow, 100m, stool);
        var charged = await RegisterAsync(db, "Charged One", "BADC-4", DateTime.UtcNow, 100m, stool);
        Pay(db, partial, 40m);
        Pay(db, paid, 100m);
        Pay(db, charged, 0m, PaymentType.Charge);
        await db.SaveChangesAsync();

        var names = (await CreateService(db).SearchAsync(new BoardSearch(null, Status: BoardStatus.Unpaid))).Value.Items.Select(i => i.PatientName);

        Assert.Equivalent(new[] { "Unpaid One", "Partial One" }, names);
    }

    [Fact]
    public async Task The_results_pending_filter_finds_registrations_with_a_form_still_to_make()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (stool, urinalysis, medical) = await SeedServicesAsync(db);
        var done = await RegisterAsync(db, "All Done", "BADC-1", DateTime.UtcNow, 200m, stool, urinalysis);
        await RegisterAsync(db, "Nothing Yet", "BADC-2", DateTime.UtcNow, 200m, stool, urinalysis);
        var half = await RegisterAsync(db, "Half Done", "BADC-3", DateTime.UtcNow, 200m, stool, urinalysis);
        await RegisterAsync(db, "Only A Form That Is Not Built", "BADC-4", DateTime.UtcNow, 100m, medical);
        Result(db, done, LabReportType.StoolFecalysis);
        Result(db, done, LabReportType.Urinalysis);
        Result(db, half, LabReportType.StoolFecalysis);
        await db.SaveChangesAsync();

        var names = (await CreateService(db).SearchAsync(new BoardSearch(null, Status: BoardStatus.ResultsPending))).Value.Items.Select(i => i.PatientName);

        Assert.Equivalent(new[] { "Nothing Yet", "Half Done" }, names);
    }

    [Fact]
    public async Task Text_company_and_paging_narrow_the_list()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (stool, _, _) = await SeedServicesAsync(db);
        var company = new Company { CompanyName = "ACME" };
        db.Companies.Add(company);
        for (var i = 1; i <= 5; i++)
        {
            var r = await RegisterAsync(db, $"Person {i}", $"BADC-{i}", DateTime.UtcNow.AddMinutes(-i), 100m, stool);
            if (i <= 3)
                r.CompanyId = company.Id;
        }

        await db.SaveChangesAsync();
        var service = CreateService(db);

        var byName = (await service.SearchAsync(new BoardSearch("Person 4"))).Value;
        var byCompany = (await service.SearchAsync(new BoardSearch(null, CompanyId: company.Id))).Value;
        var secondPage = (await service.SearchAsync(new BoardSearch(null, Page: 2, PageSize: 2))).Value;

        Assert.Equal("Person 4", byName.Items.Single().PatientName);
        Assert.Equal(3, byCompany.TotalCount);
        Assert.Equal(5, secondPage.TotalCount);
        Assert.Equal(["Person 3", "Person 4"], secondPage.Items.Select(i => i.PatientName));
        Assert.Equal(3, secondPage.TotalPages);
    }

    [Fact]
    public async Task Someone_without_registration_or_payment_access_is_refused()
    {
        await using var db = _env.CreateDb();
        _env.Session.SignIn(new AuthenticatedUser(2, "tech", "Tech", false, false, []));

        var result = await CreateService(db).SearchAsync(new BoardSearch(null));

        Assert.True(result.IsFailure);
    }
}
