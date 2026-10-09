using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Domain.Settings;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class StoolFecalysisServiceTests
{
    private readonly TestEnvironment _env = new();

    public StoolFecalysisServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private StoolFecalysisService CreateService(AppDbContext db) => new(db, _env.Session, _env.Clock);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private async Task<PatientRegistration> SeedRegistrationAsync(AppDbContext db, string name = "Emma Millstein", string code = "BADC-09OC26-00001")
    {
        var patient = new Patient { PatientCode = "2026-00000005", PatientName = name, DateOfBirth = new DateOnly(2000, 2, 8), Sex = "Female" };
        var registration = new PatientRegistration
        {
            RegistrationCode = code, Patient = patient, InputDate = _env.Clock.UtcNow, AmountDue = 100,
            Company = new Company { CompanyName = "ACME" },
        };
        registration.Services.Add(new PatientRegistrationService { Service = new Service { ServiceName = "Stool/Fecalysis", ServiceDescription = "d", Price = 100 }, Price = 100 });
        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    private LabResultHeader Header(long? registrationId = null, string? name = "Emma Millstein", bool confirmed = false) => new(
        registrationId, null, "2026-00000005", name, "26 years old", "Female", "ACME", Today, "NONE", "DR. GUTZ", "DR. GALLEN", confirmed);

    private StoolFecalysisInput Input(LabResultHeader header, long id = 0, byte[]? rowVersion = null) =>
        new(id, header, "BROWN", "FORMED", "NORMAL", rowVersion);

    // ---------------------------------------------------------------- saving

    [Fact]
    public async Task A_result_made_from_a_registration_links_the_patient_and_keeps_the_typed_snapshot()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);

        var saved = (await CreateService(db).SaveAsync(Input(Header(registration.Id) with { PatientName = "  Emma M. Millstein " }))).Value;

        Assert.NotEqual(0, saved.Id);
        Assert.Equal(registration.Id, saved.Header.RegistrationId);
        Assert.Equal("BADC-09OC26-00001", saved.Header.RegistrationCode);
        Assert.Equal("Emma M. Millstein", saved.Header.PatientName);
        Assert.Equal("BROWN", saved.Color);
        Assert.Equal("NORMAL", saved.Result);
        var row = await db.LabReports.AsNoTracking().SingleAsync();
        Assert.Equal(registration.PatientId, row.PatientId);
        Assert.Equal(LabReportType.StoolFecalysis, row.ReportType);
        Assert.Equal("DR. GALLEN", row.Pathologist);
        Assert.Equal(1, await db.Set<DiagnosticLabs.Domain.Lab.Reports.StoolFecalysisReport>().CountAsync());
    }

    [Fact]
    public async Task A_stray_result_needs_no_registration_and_no_patient()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var saved = await CreateService(db).SaveAsync(Input(Header(null, "Walk Up Person") with { PatientCode = null }));

        Assert.True(saved.IsSuccess);
        var row = await db.LabReports.AsNoTracking().SingleAsync();
        Assert.Null(row.PatientId);
        Assert.Null(row.PatientRegistrationId);
        Assert.Equal("Walk Up Person", row.PatientName);
        Assert.Equal(string.Empty, row.PatientCode);
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    public async Task A_second_result_of_the_same_kind_for_one_registration_needs_confirmation_but_a_stray_never_does()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(Input(Header(registration.Id)));

        var refused = await service.SaveAsync(Input(Header(registration.Id)));
        var confirmed = await service.SaveAsync(Input(Header(registration.Id, confirmed: true)));
        var strayOne = await service.SaveAsync(Input(Header(null, "Stray")));
        var strayTwo = await service.SaveAsync(Input(Header(null, "Stray")));

        Assert.Equal(LabResultService<StoolFecalysisInput, StoolFecalysisDetails, DiagnosticLabs.Domain.Lab.Reports.StoolFecalysisReport>.DuplicateCode, refused.Error.Code);
        Assert.Contains("already has", refused.Error.Message, StringComparison.Ordinal);
        Assert.True(confirmed.IsSuccess);
        Assert.True(strayOne.IsSuccess && strayTwo.IsSuccess);
        Assert.Equal(4, await db.LabReports.CountAsync());
    }

    [Fact]
    public async Task A_result_of_another_type_does_not_count_as_a_duplicate_nor_appear_in_the_list()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        db.LabReports.Add(new LabReport { ReportType = LabReportType.Urinalysis, PatientName = "Other", PatientRegistrationId = registration.Id, DateRequested = _env.Clock.UtcNow });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var saved = await service.SaveAsync(Input(Header(registration.Id)));
        var list = (await service.SearchAsync(new CrudSearch(null))).Value.Items;

        Assert.True(saved.IsSuccess);
        Assert.Equal("Emma Millstein", Assert.Single(list).PatientName);
    }

    [Fact]
    public async Task Validation_covers_the_name_and_the_field_lengths()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var noName = await service.SaveAsync(Input(Header(null, " ")));
        var longRemarks = await service.SaveAsync(Input(Header(null) with { Remarks = new string('x', 501) }));
        var longColor = await service.SaveAsync(Input(Header(null)) with { Color = new string('c', 51) });
        var unknownRegistration = await service.SaveAsync(Input(Header(999)));

        Assert.Contains("name is required", noName.Error.Message, StringComparison.Ordinal);
        Assert.True(longRemarks.IsFailure);
        Assert.True(longColor.IsFailure);
        Assert.Contains("no longer exists", unknownRegistration.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await db.LabReports.ToListAsync());
    }

    [Fact]
    public async Task Editing_changes_the_header_and_the_result_and_keeps_the_time_of_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input(Header(null, "First Name")))).Value;
        var original = (await db.LabReports.AsNoTracking().SingleAsync()).DateRequested;
        _env.Clock.UtcNow += TimeSpan.FromHours(3);

        var edited = (await service.SaveAsync(new StoolFecalysisInput(
            saved.Id, saved.Header with { PatientName = "Second Name", Remarks = "Repeat" }, "YELLOW", "LOOSE", "ABNORMAL", saved.RowVersion))).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal("Second Name", edited.Header.PatientName);
        Assert.Equal("YELLOW", reopened.Color);
        Assert.Equal("ABNORMAL", reopened.Result);
        Assert.Equal("Repeat", reopened.Header.Remarks);
        Assert.Equal(original, (await db.LabReports.AsNoTracking().SingleAsync()).DateRequested);
        Assert.Equal(1, await db.Set<DiagnosticLabs.Domain.Lab.Reports.StoolFecalysisReport>().CountAsync());
    }

    // ---------------------------------------------------------------- deleting and searching

    [Fact]
    public async Task A_deleted_result_is_hidden_with_its_detail_but_kept()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input(Header(null)))).Value;
        db.ChangeTracker.Clear();

        var deleted = await service.DeleteAsync(saved.Id);

        Assert.True(deleted.IsSuccess);
        Assert.Equal("Stool/Fecalysis.NotFound", (await service.GetAsync(saved.Id)).Error.Code);
        Assert.Empty((await service.SearchAsync(new CrudSearch(null))).Value.Items);
        Assert.Equal(1, await db.LabReports.IgnoreQueryFilters().CountAsync(r => r.IsDeleted));
        Assert.Equal(1, await db.Set<DiagnosticLabs.Domain.Lab.Reports.StoolFecalysisReport>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Search_matches_name_patient_code_or_registration_code_and_can_filter_by_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(Input(Header(registration.Id)));
        await service.SaveAsync(Input(Header(null, "Walk Up") with { PatientCode = null, DateRequested = Today.AddDays(-2) }));

        Assert.Single((await service.SearchAsync(new CrudSearch("Millstein"))).Value.Items);
        Assert.Single((await service.SearchAsync(new CrudSearch("BADC-09OC26"))).Value.Items);
        Assert.Single((await service.SearchAsync(new CrudSearch("2026-00000005"))).Value.Items);
        Assert.Equal("Walk Up", Assert.Single((await service.SearchAsync(new LabResultSearch(null, Today.AddDays(-2)))).Value.Items).PatientName);
        Assert.Equal(2, (await service.SearchAsync(new CrudSearch(null))).Value.Items.Count);
    }

    // ---------------------------------------------------------------- printing

    [Fact]
    public async Task The_printout_has_the_letterhead_patient_result_remarks_and_signatories()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        db.CompanySetups.Add(new CompanySetup
        {
            CompanyName = "BIO ASSAY DIAGNOSTIC CENTER", Address = "Angeles City", ContactNumbers = "888-8888", Email = "bio@yahoo.com", Code = "BADC", Logo = [1, 2, 3],
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input(Header(null, "Emma Millstein")))).Value;

        var print = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal("Stool/Fecalysis", print.Title);
        Assert.Equal("BIO ASSAY DIAGNOSTIC CENTER", print.Letterhead.CompanyName);
        Assert.Equal("Angeles City", print.Letterhead.Address);
        Assert.Equal([1, 2, 3], print.Letterhead.Logo);
        Assert.Equal(["Patient Code", "Patient Name", "Company/Physician", "Age", "Sex", "Date Requested"], print.PatientLines.Select(l => l.Label));
        Assert.Equal("Emma Millstein", print.PatientLines[1].Value);
        Assert.Equal(["Color", "Consistency"], print.ResultLines.Select(l => l.Label));
        Assert.Equal("BROWN", print.ResultLines[0].Value);
        Assert.Equal(["Result", "Remarks"], print.ResultTexts.Select(t => t.Label));
        Assert.Equal("NORMAL", print.ResultTexts[0].Text);
        Assert.Equal("NONE", print.ResultTexts[1].Text);
        Assert.Equal(["Medical Technologist", "Pathologist"], print.Signatories.Select(s => s.Role));
        Assert.Equal(["DR. GUTZ", "DR. GALLEN"], print.Signatories.Select(s => s.Name));
        Assert.Contains("COMPUTER GENERATED", print.FooterNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Printing_needs_the_print_permission_and_an_existing_result()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input(Header(null)))).Value;
        Assert.Equal("Stool/Fecalysis.NotFound", (await service.GetPrintableAsync(999)).Error.Code);

        _env.Session.SignIn(new AuthenticatedUser(7, "viewer", "Viewer", false, false, [new ModulePermission(ModuleIds.StoolFecalysis, false, false, false, false)]));

        Assert.Equal("Auth.Forbidden", (await service.GetPrintableAsync(saved.Id)).Error.Code);
        Assert.True((await service.GetAsync(saved.Id)).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(Input(Header(null)))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.DeleteAsync(saved.Id)).Error.Code);
    }

    // ---------------------------------------------------------------- registration lookup

    [Fact]
    public async Task The_registration_lookup_returns_the_patient_details_that_fill_the_form()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var lookup = new LabRegistrationLookup(db, _env.Session, _env.Clock);

        var found = (await lookup.FindAsync(ModuleIds.StoolFecalysis, " BADC-09OC26-00001 ")).Value;

        Assert.Equal(registration.Id, found.RegistrationId);
        Assert.Equal("Emma Millstein", found.PatientName);
        Assert.Equal("2026-00000005", found.PatientCode);
        Assert.Equal("Female", found.Sex);
        Assert.Equal("26 years old", found.Age);
        Assert.Equal("ACME", found.CompanyName);
        Assert.Equal("Stool/Fecalysis", Assert.Single(found.Services));
        Assert.Equal("Registration.NotFound", (await lookup.FindAsync(ModuleIds.StoolFecalysis, "nope")).Error.Code);
    }

    [Fact]
    public async Task The_registration_lookup_suggests_while_typing_and_needs_the_screen_permission()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        await SeedRegistrationAsync(db);
        var lookup = new LabRegistrationLookup(db, _env.Session, _env.Clock);

        Assert.Single((await lookup.SuggestAsync(ModuleIds.StoolFecalysis, "Mill")).Value);
        Assert.Empty((await lookup.SuggestAsync(ModuleIds.StoolFecalysis, "M")).Value);
        Assert.Empty((await lookup.SuggestAsync(ModuleIds.StoolFecalysis, "zzzz")).Value);

        _env.Session.SignIn(new AuthenticatedUser(8, "nobody", "Nobody", false, false, []));
        Assert.Equal("Auth.Forbidden", (await lookup.SuggestAsync(ModuleIds.StoolFecalysis, "Mill")).Error.Code);
        Assert.Equal("Auth.Forbidden", (await lookup.FindAsync(ModuleIds.StoolFecalysis, "x")).Error.Code);
    }
}
