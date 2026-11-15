using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Board;

/// <summary>
/// Complete Blood Count is entered in the CBC section of the Physical Examination sheet; Urinalysis and Stool/Fecalysis go on their own form or
/// on their section of the sheet, chosen per registration. The home chips, the "results pending" filter, Batch Print and the save warnings follow.
/// </summary>
public class FormSectionTests
{
    private readonly TestEnvironment _env = new();

    public FormSectionTests() => _env.Clock.UtcNow = DateTime.UtcNow;

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private sealed record Catalog(Service Stool, Service Urine, Service Cbc, Service Sheet);

    private static async Task<Catalog> SeedAsync(AppDbContext db)
    {
        var type = new ModuleType { Id = 3 };
        var stool = new Service { ServiceName = "Stool/Fecalysis", ServiceDescription = "d", Price = 90m, IsActive = true };
        var urine = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 90m, IsActive = true };
        var cbc = new Service { ServiceName = "Complete Blood Count", ServiceDescription = "d", Price = 150m, IsActive = true };
        var sheet = new Service { ServiceName = "Physical Examination", ServiceDescription = "d", Price = 0m, IsActive = true };
        db.Services.AddRange(stool, urine, cbc, sheet);
        await db.SaveChangesAsync();
        db.Modules.AddRange(
            new Module { Id = ModuleIds.StoolFecalysis, ModuleName = "Stool/Fecalysis", ModuleType = type, ServiceId = stool.Id, IsActive = true },
            new Module { Id = ModuleIds.Urinalysis, ModuleName = "Urinalysis", ModuleType = type, ServiceId = urine.Id, IsActive = true },
            new Module { Id = ModuleIds.PhysicalExamination, ModuleName = "Physical Examination", ModuleType = type, ServiceId = sheet.Id, IsActive = true });
        db.ServiceFormSections.AddRange(
            new ServiceFormSection { ServiceId = cbc.Id, ModuleId = ModuleIds.PhysicalExamination, Section = FormSection.Cbc },
            new ServiceFormSection { ServiceId = urine.Id, ModuleId = ModuleIds.PhysicalExamination, Section = FormSection.Urinalysis },
            new ServiceFormSection { ServiceId = stool.Id, ModuleId = ModuleIds.PhysicalExamination, Section = FormSection.Fecalysis });
        await db.SaveChangesAsync();
        return new Catalog(stool, urine, cbc, sheet);
    }

    private static async Task<PatientRegistration> RegisterAsync(AppDbContext db, string name, Company? company, params Service[] services)
    {
        var registration = new PatientRegistration
        {
            RegistrationCode = "R-" + name,
            Patient = new Patient { PatientCode = "P-" + name, PatientName = name },
            InputDate = DateTime.UtcNow,
            AmountDue = services.Sum(s => s.Price),
            Company = company,
        };
        foreach (var service in services)
            registration.Services.Add(new PatientRegistrationService { Service = service, Price = service.Price });

        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    private static async Task<long> SheetAsync(AppDbContext db, PatientRegistration registration, Action<PhysicalExaminationReport> fill)
    {
        var report = new LabReport
        {
            ReportType = LabReportType.PhysicalExamination, PatientRegistrationId = registration.Id, PatientId = registration.PatientId,
            PatientName = registration.Patient.PatientName, DateRequested = DateTime.UtcNow,
        };
        var detail = new PhysicalExaminationReport { LabReport = report, HematocritNValue = "0.42 - 0.52" };
        fill(detail);
        db.Set<PhysicalExaminationReport>().Add(detail);
        await db.SaveChangesAsync();
        return report.Id;
    }

    private static async Task<long> OwnResultAsync(AppDbContext db, PatientRegistration registration, LabReportType type)
    {
        var report = new LabReport
        {
            ReportType = type, PatientRegistrationId = registration.Id, PatientId = registration.PatientId,
            PatientName = registration.Patient.PatientName, DateRequested = DateTime.UtcNow,
        };
        db.LabReports.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    }

    private async Task<BoardItem> BoardOfAsync(AppDbContext db, string name) =>
        (await new RegistrationBoardService(db, _env.Session).SearchAsync(new BoardSearch(name))).Value.Items.Single();

    [Fact]
    public async Task Complete_blood_count_opens_the_cbc_section_of_the_sheet_and_turns_done_once_it_has_a_result()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var c = await SeedAsync(db);
        var registration = await RegisterAsync(db, "Ana", null, c.Cbc);

        var before = (await BoardOfAsync(db, "Ana")).Services.Single();
        Assert.Equal((ModuleIds.PhysicalExamination, FormSection.Cbc, 0, true), (before.ModuleId, before.Section, before.ResultCount, before.CanOpen));
        Assert.Null(before.OpenResultId);

        // A sheet with only the normal values (the defaults) does not count; a result does.
        var sheetId = await SheetAsync(db, registration, _ => { });
        var empty = (await BoardOfAsync(db, "Ana")).Services.Single();
        Assert.Equal((0, sheetId), (empty.ResultCount, empty.OpenResultId));

        (await db.Set<PhysicalExaminationReport>().SingleAsync()).HemoglobinResult = "140";
        await db.SaveChangesAsync();
        var done = (await BoardOfAsync(db, "Ana")).Services.Single();
        Assert.Equal((1, sheetId), (done.ResultCount, done.LatestResultId));
    }

    [Fact]
    public async Task Urinalysis_asks_where_to_enter_it_until_a_form_is_chosen_and_then_opens_that_form()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var c = await SeedAsync(db);
        var registration = await RegisterAsync(db, "Ben", null, c.Cbc, c.Urine);
        var sheetId = await SheetAsync(db, registration, s => s.WBCCountResult = "7");

        var urine = (await BoardOfAsync(db, "Ben")).Services.Single(s => s.Name == "Urinalysis");
        Assert.True(urine.NeedsChoice);
        Assert.True(urine.CanOpen);
        Assert.Null(urine.ModuleId);
        Assert.Equal([ModuleIds.Urinalysis, ModuleIds.PhysicalExamination], urine.Choices!.Select(x => x.ModuleId));
        Assert.Equal(sheetId, urine.Choices!.Single(x => x.Section == FormSection.Urinalysis).SheetId);

        var board = new RegistrationBoardService(db, _env.Session);
        Assert.True((await board.ChooseFormAsync(urine.RegistrationServiceId, ModuleIds.PhysicalExamination)).IsSuccess);
        Assert.True((await board.ChooseFormAsync(urine.RegistrationServiceId, ModuleIds.Hematology)).IsFailure);

        var chosen = (await BoardOfAsync(db, "Ben")).Services.Single(s => s.Name == "Urinalysis");
        Assert.False(chosen.NeedsChoice);
        Assert.Equal((ModuleIds.PhysicalExamination, FormSection.Urinalysis, sheetId, 0), (chosen.ModuleId, chosen.Section, chosen.OpenResultId, chosen.ResultCount));
    }

    [Fact]
    public async Task A_result_entered_in_either_place_marks_the_service_done_and_opens_where_it_is()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var c = await SeedAsync(db);
        var onSheet = await RegisterAsync(db, "Cora", null, c.Urine, c.Stool);
        var sheetId = await SheetAsync(db, onSheet, s => s.UrineColor = "Yellow");
        var ownStool = await OwnResultAsync(db, onSheet, LabReportType.StoolFecalysis);

        var services = (await BoardOfAsync(db, "Cora")).Services;
        var urine = services.Single(s => s.Name == "Urinalysis");
        var stool = services.Single(s => s.Name == "Stool/Fecalysis");
        Assert.Equal((ModuleIds.PhysicalExamination, 1, sheetId), (urine.ModuleId, urine.ResultCount, urine.LatestResultId));
        Assert.Equal((ModuleIds.StoolFecalysis, 1, ownStool), (stool.ModuleId, stool.ResultCount, stool.LatestResultId));
    }

    [Fact]
    public async Task The_results_pending_filter_counts_a_section_entered_on_the_sheet()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var c = await SeedAsync(db);
        var done = await RegisterAsync(db, "Done", null, c.Cbc, c.Urine);
        await SheetAsync(db, done, s => { s.HematocritResult = "0.45"; s.UrineOthers = "none"; });
        var half = await RegisterAsync(db, "Half", null, c.Cbc, c.Urine);
        await SheetAsync(db, half, s => s.HematocritResult = "0.45");
        var own = await RegisterAsync(db, "Own", null, c.Urine);
        await OwnResultAsync(db, own, LabReportType.Urinalysis);
        await RegisterAsync(db, "Nothing", null, c.Cbc);

        var names = (await new RegistrationBoardService(db, _env.Session).SearchAsync(new BoardSearch(null, Status: BoardStatus.ResultsPending)))
            .Value.Items.Select(i => i.PatientName);

        Assert.Equivalent(new[] { "Half", "Nothing" }, names);
    }

    [Fact]
    public async Task A_new_sheet_knows_which_sections_the_registration_puts_on_it()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var c = await SeedAsync(db);
        var registration = await RegisterAsync(db, "Dina", null, c.Cbc, c.Urine, c.Stool);
        var lookup = new LabRegistrationLookup(db, _env.Session, _env.Clock);

        Assert.Equal([FormSection.Cbc], (await lookup.GetAsync(ModuleIds.PhysicalExamination, registration.Id)).Value.SheetSections);

        var urineLine = await db.PatientRegistrationServices.SingleAsync(s => s.ServiceId == c.Urine.Id);
        urineLine.ResultModuleId = ModuleIds.PhysicalExamination;
        await db.SaveChangesAsync();
        Assert.Equal([FormSection.Cbc, FormSection.Urinalysis], (await lookup.GetAsync(ModuleIds.PhysicalExamination, registration.Id)).Value.SheetSections);

        // An older registration billed for the whole sheet has every section.
        var older = await RegisterAsync(db, "Elle", null, c.Sheet);
        Assert.Equal(3, (await lookup.GetAsync(ModuleIds.PhysicalExamination, older.Id)).Value.SheetSections.Count);
        Assert.Empty((await lookup.GetAsync(ModuleIds.Urinalysis, registration.Id)).Value.SheetSections);
    }

    [Fact]
    public async Task Entering_a_result_that_is_already_in_its_other_place_asks_first()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var c = await SeedAsync(db);
        var registration = await RegisterAsync(db, "Fe", null, c.Urine, c.Stool);
        await SheetAsync(db, registration, s => s.UrineColor = "Yellow");
        await OwnResultAsync(db, registration, LabReportType.StoolFecalysis);
        LabResultHeader Header(bool confirmed = false) => new(registration.Id, null, null, "Fe", null, null, null, LocalTime.ToLocalDate(DateTime.UtcNow), null, null, null, confirmed);

        // Urinalysis on its own form while the sheet has it
        var urinalysis = new UrinalysisService(db, _env.Session, _env.Clock);
        var refused = await urinalysis.SaveAsync(new UrinalysisInput(0, Header(), "Yellow", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null));
        Assert.Equal("LabResult.Duplicate", refused.Error.Code);
        Assert.Contains("Physical Examination sheet", refused.Error.Message);
        Assert.True((await urinalysis.SaveAsync(new UrinalysisInput(0, Header(confirmed: true), "Yellow", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null))).IsSuccess);

        // Fecalysis newly entered on the sheet while its own form has it
        var sheets = new PhysicalExaminationService(db, _env.Session, _env.Clock);
        var sheet = await db.LabReports.AsNoTracking().SingleAsync(l => l.ReportType == LabReportType.PhysicalExamination);
        var details = (await sheets.GetAsync(sheet.Id)).Value;
        var values = details.Data.Values.ToDictionary(kv => kv.Key, kv => kv.Value);
        values["FecalysisColor"] = "Brown";
        var input = new PhysicalExaminationInput(sheet.Id, details.Header, new PhysicalExaminationData(details.Data.Dates, values), details.RowVersion);
        var asked = await sheets.SaveAsync(input);
        Assert.Contains("Stool/Fecalysis", asked.Error.Message);

        // Saving the sheet again without touching the fecalysis section does not ask (only a section entered now does).
        Assert.True((await sheets.SaveAsync(input with { Header = details.Header with { ConfirmedDuplicate = true } })).IsSuccess);
        var again = (await sheets.GetAsync(sheet.Id)).Value;
        Assert.True((await sheets.SaveAsync(new PhysicalExaminationInput(sheet.Id, again.Header, again.Data, again.RowVersion))).IsSuccess);
    }

    [Fact]
    public async Task Batch_print_finds_each_patients_result_on_whichever_form_it_was_entered()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var c = await SeedAsync(db);
        var company = new Company { CompanyName = "ACME" };
        var onSheet = await RegisterAsync(db, "Gil", company, c.Urine);
        var sheetId = await SheetAsync(db, onSheet, s => s.UrinePusCells = "0-2");
        var onOwn = await RegisterAsync(db, "Hana", company, c.Urine);
        var ownId = await OwnResultAsync(db, onOwn, LabReportType.Urinalysis);
        var cbcOnly = await RegisterAsync(db, "Ivo", company, c.Urine);
        await SheetAsync(db, cbcOnly, s => s.HematocritResult = "0.4");
        db.Modules.Add(new Module { Id = ModuleIds.BatchPrint, ModuleName = "Batch Print", ModuleTypeId = 3, IsActive = true });
        await db.SaveChangesAsync();

        var batch = new BatchPrintService(db, _env.Session, null!);
        Assert.Contains(await batch.GetServicesAsync(), o => o.Name == "Complete Blood Count" && o.ModuleId == ModuleIds.PhysicalExamination);
        var rows = (await batch.SearchAsync(new BatchPrintRequest(company.Id, c.Urine.Id))).Value.Rows.ToDictionary(r => r.PatientName, r => r.ResultId);

        Assert.Equal(sheetId, rows["Gil"]);
        Assert.Equal(ownId, rows["Hana"]);
        Assert.Null(rows["Ivo"]);
    }

    [Fact]
    public void Each_section_counts_only_its_results_not_its_normal_values()
    {
        Assert.DoesNotContain(PhysicalExaminationFields.SectionFields[FormSection.Cbc], n => n.EndsWith("NValue", StringComparison.Ordinal));
        Assert.Contains("UrineOthers", PhysicalExaminationFields.SectionFields[FormSection.Urinalysis]);
        Assert.Equal(["FecalysisColor", "FecalysisConsistency", "FecalysisResult"], PhysicalExaminationFields.SectionFields[FormSection.Fecalysis]);
        var columns = typeof(PhysicalExaminationReport).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.All(PhysicalExaminationFields.SectionFields.Values.SelectMany(f => f), n => Assert.Contains(n, columns));
    }
}
