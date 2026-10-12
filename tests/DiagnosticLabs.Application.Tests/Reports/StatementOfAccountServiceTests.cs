using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Reports;

public class StatementOfAccountServiceTests
{
    private readonly TestEnvironment _env = new();

    private static readonly DateOnly Day = new(2026, 10, 10);

    public StatementOfAccountServiceTests() => _env.Clock.UtcNow = At(new DateOnly(2026, 10, 16), 9);

    private StatementOfAccountService CreateService(AppDbContext db) => new(db, _env.Session, new CodeGenerator(db, _env.Clock), _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private void SignInWith(params ModulePermission[] permissions) => _env.Session.SignIn(new AuthenticatedUser(7, "clerk", "Clerk", false, false, permissions));

    private static ModulePermission Statement(bool create, bool delete, bool print) => new(ModuleIds.StatementOfAccounts, create, false, delete, print);

    private static DateTime At(DateOnly day, int hour) => LocalTime.ToUtc(day, new TimeSpan(hour, 0, 0));

    private static async Task<Company> AddCompanyAsync(AppDbContext db, string name)
    {
        var company = new Company { CompanyName = name };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static async Task<PatientRegistration> RegisterAsync(
        AppDbContext db,
        string name,
        DateTime when,
        decimal amountDue,
        long? companyId = null,
        string batch = "",
        decimal discount = 0m,
        bool charged = false,
        decimal paid = 0m,
        Patient? patient = null)
    {
        var registration = new PatientRegistration
        {
            RegistrationCode = "R-" + Guid.NewGuid().ToString("N")[..8],
            Patient = patient ?? new Patient { PatientCode = "P-" + Guid.NewGuid().ToString("N")[..8], PatientName = name },
            InputDate = when,
            CompanyId = companyId,
            BatchName = batch,
            AmountDue = amountDue,
            DiscountTotal = discount,
        };
        if (charged)
            registration.Payments.Add(new Payment { PaymentAmount = 0m, Type = PaymentType.Charge, PaymentDate = when });
        if (paid > 0)
            registration.Payments.Add(new Payment { PaymentAmount = paid, Type = PaymentType.Payment, PaymentDate = when });

        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    private static StatementRequest ForCompany(long companyId, string? batch = null, DateOnly? from = null, DateOnly? to = null, string? attention = null) =>
        new(StatementMode.Company, CompanyId: companyId, Batch: batch, From: from, To: to, Attention: attention);

    private static StatementRequest ForPatient(long patientId, DateOnly? from = null, DateOnly? to = null) =>
        new(StatementMode.Patient, PatientId: patientId, From: from, To: to);

    // ---------------------------------------------------------------- company mode

    [Fact]
    public async Task A_company_statement_lists_only_what_was_charged_to_that_company_sorted_by_patient_name()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        var other = await AddCompanyAsync(db, "Other");
        await RegisterAsync(db, "Zed", At(Day, 9), 400m, acme.Id, "B1", charged: true);
        await RegisterAsync(db, "Amy", At(Day, 10), 400m, acme.Id, "B1", charged: true);
        await RegisterAsync(db, "Paid directly", At(Day, 11), 400m, acme.Id, "B1", paid: 400m); // the employee paid, not the company
        await RegisterAsync(db, "Not settled", At(Day, 12), 400m, acme.Id, "B1");
        await RegisterAsync(db, "Someone else's", At(Day, 13), 400m, other.Id, "B1", charged: true);

        var statement = (await CreateService(db).PreviewAsync(ForCompany(acme.Id))).Value;

        Assert.Equal(["Amy", "Zed"], statement.Lines.Select(l => l.PatientName));
        Assert.Equal("Acme", statement.CompanyName);
    }

    [Fact]
    public async Task A_company_statement_can_cover_one_batch_or_the_whole_company()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "In batch one", At(Day, 9), 400m, acme.Id, "B1", charged: true);
        await RegisterAsync(db, "In batch two", At(Day, 10), 350m, acme.Id, "B2", charged: true);
        await RegisterAsync(db, "No batch", At(Day, 11), 300m, acme.Id, "", charged: true);

        var one = (await CreateService(db).PreviewAsync(ForCompany(acme.Id, "B1"))).Value;
        var whole = (await CreateService(db).PreviewAsync(ForCompany(acme.Id))).Value;

        Assert.Equal(["In batch one"], one.Lines.Select(l => l.PatientName));
        Assert.Equal("B1", one.Batch);
        Assert.Equal(3, whole.Lines.Count);
        Assert.Null(whole.Batch);
    }

    [Fact]
    public async Task The_columns_are_gross_discount_and_net_and_a_company_statement_shows_no_payments()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, "B", discount: 15m, charged: true);
        await RegisterAsync(db, "Ben", At(Day, 10), 350m, acme.Id, "B", charged: true);
        await RegisterAsync(db, "Cy", At(Day, 11), 320m, acme.Id, "B", discount: 60m, charged: true);

        var statement = (await CreateService(db).PreviewAsync(ForCompany(acme.Id))).Value;

        Assert.Equal([400m, 350m, 320m], statement.Lines.Select(l => l.Gross));
        Assert.Equal([15m, 0m, 60m], statement.Lines.Select(l => l.Discount));
        Assert.Equal([385m, 350m, 260m], statement.Lines.Select(l => l.Net));
        Assert.Equal((1070m, 75m, 995m), (statement.GrossTotal, statement.DiscountTotal, statement.NetTotal));
        Assert.All(statement.Lines, l => Assert.Equal((0m, 0m), (l.Paid, l.Balance)));
    }

    [Fact]
    public async Task The_same_patient_can_appear_twice_one_line_per_registration()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Emma", At(Day, 9), 400m, acme.Id, "B", charged: true);
        await RegisterAsync(db, "Emma", At(Day, 10), 400m, acme.Id, "B", discount: 15m, charged: true);

        var statement = (await CreateService(db).PreviewAsync(ForCompany(acme.Id))).Value;

        Assert.Equal([400m, 385m], statement.Lines.Select(l => l.Net));
    }

    [Fact]
    public async Task The_attention_is_kept_with_the_statement_and_is_only_for_company_statements()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, "B", charged: true);

        var statement = (await CreateService(db).GenerateAsync(ForCompany(acme.Id, attention: "  MR. FRANZ HILL "))).Value;

        Assert.Equal("MR. FRANZ HILL", statement.Attention);
        Assert.Equal("MR. FRANZ HILL", (await CreateService(db).GetAsync(statement.Id)).Value.Attention);
    }

    // ---------------------------------------------------------------- patient mode

    [Fact]
    public async Task A_patient_statement_shows_every_registration_with_what_was_paid_and_what_is_left()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        var emma = new Patient { PatientCode = "P-EMMA", PatientName = "EMMA MILLSTEIN" };
        await RegisterAsync(db, "x", At(Day.AddDays(2), 9), 400m, patient: emma, paid: 250m);
        await RegisterAsync(db, "x", At(Day, 9), 400m, patient: emma);
        await RegisterAsync(db, "x", At(Day.AddDays(1), 9), 400m, patient: emma, discount: 15m, paid: 385m);
        await RegisterAsync(db, "x", At(Day.AddDays(3), 9), 400m, acme.Id, "B", patient: emma, charged: true);
        await RegisterAsync(db, "Somebody else", At(Day, 9), 999m);

        var statement = (await CreateService(db).PreviewAsync(ForPatient(emma.Id))).Value;

        // By date; the charged one has nothing left for the patient to settle.
        Assert.Equal([400m, 400m, 400m, 400m], statement.Lines.Select(l => l.Gross));
        Assert.Equal([Day, Day.AddDays(1), Day.AddDays(2), Day.AddDays(3)], statement.Lines.Select(l => l.Date));
        Assert.Equal([0m, 385m, 250m, 0m], statement.Lines.Select(l => l.Paid));
        Assert.Equal([400m, 0m, 150m, 0m], statement.Lines.Select(l => l.Balance));
        Assert.Equal((1600m, 15m, 1585m, 635m, 550m), (statement.GrossTotal, statement.DiscountTotal, statement.NetTotal, statement.PaidTotal, statement.BalanceTotal));
        Assert.Equal("EMMA MILLSTEIN", statement.PatientName);
        Assert.Equal("P-EMMA", statement.PatientCode);
    }

    [Fact]
    public async Task An_overpayment_does_not_make_a_negative_balance()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var patient = new Patient { PatientCode = "P-1", PatientName = "Ana" };
        await RegisterAsync(db, "x", At(Day, 9), 100m, patient: patient, paid: 120m);

        var statement = (await CreateService(db).PreviewAsync(ForPatient(patient.Id))).Value;

        Assert.Equal(0m, statement.Lines[0].Balance);
    }

    // ---------------------------------------------------------------- the date range

    [Fact]
    public async Task The_date_range_is_optional_inclusive_and_may_be_open_at_one_end()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Before", At(Day.AddDays(-1), 23), 100m, acme.Id, charged: true);
        await RegisterAsync(db, "First morning", At(Day, 0), 100m, acme.Id, charged: true);
        await RegisterAsync(db, "Last evening", At(Day.AddDays(1), 23), 100m, acme.Id, charged: true);
        await RegisterAsync(db, "After", At(Day.AddDays(2), 1), 100m, acme.Id, charged: true);
        var service = CreateService(db);

        var both = (await service.PreviewAsync(ForCompany(acme.Id, from: Day, to: Day.AddDays(1)))).Value;
        var none = (await service.PreviewAsync(ForCompany(acme.Id))).Value;
        var fromOnly = (await service.PreviewAsync(ForCompany(acme.Id, from: Day.AddDays(1)))).Value;
        var toOnly = (await service.PreviewAsync(ForCompany(acme.Id, to: Day))).Value;

        Assert.Equal(["First morning", "Last evening"], both.Lines.Select(l => l.PatientName));
        Assert.Equal(4, none.Lines.Count);
        Assert.Equal(["After", "Last evening"], fromOnly.Lines.Select(l => l.PatientName));
        Assert.Equal(["Before", "First morning"], toOnly.Lines.Select(l => l.PatientName));
    }

    [Fact]
    public async Task A_range_that_ends_before_it_starts_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");

        var result = await CreateService(db).PreviewAsync(ForCompany(acme.Id, from: Day.AddDays(1), to: Day));

        Assert.True(result.IsFailure);
        Assert.Contains("before", result.Error.Message);
    }

    // ---------------------------------------------------------------- validation

    [Fact]
    public async Task A_statement_needs_its_company_or_patient_and_something_to_put_on_it()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Not charged", At(Day, 9), 100m, acme.Id);
        var service = CreateService(db);

        Assert.True((await service.GenerateAsync(new StatementRequest(StatementMode.Company))).IsFailure);
        Assert.True((await service.GenerateAsync(new StatementRequest(StatementMode.Patient))).IsFailure);
        Assert.True((await service.GenerateAsync(ForCompany(acme.Id))).IsFailure); // nothing was charged to Acme
        Assert.True((await service.GenerateAsync(ForPatient(99999))).IsFailure);
        Assert.Empty(await db.StatementsOfAccount.ToListAsync());
    }

    // ---------------------------------------------------------------- saving and reprinting

    [Fact]
    public async Task A_generated_statement_gets_a_number_that_counts_up_through_the_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, charged: true);
        var service = CreateService(db);

        var first = (await service.GenerateAsync(ForCompany(acme.Id))).Value;
        var second = (await service.GenerateAsync(ForCompany(acme.Id))).Value;
        _env.Clock.UtcNow = At(new DateOnly(2026, 10, 17), 9);
        var nextDay = (await service.GenerateAsync(ForCompany(acme.Id))).Value;

        Assert.Equal("SOA-20261016-0001", first.Number);
        Assert.Equal("SOA-20261016-0002", second.Number);
        Assert.Equal("SOA-20261017-0001", nextDay.Number);
        Assert.True(first.Id > 0);
    }

    [Fact]
    public async Task A_reprint_shows_the_statement_as_it_was_whatever_changed_afterwards()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var patient = new Patient { PatientCode = "P-1", PatientName = "Emma" };
        var registration = await RegisterAsync(db, "x", At(Day, 9), 400m, patient: patient);
        var service = CreateService(db);
        var saved = (await service.GenerateAsync(ForPatient(patient.Id))).Value;

        // Afterwards: a payment, a discount, a renamed patient, and another registration.
        db.Payments.Add(new Payment { PatientRegistrationId = registration.Id, PaymentAmount = 400m, Type = PaymentType.Payment, PaymentDate = At(Day, 12) });
        registration.DiscountTotal = 50m;
        patient.PatientName = "Emma Renamed";
        await db.SaveChangesAsync();
        await RegisterAsync(db, "x", At(Day, 14), 100m, patient: patient);

        var reprint = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(saved.Number, reprint.Number);
        Assert.Equal("Emma", reprint.PatientName);
        Assert.Single(reprint.Lines);
        Assert.Equal((400m, 0m, 400m, 0m, 400m), (reprint.GrossTotal, reprint.DiscountTotal, reprint.NetTotal, reprint.PaidTotal, reprint.BalanceTotal));
        Assert.Equal(saved.Lines[0].RegistrationCode, reprint.Lines[0].RegistrationCode);
        Assert.Equal(saved.GeneratedAtUtc, reprint.GeneratedAtUtc);
    }

    [Fact]
    public async Task The_saved_statement_keeps_its_totals_for_the_list()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, "B", discount: 15m, charged: true);
        await RegisterAsync(db, "Ben", At(Day, 10), 350m, acme.Id, "B", charged: true);
        var service = CreateService(db);
        await service.GenerateAsync(ForCompany(acme.Id, "B", Day, Day));

        var item = Assert.Single(await service.SearchAsync(new StatementSearch()));

        Assert.Equal(("Acme", "B", Day, Day), (item.Name, item.Batch, item.From, item.To));
        Assert.Equal((735m, 2), (item.Net, item.LineCount));
        Assert.Equal(new DateOnly(2026, 10, 16), item.GeneratedOn);
        Assert.False(item.IsVoid);
    }

    // ---------------------------------------------------------------- void and regenerate

    [Fact]
    public async Task A_voided_statement_stays_in_the_list_and_prints_as_void()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, charged: true);
        var service = CreateService(db);
        var saved = (await service.GenerateAsync(ForCompany(acme.Id))).Value;

        Assert.True((await service.VoidAsync(saved.Id)).IsSuccess);

        Assert.True((await service.GetAsync(saved.Id)).Value.IsVoid);
        Assert.True(Assert.Single(await service.SearchAsync(new StatementSearch())).IsVoid);
        Assert.Empty(await service.SearchAsync(new StatementSearch(IncludeVoid: false)));
        Assert.True((await service.VoidAsync(saved.Id)).IsFailure); // already void
        var row = await db.StatementsOfAccount.SingleAsync();
        Assert.NotNull(row.VoidedAtUtc);
        Assert.Equal(1, row.VoidedByUserId);
    }

    [Fact]
    public async Task Regenerate_makes_a_new_statement_from_the_registrations_as_they_are_now_and_voids_the_old_one()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        var ana = await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, "B", charged: true);
        var service = CreateService(db);
        var old = (await service.GenerateAsync(ForCompany(acme.Id, "B", attention: "MR. HILL"))).Value;

        ana.DiscountTotal = 40m; // corrected after the statement went out
        await RegisterAsync(db, "Ben", At(Day, 10), 350m, acme.Id, "B", charged: true);
        await db.SaveChangesAsync();

        var fresh = (await service.RegenerateAsync(old.Id)).Value;

        Assert.NotEqual(old.Id, fresh.Id);
        Assert.Equal("SOA-20261016-0002", fresh.Number);
        Assert.Equal(["Ana", "Ben"], fresh.Lines.Select(l => l.PatientName));
        Assert.Equal((750m, 40m, 710m), (fresh.GrossTotal, fresh.DiscountTotal, fresh.NetTotal));
        Assert.Equal(("B", "MR. HILL"), (fresh.Batch, fresh.Attention)); // the same parameters

        var rows = await db.StatementsOfAccount.OrderBy(s => s.Id).ToListAsync();
        Assert.True(rows[0].IsVoid);
        Assert.False(rows[1].IsVoid);
        Assert.Equal(old.Id, rows[1].RegeneratedFromId);
        Assert.Single((await service.GetAsync(old.Id)).Value.Lines); // the old one still prints as it was
    }

    [Fact]
    public async Task Regenerate_leaves_the_old_statement_alone_when_nothing_is_left_to_put_on_it()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        var ana = await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, charged: true);
        var service = CreateService(db);
        var old = (await service.GenerateAsync(ForCompany(acme.Id))).Value;
        ana.IsDeleted = true;
        await db.SaveChangesAsync();

        var result = await service.RegenerateAsync(old.Id);

        Assert.True(result.IsFailure);
        Assert.False((await service.GetAsync(old.Id)).Value.IsVoid);
        Assert.Single(await db.StatementsOfAccount.ToListAsync());
    }

    [Fact]
    public async Task A_voided_statement_can_be_regenerated_without_voiding_anything_again()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, charged: true);
        var service = CreateService(db);
        var old = (await service.GenerateAsync(ForCompany(acme.Id))).Value;
        await service.VoidAsync(old.Id);
        var voidedAt = (await db.StatementsOfAccount.AsNoTracking().SingleAsync()).VoidedAtUtc;
        _env.Clock.UtcNow += TimeSpan.FromHours(2);

        var fresh = (await service.RegenerateAsync(old.Id)).Value;

        Assert.False(fresh.IsVoid);
        Assert.Equal(voidedAt, (await db.StatementsOfAccount.AsNoTracking().OrderBy(s => s.Id).FirstAsync()).VoidedAtUtc);
    }

    // ---------------------------------------------------------------- finding the saved ones

    [Fact]
    public async Task Saved_statements_can_be_found_by_number_company_patient_type_and_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme Foods");
        var emma = new Patient { PatientCode = "P-EMMA", PatientName = "Emma Millstein" };
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, "Batch Red", charged: true);
        await RegisterAsync(db, "x", At(Day, 9), 400m, patient: emma);
        var service = CreateService(db);
        var company = (await service.GenerateAsync(ForCompany(acme.Id, "Batch Red"))).Value;
        _env.Clock.UtcNow = At(new DateOnly(2026, 10, 18), 9);
        var patient = (await service.GenerateAsync(ForPatient(emma.Id))).Value;

        Assert.Equal([patient.Number], (await service.SearchAsync(new StatementSearch("Millstein"))).Select(i => i.Number));
        Assert.Equal([company.Number], (await service.SearchAsync(new StatementSearch("Acme Red"))).Select(i => i.Number));
        Assert.Equal([company.Number], (await service.SearchAsync(new StatementSearch("SOA-20261016"))).Select(i => i.Number));
        Assert.Equal([patient.Number], (await service.SearchAsync(new StatementSearch(Mode: StatementMode.Patient))).Select(i => i.Number));
        Assert.Equal([patient.Number, company.Number], (await service.SearchAsync(new StatementSearch())).Select(i => i.Number)); // newest first
        Assert.Equal([company.Number], (await service.SearchAsync(new StatementSearch(GeneratedFrom: new DateOnly(2026, 10, 16), GeneratedTo: new DateOnly(2026, 10, 17)))).Select(i => i.Number));
    }

    [Fact]
    public async Task Patients_are_suggested_by_name_or_code_once_two_letters_are_typed()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        db.Patients.AddRange(new Patient { PatientCode = "2026-00000001", PatientName = "Emma Millstein" }, new Patient { PatientCode = "2026-00000002", PatientName = "Ana Garcia" });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        Assert.Equal(["Emma Millstein"], (await service.SuggestPatientsAsync("Mill")).Select(p => p.PatientName));
        Assert.Equal(["Ana Garcia"], (await service.SuggestPatientsAsync("00000002")).Select(p => p.PatientName));
        Assert.Empty(await service.SuggestPatientsAsync("e"));
    }

    [Fact]
    public async Task The_batches_offered_are_the_ones_with_registrations_charged_to_the_company()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 100m, acme.Id, "Charged batch", charged: true);
        await RegisterAsync(db, "Ben", At(Day, 9), 100m, acme.Id, "Paid batch", paid: 100m);

        Assert.Equal(["Charged batch"], await CreateService(db).GetChargedBatchesAsync(acme.Id));
    }

    // ---------------------------------------------------------------- permissions

    [Fact]
    public async Task Each_action_needs_its_own_permission()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, charged: true);
        var service = CreateService(db);
        var saved = (await service.GenerateAsync(ForCompany(acme.Id))).Value;

        SignInWith(Statement(create: false, delete: false, print: false)); // may only open the screen
        Assert.True((await service.PreviewAsync(ForCompany(acme.Id))).IsFailure);
        Assert.True((await service.GenerateAsync(ForCompany(acme.Id))).IsFailure);
        Assert.True((await service.GetAsync(saved.Id)).IsFailure);
        Assert.True((await service.VoidAsync(saved.Id)).IsFailure);
        Assert.True((await service.RegenerateAsync(saved.Id)).IsFailure);
        Assert.Single(await service.SearchAsync(new StatementSearch()));

        SignInWith(Statement(create: false, delete: false, print: true));
        Assert.True((await service.GetAsync(saved.Id)).IsSuccess);

        SignInWith(Statement(create: true, delete: false, print: true));
        Assert.True((await service.GenerateAsync(ForCompany(acme.Id))).IsSuccess);
        Assert.True((await service.RegenerateAsync(saved.Id)).IsFailure); // regenerating voids the old one

        SignInWith(Statement(create: true, delete: true, print: true));
        Assert.True((await service.RegenerateAsync(saved.Id)).IsSuccess);

        SignInWith(Statement(create: true, delete: false, print: true));
        var another = (await service.GetAsync(saved.Id)).Value;
        Assert.True(another.IsVoid);
    }

    [Fact]
    public async Task Someone_without_any_access_to_the_module_sees_nothing()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var acme = await AddCompanyAsync(db, "Acme");
        await RegisterAsync(db, "Ana", At(Day, 9), 400m, acme.Id, charged: true);
        var service = CreateService(db);
        await service.GenerateAsync(ForCompany(acme.Id));

        SignInWith();

        Assert.Empty(await service.SearchAsync(new StatementSearch()));
        Assert.Empty(await service.SuggestPatientsAsync("an"));
        Assert.Empty(await service.GetChargedBatchesAsync(acme.Id));
    }
}
