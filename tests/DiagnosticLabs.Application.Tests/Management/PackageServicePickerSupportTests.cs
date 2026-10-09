using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Domain.Catalog;

namespace DiagnosticLabs.Application.Tests.Management;

/// <summary>What the service picker and the package table rely on from the Application layer.</summary>
public class PackageServicePickerSupportTests
{
    private readonly TestEnvironment _env = new();

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    [Fact]
    public async Task The_service_list_for_the_picker_keeps_the_order_the_services_were_created_in_and_hides_inactive_ones()
    {
        await using var db = _env.CreateDb();
        db.Services.AddRange(
            new Service { ServiceName = "Zinc", ServiceDescription = "d", Price = 10 },
            new Service { ServiceName = "Albumin", ServiceDescription = "d", Price = 20 },
            new Service { ServiceName = "Retired", ServiceDescription = "d", Price = 30, IsActive = false },
            new Service { ServiceName = "Calcium", ServiceDescription = "d", Price = 40 });
        await db.SaveChangesAsync();

        var options = await new ReferenceLookups(db).GetServicesAsync();

        Assert.Equal(["Zinc", "Albumin", "Calcium"], options.Select(o => o.Name));
    }

    [Fact]
    public async Task A_saved_package_comes_back_with_the_service_names_for_its_table()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        var xray = new Service { ServiceName = "X-ray", ServiceDescription = "d", Price = 400 };
        db.Services.AddRange(cbc, xray);
        await db.SaveChangesAsync();
        var service = new PackageCatalogService(db, _env.Session);

        var saved = (await service.SaveAsync(new PackageInput(
            0, "Pre-employment", "Basic", 650m, null, true,
            [new PackageServiceLine(0, cbc.Id, 250), new PackageServiceLine(0, xray.Id, 400)], null))).Value;
        var reloaded = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(["CBC", "X-ray"], saved.Services.Select(s => s.ServiceName));
        Assert.Equal(["CBC", "X-ray"], reloaded.Services.Select(s => s.ServiceName));
    }

    [Fact]
    public async Task A_service_that_was_switched_off_after_being_added_still_shows_its_name_in_the_package()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        db.Services.Add(cbc);
        await db.SaveChangesAsync();
        var service = new PackageCatalogService(db, _env.Session);
        var saved = (await service.SaveAsync(new PackageInput(
            0, "P", "P", 250m, null, true, [new PackageServiceLine(0, cbc.Id, 250)], null))).Value;
        cbc.IsActive = false;
        await db.SaveChangesAsync();

        var reloaded = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal("CBC", Assert.Single(reloaded.Services).ServiceName);
    }

    [Fact]
    public async Task Editing_a_package_keeps_the_price_typed_for_a_service_that_stays()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var cbc = new Service { ServiceName = "CBC", ServiceDescription = "d", Price = 250 };
        var urine = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 100 };
        db.Services.AddRange(cbc, urine);
        await db.SaveChangesAsync();
        var service = new PackageCatalogService(db, _env.Session);
        var saved = (await service.SaveAsync(new PackageInput(
            0, "P", "P", 330m, null, true, [new PackageServiceLine(0, cbc.Id, 230)], null))).Value; // special price 230

        // the picker adds Urinalysis at its standard price; CBC keeps the special price
        var edited = (await service.SaveAsync(new PackageInput(
            saved.Id, "P", "P", 330m, null, true,
            [saved.Services[0], new PackageServiceLine(0, urine.Id, 100)], saved.RowVersion))).Value;

        Assert.Equal(230m, edited.Services.Single(s => s.ServiceId == cbc.Id).Price);
        Assert.Equal(100m, edited.Services.Single(s => s.ServiceId == urine.Id).Price);
    }
}
