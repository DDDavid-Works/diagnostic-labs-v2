using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Inventory;
using DiagnosticLabs.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Management;

public class ManagementServiceTests
{
    private readonly TestEnvironment _env = new();

    private void SignInAsAdmin() =>
        _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    // ---------------------------------------------------------------- shared behaviour (Services as the example)

    [Fact]
    public async Task Service_requires_name_and_description_and_a_non_negative_price()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ServiceCatalogService(db, _env.Session);

        var result = await service.SaveAsync(new ServiceInput(0, " ", null, -5, true, null));

        Assert.True(result.IsFailure);
        Assert.Contains("Name can not be empty.", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Description can not be empty.", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Price must not be negative.", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await db.Services.ToListAsync());
    }

    [Fact]
    public async Task Create_edit_and_deactivate_a_service()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ServiceCatalogService(db, _env.Session);

        var created = (await service.SaveAsync(new ServiceInput(0, "CBC", "Complete blood count", 250m, true, null))).Value;
        var edited = (await service.SaveAsync(new ServiceInput(created.Id, "CBC", "Complete blood count", 300m, true, null))).Value;
        var removed = await service.DeleteAsync(created.Id);

        Assert.Equal(300m, edited.Price);
        Assert.True(removed.IsSuccess);
        Assert.Empty((await service.SearchAsync(new CrudSearch(null))).Value.Items);

        var withInactive = (await service.SearchAsync(new CrudSearch(null, IncludeInactive: true))).Value.Items;
        var item = Assert.Single(withInactive);
        Assert.False(item.IsActive);
    }

    [Fact]
    public async Task An_inactive_row_can_be_reactivated_by_saving_it_active()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ServiceCatalogService(db, _env.Session);
        var created = (await service.SaveAsync(new ServiceInput(0, "X-ray", "Chest", 400m, true, null))).Value;
        await service.DeleteAsync(created.Id);

        await service.SaveAsync(new ServiceInput(created.Id, "X-ray", "Chest", 400m, true, null));

        Assert.Single((await service.SearchAsync(new CrudSearch(null))).Value.Items);
    }

    [Fact]
    public async Task Search_matches_words_and_pages()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ServiceCatalogService(db, _env.Session);
        foreach (var name in new[] { "Alpha", "Beta", "Gamma", "Delta" })
            await service.SaveAsync(new ServiceInput(0, name, name + " test", 1m, true, null));

        var page = (await service.SearchAsync(new CrudSearch(null, 2, 3))).Value;
        var found = (await service.SearchAsync(new CrudSearch("Gamma test"))).Value;

        Assert.Equal(4, page.TotalCount);
        Assert.Equal(["Gamma"], page.Items.Select(i => i.ServiceName));
        Assert.Equal(["Gamma"], found.Items.Select(i => i.ServiceName));
    }

    [Fact]
    public async Task Each_action_needs_its_own_permission()
    {
        await using var db = _env.CreateDb();
        _env.Session.SignIn(new AuthenticatedUser(
            5, "clerk", "Clerk", false, false,
            [new ModulePermission(ModuleIds.Services, AllowCreate: true, AllowEdit: false, AllowDelete: false, AllowPrint: false)]));
        var service = new ServiceCatalogService(db, _env.Session);

        var created = await service.SaveAsync(new ServiceInput(0, "CBC", "Blood", 1m, true, null));

        Assert.True(created.IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(new ServiceInput(created.Value.Id, "CBC", "Blood", 2m, true, null))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.DeleteAsync(created.Value.Id)).Error.Code);
    }

    // ---------------------------------------------------------------- companies


    [Fact]
    public async Task Departments_only_need_a_name()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var result = await new DepartmentService(db, _env.Session).SaveAsync(new DepartmentInput(0, "Laboratory", null, true, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value.DepartmentDescription);
    }

    // ---------------------------------------------------------------- items & stock

    private async Task<(long Main, long Annex)> SeedLocationsAsync(Infrastructure.Persistence.AppDbContext db)
    {
        var main = new ItemLocation { ItemLocationName = "Main" };
        var annex = new ItemLocation { ItemLocationName = "Annex" };
        db.ItemLocations.AddRange(main, annex);
        await db.SaveChangesAsync();
        return (main.Id, annex.Id);
    }

    [Fact]
    public async Task Item_stock_lines_are_saved_and_totalled()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (main, annex) = await SeedLocationsAsync(db);
        var service = new ItemService(db, _env.Session);

        var saved = await service.SaveAsync(new ItemInput(
            0, "Syringe", 5m, true, [new ItemQuantityLine(0, main, 10), new ItemQuantityLine(0, annex, 2.5m)], null));

        Assert.True(saved.IsSuccess);
        var list = (await service.SearchAsync(new CrudSearch(null))).Value.Items.Single();
        Assert.Equal(12.5m, list.TotalQuantity);
    }

    [Fact]
    public async Task Editing_stock_updates_adds_and_removes_lines()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (main, annex) = await SeedLocationsAsync(db);
        var service = new ItemService(db, _env.Session);
        var created = (await service.SaveAsync(new ItemInput(0, "Gloves", 1m, true, [new ItemQuantityLine(0, main, 10)], null))).Value;
        var mainLine = created.Quantities.Single();

        // update the Main line, add Annex
        var edited = (await service.SaveAsync(new ItemInput(
            created.Id, "Gloves", 1m, true,
            [mainLine with { Quantity = 4 }, new ItemQuantityLine(0, annex, 6)], null))).Value;
        Assert.Equal(2, edited.Quantities.Count);
        Assert.Equal(10m, edited.Quantities.Sum(q => q.Quantity));

        // drop Main entirely
        var trimmed = (await service.SaveAsync(new ItemInput(
            created.Id, "Gloves", 1m, true, [edited.Quantities.Single(q => q.ItemLocationId == annex)], null))).Value;
        var remaining = Assert.Single(trimmed.Quantities);
        Assert.Equal(annex, remaining.ItemLocationId);
        Assert.Equal(2, await db.ItemQuantities.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.ItemQuantities.IgnoreQueryFilters().CountAsync(q => q.IsDeleted));
    }

    [Fact]
    public async Task Removing_and_re_adding_the_same_location_in_one_save_is_an_update()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (main, _) = await SeedLocationsAsync(db);
        var service = new ItemService(db, _env.Session);
        var created = (await service.SaveAsync(new ItemInput(0, "Tape", 1m, true, [new ItemQuantityLine(0, main, 3)], null))).Value;

        var result = (await service.SaveAsync(new ItemInput(
            created.Id, "Tape", 1m, true, [new ItemQuantityLine(0, main, 9)], null))).Value;

        Assert.Equal(created.Quantities.Single().Id, result.Quantities.Single().Id);
        Assert.Equal(9m, result.Quantities.Single().Quantity);
    }

    [Fact]
    public async Task Item_rules_reject_negative_stock_duplicate_locations_and_unknown_locations()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var (main, _) = await SeedLocationsAsync(db);
        var service = new ItemService(db, _env.Session);

        var negative = await service.SaveAsync(new ItemInput(0, "A", 1m, true, [new ItemQuantityLine(0, main, -1)], null));
        var duplicate = await service.SaveAsync(new ItemInput(0, "B", 1m, true, [new ItemQuantityLine(0, main, 1), new ItemQuantityLine(0, main, 2)], null));
        var unknown = await service.SaveAsync(new ItemInput(0, "C", 1m, true, [new ItemQuantityLine(0, 999, 1)], null));

        Assert.True(negative.IsFailure);
        Assert.True(duplicate.IsFailure);
        Assert.True(unknown.IsFailure);
        Assert.Empty(await db.Items.ToListAsync());
    }

    // ---------------------------------------------------------------- discounts

    [Fact]
    public async Task Each_discount_option_has_exactly_one_of_amount_or_percentage()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new DiscountService(db, _env.Session);

        Task<Result<DiscountDetails>> Save(params DiscountLine[] lines) =>
            service.SaveAsync(new DiscountInput(0, "Senior", "Senior citizen", true, lines, null));

        Assert.True((await Save(new DiscountLine(0, 10, 10))).IsFailure);
        Assert.True((await Save(new DiscountLine(0, null, null))).IsFailure);
        Assert.True((await Save(new DiscountLine(0, -1, null))).IsFailure);
        Assert.True((await Save(new DiscountLine(0, null, 101))).IsFailure);

        var ok = await Save(new DiscountLine(0, 50, null), new DiscountLine(0, null, 20));
        Assert.True(ok.IsSuccess);
        Assert.Equal(2, ok.Value.Lines.Count);
    }

    [Fact]
    public async Task Discount_options_can_be_edited_and_removed()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new DiscountService(db, _env.Session);
        var created = (await service.SaveAsync(new DiscountInput(
            0, "Promo", "Promo", true, [new DiscountLine(0, 50, null), new DiscountLine(0, null, 20)], null))).Value;
        var percent = created.Lines.Single(l => l.Percentage is not null);

        var edited = (await service.SaveAsync(new DiscountInput(
            created.Id, "Promo", "Promo", true, [percent with { Percentage = 25 }], null))).Value;

        var line = Assert.Single(edited.Lines);
        Assert.Equal(25m, line.Percentage);
        Assert.Equal(percent.Id, line.Id);
    }

    // ---------------------------------------------------------------- packages

    [Fact]
    public async Task Package_saves_with_services_company_and_shows_the_company_in_the_list()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        var xray = new Service { ServiceName = "X-ray", ServiceDescription = "d", Price = 400 };
        var company = new Company { CompanyName = "ACME" };
        db.Services.AddRange(cbc, xray);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var service = new PackageCatalogService(db, _env.Session);

        var saved = await service.SaveAsync(new PackageInput(
            0, "Pre-employment", "Basic", 600m, company.Id, true,
            [new PackageServiceLine(0, cbc.Id, 250), new PackageServiceLine(0, xray.Id, 350)], null));

        Assert.True(saved.IsSuccess);
        Assert.Equal(2, saved.Value.Services.Count);
        var row = (await service.SearchAsync(new CrudSearch(null))).Value.Items.Single();
        Assert.Equal("ACME", row.CompanyName);
        Assert.Equal(600m, row.Price);
    }

    [Fact]
    public async Task Package_rules_reject_duplicate_or_unknown_services_and_unknown_companies()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        db.Services.Add(cbc);
        await db.SaveChangesAsync();
        var service = new PackageCatalogService(db, _env.Session);

        Task<Result<PackageDetails>> Save(long? company, params PackageServiceLine[] lines) =>
            service.SaveAsync(new PackageInput(0, "P", "P", 1m, company, true, lines, null));

        Assert.True((await Save(null, new PackageServiceLine(0, cbc.Id, 1), new PackageServiceLine(0, cbc.Id, 1))).IsFailure);
        Assert.True((await Save(null, new PackageServiceLine(0, 999, 1))).IsFailure);
        Assert.True((await Save(999, new PackageServiceLine(0, cbc.Id, 1))).IsFailure);
        Assert.True((await Save(null, new PackageServiceLine(0, cbc.Id, -1))).IsFailure);
        Assert.True((await Save(null)).IsSuccess); // a package with no services is allowed
    }

    [Fact]
    public async Task Package_services_can_be_swapped_when_editing()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        var urine = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 100 };
        db.Services.AddRange(cbc, urine);
        await db.SaveChangesAsync();
        var service = new PackageCatalogService(db, _env.Session);
        var created = (await service.SaveAsync(new PackageInput(
            0, "P", "P", 250m, null, true, [new PackageServiceLine(0, cbc.Id, 250)], null))).Value;

        var swapped = (await service.SaveAsync(new PackageInput(
            created.Id, "P", "P", 100m, null, true, [new PackageServiceLine(0, urine.Id, 100)], null))).Value;

        Assert.Equal(urine.Id, Assert.Single(swapped.Services).ServiceId);
    }
}
