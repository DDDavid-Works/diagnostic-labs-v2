using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Registrations;
using DiagnosticLabs.Domain.Auditing;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Domain.Settings;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Registrations;

public class RegistrationServiceTests
{
    private readonly TestEnvironment _env = new();

    private RegistrationService CreateService(AppDbContext db) => new(db, _env.Session, new CodeGenerator(db, _env.Clock), _env.Clock);

    // Noon UTC, so the local date is the same calendar day in every realistic time zone.
    public RegistrationServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => DateOnly.FromDateTime(_env.Clock.UtcNow.ToLocalTime());

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private sealed record Seed(long Cbc, long Urine, long Xray, long CompanyId, long PackageId);

    private async Task<Seed> SeedAsync(AppDbContext db, bool withCompanyCode = true)
    {
        if (withCompanyCode)
            db.CompanySetups.Add(new CompanySetup { CompanyName = "Bayside", Code = "BADC" });

        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        var urine = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 100 };
        var xray = new Service { ServiceName = "X-ray", ServiceDescription = "d", Price = 400 };
        var company = new Company { CompanyName = "ACME" };
        var package = new Package { PackageName = "Pre-employment", PackageDescription = "d", Price = 300 };
        db.Services.AddRange(cbc, urine, xray);
        db.Companies.Add(company);
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return new Seed(cbc.Id, urine.Id, xray.Id, company.Id, package.Id);
    }

    private RegistrationInput NewInput(Seed s, string name = "Jane Roe", long patientId = 0, params long[] serviceIds) => new RegistrationInput(
        0, Today, null, null, null, patientId, name, new DateOnly(1990, 5, 17), null, "Female", "Single", "1 Test Street", "0917", null,
        0m, [.. (serviceIds.Length == 0 ? [s.Cbc, s.Urine] : serviceIds).Select(id => new RegistrationServiceLine(0, id, id == s.Cbc ? 250 : id == s.Urine ? 100 : 400))], null)
        with { AmountDue = 350m };

    // ---------------------------------------------------------------- creating

    [Fact]
    public async Task One_save_creates_the_patient_the_registration_and_its_services_with_new_codes()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed));

        Assert.True(result.IsSuccess);
        var saved = result.Value;
        Assert.Equal($"BADC-{CodeGenerator.DayPrefix(Today)}-00001", saved.RegistrationCode);
        Assert.Equal($"{_env.Clock.UtcNow.ToLocalTime().Year}-00000001", saved.PatientCode);
        Assert.Equal("Jane Roe", saved.PatientName);
        Assert.Equal(Today, saved.Date);
        Assert.Equal(350m, saved.AmountDue);
        Assert.Equal(["CBC", "Urinalysis"], saved.Services.Select(x => x.ServiceName));
        Assert.Equal(1, await db.PatientRegistrations.CountAsync());
        Assert.Equal(1, await db.Patients.CountAsync());
        Assert.Equal(2, await db.PatientRegistrationServices.CountAsync());
    }

    [Fact]
    public async Task The_patient_and_the_registration_are_both_recorded_in_the_audit_log()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        await CreateService(db).SaveAsync(NewInput(seed));

        var logged = (await db.AuditLogs.Where(a => a.Action == AuditAction.Insert).Select(a => a.EntityName).ToListAsync()).Distinct().ToList();
        Assert.Contains(nameof(Patient), logged);
        Assert.Contains(nameof(PatientRegistration), logged);
        Assert.Contains(nameof(PatientRegistrationService), logged);
    }

    [Fact]
    public async Task Registration_codes_count_up_within_a_day_and_restart_the_next_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var prefix = CodeGenerator.DayPrefix(Today);

        var first = (await service.SaveAsync(NewInput(seed, "A"))).Value;
        var second = (await service.SaveAsync(NewInput(seed, "B"))).Value;
        _env.Clock.UtcNow += TimeSpan.FromDays(1);
        var nextDay = (await service.SaveAsync(NewInput(seed, "C") with { Date = Today })).Value;

        Assert.Equal($"BADC-{prefix}-00001", first.RegistrationCode);
        Assert.Equal($"BADC-{prefix}-00002", second.RegistrationCode);
        Assert.Equal($"BADC-{CodeGenerator.DayPrefix(Today)}-00001", nextDay.RegistrationCode);
        Assert.NotEqual(prefix, CodeGenerator.DayPrefix(Today));
    }

    [Fact]
    public async Task A_counter_carried_over_from_the_old_system_is_continued()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        db.CodeSequences.Add(new CodeSequence { Prefix = CodeGenerator.DayPrefix(Today), LastNumber = 5 });
        await db.SaveChangesAsync();

        var saved = (await CreateService(db).SaveAsync(NewInput(seed))).Value;

        Assert.EndsWith("-00006", saved.RegistrationCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_registration_codes_seed_the_counter_when_there_is_no_counter_row()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var patient = new Patient { PatientCode = "2026-00000099", PatientName = "Old" };
        db.Patients.Add(patient);
        db.PatientRegistrations.Add(new PatientRegistration
        {
            RegistrationCode = $"BADC-{CodeGenerator.DayPrefix(Today)}-00012",
            Patient = patient,
            InputDate = _env.Clock.UtcNow,
        });
        await db.SaveChangesAsync();

        var saved = (await CreateService(db).SaveAsync(NewInput(seed))).Value;

        Assert.EndsWith("-00013", saved.RegistrationCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_company_code_a_new_registration_is_refused_with_guidance()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db, withCompanyCode: false);

        var result = await CreateService(db).SaveAsync(NewInput(seed));

        Assert.True(result.IsFailure);
        Assert.Contains("company code", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await db.PatientRegistrations.ToListAsync());
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    public async Task The_preview_shows_the_next_codes_without_using_them_up()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var prefix = CodeGenerator.DayPrefix(Today);

        var first = (await service.PreviewAsync(Today)).Value;
        var again = (await service.PreviewAsync(Today)).Value;
        await service.SaveAsync(NewInput(seed));
        var after = (await service.PreviewAsync(Today)).Value;

        Assert.Equal($"BADC-{prefix}-00001", first.RegistrationCode);
        Assert.Equal(first, again);
        Assert.Equal($"BADC-{prefix}-00002", after.RegistrationCode);
        Assert.EndsWith("00000002", after.PatientCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_preview_has_no_registration_code_until_the_company_code_exists()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var preview = (await CreateService(db).PreviewAsync(Today)).Value;

        Assert.Null(preview.RegistrationCode);
        Assert.NotEmpty(preview.PatientCode);
    }

    // ---------------------------------------------------------------- the patient part

    [Fact]
    public async Task An_existing_patient_is_reused_and_updated_from_the_form()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var first = (await service.SaveAsync(NewInput(seed))).Value;

        var second = (await service.SaveAsync(NewInput(seed, "Jane R. Roe", first.PatientId) with { Address = "New Address" })).Value;

        Assert.Equal(first.PatientId, second.PatientId);
        Assert.Equal(1, await db.Patients.CountAsync());
        Assert.Equal(2, await db.PatientRegistrations.CountAsync());
        var patient = await db.Patients.SingleAsync();
        Assert.Equal("Jane R. Roe", patient.PatientName);
        Assert.Equal("New Address", patient.Address);
    }

    [Fact]
    public async Task A_typed_age_is_kept_only_when_the_birth_date_is_unknown()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);

        var typed = (await service.SaveAsync(NewInput(seed) with { DateOfBirth = null, Age = " 35 " })).Value;
        var dated = (await service.SaveAsync(NewInput(seed, "Other") with { Age = "99" })).Value;

        Assert.Equal("35", typed.Age);
        Assert.Null(typed.DateOfBirth);
        Assert.Null(dated.Age);
    }

    [Fact]
    public async Task A_registration_can_not_be_moved_to_a_different_patient()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var one = (await service.SaveAsync(NewInput(seed, "One"))).Value;
        var two = (await service.SaveAsync(NewInput(seed, "Two"))).Value;

        var result = await service.SaveAsync(NewInput(seed, "Two", two.PatientId) with { Id = one.Id, RowVersion = one.RowVersion });

        Assert.True(result.IsFailure);
        Assert.Contains("different patient", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Suggestions_match_name_or_code_need_two_letters_and_are_capped()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        for (var i = 0; i < 12; i++)
            await service.SaveAsync(NewInput(seed, $"Maria {i:D2}"));
        await service.SaveAsync(NewInput(seed, "Jose Rizal") with { DateOfBirth = null, Age = "40" });

        var maria = (await service.SuggestPatientsAsync("Mar")).Value;
        var narrowed = (await service.SuggestPatientsAsync("Maria 07")).Value;
        var jose = (await service.SuggestPatientsAsync("Rizal")).Value;

        Assert.Equal(RegistrationService.MaxSuggestions, maria.Count);
        Assert.Contains(narrowed, n => n.PatientName == "Maria 07");
        Assert.Equal("40", Assert.Single(jose).Age);
        Assert.Empty((await service.SuggestPatientsAsync("m")).Value);
        Assert.Empty((await service.SuggestPatientsAsync("  ")).Value);
        Assert.Empty((await service.SuggestPatientsAsync("zzzz")).Value);
    }

    [Fact]
    public async Task A_suggested_patient_can_be_loaded()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var saved = (await service.SaveAsync(NewInput(seed))).Value;

        var patient = (await service.GetPatientAsync(saved.PatientId)).Value;

        Assert.Equal("Jane Roe", patient.PatientName);
        Assert.Equal("1 Test Street", patient.Address);
        Assert.Equal("Patient.NotFound", (await service.GetPatientAsync(999)).Error.Code);
    }

    // ---------------------------------------------------------------- validation

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task The_patient_name_is_required(string? name)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed, name!));

        Assert.True(result.IsFailure);
        Assert.Contains("Name", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task At_least_one_service_is_required()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed) with { Services = [] });

        Assert.True(result.IsFailure);
        Assert.Contains("at least one service", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await db.Patients.ToListAsync()); // nothing half-saved
    }

    [Fact]
    public async Task Services_prices_and_references_are_checked()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);

        var duplicate = await service.SaveAsync(NewInput(seed) with { Services = [new(0, seed.Cbc, 1), new(0, seed.Cbc, 1)] });
        var negativeLine = await service.SaveAsync(NewInput(seed) with { Services = [new(0, seed.Cbc, -1)] });
        var negativePrice = await service.SaveAsync(NewInput(seed) with { AmountDue = -5 });
        var unknownService = await service.SaveAsync(NewInput(seed) with { Services = [new(0, 999, 1)] });
        var unknownCompany = await service.SaveAsync(NewInput(seed) with { CompanyId = 999 });
        var unknownPackage = await service.SaveAsync(NewInput(seed) with { PackageId = 999 });
        var unknownPatient = await service.SaveAsync(NewInput(seed, patientId: 999));
        var futureBirth = await service.SaveAsync(NewInput(seed) with { DateOfBirth = Today.AddDays(2) });

        Assert.All([duplicate, negativeLine, negativePrice, unknownService, unknownCompany, unknownPackage, unknownPatient, futureBirth], r => Assert.True(r.IsFailure));
        Assert.Empty(await db.PatientRegistrations.ToListAsync());
    }

    [Fact]
    public async Task Company_package_and_batch_are_saved()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var saved = (await CreateService(db).SaveAsync(NewInput(seed) with { CompanyId = seed.CompanyId, PackageId = seed.PackageId, BatchName = " Batch A " })).Value;

        Assert.Equal(seed.CompanyId, saved.CompanyId);
        Assert.Equal(seed.PackageId, saved.PackageId);
        Assert.Equal("Batch A", saved.BatchName);
    }

    // ---------------------------------------------------------------- updating

    [Fact]
    public async Task Editing_keeps_a_typed_service_price_and_the_time_of_day_when_the_date_is_unchanged()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed))).Value;
        var originalInstant = (await db.PatientRegistrations.AsNoTracking().SingleAsync()).InputDate;
        _env.Clock.UtcNow += TimeSpan.FromHours(3);

        var edited = (await service.SaveAsync(NewInput(seed) with
        {
            Id = created.Id,
            PatientId = created.PatientId,
            RowVersion = created.RowVersion,
            Services = [created.Services[0] with { Price = 200 }, new RegistrationServiceLine(0, seed.Xray, 400)],
            AmountDue = 600,
        })).Value;

        Assert.Equal(200m, edited.Services.Single(s => s.ServiceId == seed.Cbc).Price);
        Assert.Equal(["CBC", "X-ray"], edited.Services.Select(s => s.ServiceName));
        Assert.Equal(created.Services[0].Id, edited.Services.Single(s => s.ServiceId == seed.Cbc).Id);
        Assert.Equal(originalInstant, (await db.PatientRegistrations.AsNoTracking().SingleAsync()).InputDate);
    }

    [Fact]
    public async Task A_removed_service_line_is_soft_deleted()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed))).Value;

        var edited = (await service.SaveAsync(NewInput(seed) with
        {
            Id = created.Id, PatientId = created.PatientId, RowVersion = created.RowVersion, Services = [created.Services[0]],
        })).Value;

        Assert.Single(edited.Services);
        Assert.Equal(2, await db.PatientRegistrationServices.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.PatientRegistrationServices.IgnoreQueryFilters().CountAsync(s => s.IsDeleted));
    }

    [Fact]
    public async Task Changing_the_date_moves_the_registration_to_that_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed))).Value;

        var moved = (await service.SaveAsync(NewInput(seed) with
        {
            Id = created.Id, PatientId = created.PatientId, RowVersion = created.RowVersion, Date = Today.AddDays(-3), Services = created.Services,
        })).Value;

        Assert.Equal(Today.AddDays(-3), moved.Date);
        Assert.Equal(created.RegistrationCode, moved.RegistrationCode); // the code never changes
    }

    // ---------------------------------------------------------------- deleting

    [Fact]
    public async Task Deleting_a_registration_hides_it_but_keeps_the_row()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed))).Value;

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Registration.NotFound", (await service.GetAsync(created.Id)).Error.Code);
        Assert.Empty((await service.SearchAsync(new CrudSearch(null))).Value.Items);
        Assert.Equal(1, await db.PatientRegistrations.IgnoreQueryFilters().CountAsync(r => r.IsDeleted));
    }

    [Fact]
    public async Task A_registration_with_payments_can_not_be_deleted()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed))).Value;
        db.Payments.Add(new Payment { PatientRegistrationId = created.Id, PaymentAmount = 100, PaymentDate = _env.Clock.UtcNow });
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(created.Id);

        Assert.Equal("Registration.InUse", result.Error.Code);
        Assert.True((await service.GetAsync(created.Id)).IsSuccess);
    }

    [Fact]
    public async Task A_registration_with_lab_results_can_not_be_deleted_but_a_deleted_payment_does_not_count()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed))).Value;
        var payment = new Payment { PatientRegistrationId = created.Id, PaymentAmount = 100, PaymentDate = _env.Clock.UtcNow };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        db.Payments.Remove(payment); // soft delete
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.True((await service.DeleteAsync(created.Id)).IsSuccess);

        var again = (await service.SaveAsync(NewInput(seed, "Second"))).Value;
        db.LabReports.Add(new LabReport
        {
            ReportType = LabReportType.Hematology, PatientId = again.PatientId, PatientRegistrationId = again.Id,
            PatientCode = again.PatientCode, PatientName = again.PatientName, DateRequested = _env.Clock.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Equal("Registration.InUse", (await service.DeleteAsync(again.Id)).Error.Code);
    }

    // ---------------------------------------------------------------- searching

    [Fact]
    public async Task Search_lists_the_newest_first_and_matches_code_patient_name_or_patient_code()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var a = (await service.SaveAsync(NewInput(seed, "Alice Tan") with { Date = Today.AddDays(-2) })).Value;
        var b = (await service.SaveAsync(NewInput(seed, "Bob Cruz"))).Value;

        var all = (await service.SearchAsync(new CrudSearch(null))).Value.Items;
        Assert.Equal([b.Id, a.Id], all.Select(i => i.Id));

        Assert.Single((await service.SearchAsync(new CrudSearch("Alice"))).Value.Items);
        Assert.Single((await service.SearchAsync(new CrudSearch(a.RegistrationCode))).Value.Items);
        Assert.Single((await service.SearchAsync(new CrudSearch(b.PatientCode))).Value.Items);
        Assert.Empty((await service.SearchAsync(new CrudSearch("nobody"))).Value.Items);
        Assert.Equal("Alice Tan", all[1].PatientName);
    }

    [Fact]
    public async Task Search_can_be_narrowed_by_company_and_by_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(NewInput(seed, "Walk In One"));
        await service.SaveAsync(NewInput(seed, "Acme Worker") with { CompanyId = seed.CompanyId });
        await service.SaveAsync(NewInput(seed, "Last Week") with { Date = Today.AddDays(-7) });

        var acme = (await service.SearchAsync(new RegistrationSearch(null, CompanyId: seed.CompanyId))).Value.Items;
        var today = (await service.SearchAsync(new RegistrationSearch(null, Date: Today))).Value.Items;
        var lastWeek = (await service.SearchAsync(new RegistrationSearch(null, Date: Today.AddDays(-7)))).Value.Items;
        var both = (await service.SearchAsync(new RegistrationSearch("Worker", seed.CompanyId, Today))).Value.Items;

        Assert.Equal("Acme Worker", Assert.Single(acme).PatientName);
        Assert.Equal("ACME", acme[0].CompanyName);
        Assert.Equal(2, today.Count);
        Assert.Equal("Last Week", Assert.Single(lastWeek).PatientName);
        Assert.Single(both);
    }

    [Fact]
    public async Task Search_pages_its_results()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        for (var i = 0; i < 5; i++)
            await service.SaveAsync(NewInput(seed, $"Patient {i}"));

        var page = (await service.SearchAsync(new CrudSearch(null, 2, 2))).Value;

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
    }

    // ---------------------------------------------------------------- permissions

    [Fact]
    public async Task Each_action_needs_its_own_permission_on_the_registration_screen()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed))).Value;
        _env.Session.SignIn(new AuthenticatedUser(
            7, "viewer", "Viewer", false, false, [new ModulePermission(ModuleIds.PatientRegistrations, false, false, false, false)]));

        Assert.True((await service.GetAsync(created.Id)).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(NewInput(seed, "X"))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(NewInput(seed) with { Id = created.Id, PatientId = created.PatientId })).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.DeleteAsync(created.Id)).Error.Code);
        _env.Session.SignIn(new AuthenticatedUser(8, "nobody", "Nobody", false, false, []));
        Assert.Equal("Auth.Forbidden", (await service.SearchAsync(new CrudSearch(null))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.PreviewAsync(Today)).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.SuggestPatientsAsync("ja")).Error.Code);
    }
}
