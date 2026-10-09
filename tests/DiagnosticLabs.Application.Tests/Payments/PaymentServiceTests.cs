using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Billing;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Payments;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Payments;

public class PaymentServiceTests
{
    private readonly TestEnvironment _env = new();

    public PaymentServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private PaymentService CreateService(AppDbContext db) => new(db, _env.Session, _env.Clock);

    private DateOnly Today => DateOnly.FromDateTime(_env.Clock.UtcNow.ToLocalTime());

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private async Task<PatientRegistration> SeedRegistrationAsync(AppDbContext db, decimal price = 1000m, string name = "Jane Roe", string code = "BADC-09OC26-00001", Company? company = null)
    {
        var service = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = price };
        var patient = new Patient { PatientCode = $"P-{code}", PatientName = name };
        var registration = new PatientRegistration
        {
            RegistrationCode = code, Patient = patient, InputDate = _env.Clock.UtcNow, AmountDue = price, DiscountAmount = 0, Company = company,
        };
        registration.Services.Add(new PatientRegistrationService { Service = service, Price = price });
        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        return registration;
    }

    private PaymentInput Pay(long registrationId, decimal amount, decimal? discountAmount = 0m, decimal? discountPercentage = null) =>
        new(0, registrationId, Today, PaymentType.Payment, amount, discountAmount, discountPercentage, null);

    // ---------------------------------------------------------------- math

    [Theory]
    [InlineData(1000.0, null, null, 0.0)]
    [InlineData(1000.0, 150.0, null, 150.0)]
    [InlineData(1000.0, null, 20.0, 200.0)]
    [InlineData(1000.0, 150.0, 20.0, 200.0)]       // a percentage wins over an amount
    [InlineData(1000.0, 5000.0, null, 1000.0)]   // never more than the price
    [InlineData(333.0, null, 10.0, 33.30)]
    [InlineData(0.0, 50.0, null, 0.0)]
    public void The_discount_total_follows_the_rules(double price, double? amount, double? percentage, double expected) =>
        Assert.Equal((decimal)expected, BillingMath.DiscountTotal((decimal)price, (decimal?)amount, (decimal?)percentage));

    // ---------------------------------------------------------------- paying

    [Fact]
    public async Task A_payment_is_recorded_and_reduces_the_balance()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Pay(registration.Id, 400m))).Value;
        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;

        Assert.Equal(400m, saved.Amount);
        Assert.Equal(Today, saved.PaymentDate);
        Assert.Equal(400m, balance.Paid);
        Assert.Equal(600m, balance.Balance);
        Assert.Equal(1000m, balance.AmountDue);
    }

    [Fact]
    public async Task Several_payments_add_up_and_the_last_one_may_settle_exactly()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);

        Assert.True((await service.SaveAsync(Pay(registration.Id, 400m))).IsSuccess);
        Assert.True((await service.SaveAsync(Pay(registration.Id, 600m))).IsSuccess);

        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;
        Assert.Equal(0m, balance.Balance);
        Assert.Equal(1000m, balance.Paid);
    }

    [Fact]
    public async Task Paying_more_than_the_balance_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(Pay(registration.Id, 900m));

        var tooMuch = await service.SaveAsync(Pay(registration.Id, 200m));
        await service.SaveAsync(Pay(registration.Id, 100m));
        var settled = await service.SaveAsync(Pay(registration.Id, 1m));

        Assert.Contains("balance of 100.00", tooMuch.Error.Message, StringComparison.Ordinal);
        Assert.Contains("fully paid", settled.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, await db.Payments.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_payment_needs_an_amount_above_zero(decimal amount)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);

        var result = await CreateService(db).SaveAsync(Pay(registration.Id, amount));

        Assert.True(result.IsFailure);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task A_registration_must_be_chosen_and_exist()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var none = await service.SaveAsync(Pay(0, 10m));
        var missing = await service.SaveAsync(Pay(999, 10m));

        Assert.Contains("Find the registration", none.Error.Message, StringComparison.Ordinal);
        Assert.Contains("no longer exists", missing.Error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- discount

    [Fact]
    public async Task A_percentage_discount_is_saved_on_the_registration_and_lowers_the_amount_due()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);

        await service.SaveAsync(Pay(registration.Id, 500m, null, 20m));

        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;
        Assert.Equal(200m, balance.DiscountTotal);
        Assert.Equal(20m, balance.DiscountPercentage);
        Assert.Null(balance.DiscountAmount);
        Assert.Equal(800m, balance.AmountDue);
        Assert.Equal(300m, balance.Balance);
        Assert.Equal(200m, (await db.PatientRegistrations.AsNoTracking().SingleAsync()).DiscountTotal);
    }

    [Fact]
    public async Task A_fixed_discount_is_saved_and_the_payment_may_settle_the_discounted_amount()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);

        var result = await service.SaveAsync(Pay(registration.Id, 850m, 150m));

        Assert.True(result.IsSuccess);
        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;
        Assert.Equal(0m, balance.Balance);
        Assert.Equal(150m, balance.DiscountAmount);
    }

    [Fact]
    public async Task Discounts_are_validated()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);

        var both = await service.SaveAsync(Pay(registration.Id, 10m, 5m, 5m));
        var negative = await service.SaveAsync(Pay(registration.Id, 10m, -1m));
        var tooBig = await service.SaveAsync(Pay(registration.Id, 10m, 2000m));
        var percent = await service.SaveAsync(Pay(registration.Id, 10m, null, 101m));

        Assert.All([both, negative, tooBig, percent], r => Assert.True(r.IsFailure));
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task A_discount_can_be_changed_when_a_later_payment_is_made()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(Pay(registration.Id, 500m, null, 10m));

        await service.SaveAsync(Pay(registration.Id, 100m, 0m));

        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;
        Assert.Equal(0m, balance.DiscountTotal);
        Assert.Equal(400m, balance.Balance);
    }

    // ---------------------------------------------------------------- maintained discounts

    private static async Task<Discount> SeedDiscountAsync(AppDbContext db, string name = "Senior Citizen", bool active = true)
    {
        var discount = new Discount { DiscountName = name, DiscountDescription = "d", IsActive = active };
        discount.Details.Add(new DiscountDetail { Percentage = 20m });
        discount.Details.Add(new DiscountDetail { Amount = 50m });
        db.Discounts.Add(discount);
        await db.SaveChangesAsync();
        return discount;
    }

    [Fact]
    public async Task A_maintained_discount_is_remembered_on_the_registration()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var senior = await SeedDiscountAsync(db);
        var service = CreateService(db);

        var result = await service.SaveAsync(Pay(registration.Id, 100m, null, 20m) with { DiscountId = senior.Id });
        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;

        Assert.True(result.IsSuccess);
        Assert.Equal(senior.Id, balance.DiscountId);
        Assert.Equal(200m, balance.DiscountTotal);

        var fixedOption = await service.SaveAsync(Pay(registration.Id, 100m, 50m) with { DiscountId = senior.Id });
        Assert.True(fixedOption.IsSuccess);
        Assert.Equal(50m, (await service.GetRegistrationAsync(registration.Id)).Value.DiscountTotal);
    }

    [Fact]
    public async Task A_typed_discount_clears_the_maintained_one()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var senior = await SeedDiscountAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(Pay(registration.Id, 100m, null, 20m) with { DiscountId = senior.Id });

        await service.SaveAsync(Pay(registration.Id, 100m, 30m));

        Assert.Null((await service.GetRegistrationAsync(registration.Id)).Value.DiscountId);
    }

    [Fact]
    public async Task A_maintained_discount_must_be_offered_and_the_value_one_of_its_options()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var senior = await SeedDiscountAsync(db);
        var retired = await SeedDiscountAsync(db, "Old promo", active: false);
        var service = CreateService(db);

        var wrongValue = await service.SaveAsync(Pay(registration.Id, 10m, null, 35m) with { DiscountId = senior.Id });
        var wrongAmount = await service.SaveAsync(Pay(registration.Id, 10m, 75m) with { DiscountId = senior.Id });
        var switchedOff = await service.SaveAsync(Pay(registration.Id, 10m, null, 20m) with { DiscountId = retired.Id });
        var unknown = await service.SaveAsync(Pay(registration.Id, 10m, null, 20m) with { DiscountId = 999 });

        Assert.Contains("not one of the options", wrongValue.Error.Message, StringComparison.Ordinal);
        Assert.True(wrongAmount.IsFailure);
        Assert.Contains("no longer offered", switchedOff.Error.Message, StringComparison.Ordinal);
        Assert.True(unknown.IsFailure);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task A_discount_switched_off_later_can_still_be_kept_on_that_registration()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var senior = await SeedDiscountAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(Pay(registration.Id, 100m, null, 20m) with { DiscountId = senior.Id });
        (await db.Discounts.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();

        var again = await service.SaveAsync(Pay(registration.Id, 100m, null, 20m) with { DiscountId = senior.Id });

        Assert.True(again.IsSuccess);
    }

    // ---------------------------------------------------------------- charge
    [Fact]
    public async Task A_charge_settles_the_balance_without_an_amount_and_only_once()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);

        var charge = new PaymentInput(0, registration.Id, Today, PaymentType.Charge, 0m, 0m, null, null);
        var first = await service.SaveAsync(charge);
        var second = await service.SaveAsync(charge);
        var withAmount = await service.SaveAsync(charge with { Amount = 5m });

        Assert.True(first.IsSuccess);
        Assert.Equal(0m, first.Value.Amount);
        Assert.Contains("already charged", second.Error.Message, StringComparison.Ordinal);
        Assert.True(withAmount.IsFailure);
        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;
        Assert.True(balance.IsCharged);
        Assert.Equal(0m, balance.Balance);
        Assert.Contains("charged to its company", (await service.SaveAsync(Pay(registration.Id, 10m))).Error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- editing and deleting

    [Fact]
    public async Task Editing_a_payment_does_not_count_it_against_itself()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Pay(registration.Id, 1000m))).Value;

        var raised = await service.SaveAsync(new PaymentInput(saved.Id, registration.Id, Today, PaymentType.Payment, 900m, 100m, null, saved.RowVersion));
        var summary = (await service.GetRegistrationAsync(registration.Id, saved.Id)).Value;

        Assert.True(raised.IsSuccess);
        Assert.Equal(0m, summary.Paid);
        Assert.Equal(900m, summary.AmountDue);
        Assert.Equal(900m, summary.Balance);
    }

    [Fact]
    public async Task A_payment_keeps_its_time_of_day_and_can_not_move_to_another_registration()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var one = await SeedRegistrationAsync(db, code: "A-1", name: "One");
        var two = await SeedRegistrationAsync(db, code: "A-2", name: "Two");
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Pay(one.Id, 100m))).Value;
        var original = (await db.Payments.AsNoTracking().SingleAsync()).PaymentDate;
        _env.Clock.UtcNow += TimeSpan.FromHours(5);

        var edited = await service.SaveAsync(new PaymentInput(saved.Id, one.Id, Today, PaymentType.Payment, 150m, 0m, null, saved.RowVersion));
        var moved = await service.SaveAsync(new PaymentInput(saved.Id, two.Id, Today, PaymentType.Payment, 150m, 0m, null, edited.Value.RowVersion));

        Assert.True(edited.IsSuccess);
        Assert.Equal(original, (await db.Payments.AsNoTracking().SingleAsync()).PaymentDate);
        Assert.Contains("different registration", moved.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_deleted_payment_no_longer_counts_and_is_hidden()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Pay(registration.Id, 700m))).Value;

        var deleted = await service.DeleteAsync(saved.Id);

        Assert.True(deleted.IsSuccess);
        Assert.Equal("Payment.NotFound", (await service.GetAsync(saved.Id)).Error.Code);
        Assert.Equal(1000m, (await service.GetRegistrationAsync(registration.Id)).Value.Balance);
        Assert.Equal(1, await db.Payments.IgnoreQueryFilters().CountAsync(p => p.IsDeleted));
    }

    [Fact]
    public async Task The_registration_price_changing_later_keeps_a_percentage_discount_in_step()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(Pay(registration.Id, 100m, null, 10m));

        var tracked = await db.PatientRegistrations.SingleAsync();
        tracked.AmountDue = 2000m;
        await db.SaveChangesAsync();

        var balance = (await service.GetRegistrationAsync(registration.Id)).Value;
        Assert.Equal(200m, balance.DiscountTotal); // computed live, never stale
        Assert.Equal(1800m, balance.AmountDue);
    }

    // ---------------------------------------------------------------- finding registrations

    [Fact]
    public async Task A_registration_is_found_by_its_exact_code_with_its_services()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);

        var found = (await service.FindRegistrationAsync("  BADC-09OC26-00001 ")).Value;

        Assert.Equal(registration.Id, found.RegistrationId);
        Assert.Equal("Jane Roe", found.PatientName);
        Assert.Equal("CBC", Assert.Single(found.Services).ServiceName);
        Assert.Equal("Registration.NotFound", (await service.FindRegistrationAsync("nope")).Error.Code);
        Assert.Equal("Registration.NotFound", (await service.FindRegistrationAsync("  ")).Error.Code);
    }

    [Fact]
    public async Task Typing_offers_registrations_with_their_balance()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var a = await SeedRegistrationAsync(db, 1000m, "Alice Tan", "R-1");
        await SeedRegistrationAsync(db, 500m, "Bob Cruz", "R-2");
        var service = CreateService(db);
        await service.SaveAsync(Pay(a.Id, 300m));

        var alice = (await service.SuggestRegistrationsAsync("Alice")).Value;
        var both = (await service.SuggestRegistrationsAsync("R-")).Value;

        Assert.Equal(700m, Assert.Single(alice).Balance);
        Assert.Equal(2, both.Count);
        Assert.Empty((await service.SuggestRegistrationsAsync("A")).Value);
        Assert.Empty((await service.SuggestRegistrationsAsync("zzzz")).Value);
    }

    // ---------------------------------------------------------------- searching

    [Fact]
    public async Task Search_lists_payments_newest_first_and_filters_by_text_company_and_day()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var company = new Company { CompanyName = "ACME" };
        var a = await SeedRegistrationAsync(db, 1000m, "Alice Tan", "R-1", company);
        var b = await SeedRegistrationAsync(db, 1000m, "Bob Cruz", "R-2");
        var service = CreateService(db);
        await service.SaveAsync(Pay(a.Id, 100m) with { Date = Today.AddDays(-3) });
        await service.SaveAsync(Pay(b.Id, 200m));

        var all = (await service.SearchAsync(new CrudSearch(null))).Value.Items;
        var alice = (await service.SearchAsync(new CrudSearch("Alice"))).Value.Items;
        var acme = (await service.SearchAsync(new PaymentSearch(null, CompanyId: company.Id))).Value.Items;
        var today = (await service.SearchAsync(new PaymentSearch(null, Date: Today))).Value.Items;

        Assert.Equal(["Bob Cruz", "Alice Tan"], all.Select(i => i.PatientName));
        Assert.Single(alice);
        Assert.Equal("ACME", Assert.Single(acme).CompanyName);
        Assert.Equal("Bob Cruz", Assert.Single(today).PatientName);
    }

    // ---------------------------------------------------------------- permissions

    [Fact]
    public async Task Each_action_needs_its_own_permission()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = await SeedRegistrationAsync(db);
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Pay(registration.Id, 100m))).Value;
        _env.Session.SignIn(new AuthenticatedUser(7, "viewer", "Viewer", false, false, [new ModulePermission(ModuleIds.Payments, false, false, false, false)]));

        Assert.True((await service.GetAsync(saved.Id)).IsSuccess);
        Assert.True((await service.FindRegistrationAsync(registration.RegistrationCode)).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(Pay(registration.Id, 10m))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.DeleteAsync(saved.Id)).Error.Code);
        _env.Session.SignIn(new AuthenticatedUser(8, "nobody", "Nobody", false, false, []));
        Assert.Equal("Auth.Forbidden", (await service.SearchAsync(new CrudSearch(null))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.FindRegistrationAsync("x")).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.SuggestRegistrationsAsync("ab")).Error.Code);
    }
}
