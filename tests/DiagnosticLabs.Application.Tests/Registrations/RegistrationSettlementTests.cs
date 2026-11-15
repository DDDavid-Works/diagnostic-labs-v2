using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Registrations;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Settings;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Registrations;

/// <summary>Settling a registration (Paid / Charged to company) as it is saved, instead of on the Payments screen.</summary>
public class RegistrationSettlementTests
{
    private readonly TestEnvironment _env = new();

    public RegistrationSettlementTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => DateOnly.FromDateTime(_env.Clock.UtcNow.ToLocalTime());

    private RegistrationService CreateService(AppDbContext db) => new(db, _env.Session, new CodeGenerator(db, _env.Clock), _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private void SignInAsRegistrar(bool mayRecordPayments)
    {
        List<ModulePermission> permissions = [new(ModuleIds.PatientRegistrations, true, true, false, false)];
        if (mayRecordPayments)
            permissions.Add(new ModulePermission(ModuleIds.Payments, true, false, false, false));
        _env.Session.SignIn(new AuthenticatedUser(5, "counter", "Counter", false, false, permissions));
    }

    private sealed record Seed(long Cbc, long Urine, long CompanyId);

    private static async Task<Seed> SeedAsync(AppDbContext db)
    {
        db.CompanySetups.Add(new CompanySetup { CompanyName = "Bayside", Code = "BADC" });
        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        var urine = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 100 };
        var company = new Company { CompanyName = "LNT" };
        db.Services.AddRange(cbc, urine);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return new Seed(cbc.Id, urine.Id, company.Id);
    }

    private RegistrationInput NewInput(Seed s, RegistrationSettlement settle, long? companyId = null, decimal price = 350m) => new(
        0, Today, companyId, null, null, 0, "Jusis Albarea", new DateOnly(2002, 9, 22), null, "Male", "Single", "Bareahard", "0917", null,
        price, [new RegistrationServiceLine(0, s.Cbc, 250), new RegistrationServiceLine(0, s.Urine, 100)], null, settle);

    private static RegistrationInput EditInput(RegistrationDetails d, RegistrationSettlement settle = RegistrationSettlement.None, decimal? price = null) => new(
        d.Id, d.Date, d.CompanyId, d.BatchName, d.PackageId, d.PatientId, d.PatientName, d.DateOfBirth, d.Age, d.Sex, d.CivilStatus, d.Address,
        d.ContactNumbers, d.PatientRowVersion, price ?? d.AmountDue, d.Services, d.RowVersion, settle);

    private async Task<PaymentState> BoardStateAsync(AppDbContext db) =>
        (await new RegistrationBoardService(db, _env.Session).SearchAsync(new BoardSearch(null))).Value.Items.Single().Payment.State;

    [Fact]
    public async Task Saving_as_paid_records_one_payment_of_the_whole_price_today_and_Home_shows_it_paid()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed, RegistrationSettlement.Paid));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var payment = await db.Payments.SingleAsync();
        Assert.Equal((PaymentType.Payment, 350m, result.Value.Id), (payment.Type, payment.PaymentAmount, payment.PatientRegistrationId));
        Assert.Equal(_env.Clock.UtcNow, payment.PaymentDate);
        Assert.Equal(new RegistrationPaymentStatus(1, 350m, false, 350m), result.Value.Payment);
        Assert.Equal(0m, result.Value.Payment!.Balance);
        Assert.Equal(PaymentState.Paid, await BoardStateAsync(db));
    }

    [Fact]
    public async Task Saving_as_charged_records_a_charge_without_an_amount_and_Home_shows_it_charged()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed, RegistrationSettlement.Charged, seed.CompanyId));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var payment = await db.Payments.SingleAsync();
        Assert.Equal((PaymentType.Charge, 0m), (payment.Type, payment.PaymentAmount));
        Assert.True(result.Value.Payment!.IsCharged);
        Assert.Equal(PaymentState.Charged, await BoardStateAsync(db));
    }

    [Fact]
    public async Task By_default_nothing_is_recorded_and_Home_shows_it_unpaid()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed, RegistrationSettlement.None, seed.CompanyId));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await db.Payments.CountAsync());
        Assert.False(result.Value.Payment!.HasPayments);
        Assert.Equal(PaymentState.Unpaid, await BoardStateAsync(db));
    }

    [Fact]
    public async Task Charging_needs_a_company_and_nothing_is_saved_without_one()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed, RegistrationSettlement.Charged));

        Assert.Equal("Validation.Invalid", result.Error.Code);
        Assert.Contains("company to charge", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, await db.PatientRegistrations.CountAsync());
        Assert.Equal(0, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task A_free_registration_can_not_be_marked_paid()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewInput(seed, RegistrationSettlement.Paid, price: 0m));

        Assert.Contains("nothing to pay", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, await db.PatientRegistrations.CountAsync());
    }

    [Fact]
    public async Task Settling_needs_the_right_to_create_payments()
    {
        await using var db = _env.CreateDb();
        var seed = await SeedAsync(db);
        var service = CreateService(db);

        SignInAsRegistrar(mayRecordPayments: false);
        var refused = await service.SaveAsync(NewInput(seed, RegistrationSettlement.Paid));
        var unsettled = await service.SaveAsync(NewInput(seed, RegistrationSettlement.None));
        SignInAsRegistrar(mayRecordPayments: true);
        var allowed = await service.SaveAsync(NewInput(seed, RegistrationSettlement.Paid));

        Assert.Contains("not allowed to record payments", refused.Error.Message, StringComparison.Ordinal);
        Assert.True(unsettled.IsSuccess);
        Assert.True(allowed.IsSuccess);
        Assert.Equal(2, await db.PatientRegistrations.CountAsync());
        Assert.Equal(1, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task A_saved_registration_without_payments_can_be_settled_when_edited()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed, RegistrationSettlement.None))).Value;

        var edited = await service.SaveAsync(EditInput(created, RegistrationSettlement.Paid, price: 300m));

        Assert.True(edited.IsSuccess, edited.IsFailure ? edited.Error.Message : null);
        Assert.Equal(300m, (await db.Payments.SingleAsync()).PaymentAmount);
    }

    [Fact]
    public async Task A_registration_that_already_has_a_payment_is_never_paid_a_second_time_from_here()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed, RegistrationSettlement.Paid))).Value;

        var again = await service.SaveAsync(EditInput(created, RegistrationSettlement.Paid));
        var charged = await service.SaveAsync(EditInput(created, RegistrationSettlement.Charged) with { CompanyId = seed.CompanyId });

        Assert.Contains("already has a payment", again.Error.Message, StringComparison.Ordinal);
        Assert.Contains("already has a payment", charged.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await db.Payments.CountAsync());
    }

    [Fact]
    public async Task Raising_the_price_after_it_was_paid_keeps_the_payment_and_leaves_a_balance_to_collect()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var seed = await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput(seed, RegistrationSettlement.Paid))).Value;

        var edited = await service.SaveAsync(EditInput(created, price: 450m));

        Assert.True(edited.IsSuccess);
        Assert.Equal(new RegistrationPaymentStatus(1, 350m, false, 450m), edited.Value.Payment);
        Assert.Equal(100m, edited.Value.Payment!.Balance);
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(PaymentState.Partial, await BoardStateAsync(db));
        Assert.Equal(edited.Value.Payment, (await service.GetAsync(created.Id)).Value.Payment);
    }
}
