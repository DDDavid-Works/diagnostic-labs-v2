using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Board;

/// <summary>The Home list for services whose result form was made in the Custom Lab Form designer.</summary>
public class CustomFormBoardTests
{
    private const int FormA = ModuleIds.FirstCustomForm;
    private const int FormB = ModuleIds.FirstCustomForm + 1;

    private readonly TestEnvironment _env = new();

    public CustomFormBoardTests()
    {
        _env.Clock.UtcNow = DateTime.UtcNow;
        _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));
    }

    private static async Task<(Service A, Service B, long VersionA, long VersionB)> SeedFormsAsync(AppDbContext db)
    {
        var a = new Service { ServiceName = "Lipid Profile", ServiceDescription = "d", Price = 100m };
        var b = new Service { ServiceName = "Thyroid Panel", ServiceDescription = "d", Price = 100m };
        db.Services.AddRange(a, b);
        await db.SaveChangesAsync();

        long[] versions = new long[2];
        var i = 0;
        foreach (var (moduleId, service) in new[] { (FormA, a), (FormB, b) })
        {
            var module = new Module { Id = moduleId, ModuleName = service.ServiceName, ServiceId = service.Id, IsActive = true, ModuleType = new ModuleType { Id = 90 + i } };
            var form = new CustomForm { Name = service.ServiceName, Module = module, ModuleId = moduleId };
            var version = new CustomFormVersion { VersionNumber = 1, DefinitionJson = FormDefinition.NewForm().ToJson() };
            form.Versions.Add(version);
            db.CustomForms.Add(form);
            await db.SaveChangesAsync();
            versions[i++] = version.Id;
        }

        return (a, b, versions[0], versions[1]);
    }

    private static async Task<PatientRegistration> RegisterAsync(AppDbContext db, string name, params Service[] services)
    {
        var registration = new PatientRegistration
        {
            RegistrationCode = "R-" + name,
            Patient = new Patient { PatientCode = "P-" + name, PatientName = name },
            InputDate = DateTime.UtcNow,
            AmountDue = 100m,
        };
        foreach (var service in services)
            registration.Services.Add(new PatientRegistrationService { Service = service, Price = service.Price });

        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    private static async Task<long> ResultAsync(AppDbContext db, PatientRegistration registration, long versionId)
    {
        var report = new LabReport
        {
            ReportType = LabReportType.CustomForm, PatientRegistrationId = registration.Id, PatientId = registration.PatientId,
            PatientName = registration.Patient.PatientName, DateRequested = DateTime.UtcNow,
        };
        db.LabReports.Add(report);
        await db.SaveChangesAsync();
        db.CustomFormReports.Add(new CustomFormReport { LabReportId = report.Id, CustomFormVersionId = versionId });
        await db.SaveChangesAsync();
        return report.Id;
    }

    [Fact]
    public async Task A_custom_forms_chip_can_be_opened_and_counts_only_the_results_of_its_own_form()
    {
        await using var db = _env.CreateDb();
        var (a, b, versionA, versionB) = await SeedFormsAsync(db);
        var registration = await RegisterAsync(db, "Ana", a, b);
        var first = await ResultAsync(db, registration, versionA);
        var latest = await ResultAsync(db, registration, versionA);
        var other = await ResultAsync(db, registration, versionB);

        var item = (await new RegistrationBoardService(db, _env.Session).SearchAsync(new BoardSearch(null))).Value.Items.Single();

        var chipA = item.Services.Single(s => s.ServiceId == a.Id);
        var chipB = item.Services.Single(s => s.ServiceId == b.Id);
        Assert.Equal(FormA, chipA.ModuleId);
        Assert.True(chipA.CanOpen);
        Assert.Equal(2, chipA.ResultCount);
        Assert.Equal(latest, chipA.LatestResultId);
        Assert.Equal(1, chipB.ResultCount);
        Assert.Equal(other, chipB.LatestResultId);
        Assert.NotEqual(first, latest);
    }

    [Fact]
    public async Task A_custom_form_with_no_result_yet_is_pending_until_one_is_made()
    {
        await using var db = _env.CreateDb();
        var (a, b, versionA, versionB) = await SeedFormsAsync(db);
        var both = await RegisterAsync(db, "Ana", a, b);
        var onlyB = await RegisterAsync(db, "Ben", b);
        await ResultAsync(db, both, versionA);
        await ResultAsync(db, onlyB, versionB);
        var service = new RegistrationBoardService(db, _env.Session);

        // Ana still waits for her Thyroid Panel; Ben has the only result he needs
        var pending = (await service.SearchAsync(new BoardSearch(null, Status: BoardStatus.ResultsPending))).Value.Items;
        Assert.Equal([both.Id], pending.Select(i => i.RegistrationId));

        await ResultAsync(db, both, versionB);
        Assert.Empty((await service.SearchAsync(new BoardSearch(null, Status: BoardStatus.ResultsPending))).Value.Items);
    }

    [Fact]
    public void Custom_form_modules_are_told_apart_from_the_built_in_ones()
    {
        Assert.True(LabResultModules.IsCustomForm(FormA));
        Assert.False(LabResultModules.IsCustomForm(ModuleIds.ClinicalMicroscopy));
        Assert.False(LabResultModules.IsCustomForm(ModuleIds.FormDesigner));
        Assert.Null(LabResultModules.ReportTypeOf(FormA)); // batch printing does not know custom forms yet
    }
}
