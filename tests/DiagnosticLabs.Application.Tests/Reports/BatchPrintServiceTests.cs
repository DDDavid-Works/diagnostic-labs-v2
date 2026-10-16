using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticLabs.Application.Tests.Reports;

public class BatchPrintServiceTests
{
    private readonly TestEnvironment _env = new();

    /// <summary>Stands in for the result forms: "prints" a result as a one-line report carrying the patient name and the result id.</summary>
    private sealed class FakePrinters(AppDbContext db) : ILabResultPrinters
    {
        public ILabResultPrinting? For(int moduleId) => moduleId is ModuleIds.Hematology or ModuleIds.Urinalysis or >= ModuleIds.FirstCustomForm ? new Printer(db) : null;

        private sealed class Printer(AppDbContext db) : ILabResultPrinting
        {
            public Task<Result<PrintableReport>> GetPrintableAsync(long id, CancellationToken cancellationToken = default)
            {
                var result = db.LabReports.Single(l => l.Id == id);
                return Task.FromResult(Result<PrintableReport>.Success(new PrintableReport(
                    ReportLayout.Hematology, "Hematology", new ReportLetterhead("Lab", null, null, null, null, null),
                    [new PrintLine("Patient Name", result.PatientName), new PrintLine("Result", id.ToString())], [], [], [], string.Empty)));
            }
        }
    }

    private BatchPrintService CreateService(AppDbContext db) => new(db, _env.Session, new FakePrinters(db));

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private static ModulePermission Permission(int module, bool print) => new(module, false, false, false, print);

    private sealed record World(Service Hematology, Service Urinalysis, Service Medical, Company Acme, Company Other);

    /// <summary>Hematology and Urinalysis (forms that exist here), Medical Examination (module 14: no form), and two companies.</summary>
    private static async Task<World> SeedAsync(AppDbContext db)
    {
        var type = new ModuleType { Id = 3 };
        var hematology = new Service { ServiceName = "Hematology", ServiceDescription = "d", Price = 300m };
        var urinalysis = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 110m };
        var medical = new Service { ServiceName = "Medical Examination", ServiceDescription = "d", Price = 500m };
        var acme = new Company { CompanyName = "Acme" };
        var other = new Company { CompanyName = "Other" };
        db.Services.AddRange(hematology, urinalysis, medical);
        db.Companies.AddRange(acme, other);
        await db.SaveChangesAsync();
        db.Modules.AddRange(
            new Module { Id = ModuleIds.Hematology, ModuleName = "Hematology", ModuleType = type, ServiceId = hematology.Id, IsActive = true },
            new Module { Id = ModuleIds.Urinalysis, ModuleName = "Urinalysis", ModuleType = type, ServiceId = urinalysis.Id, IsActive = true },
            new Module { Id = ModuleIds.MedicalExamination, ModuleName = "Medical Examination", ModuleType = type, ServiceId = medical.Id, IsActive = true });
        await db.SaveChangesAsync();
        return new World(hematology, urinalysis, medical, acme, other);
    }

    private static async Task<PatientRegistration> RegisterAsync(AppDbContext db, string name, Company? company, string batch, params Service[] services)
    {
        var registration = new PatientRegistration
        {
            RegistrationCode = "R-" + Guid.NewGuid().ToString("N")[..8],
            Patient = new Patient { PatientCode = "P-" + Guid.NewGuid().ToString("N")[..8], PatientName = name },
            InputDate = DateTime.UtcNow,
            CompanyId = company?.Id,
            BatchName = batch,
        };
        foreach (var service in services)
            registration.Services.Add(new PatientRegistrationService { Service = service, Price = service.Price });

        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    /// <summary>Saves a result made on <paramref name="madeOn"/> (the date the row is created is what the date range looks at).</summary>
    private async Task<LabReport> ResultAsync(AppDbContext db, PatientRegistration registration, LabReportType type, DateTime madeOn)
    {
        _env.Clock.UtcNow = madeOn;
        var report = new LabReport
        {
            ReportType = type, PatientRegistrationId = registration.Id, PatientId = registration.PatientId,
            PatientName = registration.Patient.PatientName, DateRequested = madeOn,
        };
        db.LabReports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }

    private static readonly DateTime Noon = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static string[] Names(BatchPrintSearchResult result) => [.. result.Rows.Select(r => r.PatientName)];

    private static string[] Names(IEnumerable<PrintableReport> reports) => [.. reports.Select(r => r.PatientLines.Single(l => l.Label == "Patient Name").Value!)];

    [Fact]
    public async Task It_lists_every_patient_of_the_company_with_the_service_sorted_by_name_and_whether_the_result_was_inputted()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var w = await SeedAsync(db);
        var zed = await RegisterAsync(db, "Zed", w.Acme, "B1", w.Hematology);
        var abby = await RegisterAsync(db, "abby", w.Acme, "B2", w.Hematology);
        var carl = await RegisterAsync(db, "Carl", w.Acme, "B1", w.Hematology, w.Urinalysis);
        await RegisterAsync(db, "Waiting", w.Acme, "B1", w.Hematology);
        await RegisterAsync(db, "No such service", w.Acme, "B1", w.Urinalysis);
        var zedResult = await ResultAsync(db, zed, LabReportType.Hematology, Noon);
        await ResultAsync(db, abby, LabReportType.Hematology, Noon.AddDays(1));
        var carlOld = await ResultAsync(db, carl, LabReportType.Hematology, Noon);
        var carlNew = await ResultAsync(db, carl, LabReportType.Hematology, Noon.AddDays(2));
        await ResultAsync(db, carl, LabReportType.Urinalysis, Noon); // another service's result does not count here

        var result = (await CreateService(db).SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id))).Value;

        Assert.Equal(["abby", "Carl", "Waiting", "Zed"], Names(result));
        Assert.Equal([true, true, false, true], result.Rows.Select(r => r.HasResult));
        Assert.Equal(carlNew.Id, result.Rows[1].ResultId); // the latest of Carl's two
        Assert.NotEqual(carlOld.Id, result.Rows[1].ResultId);
        Assert.Equal(zedResult.Id, result.Rows[3].ResultId);
        Assert.Equal(LocalTime.ToLocalDate(Noon.AddDays(2)), result.Rows[1].ResultDate);
        Assert.Null(result.Rows[2].ResultId);
        Assert.Null(result.Rows[2].ResultDate);
        Assert.Equal(("Hematology", "Acme"), (result.ServiceName, result.CompanyName));
    }

    [Fact]
    public async Task A_batch_name_only_means_something_inside_its_company()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var w = await SeedAsync(db);
        var mine = await RegisterAsync(db, "Mine", w.Acme, "Batch 1", w.Hematology);
        var theirs = await RegisterAsync(db, "Theirs", w.Other, "Batch 1", w.Hematology);
        var otherBatch = await RegisterAsync(db, "Other batch", w.Acme, "Batch 2", w.Hematology);
        foreach (var registration in new[] { mine, theirs, otherBatch })
            await ResultAsync(db, registration, LabReportType.Hematology, Noon);
        var service = CreateService(db);

        var batch = (await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id, "Batch 1"))).Value;
        var company = (await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id))).Value;

        Assert.Equal(["Mine"], Names(batch));
        Assert.Equal(["Mine", "Other batch"], Names(company));
    }

    [Fact]
    public async Task The_date_range_looks_at_the_day_the_result_was_made_and_includes_both_end_days()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var w = await SeedAsync(db);
        var day = new DateOnly(2026, 10, 10);
        DateTime At(int days, int hour) => LocalTime.ToUtc(day.AddDays(days), new TimeSpan(hour, 0, 0));
        var before = await RegisterAsync(db, "Before", w.Acme, "B", w.Hematology);
        var first = await RegisterAsync(db, "First day", w.Acme, "B", w.Hematology);
        var last = await RegisterAsync(db, "Last day", w.Acme, "B", w.Hematology);
        var after = await RegisterAsync(db, "After", w.Acme, "B", w.Hematology);
        var redone = await RegisterAsync(db, "Redone", w.Acme, "B", w.Hematology);
        await ResultAsync(db, before, LabReportType.Hematology, At(-1, 23));
        await ResultAsync(db, first, LabReportType.Hematology, At(0, 0));
        await ResultAsync(db, last, LabReportType.Hematology, At(1, 23));
        await ResultAsync(db, after, LabReportType.Hematology, At(2, 1));
        var inside = await ResultAsync(db, redone, LabReportType.Hematology, At(0, 9)); // made in the range, then made again after it
        await ResultAsync(db, redone, LabReportType.Hematology, At(3, 9));

        var service = CreateService(db);
        var inRange = (await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id, From: day, To: day.AddDays(1)))).Value;
        var openEnded = (await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id, From: day.AddDays(2)))).Value;
        var noRange = (await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id))).Value;

        // Every patient is listed; the ones without a result made inside the range are marked as having none.
        Assert.Equal(5, inRange.Rows.Count);
        Assert.Equal(["First day", "Last day", "Redone"], inRange.Rows.Where(r => r.HasResult).Select(r => r.PatientName));
        Assert.Equal(inside.Id, inRange.Rows.Single(r => r.PatientName == "Redone").ResultId); // the latest result inside the range
        Assert.Equal(["After", "Redone"], openEnded.Rows.Where(r => r.HasResult).Select(r => r.PatientName));
        Assert.Equal(5, noRange.Rows.Count(r => r.HasResult));
    }

    [Fact]
    public async Task It_needs_a_company_a_service_and_a_sensible_range()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var w = await SeedAsync(db);
        var service = CreateService(db);

        var neither = await service.SearchAsync(new BatchPrintRequest(0, 0));
        var backwards = await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id, From: new DateOnly(2026, 10, 11), To: new DateOnly(2026, 10, 10)));
        var unknownCompany = await service.SearchAsync(new BatchPrintRequest(999, w.Hematology.Id));

        Assert.Equal("Validation.Invalid", neither.Error.Code);
        Assert.Contains("company", neither.Error.Message);
        Assert.Contains("service", neither.Error.Message);
        Assert.Equal("Validation.Invalid", backwards.Error.Code);
        Assert.True(unknownCompany.IsFailure);
    }

    [Fact]
    public async Task It_prints_the_results_it_is_given_in_the_order_given()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var w = await SeedAsync(db);
        var ann = await RegisterAsync(db, "Ann", w.Acme, "B", w.Hematology);
        var bob = await RegisterAsync(db, "Bob", w.Acme, "B", w.Hematology);
        var annResult = await ResultAsync(db, ann, LabReportType.Hematology, Noon);
        var bobResult = await ResultAsync(db, bob, LabReportType.Hematology, Noon);
        var service = CreateService(db);

        var reports = (await service.GetPrintablesAsync(w.Hematology.Id, [bobResult.Id, annResult.Id])).Value;
        var none = (await service.GetPrintablesAsync(w.Hematology.Id, [])).Value;

        Assert.Equal(["Bob", "Ann"], Names(reports));
        Assert.Empty(none);
    }

    [Fact]
    public async Task It_offers_only_services_that_have_a_result_form_the_user_may_print()
    {
        await using var db = _env.CreateDb();
        var w = await SeedAsync(db);

        SignInAsAdmin();
        Assert.Equal(["Urinalysis", "Hematology"], (await CreateService(db).GetServicesAsync()).Select(s => s.Name)); // in the order of the forms; Medical Examination has no form here

        _env.Session.SignIn(new AuthenticatedUser(2, "u", "User", false, false, [Permission(ModuleIds.BatchPrint, true), Permission(ModuleIds.Hematology, true), Permission(ModuleIds.Urinalysis, false)]));
        Assert.Equal(["Hematology"], (await CreateService(db).GetServicesAsync()).Select(s => s.Name));

        _env.Session.SignIn(new AuthenticatedUser(3, "u", "User", false, false, [Permission(ModuleIds.BatchPrint, false), Permission(ModuleIds.Hematology, true)]));
        Assert.Empty(await CreateService(db).GetServicesAsync());
        Assert.Equal(Errors.Forbidden, (await CreateService(db).SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Hematology.Id))).Error);
    }

    [Fact]
    public async Task It_refuses_a_service_the_user_may_not_print_even_when_asked_for_by_id()
    {
        await using var db = _env.CreateDb();
        var w = await SeedAsync(db);
        _env.Session.SignIn(new AuthenticatedUser(2, "u", "User", false, false, [Permission(ModuleIds.BatchPrint, true), Permission(ModuleIds.Hematology, true)]));
        var service = CreateService(db);

        Assert.Equal(Errors.Forbidden, (await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, w.Urinalysis.Id))).Error); // no Print on Urinalysis
        Assert.Equal(Errors.Forbidden, (await service.GetPrintablesAsync(w.Urinalysis.Id, [1])).Error);
        Assert.Equal(Errors.Forbidden, (await service.GetPrintablesAsync(w.Medical.Id, [1])).Error); // a service without a form
    }

    [Fact]
    public async Task Printing_needs_the_batch_print_permission()
    {
        await using var db = _env.CreateDb();
        var w = await SeedAsync(db);
        _env.Session.SignIn(new AuthenticatedUser(3, "u", "User", false, false, [Permission(ModuleIds.BatchPrint, false), Permission(ModuleIds.Hematology, true)]));

        Assert.Equal(Errors.Forbidden, (await CreateService(db).GetPrintablesAsync(w.Hematology.Id, [1])).Error);
    }

    // ---------------------------------------------------------------- forms made in the Custom Lab Form designer

    private const int FormA = ModuleIds.FirstCustomForm;
    private const int FormB = ModuleIds.FirstCustomForm + 1;

    /// <summary>Two custom forms (modules 1000 and 1001), each with its own service and one design version.</summary>
    private static async Task<(Service A, Service B)> SeedCustomFormsAsync(AppDbContext db)
    {
        var a = new Service { ServiceName = "Lipid Profile", ServiceDescription = "d", Price = 0m };
        var b = new Service { ServiceName = "Thyroid Panel", ServiceDescription = "d", Price = 0m };
        db.Services.AddRange(a, b);
        await db.SaveChangesAsync();
        foreach (var (moduleId, name, service) in new[] { (FormA, "Lipid Profile", a), (FormB, "Thyroid Panel", b) })
        {
            db.Modules.Add(new Module { Id = moduleId, ModuleName = name, ModuleTypeId = 3, ServiceId = service.Id, IsActive = true });
            var form = new CustomForm { Name = name, ModuleId = moduleId };
            form.Versions.Add(new CustomFormVersion { VersionNumber = 1, DefinitionJson = FormDefinition.NewForm().ToJson() });
            db.CustomForms.Add(form);
        }

        await db.SaveChangesAsync();
        return (a, b);
    }

    private async Task<LabReport> CustomResultAsync(AppDbContext db, PatientRegistration registration, int moduleId)
    {
        var report = await ResultAsync(db, registration, LabReportType.CustomForm, Noon);
        var version = db.CustomFormVersions.Single(v => v.CustomForm.ModuleId == moduleId);
        db.CustomFormReports.Add(new CustomFormReport { LabReportId = report.Id, CustomFormVersionId = version.Id });
        await db.SaveChangesAsync();
        return report;
    }

    [Fact]
    public async Task Custom_forms_are_offered_like_any_other_form_when_the_user_may_print_them()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        await SeedCustomFormsAsync(db);

        SignInAsAdmin();
        Assert.Equal(["Urinalysis", "Hematology", "Lipid Profile", "Thyroid Panel"], (await CreateService(db).GetServicesAsync()).Select(s => s.Name));

        _env.Session.SignIn(new AuthenticatedUser(2, "u", "User", false, false, [Permission(ModuleIds.BatchPrint, true), Permission(FormB, true), Permission(FormA, false)]));
        Assert.Equal(["Thyroid Panel"], (await CreateService(db).GetServicesAsync()).Select(s => s.Name));
    }

    [Fact]
    public async Task A_search_for_a_custom_form_only_counts_the_results_of_that_form()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var w = await SeedAsync(db);
        var (lipid, thyroid) = await SeedCustomFormsAsync(db);
        var ann = await RegisterAsync(db, "Ann", w.Acme, "B", lipid, thyroid);
        var bob = await RegisterAsync(db, "Bob", w.Acme, "B", lipid, thyroid);
        var annLipid = await CustomResultAsync(db, ann, FormA);
        await CustomResultAsync(db, bob, FormB); // Bob has a result of the other form only
        var service = CreateService(db);

        var result = (await service.SearchAsync(new BatchPrintRequest(w.Acme.Id, lipid.Id))).Value;

        Assert.Equal(["Ann", "Bob"], Names(result));
        Assert.Equal([annLipid.Id, null], result.Rows.Select(r => r.ResultId));
        Assert.Equal("Lipid Profile", result.ServiceName);
    }

    [Fact]
    public async Task Custom_form_results_are_printed_by_the_form_they_belong_to()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var w = await SeedAsync(db);
        var (lipid, _) = await SeedCustomFormsAsync(db);
        var ann = await RegisterAsync(db, "Ann", w.Acme, "B", lipid);
        var result = await CustomResultAsync(db, ann, FormA);
        var providers = new ServiceCollection()
            .AddSingleton<IAppDbContext>(db).AddSingleton<ICurrentUser>(_env.Session).AddSingleton<IClock>(_env.Clock).BuildServiceProvider();
        var printers = new LabResultPrinters(providers);

        var printable = await new BatchPrintService(db, _env.Session, printers).GetPrintablesAsync(lipid.Id, [result.Id]);

        Assert.True(printable.IsSuccess, printable.IsFailure ? printable.Error.Message : "");
        Assert.Equal(ReportLayout.CustomForm, printable.Value.Single().Layout);
        Assert.Equal("Lipid Profile", printable.Value.Single().Title);
        Assert.Null(printers.For(ModuleIds.MedicalExamination)); // a module with no result form still has no printer
    }
}
